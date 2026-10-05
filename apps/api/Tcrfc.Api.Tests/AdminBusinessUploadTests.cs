using System.Net;
using System.Net.Http.Headers;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.AdminHonors;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminPress;
using Tcrfc.Api.Features.AdminProposals;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// E1／E2／E3／B5／B6／C5 含檔案與圖片的端點，對**真實 Azurite** 驗證（不 mock 物件儲存）：Logo 換圖與刪除、
/// 媒體專區檔案（PDF／圖片）、公開下載累計次數、提案私有檔的表單關卡與限時連結、事蹟紀錄圖集。
/// 「寫入失敗補償」的通則已由 <c>AdminNewsCoverUploadTests</c> 釘住，這裡只驗證各模組確實接上同一套 <c>UploadTransaction</c>。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminBusinessUploadTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static async Task<int> CountAsync(BlobContainerClient container, string prefix)
    {
        var count = 0;
        await foreach (var _ in container.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    // ═════════════════════════ E1 夥伴 Logo ═════════════════════════

    [AzuriteFact]
    public async Task 夥伴Logo_上傳_換圖刪舊_移除_刪除夥伴一併刪物件()
    {
        using var client = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var created = await client.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(
            new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】Logo 夥伴") } },
            ("logoDark", TestImages.SmallPng(), "dark.png", "image/png")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var partner = await BizTest.ReadAsync<AdminPartnerDetailDto>(created);
        var prefix = $"tcrfc/partners/{partner.Id}/";
        try
        {
            Assert.NotNull(partner.LogoDarkKey);
            Assert.NotNull(partner.LogoDarkUrl);
            Assert.Null(partner.LogoLightKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix)); // 主檔＋四個衍生檔

            var payload = new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】Logo 夥伴") } };
            // 換圖：新圖寫入後舊物件被刪，仍是 5 個（新的一組），且鍵不同
            var replaced = await BizTest.ReadAsync<AdminPartnerDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}",
                BizTest.Multipart(payload, ("logoDark", TestImages.SmallPng(), "dark2.png", "image/png"))));
            Assert.NotEqual(partner.LogoDarkKey, replaced.LogoDarkKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));

            // 同時上傳新檔與移除 → 400，物件數不變
            var both = await client.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}", BizTest.Multipart(
                new { partnerType = "策略夥伴", removeLogoDark = true, content = new { zh = BizTest.Zh("【測試】Logo 夥伴") } },
                ("logoDark", TestImages.SmallPng(), "x.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));

            // 假圖片 → 400，不留物件
            var fake = await client.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}",
                BizTest.Multipart(payload, ("logoLight", TestImages.FakeImageBytes(), "fake.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));

            // 加上淺色底 Logo，再移除深色底
            await client.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}", BizTest.Multipart(payload, ("logoLight", TestImages.SmallWebp(), "l.webp", "image/webp")));
            Assert.Equal(10, await CountAsync(fixture.InspectorContainer, prefix));
            var removed = await BizTest.ReadAsync<AdminPartnerDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}",
                BizTest.Multipart(new { partnerType = "策略夥伴", removeLogoDark = true, content = new { zh = BizTest.Zh("【測試】Logo 夥伴") } })));
            Assert.Null(removed.LogoDarkKey);
            Assert.NotNull(removed.LogoLightKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}");
        }

        Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
    }

    // ═════════════════════════ B6 媒體專區 ═════════════════════════

    [AzuriteFact]
    public async Task 媒體專區_PDF與高解析圖_公開下載累計次數_格式與類別規則()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        Guid? pdfId = null, imageId = null;
        try
        {
            // 假 PDF（純文字）→ 400，不建立資料
            var fake = await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
                Press("press_release", "published"), ("file", System.Text.Encoding.UTF8.GetBytes("not a pdf"), "a.pdf", "application/pdf")));
            Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
            Assert.Contains("PDF", await fake.Content.ReadAsStringAsync());

            var created = await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
                Press("press_release", "published"), ("file", BizTest.Pdf(), "release.pdf", "application/pdf")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var pdf = await BizTest.ReadAsync<AdminPressDetailDto>(created);
            pdfId = pdf.Id;
            Assert.EndsWith(".pdf", pdf.FileKey);
            Assert.NotNull(pdf.PublishedOn); // 發布且沒填日期：自動填今天
            Assert.Equal(1, await CountAsync(fixture.InspectorDocumentsContainer, $"tcrfc/press/{pdf.Id}/"));

            // 高解析圖：走圖片管線（主檔 .webp＋衍生檔），不能另外上傳封面
            var hiresCover = await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
                Press("hires_image", "draft"), ("file", TestImages.SmallPng(), "a.png", "image/png"), ("cover", TestImages.SmallPng(), "c.png", "image/png")));
            Assert.Equal(HttpStatusCode.BadRequest, hiresCover.StatusCode);
            var hires = await BizTest.ReadAsync<AdminPressDetailDto>(await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(
                Press("hires_image", "published"), ("file", TestImages.SmallPng(), "a.png", "image/png"))));
            imageId = hires.Id;
            Assert.EndsWith(".webp", hires.FileKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, $"tcrfc/press/{hires.Id}/"));

            // 文件類改成高解析圖而沒有重新上傳檔案 → 400
            var switchType = await pr.PutAsync($"/api/v1/admin/tcrfc/press-resources/{pdf.Id}", BizTest.Multipart(Press("hires_image", "published")));
            Assert.Equal(HttpStatusCode.BadRequest, switchType.StatusCode);

            // 批次改類別：文件類↔圖片類不相容，被略過並說明
            var batch = await BizTest.ReadAsync<BatchOperationResultDto>(await pr.PostAsync("/api/v1/admin/tcrfc/press-resources/batch/type",
                BizTest.Json(new { ids = new[] { pdf.Id }, resourceType = "hires_image" })));
            Assert.Equal(0, batch.UpdatedCount);
            var ok = await BizTest.ReadAsync<BatchOperationResultDto>(await pr.PostAsync("/api/v1/admin/tcrfc/press-resources/batch/type",
                BizTest.Json(new { ids = new[] { pdf.Id }, resourceType = "brand_kit" })));
            Assert.Equal(1, ok.UpdatedCount);

            // 公開：已發布的兩筆都在；下載連結轉址並累計次數
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var list = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Press.PressResourceDto>>(await anonymous.GetAsync("/api/v1/tcrfc/press?lang=zh&pageSize=100"));
            var pubPdf = Assert.Single(list.Items, r => r.Id == pdf.Id);
            Assert.Equal("brand_kit", pubPdf.ResourceType);
            Assert.Equal($"/api/v1/tcrfc/press/{pdf.Slug}/download", pubPdf.DownloadPath);
            Assert.Equal(".pdf", pubPdf.FileExtension);
            Assert.NotNull(list.Items.Single(r => r.Id == hires.Id).CoverUrl);
            var typed = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Press.PressResourceDto>>(await anonymous.GetAsync("/api/v1/tcrfc/press?type=hires_image&pageSize=100"));
            Assert.All(typed.Items, r => Assert.Equal("hires_image", r.ResourceType));

            using var noRedirect = fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var download = await noRedirect.GetAsync(pubPdf.DownloadPath);
            Assert.Equal(HttpStatusCode.Redirect, download.StatusCode);
            Assert.Contains("/documents/", download.Headers.Location!.ToString());
            await noRedirect.GetAsync(pubPdf.DownloadPath);
            var detail = await BizTest.ReadAsync<AdminPressDetailDto>(await pr.GetAsync($"/api/v1/admin/tcrfc/press-resources/{pdf.Id}"));
            Assert.Equal(2, detail.DownloadCount);

            // 改成隱藏後公開端點看不到、下載 404
            Assert.Equal(1, (await BizTest.ReadAsync<BatchOperationResultDto>(await pr.PostAsync("/api/v1/admin/tcrfc/press-resources/batch/hide",
                BizTest.Json(new { ids = new[] { pdf.Id } })))).UpdatedCount);
            Assert.Equal(HttpStatusCode.NotFound, (await noRedirect.GetAsync(pubPdf.DownloadPath)).StatusCode);
        }
        finally
        {
            if (pdfId is Guid p) { await pr.DeleteAsync($"/api/v1/admin/tcrfc/press-resources/{p}"); }
            if (imageId is Guid i) { await pr.DeleteAsync($"/api/v1/admin/tcrfc/press-resources/{i}"); }
        }

        Assert.Equal(0, await CountAsync(fixture.InspectorDocumentsContainer, $"tcrfc/press/{pdfId}/"));
        Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/press/{imageId}/"));
    }

    // ═════════════════════════ E3 提案：表單關卡與限時連結 ═════════════════════════

    [AzuriteFact]
    public async Task 提案下載_檔案放私有容器_填表單才給限時連結_Lead帶提案_過期或竄改失效()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var proposal = await BizTest.ReadAsync<AdminProposalDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/proposals",
            BizTest.Json(new { title = "【測試】下載提案", versionNo = 1, status = "draft" })));
        try
        {
            // 圖片格式不是提案檔案允許的格式 → 400
            var png = await business.PostAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}/files",
                BizTest.Multipart(new { locale = "zh" }, ("file", TestImages.SmallPng(), "a.pdf", "application/pdf")));
            Assert.Equal(HttpStatusCode.BadRequest, png.StatusCode);

            var withFile = await BizTest.ReadAsync<AdminProposalDetailDto>(await business.PostAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}/files",
                BizTest.Multipart(new { locale = "zh" }, ("file", BizTest.Pdf(), "deck.pdf", "application/pdf"))));
            Assert.Single(withFile.Files);
            Assert.Equal(1, await CountAsync(fixture.InspectorProposalsContainer, $"tcrfc/proposals/{proposal.Id}/"));
            Assert.Equal(0, await CountAsync(fixture.InspectorDocumentsContainer, $"tcrfc/proposals/{proposal.Id}/")); // 不在公開容器

            // 同語系同版本重複上傳 → 409
            Assert.Equal(HttpStatusCode.Conflict, (await business.PostAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}/files",
                BizTest.Multipart(new { locale = "zh" }, ("file", BizTest.Pdf(), "deck2.pdf", "application/pdf")))).StatusCode);

            // 草稿：公開清單看不到，下載要求 404
            var listBefore = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Proposals.PublicProposalDto>>(await anonymous.GetAsync("/api/v1/tcrfc/proposals"));
            Assert.DoesNotContain(listBefore, p => p.Id == proposal.Id);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync($"/api/v1/tcrfc/proposals/{proposal.Id}/download-requests", BizTest.Json(ValidRequest()))).StatusCode);

            await business.PutAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}", BizTest.Json(new { title = "【測試】下載提案", versionNo = 1, status = "published" }));
            var listAfter = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Proposals.PublicProposalDto>>(await anonymous.GetAsync("/api/v1/tcrfc/proposals"));
            var pub = Assert.Single(listAfter, p => p.Id == proposal.Id);
            Assert.Equal(["zh"], pub.Locales);

            var url = $"/api/v1/tcrfc/proposals/{proposal.Id}/download-requests";
            // 沒勾同意／缺欄位／Email 格式錯誤 → 400（日常中文）
            var noConsent = await anonymous.PostAsync(url, BizTest.Json(new { company = "公司", name = "王", email = "a@example.com", consent = false }));
            Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
            Assert.Contains("同意", await noConsent.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync(url, BizTest.Json(new { company = "", name = "王", email = "a@example.com", consent = true }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync(url, BizTest.Json(new { company = "公司", name = "王", email = "壞信箱", consent = true }))).StatusCode);

            // 誘捕欄位：安靜回成功，不給連結、不建 Lead
            var bot = await BizTest.ReadAsync<Tcrfc.Api.Features.Proposals.ProposalDownloadResultDto>(await anonymous.PostAsync(url,
                BizTest.Json(new { company = "Bot", name = "Bot", email = "bot@example.com", consent = true, website = "http://spam" })));
            Assert.Null(bot.DownloadPath);

            // 正常流程
            var unique = BizTest.Unique("lead");
            var ok = await BizTest.ReadAsync<Tcrfc.Api.Features.Proposals.ProposalDownloadResultDto>(await anonymous.PostAsync(url,
                BizTest.Json(new { company = $"【測試】{unique}", name = "測試者", email = $"{unique}@example.com", consent = true, lang = "zh", sourcePath = "/zh/partners/", utmSource = "test" })));
            Assert.StartsWith("/api/v1/tcrfc/proposals/downloads/", ok.DownloadPath);
            Assert.NotNull(ok.ExpiresAt);

            var file = await anonymous.GetAsync(ok.DownloadPath!);
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
            Assert.Equal(BizTest.Pdf(), await file.Content.ReadAsByteArrayAsync());
            Assert.Contains("no-store", file.Headers.CacheControl!.ToString());

            // 換俱樂部路徑 → 404；權杖被竄改 → 404
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ok.DownloadPath!.Replace("/tcrfc/", "/bw/"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ok.DownloadPath! + "x")).StatusCode);

            // Lead 出現在名單、帶著下載的提案；誘捕欄位那筆沒有建立
            var leads = await BizTest.ReadAsync<PagedResult<AdminLeadListItemDto>>(await business.GetAsync(
                $"/api/v1/admin/tcrfc/proposal-leads?proposalId={proposal.Id}&pageSize=100"));
            var lead = Assert.Single(leads.Items);
            Assert.Equal($"【測試】{unique}", lead.Company);
            Assert.Equal(proposal.Id, lead.ProposalId);
            Assert.Equal("新進", lead.Status);
            Assert.Equal("/zh/partners/", lead.SourcePath);

            // 提案改回草稿後：既有連結失效
            await business.PutAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}", BizTest.Json(new { title = "【測試】下載提案", versionNo = 1, status = "draft" }));
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ok.DownloadPath!)).StatusCode);

            // 後台預覽下載（不受表單關卡限制）
            var adminFile = await business.GetAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}/files/{withFile.Files[0].Id}/download");
            Assert.Equal(HttpStatusCode.OK, adminFile.StatusCode);
        }
        finally
        {
            await business.DeleteAsync($"/api/v1/admin/tcrfc/proposals/{proposal.Id}");
            // 刪提案後 Lead 保留（proposal_id 設為空），清掉測試 Lead。
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM enquiry_answers WHERE enquiry_id IN (SELECT id FROM enquiries WHERE utm_source = N'test' AND proposal_id IS NULL AND source_path = N'/zh/partners/' AND created_at > DATEADD(minute, -10, SYSUTCDATETIME())); " +
                "DELETE FROM enquiries WHERE utm_source = N'test' AND proposal_id IS NULL AND source_path = N'/zh/partners/' AND created_at > DATEADD(minute, -10, SYSUTCDATETIME());");
        }

        Assert.Equal(0, await CountAsync(fixture.InspectorProposalsContainer, $"tcrfc/proposals/{proposal.Id}/"));
    }

    // ═════════════════════════ B5 事蹟紀錄圖集與公開端點 ═════════════════════════

    [AzuriteFact]
    public async Task 事蹟紀錄_必填活動圖片_圖集加刪排序_公開端點三項核心資料()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Guid? orgId = null, recordId = null, programId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = new { zh = BizTest.Zh("【測試】圖集團體") } }, ("logo", TestImages.SmallPng(), "logo.png", "image/png"))));
            orgId = org.Id;
            Assert.NotNull(org.LogoUrl);

            var created = await editor.PostAsync("/api/v1/admin/tcrfc/charity/records", BizTest.Multipart(new
            {
                charityId = org.Id, happenedOn = "2026-05-01", isPinned = true,
                content = new { zh = new { donationContent = "【測試】足球 50 顆", location = "【測試】南投", briefDescription = "簡述" }, en = new { donationContent = "50 footballs" } },
            }, ("image", TestImages.SmallPng(), "a.png", "image/png")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var record = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(created);
            recordId = record.Id;
            Assert.NotNull(record.ImageKey);
            Assert.Equal(500, record.ImageWidth);

            // 加兩張到圖集、刪一張、排序
            var g1 = await AddGalleryAsync(editor, $"/api/v1/admin/tcrfc/charity/records/{record.Id}/images");
            var g2 = await AddGalleryAsync(editor, $"/api/v1/admin/tcrfc/charity/records/{record.Id}/images");
            var withGallery = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.GetAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}"));
            Assert.Equal(2, withGallery.Images.Count);
            var second = withGallery.Images[1].Id;
            var reordered = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.PutAsync(
                $"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/order", BizTest.Json(new { ids = new[] { second } })));
            Assert.Equal(second, reordered.Images[0].Id);
            Assert.Equal(HttpStatusCode.NoContent, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/{second}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/order",
                BizTest.Json(new { ids = new[] { Guid.NewGuid() } }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/{Guid.NewGuid()}")).StatusCode);
            _ = g1; _ = g2;

            // 主圖不可移除；更換主圖則舊的被刪
            var prefix = $"tcrfc/impact-records/{record.Id}/";
            var before = await CountAsync(fixture.InspectorContainer, prefix);
            var replaced = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.PutAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}",
                BizTest.Multipart(new { charityId = org.Id, happenedOn = "2026-05-01", content = new { zh = new { donationContent = "【測試】足球 50 顆" }, en = new { donationContent = "50 footballs" } } }, ("image", TestImages.SmallWebp(), "b.webp", "image/webp"))));
            Assert.NotEqual(record.ImageKey, replaced.ImageKey);
            Assert.Equal(before, await CountAsync(fixture.InspectorContainer, prefix));

            // 計畫：封面＋圖集，公開端點看得到（已發布）
            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs", BizTest.Multipart(new
            {
                charityId = org.Id, status = "published", content = new { zh = new { name = "【測試】公開計畫", donationContent = "足球" } },
            }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            programId = program.Id;
            await AddGalleryAsync(editor, $"/api/v1/admin/tcrfc/charity/programs/{program.Id}/images");

            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var pubProgram = await BizTest.ReadAsync<Tcrfc.Api.Features.CharityImpact.CharityProgramDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/charity/programs/{program.Slug}?lang=zh"));
            Assert.Equal("【測試】公開計畫", pubProgram.Name);
            Assert.NotNull(pubProgram.CoverUrl);
            Assert.Single(pubProgram.Images);
            Assert.Equal("【測試】圖集團體", pubProgram.Charity!.Name);
            Assert.NotNull(pubProgram.Charity.LogoUrl);

            var records = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.CharityImpact.ImpactRecordDto>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/records?year=2026&lang=zh&pageSize=50"));
            var pub = Assert.Single(records.Items, r => r.Id == record.Id);
            Assert.Equal("【測試】圖集團體", pub.CharityName);          // ① 公益團體名稱
            Assert.Equal("【測試】足球 50 顆", pub.DonationContent);   // ② 捐助內容
            Assert.NotNull(pub.ImageUrl);                              // ③ 活動圖片
            var years = await BizTest.ReadAsync<List<int>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/records/years"));
            Assert.Contains(2026, years);
            var en = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.CharityImpact.ImpactRecordDto>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/records?year=2026&lang=en&pageSize=50"));
            Assert.Equal("50 footballs", en.Items.Single(r => r.Id == record.Id).DonationContent);
        }
        finally
        {
            if (recordId is Guid r) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/records/{r}"); }
            if (programId is Guid p) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{p}"); }
            if (orgId is Guid o) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{o}"); }
        }

        Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/impact-records/{recordId}/"));
        Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/charity-programs/{programId}/"));
    }

    // ═════════════════════════ E2 贊助活動圖集／C5 里程碑圖片 ═════════════════════════

    [AzuriteFact]
    public async Task 贊助活動圖集與里程碑圖片_上傳與刪除()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var sponsor = await BizTest.ReadAsync<AdminSponsorDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(
            new { tier = "支持夥伴", content = new { zh = BizTest.Zh("【測試】圖集贊助商") } },
            ("logoLight", TestImages.SmallPng(), "l.png", "image/png"))));
        var milestone = await BizTest.ReadAsync<AdminMilestoneDto>(await manager.PostAsync("/api/v1/admin/tcrfc/milestones", BizTest.Multipart(
            new { happenedOn = "2031-06-01", content = new { zh = new { title = "【測試】有圖里程碑", imageAlt = "圖" } } },
            ("image", TestImages.SmallPng(), "m.png", "image/png"))));
        try
        {
            Assert.NotNull(milestone.ImageUrl);
            Assert.Equal(500, milestone.ImageWidth);

            var activation = await BizTest.ReadAsync<AdminActivationDto>(await business.PostAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations",
                BizTest.Json(new { content = new { zh = new { title = "【測試】有圖活動" } } })));
            var withImage = await AddGalleryAsync(business, $"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{activation.Id}/images");
            Assert.Contains("\"images\":[{", withImage);
            var prefix = $"tcrfc/sponsor-activations/{activation.Id}/";
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));

            // 公開贊助商清單帶活動與圖集，且不含聯絡窗口
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var raw = await (await anonymous.GetAsync("/api/v1/tcrfc/sponsors?lang=zh")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("contactEmail", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("contractEndOn", raw, StringComparison.OrdinalIgnoreCase);
            var sponsors = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Sponsors.SponsorDto>>(await anonymous.GetAsync("/api/v1/tcrfc/sponsors?lang=zh"));
            var pub = Assert.Single(sponsors, s => s.Id == sponsor.Id);
            Assert.NotNull(pub.LogoLightUrl);
            Assert.Single(pub.Activations);
            Assert.Single(pub.Activations[0].Images);

            // 刪除贊助商：活動圖集與 Logo 物件一併刪
            Assert.Equal(HttpStatusCode.NoContent, (await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}")).StatusCode);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/sponsors/{sponsor.Id}/"));
        }
        finally
        {
            await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}");
            await manager.DeleteAsync($"/api/v1/admin/tcrfc/milestones/{milestone.Id}");
        }

        Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/milestones/{milestone.Id}/"));
    }

    private static object Press(string type, string status) => new { resourceType = type, status, content = new { zh = new { title = "【測試】上傳的媒體資源" } } };

    private static object ValidRequest() => new { company = "公司", name = "王", email = "a@example.com", consent = true };

    private static async Task<string> AddGalleryAsync(HttpClient client, string url)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TestImages.SmallPng());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "g.png");
        var response = await client.PostAsync(url, form);
        Assert.True(response.IsSuccessStatusCode, $"{url} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadAsStringAsync();
    }
}
