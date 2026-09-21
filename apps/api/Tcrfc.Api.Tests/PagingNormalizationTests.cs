using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 分頁參數正規化——對應 <see cref="Tcrfc.Api.Common.PagingQuery.Normalize"/>：
/// page&lt;=0 → 1；pageSize&lt;=0 → 該端點預設值；超過上限 → 截到上限。
/// 用球員端點驗證（README：預設 50，上限 200）。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PagingNormalizationTests(ApiFixture fixture)
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    public async Task page小於等於零時正規化為1(int requestedPage, int expectedPage)
    {
        using var client = fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            $"/api/v1/tcrfc/players?page={requestedPage}", TestJson.Options);

        Assert.NotNull(result);
        Assert.Equal(expectedPage, result!.Page);
    }

    [Fact]
    public async Task pageSize小於等於零時使用該端點預設值()
    {
        using var client = fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=0", TestJson.Options);

        Assert.NotNull(result);
        Assert.Equal(50, result!.PageSize); // PlayersEndpoints 的 defaultPageSize
    }

    [Fact]
    public async Task pageSize超過上限時截斷為上限_不會一次撈整張表()
    {
        using var client = fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=999999", TestJson.Options);

        Assert.NotNull(result);
        Assert.Equal(200, result!.PageSize); // PlayersEndpoints 的 maxPageSize
    }
}
