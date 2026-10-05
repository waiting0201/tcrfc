using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.AdminComics;
using Tcrfc.Api.Features.AdminFanEvents;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminPress;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 規劃書 §4.0「圖片上傳通則」的**跨模組端到端驗證**（STATUS S2-1／S2-2／S2-3／S3-1／S3-3／AP-1 的「上傳未驗」）。
/// 每個模組各自的上傳測試只用小圖驗證「接得上」；這裡改用 3000×2000、帶 EXIF（相機廠牌、方向、GPS）的
/// 真實 JPEG 打各模組的真實端點，再**直接從 Azurite 把物件取回來**逐項斷言通則：
/// 伺服器端轉 WebP、主檔長邊 ≤ 2560、衍生檔 1280／640／320 與後台 160 方形縮圖、不留原始檔、去 EXIF（含 GPS）。
/// 不 mock 物件儲存、不看 DTO 自述，只看實際存進去的位元組。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class ImageUploadGeneralRuleAcrossModulesTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static (string, byte[], string, string) Exif(string field)
        => (field, TestImages.JpegWithExifAndGps(), "photo.jpg", "image/jpeg");

    /// <summary>
    /// 對 <paramref name="prefix"/> 底下的物件逐項驗證通則：恰好 <paramref name="expectedSets"/> 組、每組五個物件
    /// （主檔＋四個衍生檔），沒有任何非 WebP 物件（＝不留原始檔），且每個物件的實際位元組都乾淨。
    /// </summary>
    private static async Task AssertGeneralRuleAsync(BlobContainerClient container, string prefix, int expectedSets, int sourceLongEdge = 3000)
    {
        var names = new List<string>();
        await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            names.Add(item.Name);
        }

        Assert.Equal(expectedSets * 5, names.Count);
        Assert.All(names, n => Assert.EndsWith(".webp", n, StringComparison.Ordinal)); // 不留 .jpg／.png 原始檔

        var mains = names.Where(n => !n.EndsWith("-1280.webp") && !n.EndsWith("-640.webp") && !n.EndsWith("-320.webp") && !n.EndsWith("-thumb.webp")).ToList();
        Assert.Equal(expectedSets, mains.Count);

        foreach (var main in mains)
        {
            var stem = main[..^".webp".Length];
            var expectedMainLongEdge = Math.Min(sourceLongEdge, 2560);

            foreach (var (name, longEdge) in new[] { (main, expectedMainLongEdge), ($"{stem}-1280.webp", 1280), ($"{stem}-640.webp", 640), ($"{stem}-320.webp", 320) })
            {
                var (info, bytes) = await InspectAsync(container, name);
                Assert.Equal(longEdge, Math.Max(info.Width, info.Height));
                Assert.True(bytes.Length > 0);
            }

            var (thumb, _) = await InspectAsync(container, $"{stem}-thumb.webp");
            Assert.Equal(160, thumb.Width);
            Assert.Equal(160, thumb.Height);
        }
    }

    private static async Task<(ImageInfo Info, byte[] Bytes)> InspectAsync(BlobContainerClient container, string name)
    {
        var blob = container.GetBlobClient(name);
        var props = (await blob.GetPropertiesAsync()).Value;
        Assert.Equal("image/webp", props.ContentType);

        var download = await blob.DownloadContentAsync();
        var bytes = download.Value.Content.ToArray();
        Assert.Equal("WEBP", Image.DetectFormat(bytes).Name, StringComparer.OrdinalIgnoreCase);

        var info = Image.Identify(bytes);
        Assert.Null(info.Metadata.ExifProfile); // 含 GPS
        Assert.Null(info.Metadata.IptcProfile);
        Assert.Null(info.Metadata.XmpProfile);
        Assert.Null(info.Metadata.IccProfile);
        return (info, bytes);
    }

    [AzuriteFact]
    public async Task S2_1_夥伴Logo_通則逐項成立()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var created = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(
            new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】通則 Logo") } }, Exif("logoDark")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var partner = await BizTest.ReadAsync<AdminPartnerDetailDto>(created);
        try
        {
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/partners/{partner.Id}/", 1);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}");
        }
    }

    [AzuriteFact]
    public async Task S2_2_慈善事蹟圖片_通則逐項成立()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Guid? orgId = null, recordId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = new { zh = BizTest.Zh("【測試】通則團體") } }, Exif("logo"))));
            orgId = org.Id;
            var created = await editor.PostAsync("/api/v1/admin/tcrfc/charity/records", BizTest.Multipart(new
            {
                charityId = org.Id, happenedOn = "2026-05-01",
                content = new { zh = new { donationContent = "【測試】通則足球", location = "【測試】南投" } },
            }, Exif("image")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var record = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(created);
            recordId = record.Id;

            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/impact-records/{record.Id}/image/", 1);
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/charities/{org.Id}/logo/", 1);
        }
        finally
        {
            if (recordId is Guid r) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/records/{r}"); }
            if (orgId is Guid o) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{o}"); }
        }
    }

    [AzuriteFact]
    public async Task S2_3_媒體專區高解析圖_通則逐項成立()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        var created = await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
            new { resourceType = "hires_image", status = "draft", content = new { zh = new { title = "【測試】通則高解析圖" } } }, Exif("file")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var press = await BizTest.ReadAsync<AdminPressDetailDto>(created);
        try
        {
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/press/{press.Id}/", 1);
        }
        finally
        {
            await pr.DeleteAsync($"/api/v1/admin/tcrfc/press-resources/{press.Id}");
        }
    }

    [AzuriteFact]
    public async Task S3_1_漫畫頁面與球迷活動封面_通則逐項成立()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var no = 8000 + Random.Shared.Next(900);
        var episode = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(
            new { episodeNo = no, status = "draft", content = new { zh = new { title = "【測試】通則集數" } } }, Exif("cover"))));
        Guid? eventId = null;
        try
        {
            await client.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}/pages", BizTest.Multipart(new { }, Exif("files"), Exif("files")));
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/comic/episodes/{episode.Id}/pages/", 2);
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/comic/episodes/{episode.Id}/cover/", 1);

            var ev = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { slug = BizTest.Unique("evt"), status = "draft", content = new { zh = new { name = "【測試】通則活動" } } }, Exif("cover"))));
            eventId = ev.Id;
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/fan-events/{ev.Id}/cover/", 1);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}");
            if (eventId is Guid e) { await client.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{e}"); }
        }
    }

    [AzuriteFact]
    public async Task S3_3_商品圖集_通則逐項成立()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "rule", ("M", 100, 1));
        try
        {
            var uploaded = await admin.PostAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/images", BizTest.Multipart(new { }, Exif("files"), Exif("files")));
            Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync());
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"tcrfc/shop/products/{made.ProductId}/", 2);
        }
        finally
        {
            await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{made.Variants[0].Id}");
            await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}");
        }
    }

    [AzuriteFact]
    public async Task AP_1_廣告素材_通則逐項成立()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_rule", ratio: "16:9", minW: 1280, minH: 720);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var campaign = await AdminAdsTests.CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromHours(1), TimeSpan.FromDays(2));
            var response = await biz.PostAsync($"/api/v1/admin/ads/campaigns/{campaign.Id}/creatives", BizTest.Multipart(
                new { locale = "zh", altText = "ZZTEST 通則素材", theme = "both" },
                ("image", TestImages.JpegWithGps(3200, 1800), "ad.jpg", "image/jpeg")));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            await AssertGeneralRuleAsync(fixture.InspectorContainer, $"ads/creatives/{campaign.Id}/", 1, sourceLongEdge: 3200);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }
}
