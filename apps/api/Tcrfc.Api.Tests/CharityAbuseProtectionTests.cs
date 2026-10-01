using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 規劃書 §3.3／§11.2「捐款建單須有防濫用機制（Turnstile、IP 節流）」與任務要求「公開寫入端點依訪客 IP 限流」。
/// 額度用盡回 429 的驗證用獨立測試主機（額度壓到 3），不拖累其他測試的寬鬆額度。
/// </summary>
[Collection(CharityRateLimitCollection.Name)]
public sealed class CharityRateLimitTests(CharityRateLimitApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    [Fact]
    public async Task 建單與付款相關的公開寫入端點共用依IP的限流_額度用盡回429_且不再建單()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var email = Email("flood");

        for (var i = 0; i < CharityRateLimitApiFixture.Permits; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await PostDonationAsync(client, NewRequest(slug, 100 + i, email: email), NewKey())).StatusCode);
        }

        var blocked = await PostDonationAsync(client, NewRequest(slug, 999, email: email), NewKey());
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(CharityRateLimitApiFixture.Permits, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE donor_email = @e", ("@e", email)));

        // 發起付款／確認／取消與建單共用同一個「公開寫入」額度
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync($"{Base}/donations/CHAAAAAAAAAAAAAAAA/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync($"{Base}/donations/CHAAAAAAAAAAAAAAAA/pay", new { lang = "zh" }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 結果頁輪詢也依IP限流_擋大量枚舉單號()
    {
        using var client = fx.CreateClient();

        for (var i = 0; i < CharityRateLimitApiFixture.Permits + 1; i++)
        {
            var status = (await client.GetAsync($"{Base}/donations/CHAAAAAAAAAAAAAAAA")).StatusCode;
            Assert.Equal(i < CharityRateLimitApiFixture.Permits ? HttpStatusCode.NotFound : HttpStatusCode.TooManyRequests, status);
        }
    }

    [Fact]
    public async Task 後台登入依IP限流_額度用盡連對的密碼也擋_與帳號鎖定是兩層獨立防線()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClient();

        for (var i = 0; i < CharityRateLimitApiFixture.Permits; i++)
        {
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync(AdminBase + "/auth/login", new LoginRequest(admin.Username, CharityApiFixture.TestPassword, null), TestJson.WriteOptions)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync(AdminBase + "/auth/login", new LoginRequest(admin.Username, CharityApiFixture.TestPassword, null), TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 公開讀取端點不限流_店家與項目的瀏覽不被誤擋()
    {
        using var client = fx.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/projects")).StatusCode);
        }
    }
}

/// <summary>Turnstile 人機驗證：端點整合（驗證不過就不建單）＋真實 Cloudflare 驗證器的行為（以假 HTTP 處理器驗證，不連網）。</summary>
[Collection(CharityCollection.Name)]
public sealed class CharityTurnstileTests(CharityApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    [Fact]
    public async Task 人機驗證不過_回422_不建單_權杖原樣交給驗證器()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var email = Email("bot");
        fx.Turnstile.Result = false;

        var response = await PostDonationAsync(client, NewRequest(slug, 500, email: email) with { TurnstileToken = "token-from-widget" }, NewKey());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE donor_email = @e", ("@e", email)));
        var call = Assert.Single(fx.Turnstile.Calls);
        Assert.Equal("token-from-widget", call.Token);
    }

    [Fact]
    public async Task 人機驗證通過_正常建單()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();

        var response = await PostDonationAsync(client, NewRequest(slug, 500) with { TurnstileToken = "ok" }, NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static CloudflareTurnstileVerifier Verifier(StubHandler handler)
        => new(new HttpClient(handler), "secret-123", NullLogger<CloudflareTurnstileVerifier>.Instance);

    [Fact]
    public async Task Cloudflare驗證器_success為true才通過_沒帶權杖一律不過_不連網()
    {
        var ok = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true }) });
        var fail = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = false }) });

        Assert.True(await Verifier(ok).VerifyAsync("t", "203.0.113.9", default));
        Assert.Contains("secret=secret-123", ok.LastBody);
        Assert.Contains("response=t", ok.LastBody);
        Assert.Contains("remoteip=203.0.113.9", ok.LastBody);
        Assert.False(await Verifier(fail).VerifyAsync("t", null, default));

        var offline = new StubHandler(_ => throw new InvalidOperationException("不該被呼叫"));
        Assert.False(await Verifier(offline).VerifyAsync(null, null, default));
        Assert.False(await Verifier(offline).VerifyAsync("  ", null, default));
        Assert.Equal(0, offline.Calls);
    }

    [Fact]
    public async Task Cloudflare驗證服務本身壞掉_放行而不是擋住所有捐款人_第一道IP限流仍在()
    {
        var down = new StubHandler(_ => throw new HttpRequestException("連線失敗"));
        var serverError = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        Assert.True(await Verifier(down).VerifyAsync("t", null, default));
        Assert.True(await Verifier(serverError).VerifyAsync("t", null, default));
    }
}

/// <summary>未設定慈善連線字串時，整個慈善平台不存在（路由 404），主站完全不受影響。</summary>
[Collection(CharityDisabledCollection.Name)]
public sealed class CharityDisabledTests(CharityDisabledApiFixture fx)
{
    [Theory]
    [InlineData("/api/v1/donation-platform/projects")]
    [InlineData("/api/v1/donation-platform/settings")]
    [InlineData("/api/v1/donation-platform/admin/auth/me")]
    [InlineData("/api/v1/donation-platform/admin/stores")]
    public async Task 慈善端點不存在(string path)
    {
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task 主站照常運作()
    {
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/auth/me")).StatusCode);
    }
}
