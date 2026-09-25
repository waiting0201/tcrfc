using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminSessions;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-9：P1（課程／營隊項目）／P2（梯次與場次）／P3（報名管理）後台 CRUD ＋ 05 課程與活動的
/// 公開讀取與報名送出端點。形狀比照 <c>AdminTeamsPlayersStaffTests</c>／
/// <c>AdminMatchesAndStandingsTests</c>（同一批打真正 HTTP 管線與真正 <c>tcrfc_club_dev</c> 的
/// 既有先例）。
///
/// 種子測試帳號（見 apps/api/README.md「種子測試帳號」）：
/// <c>academy.manager@tcrfc.test</c>（<c>academy_program</c>，僅授權 <c>bw</c>，課程／報名 ✔全）；
/// <c>team.manager@tcrfc.test</c>（<c>team_competition</c>，僅授權 <c>tcrfc</c>，課程／報名唯讀）；
/// <c>customer.service@tcrfc.test</c>（<c>customer_service_admin</c>，僅授權 <c>tcrfc</c>，
/// 只有 <c>program.registration.view/update</c>，沒有課程項目／梯次的建立編輯權，也沒有匯出）；
/// <c>pr.media@tcrfc.test</c>（<c>pr_media</c>，僅授權 <c>tcrfc</c>，矩陣「課程／報名」欄是
/// 「—」，完全沒有 <c>program.*</c> 權限碼）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminProgramsSessionsRegistrationsTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════════ 權限矩陣 ═════════════════════════════

    [Fact]
    public async Task Programs_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/programs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Programs_跨俱樂部_擋下()
    {
        // academy.manager@tcrfc.test（academy_program）只被授權 bw，沒有 tcrfc。
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/programs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Programs_公關媒體角色矩陣是橫線_完全沒有課程權限_連檢視都被擋下()
    {
        using var client = await CreateClientAsync("pr.media@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/programs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Programs_競技球隊管理角色唯讀_建立會被擋下()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/programs");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var form = AdminArticleMultipart.Build(new CreateAdminProgramRequest
        {
            Slug = $"test-{Guid.NewGuid():N}",
            Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "唯讀角色測試" } },
        });
        var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/programs", form);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Registrations_客服角色只能處理報名_不能建立課程項目_也不能匯出()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");

        // 報名檢視／處理有權限。
        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/registrations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        // 課程項目建立沒有權限。
        var programForm = AdminArticleMultipart.Build(new CreateAdminProgramRequest
        {
            Slug = $"test-{Guid.NewGuid():N}",
            Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "客服角色測試" } },
        });
        var programResponse = await client.PostAsync("/api/v1/admin/tcrfc/programs", programForm);
        Assert.Equal(HttpStatusCode.Forbidden, programResponse.StatusCode);

        // 匯出（is_restricted）沒有指派給客服／行政，保守預設。
        var exportResponse = await client.GetAsync("/api/v1/admin/tcrfc/registrations/export");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }

    // ═════════════════════════════ P1 課程／營隊項目 ═════════════════════════════

    [Fact]
    public async Task Program_建立成功_網址名稱重複回409_類型與狀態值域驗證_可更新()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];

        try
        {
            var createForm = AdminArticleMultipart.Build(new CreateAdminProgramRequest
            {
                Slug = slug,
                ProgramType = "summer_camp",
                Audience = "混齡",
                AgeMin = 6,
                AgeMax = 12,
                Content = new AdminProgramContentInput
                {
                    Zh = new AdminProgramLocaleContent { Name = "測試夏令營", Intro = "簡介" },
                },
            });
            var createResponse = await client.PostAsync("/api/v1/admin/bw/programs", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminProgramDetailDto>(TestJson.Options);
            Assert.NotNull(created);
            Assert.Equal("draft", created!.Status); // 省略 Status 時預設草稿。

            // 網址名稱在同俱樂部重複要擋下（409）。
            var conflictForm = AdminArticleMultipart.Build(new CreateAdminProgramRequest
            {
                Slug = slug,
                Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "重複網址名稱" } },
            });
            var conflictResponse = await client.PostAsync("/api/v1/admin/bw/programs", conflictForm);
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            // 課程類型值域驗證。
            var badTypeForm = AdminArticleMultipart.Build(new CreateAdminProgramRequest
            {
                Slug = $"{slug}-bad-type",
                ProgramType = "not-a-real-type",
                Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "類型錯誤" } },
            });
            var badTypeResponse = await client.PostAsync("/api/v1/admin/bw/programs", badTypeForm);
            Assert.Equal(HttpStatusCode.BadRequest, badTypeResponse.StatusCode);

            // 年齡區間驗證（最小大於最大）。
            var badAgeForm = AdminArticleMultipart.Build(new CreateAdminProgramRequest
            {
                Slug = $"{slug}-bad-age",
                AgeMin = 10,
                AgeMax = 5,
                Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "年齡錯誤" } },
            });
            var badAgeResponse = await client.PostAsync("/api/v1/admin/bw/programs", badAgeForm);
            Assert.Equal(HttpStatusCode.BadRequest, badAgeResponse.StatusCode);

            // 更新：發布上架 ＋ 換內容。
            var updateForm = AdminArticleMultipart.Build(new UpdateAdminProgramRequest
            {
                Slug = slug,
                ProgramType = "summer_camp",
                Status = "published",
                Content = new AdminProgramContentInput
                {
                    Zh = new AdminProgramLocaleContent { Name = "測試夏令營（已發布）", Intro = "更新後的簡介" },
                },
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/bw/programs/{created.Id}", updateForm);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminProgramDetailDto>(TestJson.Options);
            Assert.Equal("published", updated!.Status);
            Assert.Equal("測試夏令營（已發布）", updated.Zh.Name);
        }
        finally
        {
            await DeleteProgramBySlugAsync(slug);
            await DeleteProgramBySlugAsync($"{slug}-bad-type");
            await DeleteProgramBySlugAsync($"{slug}-bad-age");
        }
    }

    // ═════════════════════════════ P2 梯次與場次 ═════════════════════════════

    [Fact]
    public async Task Session_建立與更新_名額額滿自動收斂狀態()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];
        Guid? programId = null;
        Guid? sessionId = null;

        try
        {
            programId = await CreateBwProgramAsync(client, slug, publish: true);

            var createResponse = await client.PostAsJsonAsync("/api/v1/admin/bw/program-sessions", new CreateAdminSessionRequest
            {
                ProgramId = programId.Value,
                Capacity = 1,
                Price = 1000,
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminSessionDetailDto>(TestJson.Options);
            Assert.NotNull(created);
            sessionId = created!.Id;
            Assert.Equal("開放", created.Status); // 名額未滿，預設推定為開放。
            Assert.Equal(0, created.EnrolledCount);

            // 狀態值域驗證。
            var badStatusResponse = await client.PutAsJsonAsync($"/api/v1/admin/bw/program-sessions/{sessionId}", new UpdateAdminSessionRequest
            {
                Capacity = 1,
                Status = "不存在的狀態",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badStatusResponse.StatusCode);

            // 日期區間驗證（開始晚於結束）。
            var badDateResponse = await client.PutAsJsonAsync($"/api/v1/admin/bw/program-sessions/{sessionId}", new UpdateAdminSessionRequest
            {
                Capacity = 1,
                StartOn = new DateOnly(2026, 12, 31),
                EndOn = new DateOnly(2026, 1, 1),
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badDateResponse.StatusCode);
        }
        finally
        {
            if (sessionId is Guid sid)
            {
                await DeleteSessionByIdAsync(sid);
            }
            await DeleteProgramBySlugAsync(slug);
        }
    }

    // ═════════════════════════════ P3 報名管理 ═════════════════════════════

    [Fact]
    public async Task Registration_後台代填_佔用名額_轉梯次連動調整新舊梯次名額_取消釋放名額()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];
        Guid programId = default;
        Guid sessionAId = default;
        Guid sessionBId = default;
        Guid? registrationId = null;

        try
        {
            programId = await CreateBwProgramAsync(client, slug, publish: true);
            sessionAId = await CreateBwSessionAsync(client, programId, capacity: 5);
            sessionBId = await CreateBwSessionAsync(client, programId, capacity: 5);

            var createResponse = await client.PostAsJsonAsync("/api/v1/admin/bw/registrations", new CreateAdminRegistrationRequest
            {
                SessionId = sessionAId,
                ApplicantName = "測試學員",
                Phone = "0912345678",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminRegistrationDetailDto>(TestJson.Options);
            Assert.NotNull(created);
            registrationId = created!.Id;
            Assert.Equal("待確認", created.Status); // 省略 Status 時預設待確認。
            Assert.StartsWith("BW-", created.RegistrationNo);

            Assert.Equal(1, await GetSessionEnrolledCountAsync(sessionAId));
            Assert.Equal(0, await GetSessionEnrolledCountAsync(sessionBId));

            // 轉梯次：A 退一位、B 佔一位。
            var transferResponse = await client.PutAsJsonAsync($"/api/v1/admin/bw/registrations/{registrationId}", new UpdateAdminRegistrationRequest
            {
                SessionId = sessionBId,
                ApplicantName = "測試學員",
                Phone = "0912345678",
                Status = "已確認",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);

            Assert.Equal(0, await GetSessionEnrolledCountAsync(sessionAId));
            Assert.Equal(1, await GetSessionEnrolledCountAsync(sessionBId));

            // 取消：釋放名額。
            var cancelResponse = await client.PutAsJsonAsync($"/api/v1/admin/bw/registrations/{registrationId}", new UpdateAdminRegistrationRequest
            {
                SessionId = sessionBId,
                ApplicantName = "測試學員",
                Phone = "0912345678",
                Status = "取消",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
            Assert.Equal(0, await GetSessionEnrolledCountAsync(sessionBId));
        }
        finally
        {
            if (registrationId is Guid rid)
            {
                await DeleteRegistrationByIdAsync(rid);
            }
            await DeleteSessionByIdAsync(sessionAId);
            await DeleteSessionByIdAsync(sessionBId);
            await DeleteProgramBySlugAsync(slug);
        }
    }

    [Fact]
    public async Task Registration_匯出CSV_有權限的角色可以匯出且格式正確()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/bw/registrations/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("報名編號", text); // BOM 之後的表頭。
        Assert.DoesNotContain("health_declaration", text, StringComparison.OrdinalIgnoreCase); // 刻意不含健康聲明欄。
    }

    // ═════════════════════════════ 05 課程與活動 公開讀取＋報名送出 ═════════════════════════════

    [Fact]
    public async Task Public_列表與詳情_只回已發布_草稿查不到()
    {
        using var adminClient = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];

        try
        {
            await CreateBwProgramAsync(adminClient, slug, publish: false); // 草稿。

            using var publicClient = fixture.CreateClient();
            var detailResponse = await publicClient.GetAsync($"/api/v1/bw/programs/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);

            var listResponse = await publicClient.GetAsync("/api/v1/bw/programs");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var list = await listResponse.Content.ReadFromJsonAsync<Tcrfc.Api.Common.PagedResult<ProgramListItemDto>>(TestJson.Options);
            Assert.DoesNotContain(list!.Items, p => p.Slug == slug);
        }
        finally
        {
            await DeleteProgramBySlugAsync(slug);
        }
    }

    [Fact]
    public async Task Public_報名送出_額滿後轉候補_已結束梯次拒絕報名()
    {
        using var adminClient = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];
        Guid programId = default;
        Guid sessionId = default;
        Guid endedSessionId = default;

        try
        {
            programId = await CreateBwProgramAsync(adminClient, slug, publish: true);
            sessionId = await CreateBwSessionAsync(adminClient, programId, capacity: 1);
            endedSessionId = await CreateBwSessionAsync(adminClient, programId, capacity: 1);

            // 標記第二個梯次為已結束。
            var endResponse = await adminClient.PutAsJsonAsync($"/api/v1/admin/bw/program-sessions/{endedSessionId}", new UpdateAdminSessionRequest
            {
                Capacity = 1,
                Status = "已結束",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

            using var publicClient = fixture.CreateClient();

            // 缺姓名 → 400。
            var missingNameResponse = await publicClient.PostAsJsonAsync(
                $"/api/v1/bw/programs/sessions/{sessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "", Phone = "0912345678" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, missingNameResponse.StatusCode);

            // 電話與 Email 都沒填 → 400。
            var missingContactResponse = await publicClient.PostAsJsonAsync(
                $"/api/v1/bw/programs/sessions/{sessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "訪客甲" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, missingContactResponse.StatusCode);

            // 第一位報名：名額 1、佔用成功 → 待確認。
            var first = await publicClient.PostAsJsonAsync(
                $"/api/v1/bw/programs/sessions/{sessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "訪客甲", Phone = "0911111111" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var firstBody = await first.Content.ReadFromJsonAsync<ProgramRegistrationSubmittedDto>(TestJson.Options);
            Assert.Equal("待確認", firstBody!.Status);

            Assert.Equal(1, await GetSessionEnrolledCountAsync(sessionId));
            Assert.Equal("額滿", await GetSessionStatusAsync(sessionId));

            // 第二位報名：名額已滿 → 候補，不佔用名額。
            var second = await publicClient.PostAsJsonAsync(
                $"/api/v1/bw/programs/sessions/{sessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "訪客乙", Phone = "0922222222" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            var secondBody = await second.Content.ReadFromJsonAsync<ProgramRegistrationSubmittedDto>(TestJson.Options);
            Assert.Equal("候補", secondBody!.Status);
            Assert.Equal(1, await GetSessionEnrolledCountAsync(sessionId)); // 候補不佔名額，數字不變。

            // 已結束的梯次拒絕報名。
            var endedResponse = await publicClient.PostAsJsonAsync(
                $"/api/v1/bw/programs/sessions/{endedSessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "訪客丙", Phone = "0933333333" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, endedResponse.StatusCode);
        }
        finally
        {
            await DeleteRegistrationsBySessionAsync(sessionId);
            await DeleteRegistrationsBySessionAsync(endedSessionId);
            await DeleteSessionByIdAsync(sessionId);
            await DeleteSessionByIdAsync(endedSessionId);
            await DeleteProgramBySlugAsync(slug);
        }
    }

    [Fact]
    public async Task Public_報名送出_跨俱樂部梯次找不到()
    {
        using var adminClient = await CreateClientAsync("academy.manager@tcrfc.test");
        var slug = $"test-program-{Guid.NewGuid():N}"[..30];
        Guid programId = default;
        Guid sessionId = default;

        try
        {
            programId = await CreateBwProgramAsync(adminClient, slug, publish: true);
            sessionId = await CreateBwSessionAsync(adminClient, programId, capacity: 5);

            using var publicClient = fixture.CreateClient();
            // 這個梯次屬於 bw，用 tcrfc 的網址去報名應該 404。
            var response = await publicClient.PostAsJsonAsync(
                $"/api/v1/tcrfc/programs/sessions/{sessionId}/registrations",
                new SubmitProgramRegistrationRequest { ApplicantName = "訪客", Phone = "0912345678" }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await DeleteRegistrationsBySessionAsync(sessionId);
            await DeleteSessionByIdAsync(sessionId);
            await DeleteProgramBySlugAsync(slug);
        }
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateBwProgramAsync(HttpClient client, string slug, bool publish)
    {
        var form = AdminArticleMultipart.Build(new CreateAdminProgramRequest
        {
            Slug = slug,
            ProgramType = "summer_camp",
            Status = publish ? "published" : "draft",
            Content = new AdminProgramContentInput { Zh = new AdminProgramLocaleContent { Name = "測試課程", Intro = "測試簡介" } },
        });
        var response = await client.PostAsync("/api/v1/admin/bw/programs", form);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminProgramDetailDto>(TestJson.Options);
        return created!.Id;
    }

    private static async Task<Guid> CreateBwSessionAsync(HttpClient client, Guid programId, int capacity)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/bw/program-sessions", new CreateAdminSessionRequest
        {
            ProgramId = programId,
            Capacity = capacity,
            Price = 500,
        }, TestJson.WriteOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminSessionDetailDto>(TestJson.Options);
        return created!.Id;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<int> GetSessionEnrolledCountAsync(Guid sessionId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT enrolled_count FROM sessions WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", sessionId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> GetSessionStatusAsync(Guid sessionId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM sessions WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", sessionId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteProgramBySlugAsync(string slug)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Id uniqueidentifier = (SELECT id FROM programs WHERE slug = @Slug);
            DELETE FROM registrations WHERE session_id IN (SELECT id FROM sessions WHERE program_id = @Id);
            DELETE FROM sessions WHERE program_id = @Id;
            DELETE FROM program_staff WHERE program_id = @Id;
            DELETE FROM program_partners WHERE program_id = @Id;
            DELETE FROM programs_i18n WHERE program_id = @Id;
            DELETE FROM programs WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Slug", slug);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteSessionByIdAsync(Guid id)
    {
        if (id == default)
        {
            return;
        }
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM registrations WHERE session_id = @Id;
            DELETE FROM sessions WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteRegistrationByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM registrations WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteRegistrationsBySessionAsync(Guid sessionId)
    {
        if (sessionId == default)
        {
            return;
        }
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM registrations WHERE session_id = @Id";
        command.Parameters.AddWithValue("@Id", sessionId);
        await command.ExecuteNonQueryAsync();
    }
}
