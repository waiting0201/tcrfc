using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Tcrfc.Api.CharityPlatform.Auth;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善後台是<b>獨立帳號體系</b>：登入、更新權杖輪替與重放偵測、鎖定、2FA，以及最重要的——<b>與主站後台互相隔離</b>
/// （主站的權杖在這裡不被接受，慈善的權杖在主站也不被接受；Cookie 名稱也不同，不會互相踢出登入）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminAuthTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Login = AdminBase + "/auth/login";
    private const string Refresh = AdminBase + "/auth/refresh";
    private const string Logout = AdminBase + "/auth/logout";
    private const string CookieName = "__Host-tcrfc-charity-admin-rt";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string username, string password, string? totp = null)
        => await client.PostAsJsonAsync(Login, new LoginRequest(username, password, totp), TestJson.WriteOptions);

    /// <summary>
    /// Kestrel／TestServer 不會替 http 請求回送帶 Secure 的 Cookie（<c>__Host-</c> 前綴要求 Secure），測試必須手動從
    /// Set-Cookie 取出原始值再自己附到下一個請求（見 AdminAuthTests 的同類做法）。
    /// </summary>
    private static string? ExtractRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var line = values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.OrdinalIgnoreCase));
        return line?[(CookieName.Length + 1)..].Split(';')[0];
    }

    private static HttpRequestMessage WithRefreshCookie(string path, string? raw)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path);
        if (raw is not null)
        {
            message.Headers.Add("Cookie", $"{CookieName}={raw}");
        }

        return message;
    }

    [Fact]
    public async Task 登入成功_回存取權杖_並設定慈善專屬的更新權杖Cookie()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();

        var response = await PostLoginAsync(client, admin.Username, CharityApiFixture.TestPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(admin.Username, body.Username);
        Assert.True(body.IsSuperAdmin);
        Assert.False(body.MustChangePassword);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var header = string.Join(";", cookies!).ToLowerInvariant();
        Assert.Contains(CookieName.ToLowerInvariant() + "=", header);
        Assert.DoesNotContain("__host-tcrfc-admin-rt=", header); // 絕不是主站的 Cookie 名稱（同一個 API 網域，同名會互相蓋掉）
        Assert.Contains("httponly", header);
        Assert.Contains("secure", header);
        Assert.Contains("samesite=none", header);
        Assert.DoesNotContain("domain=", header);                 // docs/14：__Host- 前綴絕不得設 Domain
    }

    [Fact]
    public async Task 登入_密碼錯誤與帳號不存在_回應一模一樣_不洩漏帳號是否存在()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();

        var wrongPassword = await PostLoginAsync(client, admin.Username, "WrongPassword-123");
        var unknownUser = await PostLoginAsync(client, $"ct-nobody-{Guid.NewGuid():N}@{CharityApiFixture.TestEmailDomain}", "WrongPassword-123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownUser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task 連續五次密碼錯誤鎖定帳號_鎖定期間即使密碼正確也是423()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(client, admin.Username, "WrongPassword-123")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Locked, (await PostLoginAsync(client, admin.Username, CharityApiFixture.TestPassword)).StatusCode);
    }

    [Fact]
    public async Task 停用的帳號不能登入_已簽出的權杖也立即失效()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AdminBase + "/auth/me")).StatusCode);

        await fx.ExecuteAsync("UPDATE admin_users SET status = N'disabled' WHERE id = @i", ("@i", admin.Id));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(AdminBase + "/auth/me")).StatusCode);
        using var anonymous = fx.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword)).StatusCode);
    }

    [Fact]
    public async Task 更新權杖輪替_舊權杖被重放時撤銷整個帳號的全部有效權杖()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();
        var cookie1 = ExtractRefreshCookie(await PostLoginAsync(client, admin.Username, CharityApiFixture.TestPassword));
        Assert.NotNull(cookie1);

        // 正常輪替：換到 cookie2，cookie1 被撤銷。
        var rotated = await client.SendAsync(WithRefreshCookie(Refresh, cookie1));
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var cookie2 = ExtractRefreshCookie(rotated);
        Assert.NotNull(cookie2);
        Assert.NotEqual(cookie1, cookie2);
        Assert.False(string.IsNullOrWhiteSpace((await rotated.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!.AccessToken));

        // 重放 cookie1（被偷過的徵兆）：401，而且連 cookie2 也一起被撤銷。
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(WithRefreshCookie(Refresh, cookie1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(WithRefreshCookie(Refresh, cookie2))).StatusCode);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM admin_refresh_tokens WHERE admin_user_id = @i AND revoked_at IS NULL", ("@i", admin.Id)));
    }

    [Fact]
    public async Task 更新權杖_沒帶Cookie或亂填_一律401_且只存雜湊不存原始值()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(WithRefreshCookie(Refresh, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(WithRefreshCookie(Refresh, "garbage-token"))).StatusCode);

        var raw = ExtractRefreshCookie(await PostLoginAsync(client, admin.Username, CharityApiFixture.TestPassword))!;
        var stored = await fx.ScalarAsync<string>("SELECT TOP 1 token_hash FROM admin_refresh_tokens WHERE admin_user_id = @i", ("@i", admin.Id));
        Assert.NotNull(stored);
        Assert.NotEqual(raw, stored);
        Assert.DoesNotContain(raw, stored!);
    }

    [Fact]
    public async Task 登出_撤銷更新權杖_之後無法再更新()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();
        var cookie = ExtractRefreshCookie(await PostLoginAsync(client, admin.Username, CharityApiFixture.TestPassword));

        var logout = await client.SendAsync(WithRefreshCookie(Logout, cookie));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(WithRefreshCookie(Refresh, cookie))).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 與主站後台隔離
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 主站後台的權杖在慈善後台一律當作沒登入_慈善的權杖在主站也不被接受()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        var clubToken = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test");

        using var charityClient = fx.CreateClient();
        charityClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clubToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await charityClient.GetAsync(AdminBase + "/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await charityClient.GetAsync(AdminBase + "/stores")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await charityClient.GetAsync(AdminBase + "/donations")).StatusCode);

        using var clubClient = fx.CreateClient();
        clubClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await clubClient.GetAsync("/api/v1/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task 即使用主站的金鑰簽出帶慈善issuer與audience的權杖_慈善也不接受()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSigningKey.Value));
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            CharityTokenService.Issuer, CharityTokenService.Audience,
            [new System.Security.Claims.Claim("sub", admin.Id.ToString()), new System.Security.Claims.Claim("username", admin.Username)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

        using var client = fx.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(AdminBase + "/auth/me")).StatusCode);
    }

    [Fact]
    public async Task 沒有權杖或權杖壞掉_所有後台端點一律401()
    {
        using var client = fx.CreateClient();
        string[] paths =
        [
            "/auth/me", "/stores", "/projects", "/donations", "/donations/anomalies", "/donations/anomalies/counts",
            "/projects/charity-refs", "/donations/export",
        ];

        foreach (var path in paths)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(AdminBase + path)).StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(AdminBase + "/stores")).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 個人檔案、改密碼、2FA
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 個人檔案_回角色與持有的權限碼_不含任何機密欄位()
    {
        var viewer = await fx.CreateAdminAsync(false, "viewer");
        var sysadmin = await fx.CreateAdminAsync(isSuperAdmin: true);

        using var viewerClient = fx.CreateClientFor(viewer);
        var meBody = await viewerClient.GetStringAsync(AdminBase + "/auth/me");
        var me = System.Text.Json.JsonSerializer.Deserialize<CharityMeResponse>(meBody, TestJson.Options)!;

        Assert.False(me.IsSuperAdmin);
        Assert.Contains(me.Roles, r => r.Code == "viewer");
        Assert.Contains("n3.donation.view", me.Permissions);
        Assert.DoesNotContain("n3.donation.refund", me.Permissions);
        Assert.DoesNotContain("n3.donation.reveal", me.Permissions);
        Assert.DoesNotContain("hash", meBody, StringComparison.OrdinalIgnoreCase);   // 不含密碼雜湊（mustChangePassword 旗標本身是允許的）
        Assert.DoesNotContain("secret", meBody, StringComparison.OrdinalIgnoreCase);

        using var sysClient = fx.CreateClientFor(sysadmin);
        var sys = (await sysClient.GetFromJsonAsync<CharityMeResponse>(AdminBase + "/auth/me", TestJson.Options))!;
        Assert.Contains("n3.donation.refund", sys.Permissions); // 系統管理員持有全部（含 sysadmin_only）
    }

    [Fact]
    public async Task 改密碼_目前密碼不對回401_新密碼太短回400_成功後可用新密碼登入()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClientFor(admin);

        var wrong = await client.PostAsJsonAsync(AdminBase + "/auth/change-password", new ChangePasswordRequest("nope", "NewPassword-12345"), TestJson.WriteOptions);
        var tooShort = await client.PostAsJsonAsync(AdminBase + "/auth/change-password", new ChangePasswordRequest(CharityApiFixture.TestPassword, "short"), TestJson.WriteOptions);
        var ok = await client.PostAsJsonAsync(AdminBase + "/auth/change-password", new ChangePasswordRequest(CharityApiFixture.TestPassword, "NewPassword-12345"), TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        using var anonymous = fx.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(anonymous, admin.Username, "NewPassword-12345")).StatusCode);
    }

    [Fact]
    public async Task 兩階段驗證_選用_已啟用者登入必須帶驗證碼_密鑰用慈善專屬用途加密()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClientFor(admin);

        var setup = (await (await client.PostAsync(AdminBase + "/auth/2fa/setup", null)).Content.ReadFromJsonAsync<TwoFactorSetupResponse>(TestJson.Options))!;
        var secret = TotpService.FromBase32(setup.Secret);
        var stored = await fx.ScalarAsync<string>("SELECT two_factor_secret_encrypted FROM admin_users WHERE id = @i", ("@i", admin.Id));
        Assert.DoesNotContain(setup.Secret, stored!); // 密文，不是明碼

        // 還沒確認前，登入不要求驗證碼（選用功能，尚未啟用）
        using var anonymous = fx.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword)).StatusCode);

        var confirm = await client.PostAsJsonAsync(AdminBase + "/auth/2fa/confirm", new TwoFactorConfirmRequest(TotpService.GenerateCurrentCodeForTesting(secret)), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var needCode = await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword);
        Assert.Equal(HttpStatusCode.OK, needCode.StatusCode);
        Assert.Contains("totp_required", await needCode.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword, "000000")).StatusCode);
        var ok = await PostLoginAsync(anonymous, admin.Username, CharityApiFixture.TestPassword, TotpService.GenerateCurrentCodeForTesting(secret));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await ok.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!.AccessToken));
    }
}
