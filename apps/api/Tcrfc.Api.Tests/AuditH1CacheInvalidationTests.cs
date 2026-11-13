using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminFaqs;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminSessions;
using Tcrfc.Api.Features.AdminStaff;
using Tcrfc.Api.Features.AdminVenues;
using Tcrfc.Api.Features.Faqs;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// docs/23 H-1 的 W-4／W-26／W-27／W-28：後台寫入後公開端快取要失效。用真實 <c>redis-server</c>（no-op 快取驗不出
/// 「有沒有失效」，理由同 <c>BusinessCacheInvalidationTests</c>）：先暖公開端點、改後台、再斷言公開端點立刻看到新值。
/// 測資一律加【測試】前綴，<c>finally</c> 清掉自己建的列，不動種子。
/// </summary>
[Collection(AdminWriteRedisEnabledCollection.Name)]
public sealed class AuditH1CacheInvalidationTests(AdminWriteRedisEnabledApiFixture fixture)
{
    [Fact]
    public async Task 課程公開頁_報名異動_場地改名_教練與夥伴改名_都會立刻反映()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var slug = $"test-h1-{Guid.NewGuid():N}"[..24];
        Guid? venueId = null, staffId = null, partnerId = null, programId = null, sessionId = null;
        try
        {
            var venue = await BizTest.ReadAsync<AdminVenueDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/venues",
                BizTest.Multipart(new { zh = new { name = "【測試】快取場地舊名", address = "測試路 1 號" } })));
            venueId = venue.Id;
            var staff = await BizTest.ReadAsync<AdminStaffDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/staff",
                BizTest.Multipart(new { content = new { zh = new { name = "【測試】快取教練舊名" } } })));
            staffId = staff.Id;
            var partner = await BizTest.ReadAsync<AdminPartnerDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/partners",
                BizTest.Multipart(new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】快取夥伴舊名") } })));
            partnerId = partner.Id;
            var program = await BizTest.ReadAsync<AdminProgramDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/programs",
                AdminArticleMultipart.Build(new CreateAdminProgramRequest
                {
                    Slug = slug, ProgramType = "summer_camp", Status = "published",
                    Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "【測試】快取課程", Intro = "簡介" } },
                    StaffIds = [staff.Id], PartnerIds = [partner.Id],
                })));
            programId = program.Id;
            var session = await BizTest.ReadAsync<AdminSessionDetailDto>(await admin.PostAsJsonAsync("/api/v1/admin/tcrfc/program-sessions",
                new CreateAdminSessionRequest { ProgramId = program.Id, VenueId = venue.Id, Capacity = 5, Price = 500 }, TestJson.WriteOptions));
            sessionId = session.Id;

            async Task<ProgramDetailDto> PublicAsync()
                => await BizTest.ReadAsync<ProgramDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/programs/{slug}?lang=zh"));

            var warm = await PublicAsync();
            Assert.Equal(0, warm.Sessions.Single().EnrolledCount);
            Assert.Equal("【測試】快取場地舊名", warm.Sessions.Single().VenueName);
            Assert.Equal("【測試】快取教練舊名", warm.Staff.Single().Name);
            Assert.Equal("【測試】快取夥伴舊名", warm.Partners.Single().Name);

            // W-26：場地改名
            var venueUpdate = await admin.PutAsync($"/api/v1/admin/tcrfc/venues/{venue.Id}",
                BizTest.Multipart(new { zh = new { name = "【測試】快取場地新名", address = "測試路 1 號" } }));
            Assert.Equal(HttpStatusCode.OK, venueUpdate.StatusCode);
            Assert.Equal("【測試】快取場地新名", (await PublicAsync()).Sessions.Single().VenueName);

            // W-28：教練改名、夥伴改名
            var staffUpdate = await admin.PutAsync($"/api/v1/admin/tcrfc/staff/{staff.Id}",
                BizTest.Multipart(new { content = new { zh = new { name = "【測試】快取教練新名" } } }));
            Assert.Equal(HttpStatusCode.OK, staffUpdate.StatusCode);
            Assert.Equal("【測試】快取教練新名", (await PublicAsync()).Staff.Single().Name);
            var partnerUpdate = await admin.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}",
                BizTest.Multipart(new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】快取夥伴新名") } }));
            Assert.Equal(HttpStatusCode.OK, partnerUpdate.StatusCode);
            Assert.Equal("【測試】快取夥伴新名", (await PublicAsync()).Partners.Single().Name);

            // W-4：後台代填報名 → 已報名人數 +1；改成取消 → 回到 0
            var created = await BizTest.ReadAsync<AdminRegistrationDetailDto>(await admin.PostAsJsonAsync("/api/v1/admin/tcrfc/registrations",
                new CreateAdminRegistrationRequest { SessionId = session.Id, ApplicantName = "【測試】快取學員", Phone = "0912345678" }, TestJson.WriteOptions));
            Assert.Equal(1, (await PublicAsync()).Sessions.Single().EnrolledCount);
            var cancelled = await admin.PutAsJsonAsync($"/api/v1/admin/tcrfc/registrations/{created.Id}",
                new UpdateAdminRegistrationRequest { SessionId = session.Id, ApplicantName = "【測試】快取學員", Phone = "0912345678", Status = "取消" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            Assert.Equal(0, (await PublicAsync()).Sessions.Single().EnrolledCount);
        }
        finally
        {
            if (programId is not null)
            {
                await BizTest.ExecuteSqlAsync("""
                    DELETE FROM registrations WHERE session_id IN (SELECT id FROM sessions WHERE program_id = @Id);
                    DELETE FROM sessions WHERE program_id = @Id;
                    DELETE FROM program_staff WHERE program_id = @Id;
                    DELETE FROM program_partners WHERE program_id = @Id;
                    DELETE FROM programs_i18n WHERE program_id = @Id;
                    DELETE FROM programs WHERE id = @Id;
                    """, ("@Id", programId.Value));
            }
            if (venueId is not null) await admin.DeleteAsync($"/api/v1/admin/tcrfc/venues/{venueId}");
            if (staffId is not null) await admin.DeleteAsync($"/api/v1/admin/tcrfc/staff/{staffId}");
            if (partnerId is not null) await admin.DeleteAsync($"/api/v1/admin/tcrfc/partners/{partnerId}");
        }
    }

    [Fact]
    public async Task 常見問題分類_新增改名刪除_公開分類清單立刻反映()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var slug = $"test-h1-{Guid.NewGuid():N}"[..24];
        Guid? categoryId = null;
        try
        {
            // 先暖快取（此時沒有這個分類），再新增 → 新增後必須立刻出現
            var before = await BizTest.ReadAsync<List<FaqCategoryDto>>(await anonymous.GetAsync("/api/v1/faq-categories?lang=zh"));
            Assert.DoesNotContain(before, c => c.Slug == slug);

            var created = await BizTest.ReadAsync<AdminFaqCategoryDetailDto>(await admin.PostAsJsonAsync("/api/v1/admin/faq-categories",
                new CreateAdminFaqCategoryRequest { Slug = slug, SortOrder = 999, IsEnabled = true, Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = "【測試】分類舊名" } } },
                TestJson.WriteOptions));
            categoryId = created.Id;
            var afterCreate = await BizTest.ReadAsync<List<FaqCategoryDto>>(await anonymous.GetAsync("/api/v1/faq-categories?lang=zh"));
            Assert.Equal("【測試】分類舊名", afterCreate.Single(c => c.Slug == slug).Name);

            var updated = await admin.PutAsJsonAsync($"/api/v1/admin/faq-categories/{created.Id}",
                new UpdateAdminFaqCategoryRequest { Slug = slug, SortOrder = 999, IsEnabled = true, Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = "【測試】分類新名" } } },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var afterRename = await BizTest.ReadAsync<List<FaqCategoryDto>>(await anonymous.GetAsync("/api/v1/faq-categories?lang=zh"));
            Assert.Equal("【測試】分類新名", afterRename.Single(c => c.Slug == slug).Name);

            var disabled = await admin.PutAsJsonAsync($"/api/v1/admin/faq-categories/{created.Id}",
                new UpdateAdminFaqCategoryRequest { Slug = slug, SortOrder = 999, IsEnabled = false, Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = "【測試】分類新名" } } },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
            var afterDisable = await BizTest.ReadAsync<List<FaqCategoryDto>>(await anonymous.GetAsync("/api/v1/faq-categories?lang=zh"));
            Assert.DoesNotContain(afterDisable, c => c.Slug == slug);

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/faq-categories/{created.Id}")).StatusCode);
            categoryId = null;
        }
        finally
        {
            if (categoryId is not null) await admin.DeleteAsync($"/api/v1/admin/faq-categories/{categoryId}");
        }
    }
}
