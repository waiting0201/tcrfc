using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tcrfc.Api.Data;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// E-170 的防呆：<b>在空白的 SQL Server 資料庫上，照正式庫的真實路徑把全部 EF migration 實際執行過</b>（含冪等重跑與回滾再套用）。
/// 為什麼需要：migration 常把 DDL／DML 包在 <c>EXEC(N'…')</c> 與 <c>IF…BEGIN…END</c> 裡，無效 T-SQL 在 sqlcmd 下只印一行錯誤、
/// 後面的語句照常成功，而且沒有任何測試會真的執行 migration，壞語句可以進版（E-170：<c>(a IS NULL) &lt;&gt; (b IS NULL)</c>）。
/// 透過 ADO.NET 執行時，批次內任何錯誤都會讓 <c>ExecuteNonQuery</c> 丟 <see cref="SqlException"/>，這個測試就會失敗。
///
/// 路徑（docs/20 §5：基準 migration 是空的，資料庫由 <c>db/club-schema.sql</c> 建立，history 補到「已套用」）：
/// ① 空白庫＋最新 DDL（同 <c>deploy/local-ddl.sh</c> 的 json→nvarchar(max) 轉換），把 <see cref="IdempotentFloorMigration"/> 之前的 migration 全部標記為已套用 →
/// ② 套用其餘 migration（最新 DDL 已含全部物件，驗證「冪等、不重複建立」）→ ③ 回滾到 floor 之前（執行每支 <c>Down</c>，把 migration 加的欄位與約束拿掉，還原成「舊庫」）→
/// ④ 再套用一次（這次每支 migration 的 <c>Up</c> 本體真的執行，等同舊庫升級；<c>IF NOT EXISTS</c> 守衛內的語句只有這一步會被執行到）→
/// ⑤ 清掉歷史再套用一次（又一次冪等）。
///
/// ⚠️ 這個測試是 <see cref="TestDatabaseGuard"/>「只准連 <c>tcrfc_club</c>」規則的**唯一刻意例外**：它在同一個 SQL Server instance 上建立一個
/// 名稱以 <c>tcrfc_migprobe_</c> 開頭、後綴為隨機 GUID 的**拋棄式**資料庫，測完即刪（只刪自己建的那個名字，建立與刪除都斷言前綴），不碰其他任何資料庫。
/// 需要能建立資料庫的帳號（本機 sa）；沒有權限時測試失敗並說明，不靜默略過。
/// </summary>
public sealed class MigrationsOnBlankDatabaseTests
{
    private const string ProbePrefix = "tcrfc_migprobe_";
    private const string BaselineMigration = "20260922070223_InitialBaseline";

    /// <summary>
    /// 「冪等契約」的起點：從這支 migration（含）之後的每一支 <c>Up</c>／<c>Down</c> 都必須先查現況再動（<c>docs/20</c> §5 注意事項 1）。
    /// 在它之前的 migration 是歷史包袱——它們是 EF 直接產生的 <c>CreateTable</c>／<c>AddColumn</c>，只能在「當時的舊庫」上執行一次；
    /// 正式庫與新建庫是用最新的 <c>db/club-schema.sql</c> 建的，初始化時把它們全部標記為已套用（history 補齊），不會重跑。
    /// 之後新增的 migration 時間戳一定晚於這個值，所以自動落在契約內。
    /// </summary>
    private const string IdempotentFloorMigration = "20261001145846_AlignIndexesWithDdl2";

    [Fact]
    public async Task 空白庫加最新DDL_套用冪等契約內的migration_回滾再套用_冪等重跑_全部成功()
    {
        var baseConnection = await TestDatabaseGuard.ResolveAndVerifyAsync(); // 同樣的伺服器與帳密，只換資料庫名稱
        var probeName = ProbePrefix + Guid.NewGuid().ToString("N");
        Assert.StartsWith(ProbePrefix, probeName);
        var master = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
        var probe = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = probeName }.ConnectionString;

        await ExecuteAsync(master, $"CREATE DATABASE [{probeName}]");
        try
        {
            foreach (var batch in LocalizedDdlBatches())
            {
                await ExecuteAsync(probe, batch);
            }

            var options = new DbContextOptionsBuilder<ClubDbContext>().UseSqlServer(probe, o => o.CommandTimeout(300)).Options;
            await using var context = new ClubDbContext(options);
            var all = context.Database.GetMigrations().ToList();
            var floorIndex = all.IndexOf(IdempotentFloorMigration);
            Assert.True(floorIndex > 0, $"找不到冪等契約起點 migration {IdempotentFloorMigration}。");
            var historical = all.Take(floorIndex).ToList();
            var contract = all.Skip(floorIndex).ToList();
            Assert.True(contract.Count >= 4, "預期契約內至少有幾支 migration。");

            // ① 歷史（含基準）全部標記為已套用，契約內的留作「待套用」
            await ExecuteAsync(probe,
                "CREATE TABLE [__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL);"
                + " INSERT INTO [__EFMigrationsHistory] VALUES " + string.Join(",", historical.Select(m => $"(N'{m}', N'10.0.0')")));
            Assert.Equal(contract, (await context.Database.GetPendingMigrationsAsync()).ToList());

            // ② 最新 DDL 上套用契約內全部（冪等）
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());

            // ③ 回滾到 floor 之前：契約內每支 Down 都要能執行
            await context.Database.GetService<IMigrator>().MigrateAsync(historical[^1]);
            Assert.Equal(contract, (await context.Database.GetPendingMigrationsAsync()).ToList());

            // ④ 舊庫升級：每支 Up 本體真的執行
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());

            // ⑤ 清掉契約內的歷史再套用一次（冪等）
            await ExecuteAsync(probe, "DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] IN (" + string.Join(",", contract.Select(m => $"N'{m}'")) + ")");
            Assert.Equal(contract, (await context.Database.GetPendingMigrationsAsync()).ToList());
            await context.Database.MigrateAsync();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            // 只刪自己建的那個名字（上面已斷言前綴），用 SINGLE_USER 踢掉連線池殘留連線。
            Assert.StartsWith(ProbePrefix, probeName);
            await ExecuteAsync(master,
                $"IF DB_ID(N'{probeName}') IS NOT NULL BEGIN ALTER DATABASE [{probeName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{probeName}]; END");
        }
    }

    /// <summary>讀 <c>db/club-schema.sql</c>，做與 <c>deploy/local-ddl.sh</c> 相同的轉換（欄位型別 <c>json</c> → <c>nvarchar(max)</c>，本機 SQL Server 2022 沒有原生 json），依 <c>GO</c> 切批。</summary>
    private static IEnumerable<string> LocalizedDdlBatches()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "db", "club-schema.sql")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var ddl = File.ReadAllText(Path.Combine(dir!.FullName, "db", "club-schema.sql"));
        ddl = System.Text.RegularExpressions.Regex.Replace(ddl, @"([ \t])json([ \t]+NULL)", "$1nvarchar(max)$2");
        return System.Text.RegularExpressions.Regex.Split(ddl, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Where(b => !string.IsNullOrWhiteSpace(b));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync();
    }
}
