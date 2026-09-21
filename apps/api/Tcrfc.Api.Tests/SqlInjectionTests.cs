using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 參數化查詢——全程沒有任何字串拼接 SQL（Dapper 的 <c>CommandDefinition</c> 一律用具名參數）。
/// 對應 apps/api/README.md「SQL Injection」驗收紀錄。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SqlInjectionTests(ApiFixture fixture)
{
    [Fact]
    public async Task team參數帶惡意SQL片段_回傳空結果_資料表安然無恙()
    {
        using var client = fixture.CreateClient();

        var maliciousResponse = await client.GetAsync(
            "/api/v1/tcrfc/players?team=" + Uri.EscapeDataString("D1';DROP TABLE players;--"));

        // 惡意輸入被當成一個合法但查無資料的球隊代碼字串處理——不是 500、不是拋例外，
        // 也沒有真的執行 DROP TABLE。
        Assert.Equal(System.Net.HttpStatusCode.OK, maliciousResponse.StatusCode);
        var maliciousResult = await maliciousResponse.Content.ReadFromJsonAsync<PagedResult<PlayerDto>>(TestJson.Options);
        Assert.NotNull(maliciousResult);
        Assert.Equal(0, maliciousResult!.TotalCount);

        // 資料表確實還在、資料還在——不是「剛好回傳空清單但其實表已經被砍了」。
        var sanityCheckResult = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=1", TestJson.Options);
        Assert.NotNull(sanityCheckResult);
        Assert.True(sanityCheckResult!.TotalCount > 0, "players 表應該仍然存在且有資料，若為 0 代表資料表可能已被本測試的惡意輸入影響");
    }
}
