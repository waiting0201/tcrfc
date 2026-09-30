using System.Net;
using Azure.Storage.Blobs;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>E5 廣告素材與 E4 備援素材的圖片上傳，對<b>真實 Azurite</b> 驗證（不 mock 物件儲存）：版位素材規格檢查（長寬比、最小尺寸、檔案大小、是否允許影片）、
/// 「內容被改就回到待審」、檔期已排程後素材不能刪、備援素材在沒有可投放檔期時回傳（版位永不空白）。</summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminAdCreativeUploadTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private const string Ads = "/api/v1/admin/ads";

    private static async Task<int> CountAsync(BlobContainerClient container, string prefix)
    {
        var count = 0;
        await foreach (var _ in container.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    private static HttpContent CreativePayload(string locale = "zh", string alt = "ZZTEST 素材說明", string? title = null, params (string Field, byte[] Bytes, string FileName, string ContentType)[] files)
        => BizTest.Multipart(new { locale, altText = alt, title, ctaText = "了解更多", clickUrl = "https://example.com/zz", theme = "both" }, files);

    [Fact]
    public async Task 素材上傳_版位規格檢查_改內容回到待審_已排程後不能刪_刪除時物件一併刪除()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_upload", ratio: "16:9", minW: 1280, minH: 720, maxKb: 300);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var campaign = await AdminAdsTests.CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromHours(1), TimeSpan.FromDays(2));
            var url = $"{Ads}/campaigns/{campaign.Id}/creatives";

            // 缺圖、長寬比不符、尺寸太小、版位不允許影片
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.PostAsync(url, CreativePayload())).StatusCode);
            var ratio = await biz.PostAsync(url, CreativePayload(files: ("image", AppTest.Png(1600, 1600), "a.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, ratio.StatusCode);
            Assert.Contains("長寬比", await ratio.Content.ReadAsStringAsync());
            var small = await biz.PostAsync(url, CreativePayload(files: ("image", AppTest.Png(640, 360), "a.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, small.StatusCode);
            Assert.Contains("最小尺寸", await small.Content.ReadAsStringAsync());
            var video = await biz.PostAsync(url, CreativePayload(files: [("image", AppTest.Png(1280, 720), "a.png", "image/png"), ("video", new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70 }, "v.mp4", "video/mp4")]));
            Assert.Equal(HttpStatusCode.BadRequest, video.StatusCode);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"ads/creatives/{campaign.Id}/")); // 被拒絕的請求不留下任何物件

            // 缺 alt、A/B 標記錯誤
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.PostAsync(url, CreativePayload(alt: "", files: ("image", AppTest.Png(1280, 720), "a.png", "image/png")))).StatusCode);
            var badTag = await biz.PostAsync(url, BizTest.Multipart(new { locale = "zh", altText = "x", variantTag = "C" }, ("image", AppTest.Png(1280, 720), "a.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, badTag.StatusCode);

            // 成功：待審、有圖片網址、物件寫入
            var created = await AppTest.ReadAsync<AdminAdCreativeDto>(await biz.PostAsync(url, CreativePayload(files: ("image", AppTest.Png(1280, 720), "a.png", "image/png"))));
            Assert.Equal("pending", created.ReviewStatus);
            Assert.Equal("zh", created.Locale);
            Assert.NotNull(created.ImageUrl);
            Assert.Equal(1280, created.ImageWidth);
            Assert.True(await CountAsync(fixture.InspectorContainer, $"ads/creatives/{campaign.Id}/") > 0);

            // 核可 → 改文案 → 回到待審（核可過的素材不能被偷換成沒審過的內容）
            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/approve", new { });
            var edited = await AppTest.ReadAsync<AdminAdCreativeDto>(await biz.PutAsync($"{Ads}/creatives/{created.Id}", CreativePayload(alt: "ZZTEST 新說明")));
            Assert.Equal("pending", edited.ReviewStatus);
            Assert.Equal("ZZTEST 新說明", edited.AltText);
            // 沒有任何改動的儲存不會讓已核可的素材退回待審
            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/approve", new { });
            var same = await AppTest.ReadAsync<AdminAdCreativeDto>(await biz.PutAsync($"{Ads}/creatives/{created.Id}", CreativePayload(alt: "ZZTEST 新說明")));
            Assert.Equal("approved", same.ReviewStatus);
            // 換圖：舊物件刪除、回到待審；換上規格不符的圖被擋
            var badReplace = await biz.PutAsync($"{Ads}/creatives/{created.Id}", CreativePayload(alt: "ZZTEST 新說明", files: ("image", AppTest.Png(500, 500), "b.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, badReplace.StatusCode);
            var replaced = await AppTest.ReadAsync<AdminAdCreativeDto>(await biz.PutAsync($"{Ads}/creatives/{created.Id}", CreativePayload(alt: "ZZTEST 新說明", files: ("image", AppTest.Png(1920, 1080), "b.png", "image/png"))));
            Assert.Equal("pending", replaced.ReviewStatus);
            Assert.NotEqual(created.ImageKey, replaced.ImageKey);

            // 退回需要原因，退回後要修改才能再送審；核可只能從待審
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/reject", new { })).StatusCode);
            var rejected = await AppTest.ReadAsync<AdminAdCreativeDto>(await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/reject", new { reason = "含有不得刊登的字樣" }));
            Assert.Equal("rejected", rejected.ReviewStatus);
            Assert.Equal("含有不得刊登的字樣", rejected.RejectReason);
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/approve", new { })).StatusCode);

            // 草稿檔期可以刪素材（物件一併刪）；送審並核可後不能刪，只能暫停
            var second = await AppTest.ReadAsync<AdminAdCreativeDto>(await biz.PostAsync(url, CreativePayload("en", files: ("image", AppTest.Png(1280, 720), "c.png", "image/png"))));
            var before = await CountAsync(fixture.InspectorContainer, $"ads/creatives/{campaign.Id}/");
            Assert.Equal(HttpStatusCode.NoContent, (await biz.DeleteAsync($"{Ads}/creatives/{second.Id}")).StatusCode);
            Assert.True(await CountAsync(fixture.InspectorContainer, $"ads/creatives/{campaign.Id}/") < before);

            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/approve", new { }); // 已退回 → 409，改文案送回待審後再核可
            await biz.PutAsync($"{Ads}/creatives/{created.Id}", CreativePayload(alt: "ZZTEST 修正後"));
            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/approve", new { });
            await AppTest.PostJsonAsync(biz, $"{Ads}/campaigns/{campaign.Id}/submit", new { });
            await AppTest.PostJsonAsync(biz, $"{Ads}/campaigns/{campaign.Id}/approve", new { });
            var locked = await biz.DeleteAsync($"{Ads}/creatives/{created.Id}");
            Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
            Assert.True((await AppTest.ReadAsync<AdminAdCreativeDto>(await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{created.Id}/pause", new { }))).IsPaused);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 備援素材_版位沒有可投放檔期時回傳備援_未串接圖片時不回圖_移除備援素材_公開回應不含物件鍵()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var response = await biz.PostAsync(Ads + "/slots", BizTest.Multipart(
                new { slotCode = "zztest_fallback", screenCode = "S01", rotationCap = 1, fallbackLink = "https://example.com/house", isActive = true, content = new { zh = new { name = "ZZTEST 備援版位", fallbackAlt = "自家內容替代文字" } } },
                ("fallbackImage", AppTest.Png(1200, 400), "f.png", "image/png")));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var slot = await AppTest.ReadAsync<AdminAdSlotDto>(response);
            Assert.NotNull(slot.FallbackImageUrl);
            Assert.True(await CountAsync(fixture.InspectorContainer, "ads/slots/") > 0);

            var served = await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync("/api/v1/app/ads/zztest_fallback?lang=zh"));
            Assert.True(served.IsFallback);
            Assert.Equal("廣告", served.DisclosureLabel);
            var item = Assert.Single(served.Items);
            Assert.True(item.IsFallback);
            Assert.Null(item.CreativeId); // 備援素材不計曝光：沒有素材識別，App 的量測器不會啟動
            Assert.Equal("自家內容替代文字", item.AltText);
            Assert.Equal("https://example.com/house", item.ClickUrl);
            var raw = await (await anonymous.GetAsync("/api/v1/app/ads/zztest_fallback?lang=en")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("imageKey", raw, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"disclosureLabel\":\"Ad\"", raw);

            // 移除備援素材後，版位仍然回應（沒有圖的空清單），不是 404
            var removed = await AppTest.ReadAsync<AdminAdSlotDto>(await biz.PutAsync($"{Ads}/slots/{slot.Id}", BizTest.Multipart(
                new { slotCode = "zztest_fallback", screenCode = "S01", rotationCap = 1, isActive = true, removeFallbackImage = true, content = new { zh = new { name = "ZZTEST 備援版位" } } })));
            Assert.Null(removed.FallbackImageKey);
            var empty = await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync("/api/v1/app/ads/zztest_fallback"));
            Assert.True(empty.IsFallback);
            Assert.Empty(empty.Items);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/app/ads/zztest_missing")).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }
}
