using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminSessions;
using Tcrfc.Api.Features.AdminTrials;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// P4 試訓場次與報名名單（S2-4）＋ P3 報名進階（批次、候補遞補、簽到表、進階篩選）。
/// 試訓在 tcrfc（一線隊 D1）與 bw 測試；P3 進階借用 bw 俱樂部自建測試課程（比照 <c>AdminProgramsSessionsRegistrationsTests</c>）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminTrialsAndRegistrationsTests(AdminWriteApiFixture fixture)
{
    private static object TrialPayload(Guid? teamId, Guid? venueId, string trialOn = "2027-03-20", int? capacity = 2, string? deadlineOn = "2027-03-13", string? status = null, string audience = "【測試】試訓對象") => new
    {
        teamId, venueId, trialOn, capacity, deadlineOn, status,
        content = new { zh = new { audience }, en = new { audience = "Test audience" } },
    };

    private static object Reg(string name, string? status = null, Guid? memberId = null, string? health = null) => new
    {
        applicantName = name, phone = "0900-000-321", email = "trial-reg@example.com", birthOn = "2001-01-01", guardianName = "【測試】家長",
        guardianPhone = "0900-000-322", healthDeclaration = health, note = "【測試】備註", status, memberId,
    };

    // ═════════════ P4 ═════════════

    [Fact]
    public async Task 試訓_權限_未登入401_唯讀角色可看不可寫_沒有課程權限的角色403_跨俱樂部403()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/trials")).StatusCode);

        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync("/api/v1/admin/tcrfc/trials")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(null, null)))).StatusCode);

        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync("/api/v1/admin/tcrfc/trials")).StatusCode);

        using var academy = await BizTest.ClientAsync(fixture, "academy.manager@tcrfc.test"); // 只授權 bw
        Assert.Equal(HttpStatusCode.Forbidden, (await academy.GetAsync("/api/v1/admin/tcrfc/trials")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await academy.GetAsync("/api/v1/admin/bw/trials")).StatusCode);
    }

    [Fact]
    public async Task 試訓_種子場次可讀_篩選_雙語對象()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var list = await BizTest.ReadAsync<List<AdminTrialListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/trials"));
        Assert.Contains(list, t => t.AudienceZh != null && t.AudienceZh.Contains("一線隊公開試訓") && t.AudienceEn != null);
        Assert.All(list, t => Assert.Equal("D1", t.TeamCode ?? "D1"));
        var closed = await BizTest.ReadAsync<List<AdminTrialListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/trials?status=%E5%B7%B2%E7%B5%90%E6%9D%9F"));
        Assert.All(closed, t => Assert.Equal("已結束", t.Status));
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/trials?status=oops")).StatusCode);

        // bw 看不到 tcrfc 的場次
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var bw = await BizTest.ReadAsync<List<AdminTrialListItemDto>>(await partner.GetAsync("/api/v1/admin/bw/trials"));
        Assert.All(bw, t => Assert.NotEqual("D1", t.TeamCode));
    }

    [Fact]
    public async Task 試訓_建立更新刪除_驗證()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var d1 = await B1Test.TeamIdAsync("D1");
        var bwTeam = await B1Test.TeamIdAsync("BW1");
        var venue = await B1Test.VenueIdAsync("西屯");
        Guid? id = null;
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, venue, capacity: 0)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, venue, deadlineOn: "2027-04-01")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(bwTeam, venue)))).StatusCode); // 別的俱樂部的球隊
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, Guid.NewGuid())))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, venue, status: "暫停")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, venue, audience: "")))).StatusCode);

            var created = await BizTest.ReadAsync<AdminTrialDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, venue))));
            id = created.Id;
            Assert.Equal("開放", created.Status);
            Assert.Equal("D1", created.TeamCode);
            Assert.False(created.SyncToCalendar); // L3 開關預設關閉
            Assert.Equal("Test audience", created.En!.Audience);

            var updated = await BizTest.ReadAsync<AdminTrialDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/trials/{id}",
                BizTest.Json(new { teamId = (Guid?)null, venueId = venue, trialOn = "2027-03-27", capacity = 5, status = "候補", content = new { zh = new { audience = "【測試】改對象" } } })));
            Assert.Null(updated.TeamCode);
            Assert.Equal("候補", updated.Status);
            Assert.Null(updated.En);
            Assert.Equal(5, updated.Capacity);
        }
        finally
        {
            if (id is not null)
            {
                Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/trials/{id}")).StatusCode);
            }
        }

        if (id is not null)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task 試訓報名_名額連動_額滿自動關閉_候補遞補_取消釋回名額()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var d1 = await B1Test.TeamIdAsync("D1");
        var created = await BizTest.ReadAsync<AdminTrialDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(d1, null))));
        var id = created.Id;
        try
        {
            var r1 = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations", BizTest.Json(Reg("【測試】甲"))));
            Assert.Equal("待確認", r1.Status);
            Assert.StartsWith("TCRFC-", r1.RegistrationNo);
            Assert.Equal(1, (await Trial(admin, id)).EnrolledCount);

            var r2 = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations", BizTest.Json(Reg("【測試】乙", "已確認", health: "【測試】病史"))));
            var full = await Trial(admin, id);
            Assert.Equal(2, full.EnrolledCount);
            Assert.Equal("額滿", full.Status); // 達名額上限自動關閉
            Assert.False(full.IsSignupOpen);

            var r3 = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations", BizTest.Json(Reg("【測試】丙", "候補"))));
            Assert.Equal(2, (await Trial(admin, id)).EnrolledCount); // 候補不佔名額
            Assert.Equal(1, (await Trial(admin, id)).WaitlistCount);

            // 名額不能調到低於已報名人數
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/trials/{id}",
                BizTest.Json(TrialPayload(d1, null, capacity: 1)))).StatusCode);
            // 有報名不能刪除
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/trials/{id}")).StatusCode);

            // 取消 r1 → 釋回名額（狀態維持額滿，由人工決定要不要重新開放）
            var cancelled = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/{r1.Id}",
                BizTest.Json(new { applicantName = r1.ApplicantName, status = "取消" })));
            Assert.Equal("取消", cancelled.Status);
            Assert.Equal(1, (await Trial(admin, id)).EnrolledCount);

            // 遞補 r3：候補 → 已確認；只有候補的才能遞補
            var promoted = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/{r3.Id}/promote", null));
            Assert.Equal("已確認", promoted.Status);
            Assert.Equal(2, (await Trial(admin, id)).EnrolledCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/{r3.Id}/promote", null)).StatusCode);

            // 列表與篩選
            var all = await BizTest.ReadAsync<List<AdminTrialRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations"));
            Assert.Equal(3, all.Count);
            var filtered = await BizTest.ReadAsync<List<AdminTrialRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations?status=%E5%8F%96%E6%B6%88&keyword=%E7%94%B2"));
            Assert.Single(filtered);
            var noMember = await BizTest.ReadAsync<List<AdminTrialRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations?isMember=false"));
            Assert.Equal(3, noMember.Count);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations?status=oops")).StatusCode);

            // 簽到表：不含取消與候補、不含健康聲明與備註
            var sheet = await BizTest.ReadAsync<AdminTrialSignInSheetDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/sign-in-sheet"));
            Assert.Equal(2, sheet.Rows.Count);
            Assert.DoesNotContain(sheet.Rows, r => r.ApplicantName == "【測試】甲");
            var sheetJson = await (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/sign-in-sheet")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("病史", sheetJson);

            // 詳情與別的場次的報名
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/{r2.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{Guid.NewGuid()}/registrations/{r2.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{Guid.NewGuid()}/registrations", BizTest.Json(Reg("x")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations", BizTest.Json(Reg("")))).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE trial_id = @Id; DELETE FROM trials WHERE id = @Id;", ("@Id", id));
        }
    }

    private static async Task<AdminTrialDetailDto> Trial(HttpClient client, Guid id)
        => await BizTest.ReadAsync<AdminTrialDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/trials/{id}"));

    [Fact]
    public async Task 試訓報名_客服可處理不可建立不可匯出_匯出需用途且不含健康聲明()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var created = await BizTest.ReadAsync<AdminTrialDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/trials", BizTest.Json(TrialPayload(null, null, capacity: 10))));
        var id = created.Id;
        try
        {
            var reg = await BizTest.ReadAsync<AdminTrialRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations",
                BizTest.Json(Reg("【測試】匯出", health: "【測試】不應出現在匯出的病史"))));

            using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await service.PostAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations", BizTest.Json(Reg("x")))).StatusCode);
            var confirmed = await service.PutAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/{reg.Id}", BizTest.Json(new { applicantName = reg.ApplicantName, status = "已確認", healthDeclaration = reg.HealthDeclaration }));
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/export?purpose=x")).StatusCode);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/export")).StatusCode);
            var response = await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations/export?purpose=%E5%90%8D%E5%96%AE");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
            Assert.Contains("【測試】匯出", csv);
            Assert.DoesNotContain("病史", csv);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{Guid.NewGuid()}/registrations/export?purpose=x")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE trial_id = @Id; DELETE FROM trials WHERE id = @Id;", ("@Id", id));
        }
    }

    // ═════════════ P3 進階 ═════════════

    private static string ConnectionString() => Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")!;

    [Fact]
    public async Task 報名進階_進階篩選_批次狀態_候補遞補提醒_簽到表()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var slug = BizTest.Unique("b1-prog");
        Guid programId = default;
        Guid sessionId = default;
        try
        {
            var programResponse = await admin.PostAsync("/api/v1/admin/bw/programs", AdminArticleMultipart.Build(new CreateAdminProgramRequest
            {
                Slug = slug, ProgramType = "summer_camp", Status = "published",
                Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "【測試】進階報名課程", Intro = "測試簡介" } },
            }));
            programResponse.EnsureSuccessStatusCode();
            programId = (await BizTest.ReadAsync<AdminProgramDetailDto>(programResponse)).Id;
            var sessionResponse = await admin.PostAsJsonAsync("/api/v1/admin/bw/program-sessions", new CreateAdminSessionRequest { ProgramId = programId, Capacity = 2, Price = 500 }, TestJson.WriteOptions);
            sessionResponse.EnsureSuccessStatusCode();
            sessionId = (await BizTest.ReadAsync<AdminSessionDetailDto>(sessionResponse)).Id;

            var memberId = await B1Test.MemberIdAsync("M900001");
            async Task<AdminRegistrationDetailDto> Create(string name, string status, Guid? member = null)
                => await BizTest.ReadAsync<AdminRegistrationDetailDto>(await admin.PostAsJsonAsync("/api/v1/admin/bw/registrations",
                    new CreateAdminRegistrationRequest { SessionId = sessionId, ApplicantName = name, Phone = "0900-000-500", Status = status, MemberId = member }, TestJson.WriteOptions));

            var r1 = await Create("【測試】報名甲", "待確認", memberId);
            var r2 = await Create("【測試】報名乙", "待確認");
            var r3 = await Create("【測試】報名丙", "候補");

            // 進階篩選：關鍵字、是否為會員、課程
            var byKeyword = await BizTest.ReadAsync<List<AdminRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/bw/registrations?keyword={Uri.EscapeDataString("報名乙")}"));
            Assert.Equal(r2.Id, Assert.Single(byKeyword).Id);
            var members = await BizTest.ReadAsync<List<AdminRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/bw/registrations?programId={programId}&isMember=true"));
            Assert.Equal(r1.Id, Assert.Single(members).Id);
            var byProgram = await BizTest.ReadAsync<List<AdminRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/bw/registrations?programId={programId}"));
            Assert.Equal(3, byProgram.Count);
            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var byDate = await BizTest.ReadAsync<List<AdminRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/bw/registrations?programId={programId}&dateFrom={today}&dateTo={today}"));
            Assert.Equal(3, byDate.Count);
            Assert.Empty(await BizTest.ReadAsync<List<AdminRegistrationListItemDto>>(await admin.GetAsync($"/api/v1/admin/bw/registrations?programId={programId}&dateTo=2020-01-01")));

            // 額滿（2/2）→ 沒有候補提醒；批次取消 r2 之後有空位，候補 r3 進入提醒清單
            Assert.DoesNotContain(await Reminders(admin), r => r.SessionId == sessionId);
            var batch = await BizTest.ReadAsync<BatchOperationResultDto>(await admin.PostAsJsonAsync("/api/v1/admin/bw/registrations/batch/status",
                new BatchRegistrationStatusRequest { Ids = [r2.Id, r2.Id, Guid.NewGuid(), r3.Id], Status = "取消" }, TestJson.WriteOptions));
            Assert.Equal(2, batch.UpdatedCount); // r2 與 r3 改成取消（重複的 id 只算一次），不存在的略過
            Assert.Single(batch.Skipped);
            Assert.Equal(1, await EnrolledAsync(sessionId)); // r2 釋回名額，r3 本來就沒佔名額

            var r4 = await Create("【測試】報名丁", "候補");
            var reminder = Assert.Single(await Reminders(admin), r => r.SessionId == sessionId);
            Assert.Equal(1, reminder.Vacancy);
            Assert.Equal(r4.Id, Assert.Single(reminder.Waiting).RegistrationId);
            Assert.Equal(1, reminder.Waiting[0].Order);

            // 遞補
            var promoted = await BizTest.ReadAsync<AdminRegistrationDetailDto>(await admin.PostAsync($"/api/v1/admin/bw/registrations/{r4.Id}/promote", null));
            Assert.Equal("已確認", promoted.Status);
            Assert.Equal(2, await EnrolledAsync(sessionId));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/v1/admin/bw/registrations/{r4.Id}/promote", null)).StatusCode);
            Assert.DoesNotContain(await Reminders(admin), r => r.SessionId == sessionId);

            // 批次：同狀態略過、非法狀態、空清單
            var same = await BizTest.ReadAsync<BatchOperationResultDto>(await admin.PostAsJsonAsync("/api/v1/admin/bw/registrations/batch/status",
                new BatchRegistrationStatusRequest { Ids = [r4.Id], Status = "已確認" }, TestJson.WriteOptions));
            Assert.Equal(0, same.UpdatedCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/bw/registrations/batch/status", new BatchRegistrationStatusRequest { Ids = [r4.Id], Status = "亂填" }, TestJson.WriteOptions)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/bw/registrations/batch/status", new BatchRegistrationStatusRequest { Ids = [], Status = "取消" }, TestJson.WriteOptions)).StatusCode);

            // 簽到表：只列會到場的人（r1 待確認、r4 已確認）
            var sheet = await BizTest.ReadAsync<AdminRegistrationSignInSheetDto>(await admin.GetAsync($"/api/v1/admin/bw/registrations/sign-in-sheet?sessionId={sessionId}"));
            Assert.Equal(new[] { "【測試】報名丁", "【測試】報名甲" }.OrderBy(n => n, StringComparer.Ordinal).ToArray(), sheet.Rows.Select(r => r.ApplicantName).ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/bw/registrations/sign-in-sheet")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/bw/registrations/sign-in-sheet?sessionId={Guid.NewGuid()}")).StatusCode);
            // 別的俱樂部的梯次 → 404（tcrfc 路徑找不到 bw 的梯次）
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/registrations/sign-in-sheet?sessionId={sessionId}")).StatusCode);
        }
        finally
        {
            await using var connection = new SqlConnection(ConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM registrations WHERE session_id IN (SELECT id FROM sessions WHERE program_id = @P);
                DELETE FROM sessions WHERE program_id = @P;
                DELETE FROM programs_i18n WHERE program_id = @P;
                DELETE FROM programs WHERE id = @P;
                """;
            command.Parameters.AddWithValue("@P", programId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<AdminWaitlistReminderDto>> Reminders(HttpClient client)
        => await BizTest.ReadAsync<List<AdminWaitlistReminderDto>>(await client.GetAsync("/api/v1/admin/bw/registrations/waitlist-reminders"));

    private static async Task<int> EnrolledAsync(Guid sessionId)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT enrolled_count FROM sessions WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", sessionId);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
