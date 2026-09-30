using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Features.Partners;
using Tcrfc.Api.Features.Sponsors;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// E1／E2／B5 公開讀取端點（前台 09.1、09.2、09.4、11.2–11.4）。資料來自 <c>db/seed/backoffice_seed.py</c> 的【測試】種子
/// （tcrfc：5 個夥伴、3 個贊助商、9 個方案、3 個計畫、2 筆事蹟、3 個統計項目）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class BusinessPublicEndpointsTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 夥伴_只列合作期間涵蓋今天的_類型與首頁篩選_英文回退_共同參與的公益計畫()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var all = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/tcrfc/partners?lang=zh"));
        Assert.Contains(all, p => p.Slug == "test-partner-strategic");
        Assert.DoesNotContain(all, p => p.Slug == "test-partner-training"); // 合作期間 2026-06-30 已結束

        var strategic = all.Single(p => p.Slug == "test-partner-strategic");
        Assert.Equal("【測試】示範策略夥伴", strategic.Name);
        Assert.Equal("【測試】共同推動在地足球發展的策略合作。", strategic.Content);
        Assert.Contains(strategic.CharityPrograms, c => c.Slug == "test-charity-program-a"); // 共同參與的公益計畫

        var typed = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/tcrfc/partners?type=%E5%93%81%E7%89%8C%E5%A4%A5%E4%BC%B4"));
        Assert.All(typed, p => Assert.Equal("品牌夥伴", p.PartnerType));

        var home = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/tcrfc/partners?home=true"));
        Assert.All(home, p => Assert.True(p.ShowOnHome));
        var footer = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/tcrfc/partners?footer=true"));
        Assert.All(footer, p => Assert.True(p.ShowInFooter));

        // 沒有英文版：英文請求回退中文名稱（不是空白）
        var en = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/tcrfc/partners?lang=en"));
        Assert.Equal("【測試】示範教育夥伴", en.Single(p => p.Slug == "test-partner-education").Name);
        Assert.Equal("Test Strategic Partner", en.Single(p => p.Slug == "test-partner-strategic").Name);

        // 資料依俱樂部分開：藍鯨看不到磐石的測試夥伴，未知俱樂部 404
        var bw = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync("/api/v1/bw/partners"));
        Assert.DoesNotContain(bw, p => p.Slug.StartsWith("test-partner-"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/nope/partners")).StatusCode);
    }

    [Fact]
    public async Task 贊助商_依等級排序_合約結束不列_不輸出聯絡窗口_贊助故事只列已發布文章()
    {
        var articleId = await BizTest.ScalarGuidAsync(
            "SELECT TOP 1 a.id FROM articles a JOIN clubs c ON c.id = a.club_id WHERE c.code = N'tcrfc' AND a.status = 'published' ORDER BY a.row_seq");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var sponsor = await BizTest.ReadAsync<AdminSponsorDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(new
        {
            tier = "支持夥伴", contactName = "機密聯絡人", contactEmail = "secret@example.com", articleIds = new[] { articleId },
            content = new { zh = BizTest.Zh("【測試】故事贊助商") },
        })));
        try
        {
            Assert.Single(sponsor.Articles);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.PutAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}", BizTest.Multipart(new
            {
                tier = "支持夥伴", articleIds = new[] { Guid.NewGuid() }, content = new { zh = BizTest.Zh("【測試】故事贊助商") },
            }))).StatusCode);

            using var client = await BizTest.ClientAsync(fixture, null);
            var raw = await (await client.GetAsync("/api/v1/tcrfc/sponsors?lang=zh")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("secret@example.com", raw);
            Assert.DoesNotContain("機密聯絡人", raw);

            var list = await BizTest.ReadAsync<List<SponsorDto>>(await client.GetAsync("/api/v1/tcrfc/sponsors?lang=zh"));
            var slugs = list.Select(s => s.Slug).ToList();
            Assert.Contains("test-sponsor-main", slugs);
            Assert.Contains("test-sponsor-official", slugs);
            Assert.DoesNotContain("test-sponsor-support", slugs); // 合約 2026-06-30 已結束
            Assert.True(slugs.IndexOf("test-sponsor-main") < slugs.IndexOf("test-sponsor-official")); // 主贊助排最前
            Assert.Equal(2, list.Single(s => s.Slug == "test-sponsor-main").Activations.Count);
            Assert.Single(list.Single(s => s.Id == sponsor.Id).Stories);
        }
        finally
        {
            await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}");
        }
    }

    [Fact]
    public async Task 贊助方案_只回已發布_價格不公開時完全不輸出價格()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var hidden = await BizTest.ReadAsync<AdminSponsorPackageDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "published", priceMin = 111, priceMax = 222, isPricePublic = false, content = new { zh = new { name = "【測試】不公開價格方案" } },
        })));
        var visible = await BizTest.ReadAsync<AdminSponsorPackageDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "published", priceMin = 333, priceMax = 444, isPricePublic = true, content = new { zh = new { name = "【測試】公開價格方案" } },
        })));
        var draft = await BizTest.ReadAsync<AdminSponsorPackageDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "draft", content = new { zh = new { name = "【測試】草稿方案" } },
        })));
        try
        {
            using var client = await BizTest.ClientAsync(fixture, null);
            var list = await BizTest.ReadAsync<List<SponsorPackageDto>>(await client.GetAsync("/api/v1/tcrfc/sponsor-packages?lang=zh"));
            var h = list.Single(p => p.Id == hidden.Id);
            Assert.Null(h.PriceMin);
            Assert.Null(h.PriceMax);
            var v = list.Single(p => p.Id == visible.Id);
            Assert.Equal(333, v.PriceMin);
            Assert.Equal(444, v.PriceMax);
            Assert.DoesNotContain(list, p => p.Id == draft.Id);
            Assert.True(list.Count(p => p.Slug.StartsWith("test-package-")) >= 8);
        }
        finally
        {
            foreach (var id in new[] { hidden.Id, visible.Id, draft.Id })
            {
                await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsor-packages/{id}");
            }
        }
    }

    [Fact]
    public async Task 慈善_計畫列表與詳情_只回已發布_進行狀態_置頂優先_關聯夥伴贊助商()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var list = await BizTest.ReadAsync<PagedResult<CharityProgramListItemDto>>(await client.GetAsync("/api/v1/tcrfc/charity/programs?lang=zh"));
        var slugs = list.Items.Select(p => p.Slug).ToList();
        Assert.Contains("test-charity-program-a", slugs);
        Assert.Contains("test-charity-program-b", slugs);
        Assert.DoesNotContain("test-charity-program-c", slugs); // 草稿
        Assert.True(slugs.IndexOf("test-charity-program-a") < slugs.IndexOf("test-charity-program-b")); // a 置頂
        Assert.Equal("ongoing", list.Items.Single(p => p.Slug == "test-charity-program-a").Progress);
        Assert.Equal("completed", list.Items.Single(p => p.Slug == "test-charity-program-b").Progress);

        var detail = await BizTest.ReadAsync<CharityProgramDetailDto>(await client.GetAsync("/api/v1/tcrfc/charity/programs/test-charity-program-a?lang=zh"));
        Assert.Equal("【測試】示範公益團體甲", detail.Charity!.Name);
        Assert.Single(detail.Partners);
        Assert.Single(detail.Sponsors);
        Assert.Contains("足球 50 顆", detail.DonationContent);
        Assert.StartsWith("[", detail.Content); // 區塊編輯器 JSON
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/charity/programs/test-charity-program-c")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/charity/programs/nope")).StatusCode);

        // 事蹟依計畫篩選
        var byProgram = await BizTest.ReadAsync<PagedResult<ImpactRecordDto>>(await client.GetAsync("/api/v1/tcrfc/charity/records?program=test-charity-program-a"));
        Assert.All(byProgram.Items, r => Assert.Equal("test-charity-program-a", r.ProgramSlug));
    }

    [Fact]
    public async Task 慈善_影響力數據只輸出公開項目_自動彙整_捐款導流來自後台設定()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var impact = await BizTest.ReadAsync<ImpactSummaryDto>(await client.GetAsync("/api/v1/tcrfc/charity/impact?lang=zh"));
        Assert.Contains(impact.Metrics, m => m.Name == "【測試】合作公益團體數");
        Assert.DoesNotContain(impact.Metrics, m => m.Name != null && m.Name.Contains("不公開")); // 金額類未公開
        Assert.DoesNotContain(impact.Metrics, m => m.Value == 123456);
        Assert.True(impact.CharityCount >= 2);
        Assert.True(impact.DonationItemCount >= 2);
        Assert.Contains("【測試】示範地點：南投縣", impact.Regions);
        Assert.Contains(impact.Charities, c => c.Name == "【測試】示範公益團體甲");

        var cta = await BizTest.ReadAsync<CharityCtaDto>(await client.GetAsync("/api/v1/tcrfc/charity/cta?lang=zh"));
        Assert.StartsWith("https://", cta.DonationUrl);
        Assert.Contains("台灣足球策略發展協會", cta.DonationCta);

        // 藍鯨不設慈善單元：沒有設定，導流欄位為空、清單為空
        var bwCta = await BizTest.ReadAsync<CharityCtaDto>(await client.GetAsync("/api/v1/bw/charity/cta"));
        Assert.Null(bwCta.DonationUrl);
        Assert.Null(bwCta.DonationCta);
        var bwPrograms = await BizTest.ReadAsync<PagedResult<CharityProgramListItemDto>>(await client.GetAsync("/api/v1/bw/charity/programs"));
        Assert.Empty(bwPrograms.Items);
    }

    [Fact]
    public async Task 慈善計畫_關聯報導與贊助夥伴_更新語意_省略維持_空陣列清空()
    {
        var articleId = await BizTest.ScalarGuidAsync(
            "SELECT TOP 1 a.id FROM articles a JOIN clubs c ON c.id = a.club_id WHERE c.code = N'tcrfc' AND a.status = 'published' ORDER BY a.row_seq");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
            BizTest.Multipart(new { content = new { zh = BizTest.Zh("【測試】報導團體") } })));
        Guid? programId = null;
        try
        {
            object Body(Guid[]? articleIds) => new
            {
                charityId = org.Id, status = "published", articleIds, content = new { zh = new { name = "【測試】報導計畫" } },
            };
            var created = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs", BizTest.Multipart(Body([articleId]))));
            programId = created.Id;
            Assert.Single(created.Articles);

            var kept = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{created.Id}", BizTest.Multipart(Body(null))));
            Assert.Single(kept.Articles);

            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var pub = await BizTest.ReadAsync<CharityProgramDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/charity/programs/{created.Slug}"));
            Assert.Single(pub.Articles);

            var cleared = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{created.Id}", BizTest.Multipart(Body([]))));
            Assert.Empty(cleared.Articles);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{created.Id}",
                BizTest.Multipart(Body([Guid.NewGuid()])))).StatusCode);
        }
        finally
        {
            if (programId is Guid p) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{p}"); }
            await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{org.Id}");
        }
    }
}
