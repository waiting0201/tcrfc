using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B5 慈善與社會影響後台 API（不含圖片上傳，見 <c>AdminBusinessUploadTests</c>）：授權矩陣、公益團體／計畫／
/// 事蹟／影響力數據 CRUD 與驗證、共同列唯讀、捐款導流設定（CTA 必須點明收受者）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminCharityTests(AdminWriteApiFixture fixture)
{
    private const string Receiver = "台灣足球策略發展協會";

    [Fact]
    public async Task 授權矩陣_內容編輯可寫_商務只能看_合作球隊管理沒有慈善權限_未登入401()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/charity/organizations")).StatusCode);

        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/admin/tcrfc/charity/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await business.PostAsync("/api/v1/admin/tcrfc/charity/organizations", BizTest.Multipart(NewOrg("【測試】團體")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await business.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new { }))).StatusCode);

        // 合作球隊管理：矩陣「慈善」欄為「—」，且只授權 bw。
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/charity/organizations")).StatusCode);

        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var created = await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations", BizTest.Multipart(NewOrg("【測試】內容編輯建立的團體")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(created);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{org.Id}")).StatusCode);
    }

    [Fact]
    public async Task 公益團體計畫事蹟數據_完整流程與引用保護()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        Guid? orgId = null, programId = null, partnerId = null, sponsorId = null, metricId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await Created(client.PostAsync(
                "/api/v1/admin/tcrfc/charity/organizations", BizTest.Multipart(NewOrg("【測試】流程團體", "https://example.com/org")))));
            orgId = org.Id;
            // 官網連結格式錯誤 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(NewOrg("【測試】網址錯誤", "ftp://x")))).StatusCode);

            // 需要一個夥伴與贊助商（由商務建立）
            var partner = await BizTest.ReadAsync<Tcrfc.Api.Features.AdminPartners.AdminPartnerDetailDto>(await Created(business.PostAsync(
                "/api/v1/admin/tcrfc/partners", BizTest.Multipart(new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】慈善夥伴") } }))));
            partnerId = partner.Id;
            var sponsor = await BizTest.ReadAsync<Tcrfc.Api.Features.AdminSponsors.AdminSponsorDetailDto>(await Created(business.PostAsync(
                "/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(new { tier = "支持夥伴", content = new { zh = BizTest.Zh("【測試】慈善贊助商") } }))));
            sponsorId = sponsor.Id;

            // 內容不是合法 JSON → 400；找不到公益團體 → 400；別的俱樂部的夥伴 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(NewProgram(org.Id, content: "{not json")))).StatusCode);
            // 內容是合法 JSON 但根是純量（字串、數字）→ 一樣 400：正式環境的 json 欄位只收物件或陣列（E-111）
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(NewProgram(org.Id, content: "\"純文字\"")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(NewProgram(Guid.NewGuid())))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(NewProgram(org.Id, partnerIds: [Guid.NewGuid()])))).StatusCode);

            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await Created(client.PostAsync(
                "/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(NewProgram(org.Id, partnerIds: [partner.Id], sponsorIds: [sponsor.Id], isPinned: true, end: "2020-01-01")))));
            programId = program.Id;
            Assert.Equal("completed", program.Progress); // 結束日已過
            Assert.Single(program.Partners);
            Assert.Single(program.Sponsors);
            Assert.True(program.IsPinned);

            // 更新：省略關聯＝維持、改成進行中
            var updated = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await client.PutAsync(
                $"/api/v1/admin/tcrfc/charity/programs/{program.Id}", BizTest.Multipart(NewProgram(org.Id, end: null, status: "published"))));
            Assert.Equal("ongoing", updated.Progress);
            Assert.Equal("published", updated.Status);
            Assert.Single(updated.Partners);

            // 事蹟紀錄：三項必填——沒有圖片 → 400（發生在碰儲存體之前）
            var noImage = await client.PostAsync("/api/v1/admin/tcrfc/charity/records", BizTest.Multipart(new
            {
                charityId = org.Id, content = new { zh = new { donationContent = "足球 50 顆" } },
            }));
            Assert.Equal(HttpStatusCode.BadRequest, noImage.StatusCode);
            Assert.Contains("活動圖片", await noImage.Content.ReadAsStringAsync());

            // 影響力數據：省略 isPublic ＝ 不公開（金額類預設不公開）；可不掛計畫
            var metric = await BizTest.ReadAsync<AdminImpactMetricDto>(await Created(client.PostAsync("/api/v1/admin/tcrfc/charity/metrics", BizTest.Json(new
            {
                value = 123456, content = new { zh = new { name = "【測試】累計金額", unit = "元" } },
            }))));
            metricId = metric.Id;
            Assert.False(metric.IsPublic);
            Assert.Null(metric.CharityProgramId);
            Assert.Equal("元", metric.Zh.Unit);

            // 計畫被數據引用時不能刪
            var metric2 = await BizTest.ReadAsync<AdminImpactMetricDto>(await Created(client.PostAsync("/api/v1/admin/tcrfc/charity/metrics", BizTest.Json(new
            {
                charityProgramId = program.Id, value = 1, isPublic = true, content = new { zh = new { name = "【測試】掛計畫的數據" } },
            }))));
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/metrics/{metric2.Id}")).StatusCode);

            // 團體被計畫引用時不能刪
            var conflict = await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{org.Id}");
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Contains("計畫", await conflict.Content.ReadAsStringAsync());

            // 「合作紀錄」彙整：團體詳情看得到計畫
            var detail = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/charity/organizations/{org.Id}"));
            Assert.Single(detail.Programs);
        }
        finally
        {
            if (metricId is Guid m) { await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/metrics/{m}"); }
            if (programId is Guid p) { await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{p}"); }
            if (orgId is Guid o) { await client.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{o}"); }
            if (partnerId is Guid pa) { await business.DeleteAsync($"/api/v1/admin/tcrfc/partners/{pa}"); }
            if (sponsorId is Guid sp) { await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{sp}"); }
        }
    }

    [Fact]
    public async Task 共同列_可見但唯讀_編輯與刪除回403()
    {
        var sharedId = Guid.NewGuid();
        var slug = BizTest.Unique("shared-org");
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO charities (id, club_id, slug) VALUES (@Id, NULL, @Slug); INSERT INTO charities_i18n (charity_id, locale, name) VALUES (@Id, N'zh-Hant', N'【測試】共同公益團體');",
            ("@Id", sharedId), ("@Slug", slug));
        try
        {
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            var list = await BizTest.ReadAsync<List<AdminCharityOrgListItemDto>>(await editor.GetAsync("/api/v1/admin/tcrfc/charity/organizations"));
            Assert.Contains(list, o => o.Id == sharedId && o.IsShared);

            var put = await editor.PutAsync($"/api/v1/admin/tcrfc/charity/organizations/{sharedId}", BizTest.Multipart(NewOrg("【測試】想改共同列")));
            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
            Assert.Contains("共用", await put.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{sharedId}")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM charities WHERE id = @Id", ("@Id", sharedId));
        }
    }

    [Fact]
    public async Task 捐款導流設定_網址與收受者驗證_儲存後可讀回()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var original = await BizTest.ReadAsync<AdminCharitySettingsDto>(await client.GetAsync("/api/v1/admin/tcrfc/charity/settings"));
        try
        {
            // http（非 https）→ 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                donationUrl = "http://charity.example.com/", donationCta = new { zh = $"球迷捐款（{Receiver}收受）" },
            }))).StatusCode);
            // 有網址但文案沒有點明收受者 → 400
            var noReceiver = await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                donationUrl = "https://charity.example.com/", donationCta = new { zh = "球迷捐款" },
            }));
            Assert.Equal(HttpStatusCode.BadRequest, noReceiver.StatusCode);
            Assert.Contains(Receiver, await noReceiver.Content.ReadAsStringAsync());
            // 有網址但沒有文案 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                donationUrl = "https://charity.example.com/",
            }))).StatusCode);
            // 企業合作連結：站內路徑可、非 https 外部網址不可
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                corporateUrl = "//evil.example.com",
            }))).StatusCode);

            var ok = await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                donationUrl = "https://charity.example.com/donate",
                donationCta = new { zh = $"球迷捐款（款項由{Receiver}收受）", en = "Donate" },
                fanCta = new { zh = "球迷捐款" },
                corporateCta = new { zh = "企業合作公益專案" },
                corporateUrl = "/zh/join/",
            }));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var saved = await BizTest.ReadAsync<AdminCharitySettingsDto>(ok);
            Assert.Equal("https://charity.example.com/donate", saved.DonationUrl);
            Assert.Contains(Receiver, saved.DonationCta!.Zh);

            // 公開端點讀得到，且導流網址來自這裡的設定
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var cta = await BizTest.ReadAsync<Tcrfc.Api.Features.CharityImpact.CharityCtaDto>(await anonymous.GetAsync("/api/v1/tcrfc/charity/cta?lang=zh"));
            Assert.Equal("https://charity.example.com/donate", cta.DonationUrl);
            Assert.Contains(Receiver, cta.DonationCta);
            var en = await BizTest.ReadAsync<Tcrfc.Api.Features.CharityImpact.CharityCtaDto>(await anonymous.GetAsync("/api/v1/tcrfc/charity/cta?lang=en"));
            Assert.Equal("Donate", en.DonationCta);
        }
        finally
        {
            // 還原（種子設定）。
            await client.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(original));
        }
    }

    private static object NewOrg(string nameZh, string? website = null)
        => new { websiteUrl = website, content = new { zh = BizTest.Zh(nameZh) } };

    private static object NewProgram(Guid charityId, string? content = null, Guid[]? partnerIds = null, Guid[]? sponsorIds = null,
        bool isPinned = false, string? end = null, string status = "draft")
        => new
        {
            charityId, status, isPinned, endOn = end, partnerIds, sponsorIds,
            content = new { zh = new { name = "【測試】流程計畫", content, donationContent = "足球 50 顆" } },
        };

    private static async Task<HttpResponseMessage> Created(Task<HttpResponseMessage> task)
    {
        var response = await task;
        Assert.True(response.IsSuccessStatusCode, $"→ {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return response;
    }
}
