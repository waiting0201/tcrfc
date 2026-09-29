using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminVenues;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <c>GET /api/v1/admin/{club}/venues</c>——S1-12d 後續缺口補完：全站共用場地主檔唯讀清單
/// （見 <c>Features/AdminVenues/AdminVenuesEndpoints.cs</c> 檔頭）。這支端點的內容與俱樂部無關
/// （<c>Venue</c> 不帶 <c>club_id</c>），路由段上的 <c>{club}</c> 只是借用既有授權管線。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminVenuesTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 沒有登入_回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/venues");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 兩組候選權限碼都沒有的角色_回403_且訊息不帶權限碼()
    {
        // content.editor@tcrfc.test 沒有 site.fact.view，也沒有 team.match.view（見
        // db/seed/generate-club-seed-sql.py ROLE_PERMISSIONS 的 content_editor 清單）。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/venues");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // E-52：介面不顯示權限碼——403 的 body 不得內插 site.fact.view／team.match.view 字面值。
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("site.fact.view", body);
        Assert.DoesNotContain("team.match.view", body);
    }

    [Fact]
    public async Task 只持有team_match_view的角色_可讀取()
    {
        // viewer@tcrfc.test 的角色只有 team.match.view（矩陣「球隊／賽事」唯讀），沒有
        // site.fact.view——用這個帳號驗證 AuthorizeAnyAsync 兩組候選碼「任一通過即可」的行為，
        // 不是只驗過 site.fact.view 那一條路徑。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/venues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 系統管理員_可讀取全站場地_含雙語名稱與地址()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/venues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<AdminVenueListItemDto>>(TestJson.Options);

        Assert.NotNull(list);
        // 全站至少三筆既有場地（見 db/seed：西屯足球場〔tcrfc〕、太原足球場／豐原體育場〔bw〕）——
        // 這支端點刻意不依 {club} 過濾，見上方檔頭說明。
        Assert.True(list!.Count >= 3, $"預期至少 3 筆場地，實際 {list.Count} 筆。");

        var xitun = Assert.Single(list, v => v.NameZh == "西屯足球場");
        Assert.Equal("Xitun Football Field", xitun.NameEn);
        Assert.Equal("台中市北屯區崇平路二段景谷巷 11 弄 41 號", xitun.Address);

        Assert.Contains(list, v => v.NameZh == "台中北屯太原足球場");
        Assert.Contains(list, v => v.NameZh == "台中市立豐原體育場");
    }

    [Fact]
    public async Task 回應內容與club路由段無關_磐石與藍鯨讀到同一份清單()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));

        var tcrfcList = await (await client.GetAsync("/api/v1/admin/tcrfc/venues"))
            .Content.ReadFromJsonAsync<List<AdminVenueListItemDto>>(TestJson.Options);
        var bwList = await (await client.GetAsync("/api/v1/admin/bw/venues"))
            .Content.ReadFromJsonAsync<List<AdminVenueListItemDto>>(TestJson.Options);

        Assert.NotNull(tcrfcList);
        Assert.NotNull(bwList);
        Assert.Equal(tcrfcList!.Select(v => v.Id).OrderBy(id => id), bwList!.Select(v => v.Id).OrderBy(id => id));
    }
}
