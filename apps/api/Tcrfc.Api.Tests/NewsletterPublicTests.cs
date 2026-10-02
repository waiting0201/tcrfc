using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Features.Newsletter;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// G-09 電子報前台訂閱（公開端點）。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（見 docs/18 E-121）；
/// 沒有 <c>NewsletterRepository</c> 不需資料庫的部分見 <c>SiteBackendOfflineTranslationTests</c>。
/// 規則：單一確認（勾選同意即訂閱）、退訂黏著、回應不透露名單狀態、蜜罐、兩個俱樂部名單各自獨立。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class NewsletterPublicTests(AdminWriteApiFixture fixture)
{
    private static string Email(string tag) => $"zz-test-{tag}-{Guid.NewGuid():N}"[..40] + "@example.test";

    private static Task CleanupAsync() => BizTest.ExecuteSqlAsync("DELETE FROM newsletter_subscribers WHERE email LIKE '%zz-test-%'");

    private static async Task<(string Status, string? Source, bool HasSubscribedAt, bool HasUnsubscribedAt)?> RowAsync(string club, string email)
    {
        var count = await C1Test.ScalarAsync<int>(
            "SELECT COUNT(*) FROM newsletter_subscribers n JOIN clubs c ON c.id = n.club_id WHERE c.code = @C AND n.email = @E", ("@C", club), ("@E", email));
        if (count == 0)
        {
            return null;
        }

        var status = await C1Test.ScalarAsync<string>(
            "SELECT n.status FROM newsletter_subscribers n JOIN clubs c ON c.id = n.club_id WHERE c.code = @C AND n.email = @E", ("@C", club), ("@E", email));
        var source = await C1Test.ScalarAsync<string>(
            "SELECT n.source FROM newsletter_subscribers n JOIN clubs c ON c.id = n.club_id WHERE c.code = @C AND n.email = @E", ("@C", club), ("@E", email));
        var subscribedAt = await C1Test.ScalarAsync<DateTime?>(
            "SELECT n.subscribed_at FROM newsletter_subscribers n JOIN clubs c ON c.id = n.club_id WHERE c.code = @C AND n.email = @E", ("@C", club), ("@E", email));
        var unsubscribedAt = await C1Test.ScalarAsync<DateTime?>(
            "SELECT n.unsubscribed_at FROM newsletter_subscribers n JOIN clubs c ON c.id = n.club_id WHERE c.code = @C AND n.email = @E", ("@C", club), ("@E", email));
        return (status!, source, subscribedAt is not null, unsubscribedAt is not null);
    }

    private static Task<HttpResponseMessage> SubscribeAsync(HttpClient client, string club, object body)
        => AppTest.PostJsonAsync(client, $"/api/v1/{club}/newsletter/subscribe", body);

    [Fact]
    public async Task 訂閱_成功寫入名單_來源預設頁尾_留同意時間_信箱轉小寫_重複送出冪等()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("a");
        try
        {
            var response = await SubscribeAsync(client, "tcrfc", new { email = email.ToUpperInvariant(), consent = true });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ok", await response.Content.ReadAsStringAsync());

            var row = await RowAsync("tcrfc", email);
            Assert.NotNull(row);
            Assert.Equal("subscribed", row!.Value.Status);
            Assert.Equal("官網頁尾", row.Value.Source);
            Assert.True(row.Value.HasSubscribedAt);
            Assert.False(row.Value.HasUnsubscribedAt);

            // 重複送出：仍然 200，且名單只有一筆
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true, source = "news" })).StatusCode);
            Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM newsletter_subscribers WHERE email = @E", ("@E", email)));
            Assert.Equal("官網頁尾", (await RowAsync("tcrfc", email))!.Value.Source); // 不被第二次覆蓋
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 來源只收白名單代碼_換算成固定中文標籤()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("src");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true, source = "news" })).StatusCode);
            Assert.Equal("官網新聞頁", (await RowAsync("tcrfc", email))!.Value.Source);
            Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(client, "tcrfc", new { email = Email("src2"), consent = true, source = "<script>" })).StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 驗證_沒勾選同意400_信箱格式錯400_空白400_不存在的俱樂部404_都不寫入名單()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("v");
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(client, "tcrfc", new { email, consent = false })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(client, "tcrfc", new { email = "not-an-email", consent = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(client, "tcrfc", new { email = "  ", consent = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(client, "tcrfc", new { consent = true })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SubscribeAsync(client, "no-such-club", new { email, consent = true })).StatusCode);
            Assert.Null(await RowAsync("tcrfc", email));
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 蜜罐欄位有值_回成功但不寫入()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("bot");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true, website = "http://spam.example" })).StatusCode);
            Assert.Null(await RowAsync("tcrfc", email));
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 兩個俱樂部名單各自獨立_同一信箱可只在其中一站訂閱()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("both");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true })).StatusCode);
            Assert.NotNull(await RowAsync("tcrfc", email));
            Assert.Null(await RowAsync("bw", email));
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "bw", new { email, consent = true })).StatusCode);
            Assert.NotNull(await RowAsync("bw", email));
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 退訂是黏著的_公開表單再次送出不會改回訂閱_回應與成功相同()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("sticky");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true })).StatusCode);
            await BizTest.ExecuteSqlAsync(
                "UPDATE newsletter_subscribers SET status = 'unsubscribed', unsubscribed_at = SYSUTCDATETIME() WHERE email = @E", ("@E", email));

            var again = await SubscribeAsync(client, "tcrfc", new { email, consent = true });
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Equal("unsubscribed", (await RowAsync("tcrfc", email))!.Value.Status);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 憑證退訂_成功_冪等_跨俱樂部或亂碼憑證400()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var email = Email("unsub");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true })).StatusCode);

            using var scope = fixture.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<NewsletterUnsubscribeTokens>();
            var tcrfcId = await C1Test.ClubIdAsync("tcrfc");
            var bwId = await C1Test.ClubIdAsync("bw");
            var token = tokens.Create(tcrfcId, email);

            // 把 tcrfc 的憑證拿到 bw 路由用：不得生效
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(client, "/api/v1/bw/newsletter/unsubscribe", new { token })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(client, "/api/v1/tcrfc/newsletter/unsubscribe", new { token = "亂碼" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(client, "/api/v1/tcrfc/newsletter/unsubscribe", new { })).StatusCode);
            Assert.Equal("subscribed", (await RowAsync("tcrfc", email))!.Value.Status);

            var first = await AppTest.ReadAsync<NewsletterUnsubscribeResultDto>(await AppTest.PostJsonAsync(client, "/api/v1/tcrfc/newsletter/unsubscribe", new { token }));
            Assert.True(first.Changed);
            var row = (await RowAsync("tcrfc", email))!.Value;
            Assert.Equal("unsubscribed", row.Status);
            Assert.True(row.HasUnsubscribedAt);

            var second = await AppTest.ReadAsync<NewsletterUnsubscribeResultDto>(await AppTest.PostJsonAsync(client, "/api/v1/tcrfc/newsletter/unsubscribe", new { token }));
            Assert.False(second.Changed);

            // 不在名單內的信箱用有效憑證：視為成功、不透露、不新增
            var ghost = tokens.Create(bwId, Email("ghost"));
            var ghostResult = await AppTest.ReadAsync<NewsletterUnsubscribeResultDto>(await AppTest.PostJsonAsync(client, "/api/v1/bw/newsletter/unsubscribe", new { token = ghost }));
            Assert.False(ghostResult.Changed);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 後台名單看得到前台訂閱的來源與狀態()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var email = Email("adm");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SubscribeAsync(client, "tcrfc", new { email, consent = true, source = "home" })).StatusCode);
            var list = await BizTest.ReadAsync<Tcrfc.Api.Common.PagedResult<AdminNewsletterSubscriberDto>>(
                await admin.GetAsync($"/api/v1/admin/tcrfc/newsletter/subscribers?keyword={Uri.EscapeDataString(email)}"));
            var item = Assert.Single(list.Items);
            Assert.Equal("官網首頁", item.SourceLabel);
            Assert.Equal("subscribed", item.Status);
        }
        finally
        {
            await CleanupAsync();
        }
    }
}
