using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Features.AdminPartnerStores;
using Tcrfc.Api.Features.Geocoding;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>地址定位的接縫（不需要資料庫）：假實作的決定性、查無與故障行為，以及「尚未串接」實作如實拒絕。</summary>
public sealed class GeocoderTests
{
    [Fact]
    public async Task 假定位_同地址永遠同結果_且座標落在臺灣範圍()
    {
        var geocoder = new LocalFakeGeocoder();
        var a = await geocoder.GeocodeAsync("台中市西屯區臺灣大道三段 1 號", CancellationToken.None);
        var b = await geocoder.GeocodeAsync("  台中市西屯區臺灣大道三段 1 號 ", CancellationToken.None);
        var other = await geocoder.GeocodeAsync("高雄市前鎮區中山三路 2 號", CancellationToken.None);

        Assert.NotNull(a);
        Assert.Equal(a, b);
        Assert.NotEqual(a!.Lat, other!.Lat);
        Assert.InRange(a.Lat, 22.0m, 25.2m);
        Assert.InRange(a.Lng, 120.0m, 121.9m);
        Assert.Equal("fake", a.Provider);
        Assert.Equal(a.Lat, Math.Round(a.Lat, 6)); // decimal(9,6)
    }

    [Fact]
    public async Task 假定位_查無回null_故障拋例外()
    {
        var geocoder = new LocalFakeGeocoder();
        Assert.Null(await geocoder.GeocodeAsync("查無此路 99 號", CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => geocoder.GeocodeAsync("故障路 1 號", CancellationToken.None));
    }

    [Fact]
    public async Task 尚未串接實作_如實回報不假裝成功()
    {
        var geocoder = new NotConfiguredGeocoder();
        Assert.False(geocoder.IsConfigured);
        var ex = await Assert.ThrowsAsync<Tcrfc.Api.Common.FeatureNotConfiguredException>(() => geocoder.GeocodeAsync("任何地址", CancellationToken.None));
        Assert.Equal("geocoder_not_configured", ex.Code);
    }
}

/// <summary>S2-5：特約店家「由地址定位」。預覽端點不寫入任何資料；儲存時自動定位只在管理者明確要求且沒有手動座標時發生，失敗不阻擋存檔。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPartnerStoreLocateTests(AdminWriteApiFixture fixture)
{
    private const string Base = "/api/v1/admin/tcrfc/partner-stores";

    private static object Payload(string tag, string address, decimal? lat = null, decimal? lng = null, bool autoLocate = false) => new
    {
        category = "餐飲", region = "台中市西屯區", lat, lng, autoLocate, phone = "04-0000-0000", applicableTier = "all",
        startOn = "2026-01-01", sortOrder = 50, status = "draft", isShared = false,
        content = new
        {
            zh = new { name = $"【測試】定位店家{tag}", address, offerContent = "【測試】九折" },
            en = new { name = $"Locate Test {tag}", address = "No. 1 Test Rd." },
        },
    };

    private static async Task<AdminPartnerStoreDetailDto> CreateAsync(HttpClient client, object payload, List<Guid> ids)
    {
        var created = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await client.PostAsync(Base, BizTest.Multipart(payload)));
        ids.Add(created.Id);
        return created;
    }

    private static async Task CleanupAsync(HttpClient client, List<Guid> ids)
    {
        foreach (var id in ids)
        {
            await client.DeleteAsync($"{Base}/{id}");
        }
    }

    [Fact]
    public async Task 預覽端點_權限_未登入401_沒有K4權限403_有權限200()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{Base}/locate", BizTest.Json(new { address = "台中市" }))).StatusCode);
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync($"{Base}/locate", BizTest.Json(new { address = "台中市" }))).StatusCode);
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = "台中市" }))).StatusCode);
    }

    [Fact]
    public async Task 預覽端點_回候選座標_查無404_空地址400_且不寫入任何店家()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var before = (await BizTest.ReadAsync<List<AdminPartnerStoreListItemDto>>(await service.GetAsync(Base))).Count;

        var ok = await BizTest.ReadAsync<LocatePartnerStoreResponse>(await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = "台中市西屯區測試路 1 號" })));
        var expected = await new LocalFakeGeocoder().GeocodeAsync("台中市西屯區測試路 1 號", CancellationToken.None);
        Assert.Equal(expected!.Lat, ok.Lat);
        Assert.Equal(expected.Lng, ok.Lng);

        var notFound = await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = "查無此路 99 號" }));
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Contains("查無此地址", await notFound.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = "  " }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = new string('址', 501) }))).StatusCode);

        Assert.Equal(before, (await BizTest.ReadAsync<List<AdminPartnerStoreListItemDto>>(await service.GetAsync(Base))).Count);
    }

    [Fact]
    public async Task 儲存時自動定位_手動座標優先_沒要求不定位_查無與故障不阻擋存檔()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var ids = new List<Guid>();
        try
        {
            const string address = "台中市西屯區測試路 1 號";
            var expected = await new LocalFakeGeocoder().GeocodeAsync(address, CancellationToken.None);

            // 勾選自動定位、沒填座標 → 由地址定位
            var located = await CreateAsync(service, Payload(BizTest.Unique("a"), address, autoLocate: true), ids);
            Assert.Equal("located", located.AutoLocateStatus);
            Assert.Equal(expected!.Lat, located.Lat);
            Assert.Equal(expected.Lng, located.Lng);

            // 讀回來是存進去的值，且 GET 不帶狀態
            var reloaded = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await service.GetAsync($"{Base}/{located.Id}"));
            Assert.Equal(expected.Lat, reloaded.Lat);
            Assert.Null(reloaded.AutoLocateStatus);

            // 手動座標永遠優先，即使同時勾選自動定位
            var manual = await CreateAsync(service, Payload(BizTest.Unique("b"), address, 24.5m, 120.5m, autoLocate: true), ids);
            Assert.Equal("skipped", manual.AutoLocateStatus);
            Assert.Equal(24.5m, manual.Lat);
            Assert.Equal(120.5m, manual.Lng);

            // 沒勾選就不定位（規劃書：人工確認後儲存）
            var plain = await CreateAsync(service, Payload(BizTest.Unique("c"), address), ids);
            Assert.Equal("skipped", plain.AutoLocateStatus);
            Assert.Null(plain.Lat);

            // 查無此地址：存檔成功、座標留空、狀態回報
            var notFound = await CreateAsync(service, Payload(BizTest.Unique("d"), "查無此路 99 號", autoLocate: true), ids);
            Assert.Equal("not_found", notFound.AutoLocateStatus);
            Assert.Null(notFound.Lat);
            Assert.Null(notFound.Lng);

            // 沒有地址
            var noAddress = await CreateAsync(service, Payload(BizTest.Unique("e"), "", autoLocate: true), ids);
            Assert.Equal("not_found", noAddress.AutoLocateStatus);

            // 供應商故障：同樣不阻擋存檔
            var broken = await CreateAsync(service, Payload(BizTest.Unique("f"), "故障路 1 號", autoLocate: true), ids);
            Assert.Equal("unavailable", broken.AutoLocateStatus);
            Assert.Null(broken.Lat);

            // 更新：可手動覆寫自動定位的結果，也可對舊店家重新定位
            var overridden = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await service.PutAsync($"{Base}/{located.Id}",
                BizTest.Multipart(Payload(BizTest.Unique("g"), address, 23.0m, 120.1m))));
            Assert.Equal(23.0m, overridden.Lat);
            var relocated = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await service.PutAsync($"{Base}/{plain.Id}",
                BizTest.Multipart(Payload(BizTest.Unique("h"), address, autoLocate: true))));
            Assert.Equal("located", relocated.AutoLocateStatus);
            Assert.Equal(expected.Lat, relocated.Lat);
        }
        finally
        {
            await CleanupAsync(service, ids);
        }
    }

    [Fact]
    public async Task 定位服務尚未串接_預覽回503_儲存仍成功只是座標留空()
    {
        using var factory = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IGeocoder, NotConfiguredGeocoder>()));
        using var service = await BizTest.ClientAsync(factory, "customer.service@tcrfc.test");
        var ids = new List<Guid>();
        try
        {
            var locate = await service.PostAsync($"{Base}/locate", BizTest.Json(new { address = "台中市西屯區測試路 1 號" }));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, locate.StatusCode);
            var problem = await locate.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("geocoder_not_configured", problem.GetProperty("code").GetString());

            var created = await CreateAsync(service, Payload(BizTest.Unique("n"), "台中市西屯區測試路 1 號", autoLocate: true), ids);
            Assert.Equal("unavailable", created.AutoLocateStatus);
            Assert.Null(created.Lat);
        }
        finally
        {
            await CleanupAsync(service, ids);
        }
    }
}
