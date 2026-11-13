using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// docs/23 W-29：公開事蹟紀錄列表（含 count、年份清單、影響力彙總）不得外露「草稿計畫」的事蹟與計畫名稱，
/// 規則與同檔影響力指標一致（<c>cp.id IS NULL OR cp.status = 'published'</c>）。事蹟紀錄在 API 需要圖片檔，
/// 這裡改用 SQL 直接插入並於 <c>finally</c> 刪除自己的列（測資加【測試】前綴與唯一 slug）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AuditH1CharityRecordsDraftProgramTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 草稿計畫的事蹟_不出現在公開列表_發布後才出現()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? orgId = null, programId = null, recordId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations",
                BizTest.Multipart(new { content = new { zh = BizTest.Zh("【測試】W29 團體") } })));
            orgId = org.Id;
            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs",
                BizTest.Multipart(new
                {
                    charityId = org.Id, status = "draft",
                    content = new { zh = new { name = "【測試】W29 草稿計畫", donationContent = "足球 50 顆" } },
                })));
            programId = program.Id;
            recordId = Guid.NewGuid();
            await BizTest.ExecuteSqlAsync("""
                INSERT INTO impact_records (id, club_id, charity_program_id, charity_id, happened_on)
                VALUES (@Id, (SELECT id FROM clubs WHERE code = 'tcrfc'), @ProgramId, @OrgId, '2031-05-05');
                INSERT INTO impact_records_i18n (impact_record_id, locale, donation_content, location)
                VALUES (@Id, N'zh-Hant', N'【測試】W29 草稿事蹟', N'測試地點');
                """, ("@Id", recordId.Value), ("@ProgramId", program.Id), ("@OrgId", org.Id));

            async Task<PagedResult<ImpactRecordDto>> ListAsync()
                => await BizTest.ReadAsync<PagedResult<ImpactRecordDto>>(await anonymous.GetAsync(
                    "/api/v1/tcrfc/charity/records?year=2031&lang=zh&page=1&pageSize=50"));

            var draft = await ListAsync();
            Assert.Empty(draft.Items);
            Assert.Equal(0, draft.TotalCount);
            Assert.DoesNotContain(2031, await BizTest.ReadAsync<List<int>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/records/years")));
            // 以草稿計畫 slug 直接篩選也不應洩漏
            Assert.Empty((await BizTest.ReadAsync<PagedResult<ImpactRecordDto>>(await anonymous.GetAsync(
                $"/api/v1/tcrfc/charity/records?program={program.Slug}&lang=zh&page=1&pageSize=50"))).Items);

            var published = await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}",
                BizTest.Multipart(new
                {
                    charityId = org.Id, status = "published",
                    content = new { zh = new { name = "【測試】W29 草稿計畫", donationContent = "足球 50 顆" } },
                }));
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
            var visible = await ListAsync();
            var item = Assert.Single(visible.Items);
            Assert.Equal("【測試】W29 草稿事蹟", item.DonationContent);
            Assert.Equal(1, visible.TotalCount);
        }
        finally
        {
            if (recordId is not null)
            {
                await BizTest.ExecuteSqlAsync("""
                    DELETE FROM impact_records_i18n WHERE impact_record_id = @Id;
                    DELETE FROM impact_records WHERE id = @Id;
                    """, ("@Id", recordId.Value));
            }
            if (programId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{programId}");
            if (orgId is not null) await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{orgId}");
        }
    }
}
