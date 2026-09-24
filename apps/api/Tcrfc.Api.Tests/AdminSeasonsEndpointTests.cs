using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <c>GET /api/v1/admin/{club}/seasons</c>——前端 agent 回報缺口②之一，賽事系列表單的球季下拉
/// 選單。俱樂部範圍端點，權限碼比照同模組既有的 <c>team.competition.view</c>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminSeasonsEndpointTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 沒有登入_回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/seasons");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 沒有該俱樂部授權_回403()
    {
        // content.editor@tcrfc.test 只被授權 tcrfc，打 bw 應該被 IAdminClubAuthorizer 擋下。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/bw/seasons");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 有權限的角色_看得到這個俱樂部的球季清單()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/seasons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var seasons = await response.Content.ReadFromJsonAsync<IReadOnlyList<AdminSeasonListItemDto>>(TestJson.Options);
        Assert.NotNull(seasons);
        Assert.Contains(seasons!, s => s.Code == "2026-27");
        // 俱樂部範圍：不會混進 bw 的球季（bw 種子資料有 2023／2025，tcrfc 沒有這兩個代號）。
        Assert.DoesNotContain(seasons, s => s.Code is "2023" or "2025");
    }

    [Fact]
    public async Task 檢視者角色_也能查詢球季清單_屬唯讀權限()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/seasons");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
