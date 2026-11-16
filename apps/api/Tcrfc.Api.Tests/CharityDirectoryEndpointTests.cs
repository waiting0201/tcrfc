using System.Net;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 主站「慈善清單」公開端點（K-2，2026-11-13）：慈善平台後台「從官網更新清單」讀取的唯讀清單。
/// 只含<b>已公開</b>的公益團體（至少有一個已發布計畫）與已發布的慈善計畫，輸出識別與中英名稱，不含任何聯絡窗口、金額或個資。
/// 測資一律建立後在 <c>finally</c> 刪除自己的列（【測試】前綴）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class CharityDirectoryEndpointTests(AdminWriteApiFixture fixture)
{
    private const string Path = "/api/v1/tcrfc/charity/directory";

    private static object Content(string zh, string? en) => en is null
        ? new { zh = new { name = zh } }
        : new { zh = new { name = zh }, en = new { name = en } };

    [Fact]
    public async Task 只回已公開者_草稿計畫與只有草稿計畫的團體不出現_發布後出現_下架後消失_中英名稱與主鍵()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? orgId = null, draftOnlyOrgId = null, programId = null, draftProgramId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = Content("【測試】K2 公益團體", "K2 Test Charity") })));
            orgId = org.Id;
            var draftOnlyOrg = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = Content("【測試】K2 只有草稿的團體", null) })));
            draftOnlyOrgId = draftOnlyOrg.Id;

            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(new { charityId = org.Id, status = "draft", content = Content("【測試】K2 計畫", "K2 Test Program") })));
            programId = program.Id;
            var draftProgram = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(new { charityId = draftOnlyOrg.Id, status = "draft", content = Content("【測試】K2 草稿計畫", null) })));
            draftProgramId = draftProgram.Id;

            // 全部草稿：什麼都不出現
            var none = await BizTest.ReadAsync<CharityDirectoryDto>(await anonymous.GetAsync(Path));
            Assert.DoesNotContain(none.Charities, c => c.Id == org.Id || c.Id == draftOnlyOrg.Id);
            Assert.DoesNotContain(none.Programs, p => p.Id == program.Id || p.Id == draftProgram.Id);

            // 發布一個計畫：該計畫與其公益團體出現；只有草稿計畫的團體仍不出現
            var published = await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}",
                BizTest.Multipart(new { charityId = org.Id, status = "published", content = Content("【測試】K2 計畫", "K2 Test Program") }));
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);

            var response = await anonymous.GetAsync(Path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("max-age=60", response.Headers.CacheControl?.ToString());            // 可短暫快取
            Assert.DoesNotContain("private", response.Headers.CacheControl?.ToString());
            var shown = await BizTest.ReadAsync<CharityDirectoryDto>(response);
            var charity = Assert.Single(shown.Charities, c => c.Id == org.Id);
            Assert.Equal("【測試】K2 公益團體", charity.NameZh);
            Assert.Equal("K2 Test Charity", charity.NameEn);
            Assert.Equal("published", charity.Status);
            Assert.False(string.IsNullOrWhiteSpace(charity.Slug));
            Assert.DoesNotContain(shown.Charities, c => c.Id == draftOnlyOrg.Id);
            var listed = Assert.Single(shown.Programs, p => p.Id == program.Id);
            Assert.Equal(org.Id, listed.CharityId);
            Assert.Equal("【測試】K2 計畫", listed.NameZh);
            Assert.Equal("K2 Test Program", listed.NameEn);
            Assert.DoesNotContain(shown.Programs, p => p.Id == draftProgram.Id);                // 草稿計畫不出現

            // 沒有任何個資或金額欄位（契約只有識別與名稱）
            var raw = await (await anonymous.GetAsync(Path)).Content.ReadAsStringAsync();
            foreach (var forbidden in new[] { "contactName", "contactPhone", "websiteUrl", "amount", "logoKey" })
            {
                Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
            }

            // 改網址名稱（slug）不影響對照鍵：id 不變
            var renamed = await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}",
                BizTest.Multipart(new { charityId = org.Id, status = "published", slug = BizTest.Unique("k2-renamed"), content = Content("【測試】K2 計畫（改名）", "K2 Test Program 2") }));
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            var after = (await BizTest.ReadAsync<CharityDirectoryDto>(await anonymous.GetAsync(Path))).Programs.Single(p => p.Id == program.Id);
            Assert.Equal("【測試】K2 計畫（改名）", after.NameZh);                                  // 後台寫入會讓快取失效，改名立即可見
            Assert.NotEqual(listed.Slug, after.Slug);

            // 改回草稿（下架）：計畫與團體都消失
            var unpublished = await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}",
                BizTest.Multipart(new { charityId = org.Id, status = "draft", content = Content("【測試】K2 計畫", "K2 Test Program") }));
            Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
            var gone = await BizTest.ReadAsync<CharityDirectoryDto>(await anonymous.GetAsync(Path));
            Assert.DoesNotContain(gone.Programs, p => p.Id == program.Id);
            Assert.DoesNotContain(gone.Charities, c => c.Id == org.Id);
        }
        finally
        {
            if (draftProgramId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{draftProgramId}");
            if (programId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{programId}");
            if (draftOnlyOrgId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{draftOnlyOrgId}");
            if (orgId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{orgId}");
        }
    }

    [Fact]
    public async Task 英文名稱空白輸出null_不以中文頂替_沒有繁中名稱的項目不輸出()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? orgId = null, programId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = Content("【測試】K2 無英文團體", null) })));
            orgId = org.Id;
            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(new { charityId = org.Id, status = "published", content = Content("【測試】K2 無英文計畫", null) })));
            programId = program.Id;

            var shown = await BizTest.ReadAsync<CharityDirectoryDto>(await anonymous.GetAsync(Path));
            Assert.Null(Assert.Single(shown.Charities, c => c.Id == org.Id).NameEn);
            Assert.Null(Assert.Single(shown.Programs, p => p.Id == program.Id).NameEn);
        }
        finally
        {
            if (programId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{programId}");
            if (orgId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{orgId}");
        }
    }

    [Fact]
    public async Task 藍鯨不設慈善單元_回空清單_不存在的俱樂部回404()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);

        var bw = await BizTest.ReadAsync<CharityDirectoryDto>(await anonymous.GetAsync("/api/v1/bw/charity/directory"));
        Assert.Empty(bw.Programs);                                             // 慈善單元只有磐石；藍鯨沒有專屬資料（共同列除外，種子沒有）
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/no-such-club/charity/directory")).StatusCode);
    }
}
