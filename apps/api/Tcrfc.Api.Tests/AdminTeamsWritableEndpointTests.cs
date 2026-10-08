using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 「我能寫哪些球隊」唯讀端點（<c>GET /api/v1/admin/{club}/teams/writable?module=...</c>，
/// <c>AdminTeamsEndpoints</c>）——回應主站 coordinator 任務：C1–C4 的「參賽球隊／所屬球隊」下拉
/// 選單目前列出整個俱樂部全部球隊，不分一線隊／學院／個別授權，S1-8 的列級授權
/// （<c>Security/TeamRowScope.cs</c>）只擋得住寫入端點。這支測試驗證兩種
/// <c>role_permissions.scope_type</c>：<c>all</c>（系統管理員全部看得到）、<c>academy_only</c>
/// （只看得到學院梯隊）。
///
/// 打真正的 HTTP 管線與真正的 <c>tcrfc_club</c>，**用 <c>bw</c> 俱樂部**（三支球隊：
/// <c>BW1</c> 一線隊、<c>BW-U15</c>／<c>BW-U12</c> 學院梯隊），不用 <c>tcrfc</c>（只有 <c>D1</c>
/// 一支球隊，示範不出「收斂成部分球隊」的過濾效果）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminTeamsWritableEndpointTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task module參數缺漏或不支援_擋下400()
    {
        using var client = await CreateClientAsync("super.admin@tcrfc.test");

        var missing = await client.GetAsync("/api/v1/admin/bw/teams/writable");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var invalid = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=coach");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task 系統管理員_看得到俱樂部全部球隊()
    {
        using var client = await CreateClientAsync("super.admin@tcrfc.test");

        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
        Assert.NotNull(teams);
        var codes = teams!.Select(t => t.Code).ToHashSet();
        Assert.Contains("BW1", codes);
        Assert.Contains("BW-U15", codes);
        Assert.Contains("BW-U12", codes);
    }

    [Fact]
    public async Task academy_only角色_只看得到學院梯隊_看不到一線隊()
    {
        // academy.manager@tcrfc.test：academy_program 角色，只授權 bw，team.team.* 為 academy_only
        // （S1-8 種子資料，見 apps/api/README.md「新增測試帳號」）。
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");

        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
        Assert.NotNull(teams);
        var codes = teams!.Select(t => t.Code).ToHashSet();
        Assert.Contains("BW-U15", codes);
        Assert.Contains("BW-U12", codes);
        Assert.DoesNotContain("BW1", codes); // 一線隊不是 academy 類型，即使同俱樂部也擋下。
        Assert.All(teams!, t => Assert.Equal("academy", t.Type));
    }

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

}
