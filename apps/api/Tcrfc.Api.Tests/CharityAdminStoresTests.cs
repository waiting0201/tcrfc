using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>N1 店家管理與 QR Code：授權、分潤設定的獨立授權與稽核、slug 重產、QR 產出、批次匯出、Logo 上傳、累計數字。</summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminStoresTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Stores = AdminBase + "/stores";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private static object NewStore(string? name = null, decimal? pct = null, string? status = null, string? nameEn = null) => new
    {
        nameZh = name ?? $"CT店家{Guid.NewGuid():N}"[..14],
        nameEn,
        category = "餐飲",
        address = "臺中市西區測試路 1 號",
        contactName = "王小明",
        contactPhone = "0912-345-678",
        storeSharePct = pct,
        status,
    };

    private async Task<HttpClient> ClientAsync(bool superAdmin, params string[] roles)
        => fx.CreateClientFor(await fx.CreateAdminAsync(superAdmin, roles));

    private static async Task<AdminStoreDetailDto> ReadAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<AdminStoreDetailDto>(TestJson.Options))!;

    // ═══════════════════════════════════════════════════════════════════════
    // 授權
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 授權_沒有N1權限的角色回403_唯讀角色只能看不能改_商務角色可建立與編輯()
    {
        using var noAccess = await ClientAsync(false, "customer_service_admin");   // 客服：沒有任何 N1 權限
        using var viewer = await ClientAsync(false, "viewer");
        using var biz = await ClientAsync(false, "business_sponsorship");

        Assert.Equal(HttpStatusCode.Forbidden, (await noAccess.GetAsync(Stores)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Stores)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions)).StatusCode);

        var created = await biz.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var store = await ReadAsync(created);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore(), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await biz.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore("CT店家改名"), TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // slug 與 QR 網址
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 建立店家_slug由系統產生_不可由編號推導_QR網址只帶slug不帶金額或分潤參數()
    {
        using var client = await ClientAsync(true);

        var a = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));
        var b = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));

        Assert.Matches("^[a-z2-9]{16}$", a.Slug);
        Assert.NotEqual(a.Slug, b.Slug);
        Assert.DoesNotContain(a.Id.ToString("N")[..8], a.Slug);
        Assert.Equal($"{CharityApiFixture.PublicBaseUrl}/zh/s/{a.Slug}", a.QrTargetUrl);
        Assert.DoesNotContain("?", a.QrTargetUrl);
        Assert.DoesNotContain("amount", a.QrTargetUrl!, StringComparison.OrdinalIgnoreCase);

        // 客戶端想自己指定 slug 也沒用：請求沒有這個欄位，多帶的欄位被忽略
        var tryOwn = await client.PostAsJsonAsync(Stores, new { nameZh = "CT店家指定slug", slug = "my-chosen-slug" }, TestJson.WriteOptions);
        Assert.NotEqual("my-chosen-slug", (await ReadAsync(tryOwn)).Slug);
    }

    [Fact]
    public async Task 重新產生slug_要二次確認_舊網址立即失效_新網址生效_稽核留下操作者()
    {
        var admin = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var client = fx.CreateClientFor(admin);
        var store = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));

        var noConfirm = await client.PostAsJsonAsync($"{Stores}/{store.Id}/regenerate-slug", new { confirm = false }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);
        Assert.Equal(store.Slug, (await ReadAsync(await client.GetAsync($"{Stores}/{store.Id}"))).Slug);

        var response = await client.PostAsJsonAsync($"{Stores}/{store.Id}/regenerate-slug", new { confirm = true }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AdminStoreSlugResponse>(TestJson.Options))!;
        Assert.NotEqual(store.Slug, result.Slug);

        using var publicClient = fx.CreateClient();
        var oldLanding = await publicClient.GetFromJsonAsync<JsonElement>($"{Base}/stores/{store.Slug}", TestJson.Options);
        var newLanding = await publicClient.GetFromJsonAsync<JsonElement>($"{Base}/stores/{result.Slug}", TestJson.Options);
        Assert.Equal(JsonValueKind.Null, oldLanding.GetProperty("store").ValueKind); // 舊 QR 立即失效（視同無店家歸屬，不報錯）
        Assert.Equal(JsonValueKind.Object, newLanding.GetProperty("store").ValueKind);

        Assert.Equal(1, await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_logs WHERE action = N'store.slug_regenerate' AND admin_user_id = @a AND target_id = @t", ("@a", admin.Id), ("@t", store.Id)));
    }

    [Fact]
    public async Task 重新產生slug_唯讀角色不能做()
    {
        using var admin = await ClientAsync(true);
        var store = await ReadAsync(await admin.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));
        using var viewer = await ClientAsync(false, "viewer");

        var response = await viewer.PostAsJsonAsync($"{Stores}/{store.Id}/regenerate-slug", new { confirm = true }, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 分潤設定：獨立授權＋稽核
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 設定分潤需要獨立授權_商務角色能編輯店家卻不能動分潤_系統管理員可以並留下稽核()
    {
        var sys = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var sysClient = fx.CreateClientFor(sys);
        using var biz = await ClientAsync(false, "business_sponsorship");

        // 建立時就帶分潤：商務 403，且沒有留下半成品店家
        var name = $"CT店家分潤{Guid.NewGuid():N}"[..16];
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PostAsJsonAsync(Stores, NewStore(name, pct: 5m), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_stores_i18n WHERE name = @n", ("@n", name)));

        var store = await ReadAsync(await sysClient.PostAsJsonAsync(Stores, NewStore(pct: 5m), TestJson.WriteOptions));
        Assert.Equal(5m, store.StoreSharePct);
        Assert.Equal(1, await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_logs WHERE action = N'store.share_pct_set' AND admin_user_id = @a AND target_id = @t", ("@a", sys.Id), ("@t", store.Id)));

        // 商務編輯其他欄位、分潤沿用原值（或不帶）：允許，且不產生稽核
        var sameAsBefore = await biz.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore("CT店家改", pct: 5m), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, sameAsBefore.StatusCode);
        var omitted = await biz.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore("CT店家再改"), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.Equal(5m, (await ReadAsync(omitted)).StoreSharePct);

        // 商務想改分潤：403，值不變
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore(pct: 9m), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(5m, (await ReadAsync(await sysClient.GetAsync($"{Stores}/{store.Id}"))).StoreSharePct);

        // 系統管理員改分潤：成功並留稽核（含新舊值，不含個資）
        var changed = await sysClient.PutAsJsonAsync($"{Stores}/{store.Id}", NewStore(pct: 8m), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var summary = await fx.ScalarAsync<string>(
            "SELECT TOP 1 change_summary FROM audit_logs WHERE action = N'store.share_pct_set' AND target_id = @t ORDER BY seq DESC", ("@t", store.Id));
        Assert.Contains("5", summary!);
        Assert.Contains("8", summary!);
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'store.share_pct_set' AND target_id = @t", ("@t", store.Id)));
    }

    [Fact]
    public async Task 儲存時驗證_店家分潤加項目分潤不得超過100_停止合作的店家不受限()
    {
        await CreateProjectAsync(fx, projectPct: 60m);
        using var client = await ClientAsync(true);

        var tooHigh = await client.PostAsJsonAsync(Stores, NewStore(pct: 50m), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Contains("100", await tooHigh.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Stores, NewStore(pct: 40m), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Stores, NewStore(pct: 50m, status: "inactive"), TestJson.WriteOptions)).StatusCode);
    }

    public static IEnumerable<object[]> InvalidStoreBodies() =>
    [
        ["no-name"], ["pct-over-100"], ["pct-negative"], ["pct-three-decimals"], ["bad-status"], ["date-inverted"], ["name-too-long"],
    ];

    [Theory]
    [MemberData(nameof(InvalidStoreBodies))]
    public async Task 驗證_格式錯誤回400(string scenario)
    {
        using var client = await ClientAsync(true);
        object body = scenario switch
        {
            "no-name" => new { nameZh = "  " },
            "pct-over-100" => new { nameZh = "CT店家", storeSharePct = 100.01m },
            "pct-negative" => new { nameZh = "CT店家", storeSharePct = -1m },
            "pct-three-decimals" => new { nameZh = "CT店家", storeSharePct = 5.123m },
            "bad-status" => new { nameZh = "CT店家", status = "deleted" },
            "date-inverted" => new { nameZh = "CT店家", startOn = "2026-12-31", endOn = "2026-01-01" },
            "name-too-long" => new { nameZh = new string('字', 129) },
            _ => throw new InvalidOperationException(scenario),
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Stores, body, TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // QR Code
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QR_PNG與SVG可下載_PDF尚未提供回501_格式不明回400()
    {
        using var client = await ClientAsync(true);
        var store = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));

        var png = await client.GetAsync($"{Stores}/{store.Id}/qr?format=png");
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal("image/png", png.Content.Headers.ContentType!.MediaType);
        var pngBytes = await png.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], pngBytes[..4]);

        var svg = await client.GetAsync($"{Stores}/{store.Id}/qr?format=svg");
        Assert.Equal(HttpStatusCode.OK, svg.StatusCode);
        Assert.Contains("<svg", await svg.Content.ReadAsStringAsync());

        var pdf = await client.GetAsync($"{Stores}/{store.Id}/qr?format=pdf");
        Assert.Equal(HttpStatusCode.NotImplemented, pdf.StatusCode);
        Assert.Contains("PDF", await pdf.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Stores}/{store.Id}/qr?format=gif")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Stores}/{Guid.NewGuid()}/qr")).StatusCode);

        // 預設格式是 PNG；每模組像素數可調，放大後檔案更大
        var defaultPng = await (await client.GetAsync($"{Stores}/{store.Id}/qr")).Content.ReadAsByteArrayAsync();
        var bigPng = await (await client.GetAsync($"{Stores}/{store.Id}/qr?format=png&size=20")).Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], defaultPng[..4]);
        Assert.True(bigPng.Length > defaultPng.Length);
    }

    [Fact]
    public void QR編碼_同一網址產出相同圖_不同網址不同圖_容錯等級Q比L的碼更密()
    {
        var a1 = CharityQrCodeGenerator.Png("http://charity.test/zh/s/aaaa");
        var a2 = CharityQrCodeGenerator.Png("http://charity.test/zh/s/aaaa");
        var b = CharityQrCodeGenerator.Png("http://charity.test/zh/s/bbbb");

        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);

        // Q 級（25%）的碼比 L 級（7%）在同樣內容下版本更高（模組更多）：直接用 QRCoder 比較同一網址兩種等級的矩陣邊長，
        // 確認我們用的不是預設的 L。（規劃書 §2.3：建議 Q 級，因為印刷品常有污損與局部遮擋。）
        const string url = "http://charity.test/zh/s/aaaaaaaaaaaaaaaa";
        using var generator = new QRCoder.QRCodeGenerator();
        using var q = generator.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var l = generator.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.L);
        var svgQ = CharityQrCodeGenerator.Svg(url);
        Assert.True(q.ModuleMatrix.Count >= l.ModuleMatrix.Count);
        Assert.Contains($"viewBox=\"0 0 {q.ModuleMatrix.Count} {q.ModuleMatrix.Count}\"", svgQ);
    }

    [Fact]
    public async Task QR批次匯出_需要匯出權限_zip內每家合作中店家一張_停止合作的不在內()
    {
        using var sys = await ClientAsync(true);
        using var biz = await ClientAsync(false, "business_sponsorship");
        var a = await ReadAsync(await sys.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));
        var b = await ReadAsync(await sys.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));
        var stopped = await ReadAsync(await sys.PostAsJsonAsync(Stores, NewStore(status: "inactive"), TestJson.WriteOptions));

        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync($"{Stores}/qr-export")).StatusCode);

        var response = await sys.GetAsync($"{Stores}/qr-export?format=svg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var names = zip.Entries.Select(e => e.Name).ToList();
        Assert.Contains(names, n => n.Contains(a.Slug) && n.EndsWith(".svg"));
        Assert.Contains(names, n => n.Contains(b.Slug) && n.EndsWith(".svg"));
        Assert.DoesNotContain(names, n => n.Contains(stopped.Slug));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Logo 上傳
    // ═══════════════════════════════════════════════════════════════════════

    private static MultipartFormDataContent FileBody(byte[] bytes, string name = "logo.png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", name);
        return content;
    }

    [Fact]
    public async Task Logo上傳_換圖後舊物件被刪_刪除Logo清空欄位_壞檔案與過大檔案回400()
    {
        using var client = await ClientAsync(true);
        var store = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(), TestJson.WriteOptions));
        Assert.Null(store.LogoUrl);

        var first = await ReadAsync(await client.PostAsync($"{Stores}/{store.Id}/logo", FileBody(TestImages.SmallPng())));
        Assert.StartsWith("https://img.charity-test.invalid/stores/", first.LogoUrl);
        Assert.Single(fx.Images.Keys);

        var second = await ReadAsync(await client.PostAsync($"{Stores}/{store.Id}/logo", FileBody(TestImages.SmallPng())));
        Assert.NotEqual(first.LogoUrl, second.LogoUrl);
        Assert.Single(fx.Images.Keys); // 新圖寫入成功才刪舊物件

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{Stores}/{store.Id}/logo", FileBody(TestImages.FakeImageBytes()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"{Stores}/{store.Id}/logo", FileBody(TestImages.OversizedBytes()))).StatusCode);
        Assert.Single(fx.Images.Keys); // 失敗的上傳沒有改動既有圖片

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Stores}/{store.Id}/logo")).StatusCode);
        Assert.Empty(fx.Images.Keys);
        Assert.Null((await ReadAsync(await client.GetAsync($"{Stores}/{store.Id}"))).LogoUrl);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 列表與累計數字
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 列表_累計筆數金額與應付回饋金只計已付款_篩選與分頁()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx, projectPct: 10m);
        using var client = await ClientAsync(true);
        var store = await ReadAsync(await client.PostAsJsonAsync(Stores, NewStore(pct: 5m), TestJson.WriteOptions));
        using var publicClient = fx.CreateClient();

        await CreatePaidDonationAsync(fx, publicClient, projectSlug, 1000, store.Slug);   // 店家回饋 50
        await CreatePaidDonationAsync(fx, publicClient, projectSlug, 333, store.Slug);    // 店家回饋 16
        await CreateDonationAsync(publicClient, NewRequest(projectSlug, 5000, store.Slug)); // 只建單未付款：不得計入

        var list = (await client.GetFromJsonAsync<PagedResult<AdminStoreListItemDto>>($"{Stores}?keyword={Uri.EscapeDataString(store.NameZh!)}", TestJson.Options))!;
        var row = Assert.Single(list.Items);
        Assert.Equal(2, row.PaidCount);
        Assert.Equal(1333, row.PaidTotal);
        Assert.Equal(66, row.StoreShareAccrued);

        var inactive = (await client.GetFromJsonAsync<PagedResult<AdminStoreListItemDto>>($"{Stores}?status=inactive&pageSize=5", TestJson.Options))!;
        Assert.DoesNotContain(inactive.Items, i => i.Id == store.Id);
        Assert.All(inactive.Items, i => Assert.Equal("inactive", i.Status));
        Assert.Equal(5, (await client.GetFromJsonAsync<PagedResult<AdminStoreListItemDto>>($"{Stores}?pageSize=5", TestJson.Options))!.PageSize);
    }
}
