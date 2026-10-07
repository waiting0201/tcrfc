using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminFanEvents;
using Tcrfc.Api.Features.AdminPress;
using Tcrfc.Api.Features.FanEvents;
using Tcrfc.Api.Features.Press;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 球迷活動與媒體專區封面的雙語替代文字（§4.0 圖片欄位組，<c>fan_events_i18n.cover_alt</c>／<c>press_resources_i18n.cover_alt</c>）：
/// 後台逐語系讀寫、過長 400、公開端點依語系輸出且英文空白回退繁中。含圖片，需要 Azurite。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class CoverAltTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    [AzuriteFact]
    public async Task 球迷活動封面替代文字_後台讀寫_公開依語系輸出_英文空白回退繁中_過長400()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? id = null;
        try
        {
            var tooLong = await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { status = "draft", content = new { zh = new { name = "x", coverAlt = new string('長', 201) } } }));
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

            var slug = BizTest.Unique("alt");
            var created = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new
                {
                    slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"),
                    content = new { zh = new { name = "【測試】封面替代文字", coverAlt = "球迷合照" }, en = new { name = "Cover alt event" } },
                }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            id = created.Id;
            Assert.Equal("球迷合照", created.Zh.CoverAlt);
            Assert.Null(created.En!.CoverAlt);

            var detailZh = await BizTest.ReadAsync<FanEventDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=zh"));
            Assert.Equal("球迷合照", detailZh.Event.CoverAlt);
            // 英文沒填 → 回退繁中
            var detailEn = await BizTest.ReadAsync<FanEventDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=en"));
            Assert.Equal("球迷合照", detailEn.Event.CoverAlt);

            // 補英文並更新：英文語系輸出英文
            var updated = await client.PutAsync($"/api/v1/admin/tcrfc/fan-events/{id}", BizTest.Multipart(
                new
                {
                    slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"),
                    content = new { zh = new { name = "【測試】封面替代文字", coverAlt = "球迷合照" }, en = new { name = "Cover alt event", coverAlt = "Fans photo" } },
                }));
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var detailEn2 = await BizTest.ReadAsync<FanEventDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=en"));
            Assert.Equal("Fans photo", detailEn2.Event.CoverAlt);
        }
        finally
        {
            if (id is not null)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{id}");
            }
        }
    }

    [AzuriteFact]
    public async Task 媒體資源封面替代文字_高解析圖後台讀寫_公開依語系輸出()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? id = null;
        try
        {
            var created = await BizTest.ReadAsync<AdminPressDetailDto>(await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
                new
                {
                    resourceType = "hires_image", status = "published",
                    content = new { zh = new { title = "【測試】封面替代文字", coverAlt = "主視覺" }, en = new { title = "Cover alt", coverAlt = "Key visual" } },
                }, ("file", TestImages.SmallPng(), "a.png", "image/png"))));
            id = created.Id;
            Assert.Equal("主視覺", created.Zh.CoverAlt);
            Assert.Equal("Key visual", created.En!.CoverAlt);

            var zh = await BizTest.ReadAsync<PagedResult<PressResourceDto>>(await anonymous.GetAsync("/api/v1/tcrfc/press?lang=zh&pageSize=100"));
            Assert.Equal("主視覺", zh.Items.Single(r => r.Id == id).CoverAlt);
            var en = await BizTest.ReadAsync<PagedResult<PressResourceDto>>(await anonymous.GetAsync("/api/v1/tcrfc/press?lang=en&pageSize=100"));
            Assert.Equal("Key visual", en.Items.Single(r => r.Id == id).CoverAlt);

            var tooLong = await pr.PutAsync($"/api/v1/admin/tcrfc/press-resources/{id}", BizTest.Multipart(
                new { resourceType = "hires_image", status = "published", content = new { zh = new { title = "x", coverAlt = new string('長', 201) } } }));
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        }
        finally
        {
            if (id is not null)
            {
                await pr.DeleteAsync($"/api/v1/admin/tcrfc/press-resources/{id}");
            }
        }
    }
}
