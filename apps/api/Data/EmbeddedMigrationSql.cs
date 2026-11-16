using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tcrfc.Api.Data;

/// <summary>
/// 讓 EF migration 執行與 <c>db/migrations/*.sql</c> 同源的資料遷移腳本（docs/20 §5「資料遷移以 EF migration 包裝」）。
/// 腳本檔以內嵌資源（<c>Data/Migrations/Sql/*.sql</c>，與 db/migrations 逐位元組相同，測試比對）讀入，
/// 依 <c>GO</c> 切批，每一批包成 <c>EXEC(N'…')</c> 再交給 <see cref="MigrationBuilder.Sql(string, bool)"/>。
/// 為什麼要 EXEC：EF 的 idempotent script 會把一支 migration 的所有命令放進同一個
/// <c>IF NOT EXISTS (…__EFMigrationsHistory…) BEGIN … END</c>（同一個 T-SQL 批次），原樣貼上會讓
/// 各批的 <c>DECLARE @id</c> 重複宣告（Msg 134）；EXEC 讓每一批擁有自己的變數範圍，也保留原本「批與批互不相干」的語意。
/// 腳本自帶的 BEGIN TRAN／COMMIT 在 EF 的外層交易內成為巢狀交易（最後由外層一次提交，整支 migration 全有或全無）；
/// 事後斷言的 THROW 會讓 sqlcmd -b／EF 中止並回滾。
/// </summary>
public static class EmbeddedMigrationSql
{
    // 每批開頭重設連線層級選項：SET 只在 EXEC 範圍內有效，檔頭那一批的 SET 帶不進後面各批。
    private const string Prelude = "SET NOCOUNT ON; SET XACT_ABORT ON;\n";

    private static readonly Regex GoLine = new(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static string Read(string resourceName)
    {
        var asm = typeof(EmbeddedMigrationSql).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"找不到內嵌的遷移腳本資源：{resourceName}");
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<string> Batches(string sql) =>
        GoLine.Split(sql).Select(b => b.Trim()).Where(b => b.Length > 0).ToList();

    public static string WrapBatch(string batch) =>
        "EXEC(N'" + (Prelude + batch + "\n").Replace("'", "''") + "');";

    public static void Run(MigrationBuilder migrationBuilder, string resourceName)
    {
        foreach (var batch in Batches(Read(resourceName)))
        {
            migrationBuilder.Sql(WrapBatch(batch));
        }
    }
}
