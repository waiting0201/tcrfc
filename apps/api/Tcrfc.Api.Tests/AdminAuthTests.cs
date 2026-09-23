using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// J1 登入核心的端對端測試：打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>，涵蓋密碼驗證、
/// 鎖定政策、強制 2FA、更新權杖輪替與重放偵測。種子帳號見
/// <c>db/seed/generate-club-seed-sql.py</c>「18.4 admin_users」。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminAuthTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 登入成功_回傳存取權杖與更新權杖Cookie()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new LoginRequest("clean.login@tcrfc.test", "SuperAdmin@123", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(body.MustChangePassword);
        Assert.True(body.IsSuperAdmin);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        // ⚠️ Kestrel 把 Cookie 屬性一律寫成小寫（httponly／secure／samesite），不是 .NET
        // CookieOptions 屬性名稱的大小寫——比對前先轉小寫，避免抓錯大小寫看起來像是缺屬性。
        var cookieHeader = string.Join(";", cookies!).ToLowerInvariant();
        Assert.Contains("__host-tcrfc-admin-rt=", cookieHeader);
        Assert.Contains("httponly", cookieHeader);
        Assert.Contains("secure", cookieHeader);
        Assert.Contains("samesite=none", cookieHeader);
        Assert.DoesNotContain("domain=", cookieHeader); // docs/14-invariants.md：絕對不得設 Domain 屬性
    }

    [Fact]
    public async Task 登入_密碼錯誤_回401且訊息不洩露帳號是否存在()
    {
        using var client = fixture.CreateClient();

        try
        {
            var wrongPassword = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest("super.admin@tcrfc.test", "TotallyWrongPassword", null));
            var unknownUser = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest("this-username-does-not-exist@tcrfc.test", "AnyPassword123", null));

            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

            // ⚠️ 兩種情況的回應本體要一模一樣（同一個 message），不透露「帳號存在但密碼錯」
            // 跟「帳號根本不存在」的差異——這是防使用者列舉攻擊的刻意設計。
            var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync();
            var unknownUserBody = await unknownUser.Content.ReadAsStringAsync();
            Assert.Equal(wrongPasswordBody, unknownUserBody);
        }
        finally
        {
            // 🔴 這個測試本身就是在製造一次失敗嘗試（故意的），失敗嘗試會累加
            // super.admin@tcrfc.test 的 failed_attempt_count；重複跑這個測試檔（例如
            // 本機開發反覆 dotnet test）第五次以後這個帳號會被鎖定，導致這個測試本身
            // 從驗證「密碼錯誤回 401」意外變成驗證「帳號被鎖回 423」而失敗——
            // 不是被測系統的 bug，是這個測試沒有清理自己造成的副作用。每次測完歸零。
            await ResetAccountLockAsync("super.admin@tcrfc.test");
        }
    }

    [Fact]
    public async Task 登入_連續五次失敗後鎖定_第六次即使密碼正確也擋下()
    {
        using var client = fixture.CreateClient();
        const string username = "lockout.test@tcrfc.test";

        try
        {
            for (var i = 0; i < 5; i++)
            {
                var failed = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                    new LoginRequest(username, "WrongPassword", null));
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            // 第六次，即使這次密碼是對的，帳號應該已經被鎖定。
            var lockedAttempt = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest(username, "Viewer@123", null));
            Assert.Equal(HttpStatusCode.Locked, lockedAttempt.StatusCode);
        }
        finally
        {
            await ResetAccountLockAsync(username);
        }
    }

    [Fact]
    public async Task 完整2FA設定流程_設定前登入不需驗證碼_設定後登入需要正確驗證碼()
    {
        using var client = fixture.CreateClient();
        const string username = "fresh.setup@tcrfc.test";

        try
        {
            // ① 尚未啟用 2FA 時，登入不需要驗證碼即可成功，但 mustChangePassword 為 true。
            var firstLogin = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest(username, "Admin@123", null));
            Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
            var firstLoginBody = await firstLogin.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
            Assert.True(firstLoginBody!.MustChangePassword);
            Assert.False(firstLoginBody.TwoFactorEnabled);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstLoginBody.AccessToken);

            // ② 先滿足「強制先改密碼」，否則後面打俱樂部範圍端點會被 AdminClubAuthorizer 擋下
            //    （2FA 設定端點本身不受此限，但這裡刻意連同密碼政策一起驗一次完整流程）。
            var changePassword = await client.PostAsJsonAsync("/api/v1/admin/auth/change-password",
                new ChangePasswordRequest("Admin@123", "NewPassword-Fresh-1"));
            Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);

            // ③ 開始 2FA 設定，拿到 Base32 密鑰。
            var setupResponse = await client.PostAsync("/api/v1/admin/auth/2fa/setup", null);
            Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
            var setup = await setupResponse.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(TestJson.Options);
            Assert.NotNull(setup);
            Assert.StartsWith("otpauth://totp/", setup!.OtpAuthUrl);

            // ④ 用錯的驗證碼確認會被拒絕。
            var wrongConfirm = await client.PostAsJsonAsync("/api/v1/admin/auth/2fa/confirm", new TwoFactorConfirmRequest("000000"));
            Assert.Equal(HttpStatusCode.BadRequest, wrongConfirm.StatusCode);

            // ⑤ 模擬驗證器 App：從 Base32 密鑰算出目前時間視窗的正確驗證碼並送出。
            var secret = TotpService.FromBase32(setup.Secret);
            var correctCode = TotpService.GenerateCurrentCodeForTesting(secret);
            var confirmResponse = await client.PostAsJsonAsync("/api/v1/admin/auth/2fa/confirm", new TwoFactorConfirmRequest(correctCode));
            Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

            // ⑥ 重新登入（新密碼）：這次不帶驗證碼應該回 totp_required，不是直接成功也不是直接失敗。
            using var freshClient = fixture.CreateClient();
            var loginWithoutCode = await freshClient.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest(username, "NewPassword-Fresh-1", null));
            Assert.Equal(HttpStatusCode.OK, loginWithoutCode.StatusCode);
            var withoutCodeJson = await loginWithoutCode.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("totp_required", withoutCodeJson.GetProperty("status").GetString());

            // ⑦ 帶正確驗證碼重新登入才會真的成功。
            var secondCode = TotpService.GenerateCurrentCodeForTesting(secret);
            var loginWithCode = await freshClient.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest(username, "NewPassword-Fresh-1", secondCode));
            Assert.Equal(HttpStatusCode.OK, loginWithCode.StatusCode);
            var loginWithCodeBody = await loginWithCode.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
            Assert.True(loginWithCodeBody!.TwoFactorEnabled);
        }
        finally
        {
            await ResetFreshSetupAccountAsync();
        }
    }

    [Fact]
    public async Task 更新權杖_輪替後舊權杖失效_重用會被偵測且整批撤銷()
    {
        using var client = fixture.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new LoginRequest("clean.login@tcrfc.test", "SuperAdmin@123", null));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var originalCookieValue = ExtractSetCookieValue(login, "__Host-tcrfc-admin-rt");

        // 第一次 refresh：帶著登入拿到的 Cookie，應該成功，並輪替出一把新的更新權杖。
        SetCookie(client, originalCookieValue);
        var firstRefresh = await client.PostAsync("/api/v1/admin/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var newCookieValue = ExtractSetCookieValue(firstRefresh, "__Host-tcrfc-admin-rt");
        Assert.NotEqual(originalCookieValue, newCookieValue); // 輪替：新值必須跟舊值不同。

        // 拿著「舊」Cookie（第一次 refresh 之前那把，已經被撤銷）再打一次——模擬權杖被偷後
        // 攻擊者重放，也模擬使用者裝置上殘留舊 Cookie 的正常重放（兩種成因，同一種防禦）。
        using var replayClient = fixture.CreateClient();
        SetCookie(replayClient, originalCookieValue);
        var replayedRefresh = await replayClient.PostAsync("/api/v1/admin/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, replayedRefresh.StatusCode);

        // 重放偵測應該已經撤銷整條鏈——連剛剛輪替出來的「新」權杖現在也該失效了。
        using var afterRevokeClient = fixture.CreateClient();
        SetCookie(afterRevokeClient, newCookieValue);
        var afterRevoke = await afterRevokeClient.PostAsync("/api/v1/admin/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task 登出後_更新權杖立刻失效()
    {
        using var client = fixture.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new LoginRequest("clean.login@tcrfc.test", "SuperAdmin@123", null));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookieValue = ExtractSetCookieValue(login, "__Host-tcrfc-admin-rt");

        SetCookie(client, cookieValue);
        var logout = await client.PostAsync("/api/v1/admin/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var afterLogoutClient = fixture.CreateClient();
        SetCookie(afterLogoutClient, cookieValue);
        var refreshAfterLogout = await afterLogoutClient.PostAsync("/api/v1/admin/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    /// <summary>
    /// 🔴 WebApplicationFactory 的 in-memory TestServer 走的是 http（不是 https），而更新權杖
    /// Cookie 標記 <c>Secure</c>（__Host- 前綴強制要求，見 AdminAuthEndpoints.cs）——
    /// .NET 的 <see cref="System.Net.CookieContainer"/>（HttpClient 預設自動 Cookie 處理用的就是它）
    /// 依 RFC 6265 規則，**收到 Secure Cookie 但連線不是安全來源時會拒絕存、或存了也不在 http
    /// 連線上送出**。這代表 <c>fixture.CreateClient()</c> 的自動 Cookie jar 在本測試環境完全用不上，
    /// 必須手動從 Set-Cookie 標頭取值、手動掛回下一個請求的 Cookie 標頭——這不是本測試的權宜
    /// 做法，是這個安全屬性組合在純 http 測試環境下的必然結果，正式環境（HTTPS）瀏覽器會用
    /// 真正的自動 Cookie 行為，不受此限。
    /// </summary>
    private static string ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "回應沒有 Set-Cookie 標頭。");
        var setCookieHeader = string.Join(";", cookies!);
        var marker = $"{cookieName}=";
        var start = setCookieHeader.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"回應標頭裡找不到 {cookieName}：{setCookieHeader}");
        start += marker.Length;
        var end = setCookieHeader.IndexOf(';', start);
        return end < 0 ? setCookieHeader[start..] : setCookieHeader[start..end];
    }

    private static void SetCookie(HttpClient client, string cookieValue)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"__Host-tcrfc-admin-rt={cookieValue}");
    }

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ResetAccountLockAsync(string username)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE admin_users SET failed_attempt_count = 0, locked_until = NULL WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>把 fresh.setup@tcrfc.test 重設回種子時的原始狀態，讓這條測試可以重複執行
    /// （見種子腳本「18.4」對這個帳號用途的說明）。</summary>
    private static async Task ResetFreshSetupAccountAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE admin_users
            SET password_hash = @OriginalHash,
                must_change_password = 1,
                two_factor_enabled = 0,
                two_factor_secret_encrypted = NULL,
                two_factor_confirmed_at = NULL,
                failed_attempt_count = 0,
                locked_until = NULL
            WHERE username = 'fresh.setup@tcrfc.test'
            """;
        command.Parameters.AddWithValue("@OriginalHash",
            "$argon2id$v=19$m=65536,t=3,p=1$qA4b7/CNXFRrB044hvtBzQ==$j2coAMUKbu3mFe+Vyf1oXd4E1Rq8F1RHO/KHt4lDHQ4=");
        await command.ExecuteNonQueryAsync();
    }
}
