using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// club_id 過濾繞不過去的實測——對應 docs/14-invariants.md、主站規劃書 §5.4，
/// 以及 apps/api/README.md「club_id 強制機制」段落已用 curl 手動驗證過的四個情境，
/// 這裡把它們變成自動化回歸測試。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ClubScopingTests(ApiFixture fixture)
{
    [Fact]
    public async Task 跨俱樂部讀清單_藍鯨球員數為零_主站不為零()
    {
        using var client = fixture.CreateClient();

        var tcrfcResult = await client.GetFromJsonAsync<PagedResult<PlayerDto>>("/api/v1/tcrfc/players?pageSize=200", TestJson.Options);
        var bwResult = await client.GetFromJsonAsync<PagedResult<PlayerDto>>("/api/v1/bw/players?pageSize=200", TestJson.Options);

        Assert.NotNull(tcrfcResult);
        Assert.NotNull(bwResult);
        Assert.True(tcrfcResult!.TotalCount > 0, "種子資料應該有 tcrfc 球員，若為 0 代表種子資料或連線設定有問題，不是本測試要驗證的行為");
        Assert.Equal(0, bwResult!.TotalCount);
    }

    [Fact]
    public async Task 不存在的俱樂部代碼_回傳404()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist/players");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task 跨俱樂部讀單篇文章_同一個slug換club路由_回傳404不是內容()
    {
        using var client = fixture.CreateClient();

        // 先找一篇真的存在於 tcrfc 底下的文章 slug，不要硬編碼假設種子資料的確切內容。
        var list = await client.GetFromJsonAsync<PagedResult<ArticleListItemDto>>("/api/v1/tcrfc/news?pageSize=1", TestJson.Options);
        Assert.NotNull(list);
        Assert.True(list!.Items.Count > 0, "種子資料應該至少有一篇 tcrfc 新聞，若沒有代表種子資料有問題");
        var slug = list.Items[0].Slug;

        // 確認用 tcrfc 路由讀得到這篇（先確認前提成立，避免下面的 404 只是因為 slug 打錯）。
        var ownClubResponse = await client.GetAsync($"/api/v1/tcrfc/news/{slug}");
        Assert.Equal(HttpStatusCode.OK, ownClubResponse.StatusCode);

        // 同一個 slug，換成 bw 的路由 —— WHERE (club_id = @ClubId OR club_id IS NULL) 應該擋下來。
        var crossClubResponse = await client.GetAsync($"/api/v1/bw/news/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, crossClubResponse.StatusCode);
    }
}
