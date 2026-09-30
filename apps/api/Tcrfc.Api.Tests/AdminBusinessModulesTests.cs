using System.Net;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminProposals;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// E1 夥伴／E2 贊助商與方案與活動／E3 提案與 Lead 後台 API（不含檔案上傳，見 <c>AdminBusinessUploadTests</c>）。
/// 打真正的 HTTP 管線與真正的 <c>tcrfc_club</c>；每個測試建立的資料在 finally 內刪除。
/// 角色：business.sponsorship＝商務／贊助（矩陣「商業／贊助」✔全）、content.editor／viewer＝唯讀、
/// pr.media＝唯讀、partner.club＝合作球隊管理（僅 bw，無 tcrfc 授權）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminBusinessModulesTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════ E1 夥伴 ═════════════════════════

    [Fact]
    public async Task 夥伴_未登入_401_跨俱樂部_403()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/partners")).StatusCode);

        // content.editor 只授權 tcrfc，沒有 bw。
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/bw/partners")).StatusCode);
    }

    [Fact]
    public async Task 夥伴_唯讀角色可看不可寫_商務可寫()
    {
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/partners")).StatusCode);
        var denied = await viewer.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "唯讀不能建立")));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var created = await business.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "【測試】商務建立")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await BizTest.ReadAsync<AdminPartnerDetailDto>(created);
        try
        {
            Assert.Equal("【測試】商務建立", dto.Zh.Name);
            Assert.StartsWith("partner-", dto.Slug); // 沒有英文名稱：自動產生的網址名稱
        }
        finally
        {
            await business.DeleteAsync($"/api/v1/admin/tcrfc/partners/{dto.Id}");
        }
    }

    [Fact]
    public async Task 夥伴_建立更新排序刪除_驗證與重複網址名稱()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var slug = BizTest.Unique("t-partner");
        var ids = new List<Guid>();
        try
        {
            // 網址名稱格式錯誤 → 400
            var badSlug = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "格式錯誤", slug: "Bad Slug")));
            Assert.Equal(HttpStatusCode.BadRequest, badSlug.StatusCode);
            // 官網連結不是 http(s) → 400
            var badUrl = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "網址錯誤", website: "javascript:alert(1)")));
            Assert.Equal(HttpStatusCode.BadRequest, badUrl.StatusCode);
            // 合作期間結束早於開始 → 400
            var badRange = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "期間錯誤", start: "2026-05-01", end: "2026-01-01")));
            Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
            var badRangeText = await badRange.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Exception", badRangeText);

            var a = await CreatePartnerAsync(client, "策略夥伴", "【測試】夥伴甲", slug);
            var b = await CreatePartnerAsync(client, "品牌夥伴", "【測試】夥伴乙", null);
            ids.AddRange([a.Id, b.Id]);

            // 網址名稱重複 → 409
            var dup = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(NewPartner("策略夥伴", "重複", slug: slug)));
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

            // 更新：含英文版與合作內容
            var update = await client.PutAsync($"/api/v1/admin/tcrfc/partners/{a.Id}", BizTest.Multipart(new
            {
                slug, partnerType = "國際夥伴", country = "日本", websiteUrl = "https://example.com/a", showOnHome = true, sortOrder = 5,
                content = new { zh = BizTest.Zh("【測試】夥伴甲改", "合作內容"), en = BizTest.Zh("Partner A", "Content") },
            }));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = await BizTest.ReadAsync<AdminPartnerDetailDto>(update);
            Assert.Equal("Partner A", updated.En!.Name);
            Assert.Equal("合作內容", updated.Zh.Content);
            Assert.True(updated.ShowOnHome);

            // 類型清單含標準類型與實際用過的類型
            var types = await BizTest.ReadAsync<AdminPartnerTypesDto>(await client.GetAsync("/api/v1/admin/tcrfc/partners/types"));
            Assert.Contains("策略夥伴", types.StandardTypes);
            Assert.Contains("國際夥伴", types.UsedTypes);

            // 排序：b 排在 a 前面
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/v1/admin/tcrfc/partners/order", BizTest.Json(new { ids = new[] { b.Id, a.Id } }))).StatusCode);
            var list = await BizTest.ReadAsync<List<AdminPartnerListItemDto>>(await client.GetAsync("/api/v1/admin/tcrfc/partners?keyword=%E3%80%90%E6%B8%AC%E8%A9%A6%E3%80%91%E5%A4%A5%E4%BC%B4"));
            Assert.True(list.FindIndex(x => x.Id == b.Id) < list.FindIndex(x => x.Id == a.Id));

            // 排序清單含不存在的 id → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/admin/tcrfc/partners/order", BizTest.Json(new { ids = new[] { Guid.NewGuid() } }))).StatusCode);
        }
        finally
        {
            foreach (var id in ids)
            {
                Assert.True((await client.DeleteAsync($"/api/v1/admin/tcrfc/partners/{id}")).StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound);
            }
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/partners/{ids[0]}")).StatusCode);
    }

    [Fact]
    public async Task 夥伴_跨俱樂部的id一律404()
    {
        using var superAdmin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var partner = await CreatePartnerAsync(superAdmin, "策略夥伴", "【測試】藍鯨夥伴", null, club: "bw");
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await superAdmin.GetAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await superAdmin.GetAsync($"/api/v1/admin/bw/partners/{partner.Id}")).StatusCode);
        }
        finally
        {
            await superAdmin.DeleteAsync($"/api/v1/admin/bw/partners/{partner.Id}");
        }
    }

    // ═════════════════════════ E2 贊助商／方案／活動 ═════════════════════════

    [Fact]
    public async Task 贊助商_等級驗證_到期提醒狀態_方案與活動()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        Guid? sponsorId = null, packageId = null;
        try
        {
            var package = await BizTest.ReadAsync<AdminSponsorPackageDetailDto>(await PostOk(client, "/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
            {
                status = "published", priceMin = 100, priceMax = 200, isPricePublic = false, sortOrder = 99,
                content = new { zh = new { name = "【測試】方案", benefitList = "一\n二", audience = "企業" } },
            })));
            packageId = package.Id;

            // 等級不合法 → 400
            var badTier = await client.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(NewSponsor("超級贊助", "2026-01-01", "2027-01-01")));
            Assert.Equal(HttpStatusCode.BadRequest, badTier.StatusCode);
            Assert.Contains("主贊助", await badTier.Content.ReadAsStringAsync());
            // Email 格式錯誤 → 400
            var badMail = await client.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(NewSponsor("主贊助", "2026-01-01", "2027-01-01", email: "not-an-email")));
            Assert.Equal(HttpStatusCode.BadRequest, badMail.StatusCode);

            // 已到提醒日、合約尚未結束 → alert
            var created = await client.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(NewSponsor(
                "官方贊助", "2025-01-01", "2099-12-31", alertOn: "2026-01-01", packageIds: [package.Id])));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var sponsor = await BizTest.ReadAsync<AdminSponsorDetailDto>(created);
            sponsorId = sponsor.Id;
            Assert.Equal("alert", sponsor.ContractStatus);
            Assert.Single(sponsor.Packages);

            var alerts = await BizTest.ReadAsync<List<AdminSponsorListItemDto>>(await client.GetAsync("/api/v1/admin/tcrfc/sponsors?contractStatus=alert"));
            Assert.Contains(alerts, s => s.Id == sponsor.Id);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/admin/tcrfc/sponsors?contractStatus=foo")).StatusCode);

            // 更新：PackageIds 省略＝維持；空陣列＝清空。合約已結束 → expired
            var keep = await client.PutAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}", BizTest.Multipart(NewSponsor("官方贊助", "2020-01-01", "2020-12-31")));
            var kept = await BizTest.ReadAsync<AdminSponsorDetailDto>(keep);
            Assert.Equal("expired", kept.ContractStatus);
            Assert.Single(kept.Packages);
            var cleared = await client.PutAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}", BizTest.Multipart(NewSponsor("官方贊助", "2020-01-01", "2020-12-31", packageIds: [])));
            Assert.Empty((await BizTest.ReadAsync<AdminSponsorDetailDto>(cleared)).Packages);

            // 贊助活動：建立／列表／更新／刪除
            var act = await BizTest.ReadAsync<AdminActivationDto>(await PostOk(client, $"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations", BizTest.Json(new
            {
                happenedOn = "2026-09-13", sortOrder = 0, content = new { zh = new { title = "【測試】活動", resultSummary = "成效" } },
            })));
            var acts = await BizTest.ReadAsync<List<AdminActivationDto>>(await client.GetAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations"));
            Assert.Single(acts);
            var upd = await client.PutAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{act.Id}", BizTest.Json(new
            {
                happenedOn = "2026-09-14", content = new { zh = new { title = "【測試】活動改" }, en = new { title = "Activation" } },
            }));
            Assert.Equal("Activation", (await BizTest.ReadAsync<AdminActivationDto>(upd)).En!.Title);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{act.Id}")).StatusCode);
            // 其他贊助商底下的活動路徑：找不到
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/sponsors/{Guid.NewGuid()}/activations")).StatusCode);
        }
        finally
        {
            if (sponsorId is Guid s) { await client.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{s}"); }
            if (packageId is Guid p) { await client.DeleteAsync($"/api/v1/admin/tcrfc/sponsor-packages/{p}"); }
        }
    }

    [Fact]
    public async Task 贊助方案_價格區間驗證_狀態驗證()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var badRange = await client.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "draft", priceMin = 500, priceMax = 100, content = new { zh = new { name = "【測試】價格錯誤" } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
        var badStatus = await client.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "scheduled", content = new { zh = new { name = "【測試】狀態錯誤" } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task 贊助商_唯讀角色不能建立_公關媒體可看()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await pr.GetAsync("/api/v1/admin/tcrfc/sponsors")).StatusCode);
        var denied = await pr.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(NewSponsor("主贊助", "2026-01-01", "2027-01-01")));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // ═════════════════════════ E3 提案與 Lead ═════════════════════════

    [Fact]
    public async Task 提案_沒有檔案不能發布_更新_刪除()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        // 建立時直接發布 → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/proposals",
            BizTest.Json(new { title = "【測試】提案", status = "published" }))).StatusCode);

        var created = await BizTest.ReadAsync<AdminProposalDetailDto>(await PostOk(client, "/api/v1/admin/tcrfc/proposals",
            BizTest.Json(new { title = "【測試】提案 A", versionNo = 1, status = "draft" })));
        try
        {
            // 沒有檔案 → 不能改成發布
            var publish = await client.PutAsync($"/api/v1/admin/tcrfc/proposals/{created.Id}",
                BizTest.Json(new { title = "【測試】提案 A", versionNo = 1, status = "published" }));
            Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
            Assert.Contains("檔案", await publish.Content.ReadAsStringAsync());

            var upd = await client.PutAsync($"/api/v1/admin/tcrfc/proposals/{created.Id}",
                BizTest.Json(new { title = "【測試】提案 A 改", versionNo = 2, status = "draft" }));
            Assert.Equal(2, (await BizTest.ReadAsync<AdminProposalDetailDto>(upd)).VersionNo);

            // 檔案上傳的語言值不合法 → 400（先驗證再上傳）
            var badLocale = await client.PostAsync($"/api/v1/admin/tcrfc/proposals/{created.Id}/files",
                BizTest.Multipart(new { locale = "fr" }, ("file", BizTest.Pdf(), "a.pdf", "application/pdf")));
            Assert.Equal(HttpStatusCode.BadRequest, badLocale.StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/proposals/{created.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Lead_列表_篩選_更新狀態_指派驗證_匯出需要受限碼()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var leads = await BizTest.ReadAsync<PagedResult<AdminLeadListItemDto>>(await business.GetAsync("/api/v1/admin/tcrfc/proposal-leads?pageSize=100"));
        var seeded = leads.Items.Where(l => l.Company != null && l.Company.StartsWith("【測試】示範公司")).ToList();
        Assert.True(seeded.Count >= 3, "種子應有三筆 Lead");
        Assert.All(seeded, l => Assert.EndsWith("@example.com", l.Email));

        var target = seeded[0];
        var original = await BizTest.ReadAsync<AdminLeadDetailDto>(await business.GetAsync($"/api/v1/admin/tcrfc/proposal-leads/{target.Id}"));
        try
        {
            // 狀態值不合法 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await business.PutAsync($"/api/v1/admin/tcrfc/proposal-leads/{target.Id}",
                BizTest.Json(new { status = "亂填" }))).StatusCode);
            // 指派給沒有 Lead 權限的帳號（檢視者）→ 400
            using var superAdmin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            var users = await BizTest.ReadAsync<List<AdminLeadAssigneeDto>>(await business.GetAsync("/api/v1/admin/tcrfc/proposal-leads/assignable-users"));
            Assert.NotEmpty(users);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.PutAsync($"/api/v1/admin/tcrfc/proposal-leads/{target.Id}",
                BizTest.Json(new { status = "處理中", assigneeAdminUserId = Guid.NewGuid() }))).StatusCode);

            var ok = await business.PutAsync($"/api/v1/admin/tcrfc/proposal-leads/{target.Id}",
                BizTest.Json(new { status = "已結案", assigneeAdminUserId = users[0].Id, internalNote = "已聯絡", tags = "熱門" }));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var detail = await BizTest.ReadAsync<AdminLeadDetailDto>(ok);
            Assert.Equal("已結案", detail.Status);
            Assert.Equal("熱門", detail.Tags);

            var filtered = await BizTest.ReadAsync<PagedResult<AdminLeadListItemDto>>(await business.GetAsync("/api/v1/admin/tcrfc/proposal-leads?status=%E5%B7%B2%E7%B5%90%E6%A1%88&pageSize=100"));
            Assert.Contains(filtered.Items, l => l.Id == target.Id);

            // 匯出：商務有受限碼、內容編輯沒有 lead 權限
            var csv = await business.GetAsync("/api/v1/admin/tcrfc/proposal-leads/export");
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            var csvText = await csv.Content.ReadAsStringAsync();
            Assert.Contains("公司", csvText);
            Assert.Contains("example.com", csvText);
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/tcrfc/proposal-leads")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/tcrfc/proposal-leads/export")).StatusCode);
        }
        finally
        {
            await business.PutAsync($"/api/v1/admin/tcrfc/proposal-leads/{target.Id}", BizTest.Json(new
            {
                status = original.Status ?? "新進", assigneeAdminUserId = original.AssigneeAdminUserId, internalNote = original.InternalNote, tags = original.Tags,
            }));
        }
    }

    // ═════════════════════════ helpers ═════════════════════════

    private static object NewPartner(string type, string nameZh, string? slug = null, string? website = null, string? start = null, string? end = null)
        => new { slug, partnerType = type, websiteUrl = website, startOn = start, endOn = end, content = new { zh = BizTest.Zh(nameZh) } };

    private static async Task<AdminPartnerDetailDto> CreatePartnerAsync(HttpClient client, string type, string nameZh, string? slug, string club = "tcrfc")
    {
        var response = await client.PostAsync($"/api/v1/admin/{club}/partners", BizTest.Multipart(NewPartner(type, nameZh, slug)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await BizTest.ReadAsync<AdminPartnerDetailDto>(response);
    }

    private static object NewSponsor(string tier, string start, string end, string? email = null, string? alertOn = null, Guid[]? packageIds = null)
        => new
        {
            tier, contractStartOn = start, contractEndOn = end, expiryAlertOn = alertOn, contactEmail = email, packageIds,
            content = new { zh = BizTest.Zh("【測試】贊助商", "內容") },
        };

    private static async Task<HttpResponseMessage> PostOk(HttpClient client, string url, HttpContent content)
    {
        var response = await client.PostAsync(url, content);
        Assert.True(response.IsSuccessStatusCode, $"{url} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return response;
    }
}
