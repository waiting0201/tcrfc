using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Security;
using Tcrfc.Api.Features.MemberAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S2-11 會員前台：註冊、驗證、登入、更新權杖、鎖定、忘記／重設／變更密碼、個人資料、刪除帳號、LINE 登入與綁定、權杖隔離。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class MemberAuthTests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private static StringContent Json(object o) => BizTest.Json(o);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static string? CodeOf(JsonElement problem) => problem.TryGetProperty("code", out var c) ? c.GetString() : null;

    private static string? RefreshCookieValue(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var line = values.FirstOrDefault(v => v.StartsWith(MemberAuthEndpoints.RefreshCookieName + "=", StringComparison.Ordinal));
        return line?.Split(';')[0][(MemberAuthEndpoints.RefreshCookieName.Length + 1)..];
    }

    // ═════════════ 註冊與驗證 ═════════════

    [Fact]
    public async Task 註冊_成功建立未驗證帳號並寄出驗證信_重複Email與弱密碼被擋()
    {
        using var client = _scope.Client();
        var email = _scope.NewEmail("reg");
        var response = await client.PostAsync("/api/v1/member/auth/register",
            Json(new { club = "tcrfc", email = email.ToUpperInvariant(), password = "Abcdefg1", name = "【M測試】註冊", lang = "en" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("emailVerificationRequired").GetBoolean());
        Assert.True(body.GetProperty("emailSent").GetBoolean());
        Assert.StartsWith("M", body.GetProperty("memberNo").GetString());
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        // Email 一律存小寫；驗證信是英文（lang=en），連結帶 token
        var mail = MemberTestDoubles.Email.LastTo(email, "verify");
        Assert.NotNull(mail);
        Assert.Contains("verify your email", mail!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/en/member/verify-email?token=", mail.TextBody);

        // 密碼不是明文存放
        var stored = await BizTest.ScalarGuidAsync("SELECT id FROM members WHERE email = @E AND password_hash LIKE '$argon2id$%' AND email_verified_at IS NULL", ("@E", email));
        Assert.NotEqual(Guid.Empty, stored);

        var dup = await client.PostAsync("/api/v1/member/auth/register", Json(new { club = "tcrfc", email, password = "Abcdefg1", name = "重複" }));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("email_taken", CodeOf(await ReadJsonAsync(dup)));

        foreach (var weak in new[] { "short1", "allletters", "12345678", "password123" })
        {
            var r = await client.PostAsync("/api/v1/member/auth/register", Json(new { club = "tcrfc", email = _scope.NewEmail("weak"), password = weak, name = "弱" }));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/register",
            Json(new { club = "tcrfc", email = "not-an-email", password = "Abcdefg1", name = "x" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/member/auth/register",
            Json(new { club = "nope", email = _scope.NewEmail("noclub"), password = "Abcdefg1", name = "x" }))).StatusCode);
    }

    [Fact]
    public async Task Email驗證_完成後成為一般會員並有會員卡_無效或錯用途的權杖被擋()
    {
        using var client = _scope.Client();
        var email = _scope.NewEmail("verify");
        await client.PostAsync("/api/v1/member/auth/register", Json(new { club = "tcrfc", email, password = "Abcdefg1", name = "【M測試】驗證" }));
        var token = CapturingEmailSender.TokenOf(MemberTestDoubles.Email.LastTo(email, "verify")!);

        // 未驗證前不能登入（密碼正確才告知未驗證）
        var early = await client.PostAsync("/api/v1/member/auth/login", Json(new { email, password = "Abcdefg1" }));
        Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
        Assert.Equal("email_not_verified", CodeOf(await ReadJsonAsync(early)));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/verify-email", Json(new { token = token + "x" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/verify-email", Json(new { token = "" }))).StatusCode);

        var ok = await client.PostAsync("/api/v1/member/auth/verify-email", Json(new { token }));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        // 冪等：再點一次仍成功
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/member/auth/verify-email", Json(new { token }))).StatusCode);

        var session = await MemberTestScope.LoginAsync(client, email, "Abcdefg1");
        using var me = _scope.Client(session.AccessToken);
        var memberships = await ReadJsonAsync(await me.GetAsync("/api/v1/member/memberships"));
        var first = memberships.GetProperty("memberships")[0];
        Assert.Equal("tcrfc", first.GetProperty("club").GetProperty("code").GetString());
        Assert.Equal("registered", first.GetProperty("tier").GetString());
        Assert.Equal("active", first.GetProperty("status").GetString());
        Assert.Equal(1, first.GetProperty("cards").GetArrayLength());

        // 重設密碼用途的權杖不能拿來驗證 Email
        await client.PostAsync("/api/v1/member/auth/forgot-password", Json(new { email, club = "tcrfc" }));
        var resetToken = CapturingEmailSender.TokenOf(MemberTestDoubles.Email.LastTo(email, "reset")!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/verify-email", Json(new { token = resetToken }))).StatusCode);
    }

    [Fact]
    public async Task 重寄驗證信與忘記密碼_一律回202_不洩漏Email是否註冊()
    {
        using var client = _scope.Client();
        var before = MemberTestDoubles.Email.Sent.Count;
        var unknown = _scope.NewEmail("ghost");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/member/auth/forgot-password", Json(new { email = unknown, club = "tcrfc" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/member/auth/resend-verification", Json(new { email = unknown, club = "tcrfc" }))).StatusCode);
        Assert.Equal(before, MemberTestDoubles.Email.Sent.Count); // 不存在的帳號沒有寄出任何信
    }

    // ═════════════ 登入、更新權杖 ═════════════

    [Fact]
    public async Task 登入_失敗訊息一致_成功後Cookie模式與Body模式各自正確()
    {
        var m = await _scope.CreateVerifiedMemberAsync("login");
        using var client = _scope.Client();

        var wrong = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = "Wrong-pass1" }));
        var unknown = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = "nobody-" + Guid.NewGuid().ToString("N") + "@example.test", password = "Wrong-pass1" }));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var wrongBody = await ReadJsonAsync(wrong);
        var unknownBody = await ReadJsonAsync(unknown);
        Assert.Equal(wrongBody.GetProperty("detail").GetString(), unknownBody.GetProperty("detail").GetString()); // 不洩漏帳號存不存在
        Assert.Equal("invalid_credentials", CodeOf(wrongBody));

        // Cookie 模式（預設）：更新權杖只在 HttpOnly Cookie，回應本文沒有
        var cookieLogin = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password, rememberMe = true }));
        Assert.Equal(HttpStatusCode.OK, cookieLogin.StatusCode);
        var cookieHeader = string.Join(";", cookieLogin.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("__host-tcrfc-member-rt=", cookieHeader);
        Assert.Contains("httponly", cookieHeader);
        Assert.Contains("secure", cookieHeader);
        Assert.Contains("samesite=none", cookieHeader);
        Assert.DoesNotContain("domain=", cookieHeader);
        Assert.Contains("expires=", cookieHeader); // 記住我：持久 Cookie
        var cookieBody = await ReadJsonAsync(cookieLogin);
        Assert.False(cookieBody.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String);
        Assert.False(cookieBody.GetProperty("member").GetProperty("hasPassword").ValueKind == JsonValueKind.Null);

        // 沒勾記住我：工作階段 Cookie（沒有 expires）
        var sessionLogin = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }));
        Assert.DoesNotContain("expires=", string.Join(";", sessionLogin.Headers.GetValues("Set-Cookie")).ToLowerInvariant());

        // Body 模式：沒有 Set-Cookie、回應本文有更新權杖
        var bodyLogin = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password, tokenDelivery = "body" }));
        Assert.False(bodyLogin.Headers.Contains("Set-Cookie"));
        Assert.False(string.IsNullOrEmpty((await ReadJsonAsync(bodyLogin)).GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task 更新權杖_輪替後舊的失效_重放偵測會撤銷整批_登出登出全部裝置()
    {
        var m = await _scope.CreateVerifiedMemberAsync("refresh");
        using var client = _scope.Client();
        var first = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);
        var second = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password); // 第二台裝置

        // 輪替（Body 模式）
        var rotated = await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = first.RefreshToken }));
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var newRefresh = (await ReadJsonAsync(rotated)).GetProperty("refreshToken").GetString();
        Assert.NotEqual(first.RefreshToken, newRefresh);

        // 舊的再用 → 401，且判定外流：整批撤銷（連新的、連第二台裝置的都失效）
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = first.RefreshToken }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = newRefresh }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = second.RefreshToken }))).StatusCode);

        // Cookie 模式更新：手動帶 Cookie（TestServer 不會替 Secure Cookie 自動回傳）
        var login = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }));
        var cookie = RefreshCookieValue(login)!;
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/member/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"{MemberAuthEndpoints.RefreshCookieName}={cookie}");
        var viaCookie = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, viaCookie.StatusCode);
        Assert.NotEqual(cookie, RefreshCookieValue(viaCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", null)).StatusCode); // 沒帶

        // 登出：該權杖撤銷
        var a = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/member/auth/logout", Json(new { refreshToken = a.RefreshToken }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = a.RefreshToken }))).StatusCode);

        // 登出全部裝置（需要登入）
        var b = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);
        var c = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/logout-all", null)).StatusCode);
        using var authed = _scope.Client(b.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await authed.PostAsync("/api/v1/member/auth/logout-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = b.RefreshToken }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = c.RefreshToken }))).StatusCode);
    }

    [Fact]
    public async Task 登入失敗次數限制_五次鎖定十五分鐘_鎖定中連正確密碼也不放行_成功登入歸零()
    {
        var m = await _scope.CreateVerifiedMemberAsync("lock");
        using var client = _scope.Client();
        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = "Wrong-pass1" }))).StatusCode);
        }

        // 第 5 次失敗前先用正確密碼成功一次 → 計數歸零
        await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);
        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = "Wrong-pass1" }))).StatusCode);
        }

        var locked = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }));
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        var problem = await ReadJsonAsync(locked);
        Assert.Equal("account_locked", CodeOf(problem));
        Assert.True(problem.TryGetProperty("lockedUntil", out _));

        await BizTest.ExecuteSqlAsync("UPDATE members SET locked_until = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE email = @E", ("@E", m.Email));
        await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password); // 鎖定到期即可登入
    }

    [Fact]
    public async Task 停用的帳號_密碼正確才告知已停用_既有權杖立即失效()
    {
        var m = await _scope.CreateVerifiedMemberAsync("suspend");
        using var client = _scope.Client();
        await BizTest.ExecuteSqlAsync("UPDATE members SET status = 'suspended' WHERE email = @E", ("@E", m.Email));
        var wrong = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = "Wrong-pass1" }));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var right = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }));
        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        Assert.Equal("account_suspended", CodeOf(await ReadJsonAsync(right)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await m.Client.GetAsync("/api/v1/member/me")).StatusCode); // 即時查庫，不必等權杖過期
    }

    // ═════════════ 密碼 ═════════════

    [Fact]
    public async Task 忘記密碼_重設後舊密碼失效_連結只能用一次_全部裝置登出_順便完成Email驗證()
    {
        var m = await _scope.CreateVerifiedMemberAsync("reset");
        using var client = _scope.Client();
        var session = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/member/auth/forgot-password", Json(new { email = m.Email.ToUpperInvariant(), club = "tcrfc" }))).StatusCode);
        var token = CapturingEmailSender.TokenOf(MemberTestDoubles.Email.LastTo(m.Email, "reset")!);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/reset-password", Json(new { token, newPassword = "short" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/reset-password", Json(new { token = token + "x", newPassword = "Brand-new1pass" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/member/auth/reset-password", Json(new { token, newPassword = "Brand-new1pass" }))).StatusCode);

        // 同一條連結第二次：密碼指紋已變 → 無效
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/reset-password", Json(new { token, newPassword = "Another-new1pass" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }))).StatusCode);
        await MemberTestScope.LoginAsync(client, m.Email, "Brand-new1pass");
        // 重設前發出的更新權杖全部撤銷
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = session.RefreshToken }))).StatusCode);

        // 未驗證 Email 的人能收到重設信 → 視同控制信箱，順便完成驗證
        var email = _scope.NewEmail("reset-unverified");
        await client.PostAsync("/api/v1/member/auth/register", Json(new { club = "tcrfc", email, password = "Abcdefg1", name = "x" }));
        await client.PostAsync("/api/v1/member/auth/forgot-password", Json(new { email, club = "tcrfc" }));
        var t2 = CapturingEmailSender.TokenOf(MemberTestDoubles.Email.LastTo(email, "reset")!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/member/auth/reset-password", Json(new { token = t2, newPassword = "Brand-new2pass" }))).StatusCode);
        await MemberTestScope.LoginAsync(client, email, "Brand-new2pass");
    }

    [Fact]
    public async Task 變更密碼_要目前密碼_成功後撤銷其他裝置並替本機發新權杖()
    {
        var m = await _scope.CreateVerifiedMemberAsync("chgpwd");
        using var client = _scope.Client();
        var other = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/change-password", Json(new { currentPassword = MemberTestScope.Password, newPassword = "New-pass1234" }))).StatusCode);
        var wrong = await m.Client.PostAsync("/api/v1/member/auth/change-password", Json(new { currentPassword = "Wrong-pass1", newPassword = "New-pass1234" }));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsync("/api/v1/member/auth/change-password", Json(new { currentPassword = MemberTestScope.Password, newPassword = "weak" }))).StatusCode);

        var ok = await m.Client.PostAsync("/api/v1/member/auth/change-password",
            Json(new { currentPassword = MemberTestScope.Password, newPassword = "New-pass1234", tokenDelivery = "body" }));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ReadJsonAsync(ok);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refreshToken").GetString())); // 本機繼續登入
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = other.RefreshToken }))).StatusCode); // 其他裝置被登出
        await MemberTestScope.LoginAsync(client, m.Email, "New-pass1234");
    }

    // ═════════════ 個人資料與刪除 ═════════════

    [Fact]
    public async Task 個人資料_讀取與更新_Email不可改_驗證與未登入()
    {
        var m = await _scope.CreateVerifiedMemberAsync("profile");
        using var anonymous = _scope.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/member/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsync("/api/v1/member/me", Json(new { name = "x" }))).StatusCode);

        var me = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/me"));
        Assert.Equal(m.Email, me.GetProperty("email").GetString());
        Assert.Equal("web", me.GetProperty("signupSource").GetString());
        Assert.True(me.GetProperty("emailVerified").GetBoolean());
        Assert.True(me.GetProperty("hasPassword").GetBoolean());
        Assert.False(me.GetProperty("lineBound").GetBoolean());
        var raw = me.GetRawText();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("argon2", raw, StringComparison.OrdinalIgnoreCase);

        var updated = await m.Client.PutAsync("/api/v1/member/me", Json(new { name = "新名字", phone = "0912-345-678", birthOn = "1990-05-06", locale = "en", email = "hack@example.test" }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var json = await ReadJsonAsync(updated);
        Assert.Equal("新名字", json.GetProperty("name").GetString());
        Assert.Equal("en", json.GetProperty("locale").GetString());
        Assert.Equal(m.Email, json.GetProperty("email").GetString()); // Email 是登入鍵，不能由這支端點改

        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PutAsync("/api/v1/member/me", Json(new { name = "" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PutAsync("/api/v1/member/me", Json(new { name = "x", phone = "abc" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PutAsync("/api/v1/member/me", Json(new { name = "x", birthOn = "2999-01-01" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PutAsync("/api/v1/member/me", Json(new { name = "x", locale = "fr" }))).StatusCode);
    }

    [Fact]
    public async Task 刪除帳號_欄位清除不是刪列_會員卡作廢_權杖失效_無法再登入()
    {
        var m = await _scope.CreateVerifiedMemberAsync("delete");
        using var client = _scope.Client();
        var cards = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/cards"));
        var cardToken = cards[0].GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/m/{cardToken}")).StatusCode);
        var session = await MemberTestScope.LoginAsync(client, m.Email, MemberTestScope.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, (await m.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/member/me") { Content = Json(new { password = "Wrong-pass1" }) })).StatusCode);
        var deleted = await m.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/member/me") { Content = Json(new { password = MemberTestScope.Password }) });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        _scope.Track($"deleted-{m.MemberNo.ToLowerInvariant()}@deleted.invalid");

        Assert.Equal(HttpStatusCode.Unauthorized, (await m.Client.GetAsync("/api/v1/member/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email = m.Email, password = MemberTestScope.Password }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/refresh", Json(new { refreshToken = session.RefreshToken }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/m/{cardToken}")).StatusCode); // 會員卡 token 作廢

        // 欄位清除、保留列與會員編號
        var cleared = await BizTest.ScalarGuidAsync(
            "SELECT id FROM members WHERE member_no = @N AND status = 'deleted' AND phone IS NULL AND birth_on IS NULL AND email LIKE 'deleted-%@deleted.invalid' AND password_hash LIKE '!deleted-%'",
            ("@N", m.MemberNo));
        Assert.NotEqual(Guid.Empty, cleared);
        // 同一個 Email 可以重新註冊（舊帳號已改寫）
        var again = await client.PostAsync("/api/v1/member/auth/register", Json(new { club = "tcrfc", email = m.Email, password = "Abcdefg1", name = "再註冊" }));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    // ═════════════ 權杖隔離 ═════════════

    [Fact]
    public async Task 會員權杖與後台權杖互不認帳()
    {
        var m = await _scope.CreateVerifiedMemberAsync("sep");
        var adminToken = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("clean.login@tcrfc.test");

        // 後台權杖打會員端點 → 401
        using var asAdmin = _scope.Client(adminToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asAdmin.GetAsync("/api/v1/member/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await asAdmin.GetAsync("/api/v1/member/memberships")).StatusCode);

        // 會員權杖打後台端點 → 401（不是 403：後台根本不認這把權杖）
        Assert.Equal(HttpStatusCode.Unauthorized, (await m.Client.GetAsync("/api/v1/admin/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await m.Client.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);

        // 垃圾與竄改的權杖
        using var garbage = _scope.Client("not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await garbage.GetAsync("/api/v1/member/me")).StatusCode);
        using var tampered = _scope.Client(m.AccessToken[..^3] + "abc");
        Assert.Equal(HttpStatusCode.Unauthorized, (await tampered.GetAsync("/api/v1/member/me")).StatusCode);
    }

    [Fact]
    public void 會員權杖簽章金鑰_未設獨立金鑰時由後台金鑰衍生_且與後台金鑰不同()
    {
        var club = "club-signing-key-for-testing-purposes-0123456789";
        var derived = MemberTokenService.DeriveKeyBytes(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JWT_SIGNING_KEY_CLUB"] = club }).Build());
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(club), derived);
        Assert.Equal(32, derived.Length);
        var dedicated = MemberTokenService.DeriveKeyBytes(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SIGNING_KEY_CLUB"] = club, ["JWT_SIGNING_KEY_MEMBER"] = "dedicated-member-signing-key-0123456789abcdef",
        }).Build());
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("dedicated-member-signing-key-0123456789abcdef"), dedicated);
        Assert.Throws<InvalidOperationException>(() => MemberTokenService.DeriveKeyBytes(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SIGNING_KEY_CLUB"] = club, ["JWT_SIGNING_KEY_MEMBER"] = "short",
        }).Build()));
    }

    // ═════════════ LINE ═════════════

    private async Task<(string State, string Nonce)> AuthorizeAsync(HttpClient client, string mode, string? redirectUri = null)
    {
        var response = await client.PostAsync("/api/v1/member/auth/line/authorize", Json(new { club = "tcrfc", mode, redirectUri }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.StartsWith("https://line.test/authorize", json.GetProperty("authorizeUrl").GetString());
        return (json.GetProperty("state").GetString()!, "");
    }

    [Fact]
    public async Task LINE_未設定憑證時回503_不是500()
    {
        using var factory = fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ILineLoginClient>();
            s.AddSingleton<ILineLoginClient, LineLoginClient>(); // 真實實作，設定全空
        }));
        using var client = factory.CreateClient();
        var authorize = await client.PostAsync("/api/v1/member/auth/line/authorize", Json(new { club = "tcrfc", mode = "login" }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, authorize.StatusCode);
        Assert.Equal("line_not_configured", CodeOf(await ReadJsonAsync(authorize)));
        var callback = await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code = "x", state = "y" }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, callback.StatusCode);
    }

    [Fact]
    public async Task LINE_登入流程_找不到會員要補Email才註冊_之後可直接用LINE登入_不自動併入同Email帳號()
    {
        using var client = _scope.Client();
        var lineId = "U" + Guid.NewGuid().ToString("N");
        var code = "code-" + Guid.NewGuid().ToString("N");
        MemberTestDoubles.Line.Register(code, new LineIdentity(lineId, "LINE 小明", "line-suggest@example.test"));

        // 授權網址：回呼網址必須在白名單，狀態被竄改不受理
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/line/authorize", Json(new { club = "tcrfc", mode = "login", redirectUri = "https://evil.test/cb" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/line/authorize", Json(new { club = "tcrfc", mode = "nope" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/line/authorize", Json(new { club = "tcrfc", mode = "bind" }))).StatusCode); // 綁定要先登入
        var (state, _) = await AuthorizeAsync(client, "login", FakeLineLoginClient.RedirectUri);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = state + "x" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code = "unknown-code", state }))).StatusCode);

        // 第一次：找不到會員 → 帶票據，要求補 Email
        var first = await ReadJsonAsync(await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state })));
        Assert.Equal("signup_required", first.GetProperty("status").GetString());
        Assert.Equal("line-suggest@example.test", first.GetProperty("suggestedEmail").GetString());
        var ticket = first.GetProperty("ticket").GetString()!;

        // 票據用途不能互換；Email 已有帳號 → 409（不自動併入）
        var existing = await _scope.CreateVerifiedMemberAsync("linemerge");
        var conflict = await client.PostAsync("/api/v1/member/auth/line/complete", Json(new { club = "tcrfc", ticket, email = existing.Email, tokenDelivery = "body" }));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("email_taken", CodeOf(await ReadJsonAsync(conflict)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/member/auth/line/complete", Json(new { club = "tcrfc", ticket = state, email = _scope.NewEmail("x") }))).StatusCode);

        var email = _scope.NewEmail("lineuser");
        var complete = await client.PostAsync("/api/v1/member/auth/line/complete", Json(new { club = "tcrfc", ticket, email, tokenDelivery = "body" }));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var session = await ReadJsonAsync(complete);
        Assert.Equal("LINE 小明", (await ReadJsonAsync(await _scope.Client(session.GetProperty("accessToken").GetString()).GetAsync("/api/v1/member/me"))).GetProperty("name").GetString());
        Assert.False(session.GetProperty("member").GetProperty("hasPassword").GetBoolean());
        Assert.True(session.GetProperty("member").GetProperty("lineBound").GetBoolean());
        Assert.False(string.IsNullOrEmpty(session.GetProperty("refreshToken").GetString()));
        Assert.NotNull(MemberTestDoubles.Email.LastTo(email, "verify")); // Email 仍要驗證
        var lineMemberToken = session.GetProperty("accessToken").GetString()!;
        using var lineClient = _scope.Client(lineMemberToken);
        var profile = await ReadJsonAsync(await lineClient.GetAsync("/api/v1/member/me"));
        Assert.Equal("line", profile.GetProperty("signupSource").GetString());
        Assert.False(profile.GetProperty("emailVerified").GetBoolean());
        var memberships = await ReadJsonAsync(await lineClient.GetAsync("/api/v1/member/memberships"));
        Assert.Equal("tcrfc", memberships.GetProperty("memberships")[0].GetProperty("club").GetProperty("code").GetString()); // LINE 已證明身分，直接成為一般會員

        // LINE 帳號沒有密碼：不能用密碼登入
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email, password = "Abcdefg1" }))).StatusCode);

        // 第二次：用 LINE 直接登入（Body 模式）
        var (state2, _) = await AuthorizeAsync(client, "login");
        var second = await ReadJsonAsync(await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = state2, tokenDelivery = "body" })));
        Assert.Equal("logged_in", second.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(second.GetProperty("session").GetProperty("refreshToken").GetString()));

        // 只能用 LINE 登入的帳號不能解除綁定；先設定第一組密碼（不用目前密碼）再解除
        Assert.Equal(HttpStatusCode.Conflict, (await lineClient.DeleteAsync("/api/v1/member/me/line")).StatusCode);
        var setPassword = await lineClient.PostAsync("/api/v1/member/auth/change-password", Json(new { newPassword = "First-pass123", tokenDelivery = "body" }));
        Assert.Equal(HttpStatusCode.OK, setPassword.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await lineClient.DeleteAsync("/api/v1/member/me/line")).StatusCode);
        // 解除後同一個 LINE 帳號找不到會員，又回到「要補 Email 註冊」
        var (state3, _) = await AuthorizeAsync(client, "login");
        var third = await ReadJsonAsync(await client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = state3 })));
        Assert.Equal("signup_required", third.GetProperty("status").GetString());
    }

    [Fact]
    public async Task LINE_綁定流程_已登入才能綁_state不能替別人綁_同一個LINE只能綁一個帳號()
    {
        var a = await _scope.CreateVerifiedMemberAsync("bindA");
        var b = await _scope.CreateVerifiedMemberAsync("bindB");
        using var anonymous = _scope.Client();
        var lineId = "U" + Guid.NewGuid().ToString("N");
        var code = "code-" + Guid.NewGuid().ToString("N");
        MemberTestDoubles.Line.Register(code, new LineIdentity(lineId, "綁定者", null));

        var (state, _) = await AuthorizeAsync(a.Client, "bind");
        // 沒登入、或登入的是別人 → 擋下
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state }))).StatusCode);

        var bound = await ReadJsonAsync(await a.Client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state })));
        Assert.Equal("bound", bound.GetProperty("status").GetString());
        Assert.True((await ReadJsonAsync(await a.Client.GetAsync("/api/v1/member/me"))).GetProperty("lineBound").GetBoolean());
        // 資料庫只存雜湊與密文，沒有 LINE userId 明文
        var plain = await BizTest.ExecuteSqlAsync("UPDATE members SET internal_note = internal_note WHERE line_user_id_encrypted LIKE '%' + @L + '%' OR line_user_id_hash = @L", ("@L", lineId));
        Assert.Equal(0, plain);

        // 已綁定者再綁 → 409；另一個帳號想綁同一個 LINE → 409
        var (stateAgain, _) = await AuthorizeAsync(a.Client, "bind");
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = stateAgain }))).StatusCode);
        var (stateB, _) = await AuthorizeAsync(b.Client, "bind");
        var inUse = await b.Client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = stateB }));
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        Assert.Equal("line_in_use", CodeOf(await ReadJsonAsync(inUse)));

        // 有密碼的帳號可以解除，解除後 B 就能綁
        Assert.Equal(HttpStatusCode.NoContent, (await a.Client.DeleteAsync("/api/v1/member/me/line")).StatusCode);
        var (stateB2, _) = await AuthorizeAsync(b.Client, "bind");
        Assert.Equal(HttpStatusCode.OK, (await b.Client.PostAsync("/api/v1/member/auth/line/callback", Json(new { code, state = stateB2 }))).StatusCode);
    }

    // ═════════════ 限流 ═════════════

    [Fact]
    public async Task 登入類端點依訪客IP限流_額度用盡回429()
    {
        using var factory = fixture.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { [Tcrfc.Api.Common.PublicRateLimitPolicies.MemberAuthPermitLimitConfigKey] = "3" })));
        using var client = factory.CreateClient();
        for (var i = 1; i <= 3; i++)
        {
            var r = await client.PostAsync("/api/v1/member/auth/login", Json(new { email = $"rl-{i}@example.test", password = "Wrong-pass1" }));
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/member/auth/login", Json(new { email = "rl-4@example.test", password = "Wrong-pass1" }))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/member/auth/forgot-password", Json(new { email = "rl@example.test", club = "tcrfc" }))).StatusCode);
    }
}
