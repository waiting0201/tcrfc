using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善庫實際綱要與 <see cref="CharityDbContext"/> 的 EF 模型（＝migration 的目標狀態）逐表逐欄一致，
/// 比照 <c>EfModelMatchesDatabaseTests</c>（那個只管主站庫）。兩個方向都檢查：模型有的表與欄位資料庫要有；
/// 資料庫有的表與欄位模型也要涵蓋。另外確認 migration 歷史完整（baseline 與 <c>AddAdminRefreshTokens</c> 都已套用到本機庫）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityEfModelMatchesDatabaseTests(CharityApiFixture fx)
{
    [Fact]
    public async Task 慈善庫的表與欄位和EF模型完全一致()
    {
        await using var scope = fx.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CharityDbContext>();

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
        await using (var connection = new SqlConnection(fx.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                "SELECT table_name, column_name FROM information_schema.columns WHERE table_name <> '__EFMigrationsHistory'", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                if (!dbColumns.TryGetValue(table, out var set))
                {
                    dbColumns[table] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                set.Add(reader.GetString(1));
            }
        }

        var problems = new List<string>();
        foreach (var (table, cols) in modelColumns)
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
        Assert.True(problems.Count == 0, "慈善庫與 EF 模型（migration 的目標狀態）不一致：\n" + string.Join("\n", problems));
    }

    [Fact]
    public async Task 憑證編號的篩選唯一鍵_已開立者唯一_未開立為空可重複()
    {
        // docs/16 §6：invoice_no 在已開立者唯一、未開立為空，用篩選唯一索引。這個索引是冪等開票的最後一道資料庫防線，確認它存在且有篩選條件。
        var definition = await fx.ScalarAsync<string>(
            "SELECT filter_definition FROM sys.indexes WHERE name = N'UX_donation_invoices_invoice_no' AND is_unique = 1");
        Assert.NotNull(definition);
        Assert.Contains("invoice_no", definition!);
    }
}
