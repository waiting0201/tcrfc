using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 主站／藍鯨公開表單的 Cloudflare Turnstile 驗證（STATUS S1-17）。端點整合用假驗證器（驗證「何時驗、驗失敗回什麼」），
/// 真實 Cloudflare 驗證器的行為用假 HttpMessageHandler 驗證，全程不連網。
/// 每個測試開自己的測試主機（自己的限流額度），並在 finally 還原 <c>forms.captcha_enabled</c>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ClubTurnstileEndpointTests(AdminWriteApiFixture fixture)
{
    private const string Url = $"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}/submissions";

    private sealed class FakeVerifier(bool enabled, bool result) : IClubTurnstileVerifier
    {
        public int Calls { get; private set; }
        public string? LastToken { get; private set; }
        public string? LastIp { get; private set; }
        public bool IsEnabled => enabled;

        public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
        {
            Calls++;
            LastToken = token;
            LastIp = remoteIp;
            return Task.FromResult(result);
        }
    }

    private HttpClient CreateClient(FakeVerifier verifier) =>
        fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IClubTurnstileVerifier>();
            s.AddSingleton<IClubTurnstileVerifier>(verifier);
        })).CreateClient();

    private static SubmitFormRequest Request(string contact, string? token = null, string? website = null) => new()
    {
        Answers = new Dictionary<string, string>
        {
            ["name"] = "驗證測試",
            ["contact"] = contact,
            ["subject"] = "test",
            ["message"] = "test",
            ["privacy_consent"] = "true",
        },
        TurnstileToken = token,
        Website = website,
    };

    // 每次執行都用新聯絡信箱，避免前一次執行殘留的資料干擾計數。
    private static readonly string RunId = Guid.NewGuid().ToString("N")[..8];
    private static string Uniq(string tag) => $"{tag}-{RunId}@example.com";

    private static string ConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING") ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<bool> SetCaptchaAsync(bool enabled)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @old bit = (SELECT f.captcha_enabled FROM forms f JOIN clubs c ON c.id = f.club_id WHERE c.code = N'tcrfc' AND f.form_code = @Code);
            UPDATE f SET captcha_enabled = @Enabled FROM forms f JOIN clubs c ON c.id = f.club_id WHERE c.code = N'tcrfc' AND f.form_code = @Code;
            SELECT @old;
            """;
        command.Parameters.AddWithValue("@Code", FormCatalog.GeneralContact);
        command.Parameters.AddWithValue("@Enabled", enabled);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountAsync(string contact)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1) FROM enquiry_answers ea JOIN form_fields ff ON ff.id = ea.form_field_id
            WHERE ff.field_key = 'contact' AND ea.value = @Contact
            """;
        command.Parameters.AddWithValue("@Contact", contact);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertCaptchaFailedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("captcha_failed", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("人機驗證未通過，請重新整理頁面後再試一次。", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task 未設密鑰_旗標開也放行_不呼叫驗證器_照常寫入()
    {
        var verifier = new FakeVerifier(enabled: false, result: false);
        using var client = CreateClient(verifier);
        var old = await SetCaptchaAsync(true);
        try
        {
            var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-notconfigured")), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, verifier.Calls);
            Assert.Equal(1, await CountAsync(Uniq("ts-notconfigured")));
        }
        finally { await SetCaptchaAsync(old); }
    }

    [Fact]
    public async Task 旗標關閉_即使已設密鑰也不驗證()
    {
        var verifier = new FakeVerifier(enabled: true, result: false);
        using var client = CreateClient(verifier);
        var old = await SetCaptchaAsync(false);
        try
        {
            var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-flagoff")), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, verifier.Calls);
        }
        finally { await SetCaptchaAsync(old); }
    }

    [Fact]
    public async Task 旗標開且已設密鑰_缺token回422_captcha_failed_不寫入()
    {
        // 假驗證器模擬真實行為：缺 token 一律不過。
        var verifier = new FakeVerifier(enabled: true, result: false);
        using var client = CreateClient(verifier);
        var old = await SetCaptchaAsync(true);
        try
        {
            var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-notoken")), TestJson.WriteOptions);
            await AssertCaptchaFailedAsync(response);
            Assert.Equal(0, await CountAsync(Uniq("ts-notoken")));
        }
        finally { await SetCaptchaAsync(old); }
    }

    [Fact]
    public async Task 旗標開且驗證失敗回422_驗證成功才寫入_且token與訪客IP傳給驗證器()
    {
        var failing = new FakeVerifier(enabled: true, result: false);
        var passing = new FakeVerifier(enabled: true, result: true);
        var old = await SetCaptchaAsync(true);
        try
        {
            using (var client = CreateClient(failing))
            {
                var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-badtoken"), "bad-token"), TestJson.WriteOptions);
                await AssertCaptchaFailedAsync(response);
                Assert.Equal(0, await CountAsync(Uniq("ts-badtoken")));
            }

            using (var client = CreateClient(passing))
            {
                var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-goodtoken"), "good-token"), TestJson.WriteOptions);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(1, await CountAsync(Uniq("ts-goodtoken")));
                Assert.Equal("good-token", passing.LastToken);
                Assert.False(string.IsNullOrEmpty(passing.LastIp)); // 來自 ClientIpResolver（訪客真實 IP），不是空值
            }
        }
        finally { await SetCaptchaAsync(old); }
    }

    [Fact]
    public async Task honeypot命中排在人機驗證之前_安靜回成功且不呼叫驗證器()
    {
        var verifier = new FakeVerifier(enabled: true, result: false);
        using var client = CreateClient(verifier);
        var old = await SetCaptchaAsync(true);
        try
        {
            var response = await client.PostAsJsonAsync(Url, Request(Uniq("ts-honeypot"), website: "http://spam.example.com"), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, verifier.Calls);
            Assert.Equal(0, await CountAsync(Uniq("ts-honeypot")));
        }
        finally { await SetCaptchaAsync(old); }
    }
}

/// <summary>真實 Cloudflare 驗證器的行為（假 HttpMessageHandler，不連網）。</summary>
public sealed class ClubTurnstileVerifierTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }

    private static CloudflareClubTurnstileVerifier Create(StubHandler handler) =>
        new(new HttpClient(handler), "test-secret", NullLogger<CloudflareClubTurnstileVerifier>.Instance);

    private static Task<HttpResponseMessage> Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });

    [Fact]
    public async Task 未設定實作_停用且一律放行()
    {
        var verifier = new NotConfiguredClubTurnstileVerifier();
        Assert.False(verifier.IsEnabled);
        Assert.True(await verifier.VerifyAsync(null, null, CancellationToken.None));
    }

    [Fact]
    public async Task 缺token不過_且不打Cloudflare()
    {
        var handler = new StubHandler(_ => Json("""{"success":true}"""));
        var verifier = Create(handler);
        Assert.True(verifier.IsEnabled);
        Assert.False(await verifier.VerifyAsync(null, "1.2.3.4", CancellationToken.None));
        Assert.False(await verifier.VerifyAsync("  ", "1.2.3.4", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Cloudflare回success_true通過_並帶secret_response_remoteip()
    {
        var handler = new StubHandler(_ => Json("""{"success":true}"""));
        Assert.True(await Create(handler).VerifyAsync("tok", "203.0.113.9", CancellationToken.None));
        Assert.Contains("secret=test-secret", handler.LastBody);
        Assert.Contains("response=tok", handler.LastBody);
        Assert.Contains("remoteip=203.0.113.9", handler.LastBody);
    }

    [Fact]
    public async Task Cloudflare回success_false不過()
    {
        var handler = new StubHandler(_ => Json("""{"success":false,"error-codes":["invalid-input-response"]}"""));
        Assert.False(await Create(handler).VerifyAsync("tok", "unknown", CancellationToken.None));
        Assert.DoesNotContain("remoteip", handler.LastBody); // 解析不到 IP 時不送
    }

    [Fact]
    public async Task Cloudflare異常_5xx_連線失敗_逾時_壞JSON_一律放行()
    {
        Assert.True(await Create(new StubHandler(_ => Json("{}", HttpStatusCode.InternalServerError))).VerifyAsync("tok", null, CancellationToken.None));
        Assert.True(await Create(new StubHandler(_ => throw new HttpRequestException("down"))).VerifyAsync("tok", null, CancellationToken.None));
        Assert.True(await Create(new StubHandler(_ => throw new TaskCanceledException("timeout"))).VerifyAsync("tok", null, CancellationToken.None));
        Assert.True(await Create(new StubHandler(_ => Json("not json"))).VerifyAsync("tok", null, CancellationToken.None));
    }
}
