using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Features.MemberAuth;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>測試替身：收集寄出的信（取代 <see cref="IEmailSender"/>）。</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public bool IsConfigured => true;

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) { return _sent.ToList(); } }
    }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        lock (_sent)
        {
            _sent.Add(message);
        }

        return Task.FromResult(true);
    }

    public EmailMessage? LastTo(string email, string? kind = null)
        => Sent.LastOrDefault(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase) && (kind is null || m.Kind == kind));

    /// <summary>信件內連結的 <c>token=</c> 參數（已解碼）。</summary>
    public static string TokenOf(EmailMessage message)
    {
        var match = Regex.Match(message.TextBody, @"token=([^\s&]+)");
        Assert.True(match.Success, "信件內找不到 token 連結：" + message.TextBody);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}

/// <summary>測試替身：LINE Login 用戶端。授權碼對應的身分由測試預先登記；未登記的碼視為授權失敗。</summary>
public sealed class FakeLineLoginClient : ILineLoginClient
{
    public const string RedirectUri = "https://tcrfc.test/line/callback";

    private readonly Dictionary<string, LineIdentity> _codes = new(StringComparer.Ordinal);

    public bool IsConfigured => true;

    public IReadOnlyList<string> AllowedRedirectUris { get; } = [RedirectUri];

    public void Register(string code, LineIdentity identity)
    {
        lock (_codes)
        {
            _codes[code] = identity;
        }
    }

    public string BuildAuthorizeUrl(string state, string nonce, string redirectUri)
        => $"https://line.test/authorize?state={Uri.EscapeDataString(state)}&nonce={nonce}&redirect_uri={Uri.EscapeDataString(redirectUri)}";

    public Task<LineIdentity> ExchangeAsync(string code, string redirectUri, string nonce, CancellationToken cancellationToken)
    {
        lock (_codes)
        {
            return _codes.TryGetValue(code, out var identity)
                ? Task.FromResult(identity)
                : throw new MemberValidationException("LINE 授權已失效，請重新操作一次。", "line_exchange_failed");
        }
    }
}

public static class MemberTestDoubles
{
    public static readonly CapturingEmailSender Email = new();
    public static readonly FakeLineLoginClient Line = new();
}

/// <summary>會員前台測試共用工具：建立已驗證會員、取得權杖、清除測試資料（連同新表）。</summary>
internal sealed class MemberTestScope(WebApplicationFactory<Program> factory) : IAsyncDisposable
{
    public const string Password = "Test-Passw0rd!";

    private readonly List<string> _emails = [];

    /// <summary>新的用戶端；帶 <paramref name="bearer"/> 時附上會員存取權杖。</summary>
    public HttpClient Client(string? bearer = null)
    {
        var client = factory.CreateClient();
        if (bearer is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return client;
    }

    public string NewEmail(string tag)
    {
        var email = $"mtest-{tag}-{Guid.NewGuid():N}@example.test".ToLowerInvariant();
        _emails.Add(email);
        return email;
    }

    public void Track(string email) => _emails.Add(email.ToLowerInvariant());

    public sealed record TestMember(string Email, string MemberNo, string AccessToken, HttpClient Client, Guid MemberId);

    /// <summary>註冊 → 取信中的連結 → 驗證 → 登入（body 模式取得更新權杖）。回傳帶著 Bearer 的用戶端。</summary>
    public async Task<TestMember> CreateVerifiedMemberAsync(string tag, string club = "tcrfc", string? password = null)
    {
        var anonymous = Client();
        var email = NewEmail(tag);
        var pwd = password ?? Password;
        var register = await anonymous.PostAsJsonAsync("/api/v1/member/auth/register",
            new { club, email, password = pwd, name = "【M測試】" + tag, phone = "0900-000-111", birthOn = "1990-01-01", lang = "zh" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var verifyMail = MemberTestDoubles.Email.LastTo(email, "verify") ?? throw new InvalidOperationException("沒有驗證信");
        var verify = await anonymous.PostAsJsonAsync("/api/v1/member/auth/verify-email", new { token = CapturingEmailSender.TokenOf(verifyMail) }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var session = await LoginAsync(anonymous, email, pwd);
        var client = Client(session.AccessToken);
        var memberId = await B1Test.ScalarAsync("SELECT id FROM members WHERE email = @E", ("@E", email));
        return new TestMember(email, session.MemberNo, session.AccessToken, client, memberId);
    }

    /// <summary>走完整個付款流程（建立訂單 → 請款 → 確認）把會員升為球迷會員，回傳訂單編號。用本機假金流。</summary>
    public static async Task<string> BuyFanClubAsync(TestMember member, string planCode = "single", string club = "tcrfc")
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{club}/member/membership-orders") { Content = BizTest.Json(new { planCode }) };
        create.Headers.Add("Idempotency-Key", "test-" + Guid.NewGuid().ToString("N"));
        var created = await member.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var orderNo = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNo").GetString()!;
        var pay = await member.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null);
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        var confirm = await member.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "FAKE-" + orderNo }));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("activated", (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        return orderNo;
    }

    public sealed record LoginResult(string AccessToken, string? RefreshToken, string MemberNo);

    public static async Task<LoginResult> LoginAsync(HttpClient client, string email, string password, bool rememberMe = false)
    {
        var response = await client.PostAsJsonAsync("/api/v1/member/auth/login", new { email, password, rememberMe, tokenDelivery = "body" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new LoginResult(json.GetProperty("accessToken").GetString()!,
            json.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String ? rt.GetString() : null,
            json.GetProperty("member").GetProperty("memberNo").GetString()!);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var email in _emails.Distinct())
        {
            await CleanupByEmailAsync(email);
        }
    }

    /// <summary>刪除這個 Email（或已刪除帳號改寫後的佔位 Email）對應會員的全部測試資料，依外鍵順序。</summary>
    private static async Task CleanupByEmailAsync(string email)
    {
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @ids TABLE (id uniqueidentifier);
            INSERT INTO @ids SELECT id FROM members WHERE email = @E; -- 自行刪除帳號的會員 Email 會被改寫成佔位值，呼叫端要 Track 那個值
            DELETE FROM jersey_issues WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM membership_payments WHERE membership_id IN (SELECT id FROM memberships WHERE member_id IN (SELECT id FROM @ids));
            DELETE FROM membership_orders WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM member_cards WHERE membership_id IN (SELECT id FROM memberships WHERE member_id IN (SELECT id FROM @ids));
            DELETE FROM fan_event_registrations WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM registrations WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM memberships WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM member_refresh_tokens WHERE member_id IN (SELECT id FROM @ids);
            UPDATE app_devices SET member_id = NULL WHERE member_id IN (SELECT id FROM @ids);
            DELETE FROM members WHERE id IN (SELECT id FROM @ids);
            """, ("@E", email));
    }
}
