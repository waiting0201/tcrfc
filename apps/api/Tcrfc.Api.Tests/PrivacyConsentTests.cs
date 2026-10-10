using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.AdminTrials;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Features.Trials;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 2026-10-09 三項拍板（主站規劃書 v3.25、App 規劃書 v3.18）：
/// ① 報名與詢問留存隱私同意（<c>privacy_consented_at</c>／<c>privacy_policy_version</c>，伺服器寫入、不信任客戶端）；
/// ② 慈善計畫詳情的夥伴與贊助商只列期間涵蓋今日者；
/// ③ 「捐助洽詢」表單種類移除（9 → 8 種）。
/// 公開送出端點掛了每 IP 的濫用防護，所以寫入與驗證規則直接呼叫 repository（同一條程式路徑），HTTP 只留少數幾次驗證端點形狀。
/// 測試資料以 ZZPRIV 辨識，finally 一律清掉；改共用庫設定前後快照還原。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class PrivacyConsentTests(AdminWriteApiFixture fixture)
{
    private const string Marker = "ZZPRIV";

    private static async Task<(Guid ClubId, Tcrfc.Api.Security.ClubScope Scope)> TcrfcScopeAsync()
    {
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        return (clubId, ClubScopeTestFactory.Create(clubId, "tcrfc"));
    }

    private static async Task<Guid> InsertSessionAsync()
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
            DECLARE @prog uniqueidentifier = (SELECT TOP 1 id FROM programs WHERE club_id = @club);
            INSERT INTO sessions (id, club_id, program_id, capacity, enrolled_count, status) VALUES (@I, @club, @prog, 50, 0, N'開放');
            """, ("@I", id));
        return id;
    }

    private static async Task<Guid> InsertTrialAsync()
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
            INSERT INTO trials (id, club_id, trial_on, capacity, status) VALUES (@I, @club, DATEADD(day, 10, CAST(SYSUTCDATETIME() AS date)), 50, N'開放');
            INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@I, N'zh-Hant', N'ZZPRIV 隱私同意測試場');
            """, ("@I", id));
        return id;
    }

    private static async Task CleanupAsync(params Guid[] sessionOrTrialIds)
    {
        foreach (var id in sessionOrTrialIds)
        {
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM registrations WHERE session_id = @I OR trial_id = @I; DELETE FROM sessions WHERE id = @I; DELETE FROM trials_i18n WHERE trial_id = @I; DELETE FROM trials WHERE id = @I;",
                ("@I", id));
        }
    }

    private static async Task<(DateTime? At, string? Version)> RegistrationConsentAsync(string registrationNo)
    {
        var at = await C1Test.ScalarAsync<DateTime?>("SELECT privacy_consented_at FROM registrations WHERE registration_no = @N", ("@N", registrationNo));
        var version = await C1Test.ScalarAsync<string?>("SELECT privacy_policy_version FROM registrations WHERE registration_no = @N", ("@N", registrationNo));
        return (at, version);
    }

    // ═════════════════════════ ① 課程報名 ═════════════════════════

    [Fact]
    public async Task 課程報名_勾選同意_伺服器寫入UTC現在時間與預設版本_沒設定版本用1點0()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "legal.%");
        var (clubId, scope) = await TcrfcScopeAsync();
        var sessionId = await InsertSessionAsync();
        try
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM settings WHERE club_id = @C AND setting_key = N'legal.privacy_policy_version'", ("@C", clubId));
            using var di = fixture.Services.CreateScope();
            var repository = di.ServiceProvider.GetRequiredService<ProgramsRepository>();
            var before = DateTime.UtcNow.AddSeconds(-2);
            var result = await repository.SubmitRegistrationAsync(scope, sessionId,
                new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = Marker + " 甲", Phone = "0912000001" }, CancellationToken.None);

            var (at, version) = await RegistrationConsentAsync(result.RegistrationNo);
            Assert.NotNull(at);
            Assert.InRange(at!.Value, before, DateTime.UtcNow.AddSeconds(2)); // UTC 現在時間，不是客戶端值。
            Assert.Equal(PrivacyConsentStamp.DefaultVersion, version);
        }
        finally
        {
            await CleanupAsync(sessionId);
            await restore();
        }
    }

    [Fact]
    public async Task 課程報名_後台設定的政策版本會寫入_改版後新報名記新版本_舊報名不變()
    {
        var restoreLegal = await C1Test.SnapshotSettingsAsync("tcrfc", "legal.%");
        var restorePolicy = await C1Test.SnapshotSettingsAsync("tcrfc", "policy.%");
        var restoreMaintenance = await C1Test.SnapshotSettingsAsync("tcrfc", "maintenance.%");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var (_, scope) = await TcrfcScopeAsync();
        var sessionId = await InsertSessionAsync();
        try
        {
            var current = await BizTest.ReadAsync<AdminGlobalSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/global-settings"));
            // 省略 privacyPolicyVersion 的舊版前端請求：維持不變，不會把版本清掉。
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync("/api/v1/admin/tcrfc/global-settings", BizTest.Multipart(new
            {
                cookiePolicy = new { bodyZh = current.Policies.Single(p => p.Code == "cookie").BodyZh, bodyEn = current.Policies.Single(p => p.Code == "cookie").BodyEn },
                privacyPolicy = new { bodyZh = current.Policies.Single(p => p.Code == "privacy").BodyZh, bodyEn = current.Policies.Single(p => p.Code == "privacy").BodyEn },
                memberTerms = new { bodyZh = current.Policies.Single(p => p.Code == "member-terms").BodyZh, bodyEn = current.Policies.Single(p => p.Code == "member-terms").BodyEn },
                maintenanceEnabled = current.Maintenance.Enabled, maintenanceMessageZh = current.Maintenance.MessageZh, maintenanceMessageEn = current.Maintenance.MessageEn,
                privacyPolicyVersion = "ZZPRIV-v1",
            }))).StatusCode);

            var saved = await BizTest.ReadAsync<AdminGlobalSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/global-settings"));
            Assert.Equal("ZZPRIV-v1", saved.PrivacyPolicyVersion);

            using var di = fixture.Services.CreateScope();
            var repository = di.ServiceProvider.GetRequiredService<ProgramsRepository>();
            var first = await repository.SubmitRegistrationAsync(scope, sessionId,
                new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = Marker + " 乙", Phone = "0912000002" }, CancellationToken.None);
            Assert.Equal("ZZPRIV-v1", (await RegistrationConsentAsync(first.RegistrationNo)).Version);

            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync("/api/v1/admin/tcrfc/global-settings", BizTest.Multipart(new
            {
                cookiePolicy = new { bodyZh = current.Policies.Single(p => p.Code == "cookie").BodyZh },
                privacyPolicy = new { bodyZh = current.Policies.Single(p => p.Code == "privacy").BodyZh },
                memberTerms = new { bodyZh = current.Policies.Single(p => p.Code == "member-terms").BodyZh },
                maintenanceEnabled = current.Maintenance.Enabled,
                privacyPolicyVersion = "ZZPRIV-v2",
            }))).StatusCode);
            var second = await repository.SubmitRegistrationAsync(scope, sessionId,
                new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = Marker + " 丙", Phone = "0912000003" }, CancellationToken.None);
            Assert.Equal("ZZPRIV-v2", (await RegistrationConsentAsync(second.RegistrationNo)).Version);
            Assert.Equal("ZZPRIV-v1", (await RegistrationConsentAsync(first.RegistrationNo)).Version); // 舊報名不回改。

            // 超過 50 字 → 400 欄位錯誤；空字串 → 清除、回預設。
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/global-settings", BizTest.Multipart(new { privacyPolicyVersion = new string('v', 51) }))).StatusCode);
            var cleared = await BizTest.ReadAsync<AdminGlobalSettingsDto>(await admin.PutAsync("/api/v1/admin/tcrfc/global-settings", BizTest.Multipart(new
            {
                cookiePolicy = new { bodyZh = current.Policies.Single(p => p.Code == "cookie").BodyZh },
                privacyPolicy = new { bodyZh = current.Policies.Single(p => p.Code == "privacy").BodyZh },
                memberTerms = new { bodyZh = current.Policies.Single(p => p.Code == "member-terms").BodyZh },
                maintenanceEnabled = current.Maintenance.Enabled,
                privacyPolicyVersion = "",
            })));
            Assert.Equal(PrivacyConsentStamp.DefaultVersion, cleared.PrivacyPolicyVersion);
        }
        finally
        {
            await CleanupAsync(sessionId);
            // 上面的 PUT 也會動到政策內文與維護模式設定，全部快照還原。
            await restoreLegal();
            await restorePolicy();
            await restoreMaintenance();
        }
    }

    [Fact]
    public async Task 課程報名_沒勾同意_400_不寫入報名_不佔名額()
    {
        var (_, scope) = await TcrfcScopeAsync();
        var sessionId = await InsertSessionAsync();
        try
        {
            using var di = fixture.Services.CreateScope();
            var repository = di.ServiceProvider.GetRequiredService<ProgramsRepository>();
            await Assert.ThrowsAsync<ProgramRegistrationValidationException>(() => repository.SubmitRegistrationAsync(scope, sessionId,
                new SubmitProgramRegistrationRequest { ApplicantName = Marker + " 未勾", Phone = "0912000004" }, CancellationToken.None));

            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM registrations WHERE session_id = @I", ("@I", sessionId)));
            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT enrolled_count FROM sessions WHERE id = @I", ("@I", sessionId)));
        }
        finally
        {
            await CleanupAsync(sessionId);
        }
    }

    [Fact]
    public async Task 課程報名_HTTP_客戶端自帶的同意時間與版本被忽略_沒勾同意回400_後台明細唯讀可見()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "legal.%");
        var sessionId = await InsertSessionAsync();
        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var url = $"/api/v1/tcrfc/programs/sessions/{sessionId}/registrations";
            var missing = await AppTest.PostJsonAsync(client, url, new { applicantName = Marker + " 無勾", phone = "0912000005" });
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            var explicitFalse = await AppTest.PostJsonAsync(client, url, new { applicantName = Marker + " 勾假", phone = "0912000005", privacyConsent = false });
            Assert.Equal(HttpStatusCode.BadRequest, explicitFalse.StatusCode);

            var ok = await BizTest.ReadAsync<ProgramRegistrationSubmittedDto>(await AppTest.PostJsonAsync(client, url, new
            {
                applicantName = Marker + " 偽造", phone = "0912000006", privacyConsent = true,
                privacyConsentedAt = "2001-01-01T00:00:00Z", privacy_consented_at = "2001-01-01T00:00:00Z", privacyPolicyVersion = "FORGED", privacy_policy_version = "FORGED",
            }));
            var (at, version) = await RegistrationConsentAsync(ok.RegistrationNo);
            Assert.True(at > DateTime.UtcNow.AddMinutes(-2)); // 不是 2001 年。
            Assert.NotEqual("FORGED", version);

            var id = await BizTest.ScalarGuidAsync("SELECT id FROM registrations WHERE registration_no = @N", ("@N", ok.RegistrationNo));
            var detail = await BizTest.ReadAsync<AdminRegistrationDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/registrations/{id}"));
            Assert.Equal(at, detail.PrivacyConsentedAt);
            Assert.Equal(version, detail.PrivacyPolicyVersion);
        }
        finally
        {
            await CleanupAsync(sessionId);
            await restore();
        }
    }

    // ═════════════════════════ ① 試訓報名 ═════════════════════════

    [Fact]
    public async Task 試訓報名_勾選同意寫入時間與版本_沒勾400不佔名額_後台明細可見()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "legal.%");
        var (clubId, scope) = await TcrfcScopeAsync();
        var trialId = await InsertTrialAsync();
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            await BizTest.ExecuteSqlAsync(
                """
                DELETE FROM settings WHERE club_id = @C AND setting_key = N'legal.privacy_policy_version';
                INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group) VALUES (NEWID(), @C, N'legal.privacy_policy_version', N'  ZZPRIV-trial  ', N'legal');
                """, ("@C", clubId));
            using var di = fixture.Services.CreateScope();
            var repository = di.ServiceProvider.GetRequiredService<TrialsRepository>();

            await Assert.ThrowsAsync<Tcrfc.Api.Common.PublicValidationException>(() => repository.SubmitRegistrationAsync(scope, trialId,
                new SubmitTrialRegistrationRequest { ApplicantName = Marker + " 未勾", Phone = "0913000001" }, null, CancellationToken.None));
            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT enrolled_count FROM trials WHERE id = @I", ("@I", trialId)));

            var before = DateTime.UtcNow.AddSeconds(-2);
            var result = await repository.SubmitRegistrationAsync(scope, trialId,
                new SubmitTrialRegistrationRequest { PrivacyConsent = true, ApplicantName = Marker + " 試訓", Phone = "0913000002" }, null, CancellationToken.None);
            var (at, version) = await RegistrationConsentAsync(result.RegistrationNo);
            Assert.InRange(at!.Value, before, DateTime.UtcNow.AddSeconds(2));
            Assert.Equal("ZZPRIV-trial", version); // 去頭尾空白。

            var regId = await BizTest.ScalarGuidAsync("SELECT id FROM registrations WHERE registration_no = @N", ("@N", result.RegistrationNo));
            var detail = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{trialId}/registrations/{regId}"));
            Assert.Equal(at, detail.PrivacyConsentedAt);
            Assert.Equal("ZZPRIV-trial", detail.PrivacyPolicyVersion);
        }
        finally
        {
            await CleanupAsync(trialId);
            await restore();
        }
    }

    [Fact]
    public async Task 後台代填報名_不留存隱私同意_兩欄為空()
    {
        var sessionId = await InsertSessionAsync();
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var created = await BizTest.ReadAsync<AdminRegistrationDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/registrations",
                BizTest.Json(new { sessionId, applicantName = Marker + " 代填", phone = "0912000007" })));
            Assert.Null(created.PrivacyConsentedAt);
            Assert.Null(created.PrivacyPolicyVersion);
        }
        finally
        {
            await CleanupAsync(sessionId);
        }
    }

    // ═════════════════════════ ① 表單詢問 ═════════════════════════

    [Fact]
    public async Task 表單詢問_勾選同意欄位寫入時間與版本_客戶端傳值被忽略_後台明細可見_必填同意沒勾回400()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "legal.%");
        await using var testForm = await UnlockedTestForm.CreateAsync();
        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var contact = $"zzpriv-{Guid.NewGuid():N}@example.test";
        try
        {
            var url = $"/api/v1/tcrfc/forms/{testForm.Code}/submissions";
            var notChecked = await client.PostAsync(url, BizTest.Json(new { answers = new Dictionary<string, string> { ["name"] = Marker, ["contact"] = contact, ["privacy_consent"] = "false" } }));
            Assert.Equal(HttpStatusCode.BadRequest, notChecked.StatusCode);
            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM enquiries WHERE form_id = @F", ("@F", testForm.Id)));

            var before = DateTime.UtcNow.AddSeconds(-2);
            var response = await client.PostAsync(url, BizTest.Json(new
            {
                answers = new Dictionary<string, string> { ["name"] = Marker, ["contact"] = contact, ["privacy_consent"] = "true" },
                privacyConsentedAt = "2001-01-01T00:00:00Z", privacyPolicyVersion = "FORGED",
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var enquiryId = await BizTest.ScalarGuidAsync("SELECT id FROM enquiries WHERE form_id = @F", ("@F", testForm.Id));
            var detail = await BizTest.ReadAsync<AdminEnquiryDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/enquiries/{enquiryId}"));
            Assert.NotNull(detail.PrivacyConsentedAt);
            Assert.InRange(detail.PrivacyConsentedAt!.Value, before, DateTime.UtcNow.AddSeconds(2));
            Assert.NotEqual("FORGED", detail.PrivacyPolicyVersion);
            Assert.False(string.IsNullOrWhiteSpace(detail.PrivacyPolicyVersion));
        }
        finally
        {
            await restore();
        }
    }

    // ═════════════════════════ ③ 捐助洽詢移除 ═════════════════════════

    [Fact]
    public async Task 捐助洽詢_目錄只剩八種_公開讀取與送出都404_後台表單清單與收件匣都沒有它()
    {
        Assert.Equal(8, FormCatalog.AllCodes.Count);
        Assert.DoesNotContain("donation_enquiry", FormCatalog.AllCodes);
        Assert.False(FormCatalog.IsKnownCode("donation_enquiry"));

        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/forms/donation_enquiry")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/tcrfc/forms/donation_enquiry/submissions",
            BizTest.Json(new { answers = new Dictionary<string, string> { ["name"] = Marker, ["contact"] = "x@example.test", ["privacy_consent"] = "true" } }))).StatusCode);

        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var forms = await BizTest.ReadAsync<List<Tcrfc.Api.Features.AdminForms.AdminFormListItemDto>>(await admin.GetAsync($"/api/v1/admin/{club}/forms"));
            Assert.DoesNotContain(forms, f => f.FormCode == "donation_enquiry");
            Assert.Equal(8, forms.Count);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/tcrfc/enquiries/assignable-users?formCode=donation_enquiry")).StatusCode);
        Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM forms WHERE form_code = N'donation_enquiry'"));
    }

    // ═════════════════════════ ② 慈善計畫詳情夥伴／贊助商期間 ═════════════════════════

    [Fact]
    public async Task 慈善計畫詳情_夥伴與贊助商只列期間涵蓋今日者_起訖為空視為進行中()
    {
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var programId = await BizTest.ScalarGuidAsync("SELECT id FROM charity_programs WHERE slug = N'test-charity-program-a'");
        var ids = new Dictionary<string, Guid>();
        string[] keys = ["pNull", "pCurrent", "pExpired", "pFuture", "sNull", "sCurrent", "sExpired", "sFuture"];
        foreach (var k in keys) { ids[k] = Guid.NewGuid(); }
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            await BizTest.ExecuteSqlAsync(
                """
                INSERT INTO partners (id, club_id, slug, start_on, end_on) VALUES
                  (@pNull, @C, N'zzpriv-p-null', NULL, NULL),
                  (@pCurrent, @C, N'zzpriv-p-current', DATEADD(day, -30, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date))),
                  (@pExpired, @C, N'zzpriv-p-expired', DATEADD(day, -90, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, -3, CAST(SYSUTCDATETIME() AS date))),
                  (@pFuture, @C, N'zzpriv-p-future', DATEADD(day, 5, CAST(SYSUTCDATETIME() AS date)), NULL);
                INSERT INTO partners_i18n (partner_id, locale, name) SELECT id, N'zh-Hant', N'ZZPRIV 夥伴 ' + slug FROM partners WHERE slug LIKE N'zzpriv-p-%';
                INSERT INTO sponsors (id, club_id, slug, contract_start_on, contract_end_on) VALUES
                  (@sNull, @C, N'zzpriv-s-null', NULL, NULL),
                  (@sCurrent, @C, N'zzpriv-s-current', DATEADD(day, -30, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date))),
                  (@sExpired, @C, N'zzpriv-s-expired', DATEADD(day, -90, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, -3, CAST(SYSUTCDATETIME() AS date))),
                  (@sFuture, @C, N'zzpriv-s-future', DATEADD(day, 5, CAST(SYSUTCDATETIME() AS date)), NULL);
                INSERT INTO sponsors_i18n (sponsor_id, locale, name) SELECT id, N'zh-Hant', N'ZZPRIV 贊助 ' + slug FROM sponsors WHERE slug LIKE N'zzpriv-s-%';
                INSERT INTO charity_program_partners (charity_program_id, partner_id) SELECT @P, id FROM partners WHERE slug LIKE N'zzpriv-p-%';
                INSERT INTO charity_program_sponsors (charity_program_id, sponsor_id) SELECT @P, id FROM sponsors WHERE slug LIKE N'zzpriv-s-%';
                """,
                [("@C", clubId), ("@P", programId), .. keys.Select(k => (k, (object?)ids[k])).Select(t => ("@" + t.k, t.Item2))]);

            var detail = await BizTest.ReadAsync<CharityProgramDetailDto>(await client.GetAsync("/api/v1/tcrfc/charity/programs/test-charity-program-a?lang=zh"));
            var partnerSlugs = detail.Partners.Select(p => p.Slug).Where(s => s!.StartsWith("zzpriv-")).OrderBy(s => s).ToList();
            var sponsorSlugs = detail.Sponsors.Select(p => p.Slug).Where(s => s!.StartsWith("zzpriv-")).OrderBy(s => s).ToList();
            Assert.Equal(["zzpriv-p-current", "zzpriv-p-null"], partnerSlugs);
            Assert.Equal(["zzpriv-s-current", "zzpriv-s-null"], sponsorSlugs);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync(
                """
                DELETE FROM charity_program_partners WHERE partner_id IN (SELECT id FROM partners WHERE slug LIKE N'zzpriv-p-%');
                DELETE FROM charity_program_sponsors WHERE sponsor_id IN (SELECT id FROM sponsors WHERE slug LIKE N'zzpriv-s-%');
                DELETE FROM partners_i18n WHERE partner_id IN (SELECT id FROM partners WHERE slug LIKE N'zzpriv-p-%');
                DELETE FROM sponsors_i18n WHERE sponsor_id IN (SELECT id FROM sponsors WHERE slug LIKE N'zzpriv-s-%');
                DELETE FROM partners WHERE slug LIKE N'zzpriv-p-%';
                DELETE FROM sponsors WHERE slug LIKE N'zzpriv-s-%';
                """);
        }
    }
}
