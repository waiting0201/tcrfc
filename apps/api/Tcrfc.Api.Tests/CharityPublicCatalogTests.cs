using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>CH-2 前台公開唯讀端點：站台文案、掃碼落地頁的店家、項目卡片牆與詳情。</summary>
[Collection(CharityCollection.Name)]
public sealed class CharityPublicCatalogTests(CharityApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    [Fact]
    public async Task 站台文案_繁中英文各自回傳_並帶出全站預設金額範圍()
    {
        using var client = fx.CreateClient();

        var zh = (await client.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings?lang=zh", TestJson.Options))!;
        var en = (await client.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings?lang=en", TestJson.Options))!;

        Assert.False(string.IsNullOrWhiteSpace(zh.Notice));
        Assert.False(string.IsNullOrWhiteSpace(en.Notice));
        Assert.NotEqual(zh.Notice, en.Notice);
        Assert.False(zh.IsFallback);
        Assert.True(zh.DefaultMinAmount >= 1);
        Assert.True(zh.DefaultMaxAmount > zh.DefaultMinAmount);
        Assert.True(zh.CreditListEnabled);
    }

    [Fact]
    public async Task 掃碼落地頁_有效店家回店名_對不到有效店家回store為null_不報錯()
    {
        var (_, slug) = await CreateStoreAsync(fx, nameEn: "CT Store EN");
        var (_, inactive) = await CreateStoreAsync(fx, status: "inactive");
        var (_, noEn) = await CreateStoreAsync(fx);
        using var client = fx.CreateClient();

        var zh = (await client.GetFromJsonAsync<PublicStoreLandingDto>($"{Base}/stores/{slug}?lang=zh", TestJson.Options))!;
        var en = (await client.GetFromJsonAsync<PublicStoreLandingDto>($"{Base}/stores/{slug}?lang=en", TestJson.Options))!;
        var fallback = (await client.GetFromJsonAsync<PublicStoreLandingDto>($"{Base}/stores/{noEn}?lang=en", TestJson.Options))!;

        Assert.StartsWith("CT店家", zh.Store!.Name);
        Assert.Equal("CT Store EN", en.Store!.Name);
        Assert.False(en.Store.IsFallback);
        Assert.StartsWith("CT店家", fallback.Store!.Name);
        Assert.True(fallback.Store.IsFallback); // 英文缺漏回退繁中並標示
        Assert.Null(zh.Store.LogoUrl);          // 沒上傳 Logo：前台降級為純文字店名，不留空框

        foreach (var invalid in new[] { inactive, "no-such-store" })
        {
            var response = await client.GetAsync($"{Base}/stores/{invalid}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null((await response.Content.ReadFromJsonAsync<PublicStoreLandingDto>(TestJson.Options))!.Store);
        }
    }

    [Fact]
    public async Task 項目卡片牆_只列已上架的_依排序_草稿不出現()
    {
        var (_, published) = await CreateProjectAsync(fx);
        var (_, draft) = await CreateProjectAsync(fx, status: "draft");
        using var client = fx.CreateClient();

        var cards = (await client.GetFromJsonAsync<List<PublicProjectCardDto>>($"{Base}/projects", TestJson.Options))!;

        Assert.Contains(cards, c => c.Slug == published);
        Assert.DoesNotContain(cards, c => c.Slug == draft);
        Assert.All(cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
    }

    [Fact]
    public async Task 項目詳情_含金額選項與生效的金額範圍_項目沒設就用全站預設()
    {
        var (_, withRange) = await CreateProjectAsync(fx, min: 200, max: 5000, options: [500, 100, 300]);
        var (_, noRange) = await CreateProjectAsync(fx, options: []);
        using var client = fx.CreateClient();
        var settings = (await client.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings", TestJson.Options))!;

        var a = (await client.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{withRange}", TestJson.Options))!;
        var b = (await client.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{noRange}", TestJson.Options))!;

        Assert.Equal([100, 300, 500], a.AmountOptions);
        Assert.Equal((200, 5000), (a.MinAmount, a.MaxAmount));
        Assert.Equal((settings.DefaultMinAmount, settings.DefaultMaxAmount), (b.MinAmount, b.MaxAmount));
        Assert.Equal("b2c_invoice", a.InvoiceMode);
        Assert.Equal(JsonValueKind.Object, a.Description!.Value.ValueKind);
        Assert.Equal("測試用款項用途", a.FundUsage);
    }

    [Fact]
    public async Task 項目詳情_草稿與不存在回404()
    {
        var (_, draft) = await CreateProjectAsync(fx, status: "draft");
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/projects/{draft}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/projects/ct-nope")).StatusCode);
    }

    [Fact]
    public async Task 公開回應不洩漏分潤設定_募款進度_加密欄位與物件鍵()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx, projectPct: 12m);
        var (_, storeSlug) = await CreateStoreAsync(fx, storePct: 7m);
        using var client = fx.CreateClient();

        var bodies = new[]
        {
            await client.GetStringAsync($"{Base}/projects"),
            await client.GetStringAsync($"{Base}/projects/{projectSlug}"),
            await client.GetStringAsync($"{Base}/stores/{storeSlug}"),
            await client.GetStringAsync($"{Base}/settings"),
        };

        foreach (var body in bodies)
        {
            Assert.DoesNotContain("share", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pct", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("encrypted", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("raised", body, StringComparison.OrdinalIgnoreCase);   // 規劃書 §3.2：前台不呈現募款進度
            Assert.DoesNotContain("target", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("progress", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("coverKey", body);
            Assert.DoesNotContain("logoKey", body);
        }
    }
}
