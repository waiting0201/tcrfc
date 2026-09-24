using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <c>GET /api/v1/admin/auth/me</c>——前端 agent 回報缺口①（站台切換器需要的個人檔案與俱樂部授權
/// 清單）。見 <c>Features/AdminAuth/AdminAuthService.GetMeAsync</c> 上的完整說明。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminMeEndpointTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 沒有登入_回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 一般角色_只看得到自己被授權且未過期的俱樂部()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options);
        Assert.NotNull(me);
        Assert.Equal("content.editor@tcrfc.test", me!.Username);
        Assert.False(me.IsSuperAdmin);
        Assert.Contains(me.Roles, r => r.Code == "content_editor");
        Assert.Single(me.ClubGrants); // 種子資料只授權 tcrfc
        Assert.Equal("tcrfc", me.ClubGrants[0].ClubCode);
        // ⚠️ 種子資料（db/seed/generate-club-seed-sql.py §18.4）從未對任何帳號設定
        // primary_club_id（一律 NULL），所以這裡不斷言 IsPrimary 為真——正確行為就是
        // PrimaryClubId 為 null 時任何俱樂部都不是「預設」，見 PrimaryClubCode 那個斷言。
        Assert.Null(me.PrimaryClubCode);
        // ⛔ 這是回應形狀本身的保證：MeResponse 沒有任何密碼雜湊／2FA 密文欄位可以序列化出來
        // （見 AdminAuthDtos.cs），不需要另外斷言「不包含某個字串」。
    }

    [Fact]
    public async Task 授權已過期的帳號_該筆授權不會出現在清單裡()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("expired.grant@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options);
        Assert.NotNull(me);
        Assert.Empty(me!.ClubGrants); // 種子資料把這個帳號的 tcrfc 授權設為昨天到期
    }

    [Fact]
    public async Task 系統管理員_看得到全部啟用中的俱樂部_不受AdminUserClub限制()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options);
        Assert.NotNull(me);
        Assert.True(me!.IsSuperAdmin);
        // super.admin@tcrfc.test 的種子資料完全沒有 AdminUserClub 授權列，
        // 但身為系統管理員仍應看到全部啟用中的俱樂部（至少 tcrfc／bw 兩個）。
        Assert.Contains(me.ClubGrants, g => g.ClubCode == "tcrfc");
        Assert.Contains(me.ClubGrants, g => g.ClubCode == "bw");
        Assert.All(me.ClubGrants, g => Assert.Null(g.ExpiresOn));
    }
}
