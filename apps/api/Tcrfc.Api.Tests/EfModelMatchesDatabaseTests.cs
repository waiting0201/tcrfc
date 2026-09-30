using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Data;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 本機庫實際綱要與 EF 模型（＝migration 的目標狀態）逐表逐欄一致（D 批收尾，2026-09-30：C 批的 migration 一度沒有在本機執行、歷史對不上，
/// 靠人工比對很容易漏）。做兩個方向：EF 模型的每個表與欄位在資料庫都存在；資料庫的每個表與欄位也都被 EF 模型涵蓋（沒有「資料庫多出一欄但模型不知道」）。
/// 這不比對型別與約束（那是 <c>dotnet ef migrations has-pending-model-changes</c> 對照 snapshot 的工作），只擋最常出事的「表或欄位對不上」。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class EfModelMatchesDatabaseTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 資料庫的表與欄位和EF模型完全一致()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
        var modelColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null)
            {
                continue;
            }

            var id = StoreObjectIdentifier.Table(table, entity.GetSchema());
            if (!modelColumns.TryGetValue(table, out var set))
            {
                modelColumns[table] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var property in entity.GetProperties())
            {
                set.Add(property.GetColumnName(id)!);
            }
        }

        var dbColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var views = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var connection = new SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")))
        {
            await connection.OpenAsync();
            await using (var command = new SqlCommand("SELECT table_name FROM information_schema.views", connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) { views.Add(reader.GetString(0)); }
            }

            await using var columns = new SqlCommand("SELECT table_name, column_name FROM information_schema.columns", connection);
            await using var r = await columns.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var table = r.GetString(0);
                if (table == "__EFMigrationsHistory" || views.Contains(table)) { continue; }
                if (!dbColumns.TryGetValue(table, out var set)) { dbColumns[table] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
                set.Add(r.GetString(1));
            }
        }

        var problems = new List<string>();
        foreach (var (table, cols) in modelColumns.Where(m => !views.Contains(m.Key)))
        {
            if (!dbColumns.TryGetValue(table, out var actual))
            {
                problems.Add($"EF 模型有表 {table}，資料庫沒有。");
                continue;
            }

            problems.AddRange(cols.Except(actual, StringComparer.OrdinalIgnoreCase).Select(c => $"{table}.{c}：EF 模型有、資料庫沒有。"));
            problems.AddRange(actual.Except(cols, StringComparer.OrdinalIgnoreCase).Select(c => $"{table}.{c}：資料庫有、EF 模型沒有。"));
        }

        problems.AddRange(dbColumns.Keys.Except(modelColumns.Keys, StringComparer.OrdinalIgnoreCase).Select(t => $"資料庫有表 {t}，EF 模型沒有。"));
        Assert.True(problems.Count == 0, "本機庫與 EF 模型（migration 的目標狀態）不一致：\n" + string.Join("\n", problems));
    }
}
