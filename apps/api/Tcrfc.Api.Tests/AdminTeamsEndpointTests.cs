using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <c>GET /api/v1/admin/teams</c>——前端 agent 回報缺口②之二，J4「球隊授權」畫面用的跨俱樂部球隊
/// 下拉選單。全域端點（無 <c>{club}</c> 路由段），權限碼比照同模組既有的
/// <c>system.team_grant.view</c>（sysadmin_only）。見 <c>AdminTeamsEndpoints</c> 檔頭「怎麼讓它
/// 拿得到跨俱樂部的球隊清單」的完整說明。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminTeamsEndpointTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 沒有登入_回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 非系統管理員角色_回403()
    {
        // content_editor 角色沒有任何 system.* 權限碼（sysadmin_only，見種子腳本）。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/teams");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 系統管理員_一次拿到跨俱樂部的球隊清單()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminTeamListItemDto>>(TestJson.Options);
        Assert.NotNull(teams);
        // 🔴 核心斷言：同一次呼叫裡同時看得到兩個俱樂部的球隊，證明真的是跨俱樂部查詢，
        // 不是被哪一個俱樂部的範圍過濾卡住。
        Assert.Contains(teams!, t => t.ClubCode == "tcrfc" && t.Code == "D1");
        Assert.Contains(teams, t => t.ClubCode == "bw" && t.Code == "BW1");
    }

    [Fact]
    public async Task 用clubCode篩選_只回那個俱樂部的球隊()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/teams?clubCode=bw");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminTeamListItemDto>>(TestJson.Options);
        Assert.NotNull(teams);
        Assert.NotEmpty(teams!);
        Assert.All(teams, t => Assert.Equal("bw", t.ClubCode));
    }
}
