using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-12c：GEO-05 結構化資料完整性檢查——後台報表（<c>seo.schema.view</c>，
/// <see cref="AdminSeoSchemaCompletenessRepository"/>）與已接上輸出的兩個公開型別
/// （<c>Article</c>／<c>SportsEvent</c> 的 <c>schemaEligible</c> 欄位）。判斷邏輯本身的純單元測試
/// 在 <see cref="SchemaCompletenessTests"/>，這裡只驗證「資料庫查出來的值有沒有正確接上判斷、
/// 正反例會不會如預期出現／消失」。打真正的 HTTP 管線與真正的 <c>tcrfc_club_test</c>，不 mock。
/// 🔴 用 <see cref="AdminWriteAzuriteEnabledApiFixture"/>（不是普通的 <see cref="AdminWriteApiFixture"/>）：
/// Article 正反例要真的上傳一張 OG 圖片，跟 <c>AdminSeoImageTests</c> 同一個理由，需要真正的
/// Azurite 容器（<c>IImageStorageService</c> 不 mock）。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminSeoSchemaCompletenessTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private const string ReportPath = "/api/v1/admin/tcrfc/seo/schema-completeness";

    // ═════════════════════════════ 基本授權 ═════════════════════════════

    [Fact]
    public async Task Report_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ReportPath)).StatusCode);
    }

    [Fact]
    public async Task Report_內容編輯角色_沒有sysadminonly權限_403()
    {
        // 矩陣「SEO／設定」欄除了內容編輯的「單頁 SEO」外，十個角色裡只有系統管理員打勾——
        // seo.schema.view 是 sysadmin_only，理由跟既有 seo.setting.*／seo.report.* 一致
        // （見 AdminSeoSchemaCompletenessEndpoints 檔頭）。
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(ReportPath)).StatusCode);
    }

    // ═════════════════════════════ SportsEvent（matches）正反例 ═════════════════════════════

    [Fact]
    public async Task SportsEvent_缺開球時間主客場場地賽事名稱_報表列出_公開端點不輸出_補齊後兩者恢復()
    {
        using var teamManager = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        // 建立時刻意不帶 Kickoff／HomeAway／Venue／CompetitionId——比照既有
        // AdminMatchesAndStandingsTests.NewMatchRequest 的最小欄位集合，自然產生一筆
        // SportsEvent 必填欄位缺漏的資料，不需要另外用 SQL 竄改。
        var createResponse = await teamManager.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", new CreateAdminMatchRequest
        {
            SeasonId = seasonId,
            TeamIds = [teamId],
            MatchOn = new DateOnly(2026, 11, 20),
            Opponent = $"S1-12c 測試對手 {Guid.NewGuid():N}",
            Status = "scheduled",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options))!;

        try
        {
            using var superAdmin = await CreateClientAsync("super.admin@tcrfc.test");

            // ── 缺漏時：報表列出，且列出的缺漏欄位剛好是那四個 ──────────────────
            var reportBefore = await GetSchemaReportAsync(superAdmin, "tcrfc");
            var issueBefore = Assert.Single(reportBefore.Items, i => i.SchemaTypeName == "SportsEvent" && i.Id == created.Id);
            Assert.Equal("match", issueBefore.EntityType);
            var missingLabelsBefore = issueBefore.MissingFields.Select(f => f.LabelZh).ToHashSet();
            Assert.Equal(new HashSet<string> { "開球時間", "主客場", "場地", "賽事名稱" }, missingLabelsBefore);

            // ── 缺漏時：公開端點（schedule 列表）的 schemaEligible 為 false ──────────────
            using var publicClient = fixture.CreateClient();
            var scheduleBefore = await GetPublicScheduleAsync(publicClient, created.Id);
            Assert.False(scheduleBefore!.SchemaEligible);

            // ── 補齊四個欄位後：報表不再列出，公開端點 schemaEligible 恢復 true ──────────
            var competitionId = await GetCompetitionIdAsync("tcrfc", "enterprise-a");
            var updateResponse = await teamManager.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{created.Id}", new UpdateAdminMatchRequest
            {
                SeasonId = seasonId,
                CompetitionId = competitionId,
                TeamIds = [teamId],
                MatchOn = created.MatchOn,
                Kickoff = "19:00",
                HomeAway = "HOME",
                Opponent = created.Opponent!,
                Venue = "測試主場",
                Status = "scheduled",
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var reportAfter = await GetSchemaReportAsync(superAdmin, "tcrfc");
            Assert.DoesNotContain(reportAfter.Items, i => i.SchemaTypeName == "SportsEvent" && i.Id == created.Id);

            var scheduleAfter = await GetPublicScheduleAsync(publicClient, created.Id);
            Assert.True(scheduleAfter!.SchemaEligible);

            // ── 跨俱樂部：bw 的報表看不到 tcrfc 這筆測試資料 ──────────────────────────
            var bwReport = await GetSchemaReportAsync(superAdmin, "bw");
            Assert.DoesNotContain(bwReport.Items, i => i.Id == created.Id);
        }
        finally
        {
            var deleteResponse = await teamManager.DeleteAsync($"/api/v1/admin/tcrfc/matches/{created.Id}");
            Assert.True(deleteResponse.IsSuccessStatusCode, $"清理測試賽事失敗：{deleteResponse.StatusCode}");
        }
    }

    // ═════════════════════════════ Article（news）正反例 ═════════════════════════════

    [Fact]
    public async Task Article_缺圖片_報表列出_公開端點不輸出_補上OG圖片後兩者恢復()
    {
        // 🔴 這個情境要確保「全站預設 OG 圖片」在測試期間確實是 null，否則即使這篇文章自己沒有
        // 圖片，也會透過三層優先序的第二層（clubs.og_image_key）意外變成「有圖」，讓這個測試
        // 在別的測試留下全站預設圖時變得不穩定。比照既有 AdminSeoTests 的「先讀出原值、
        // finally 還原」既有紀律，不是本測試自己發明的做法。
        var originalClubOgImage = await CaptureClubOgImageAsync("tcrfc");
        await SetClubOgImageAsync("tcrfc", null, null, null);

        using var editor = await CreateClientAsync("content.editor@tcrfc.test");
        var slug = $"s1-12c-schema-{Guid.NewGuid():N}";

        var createResponse = await editor.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"GEO-05 測試 {slug}" } },
        }));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        var publishResponse = await editor.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();
        var published = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            using var superAdmin = await CreateClientAsync("super.admin@tcrfc.test");

            // ── 缺漏時：報表列出，缺漏欄位只有「文章圖片」（標題／發布時間都已具備）───────
            var reportBefore = await GetSchemaReportAsync(superAdmin, "tcrfc");
            var issueBefore = Assert.Single(reportBefore.Items, i => i.SchemaTypeName == "Article" && i.Id == created.Id);
            Assert.Equal("article", issueBefore.EntityType);
            Assert.Equal($"/zh/news/{slug}/", issueBefore.Path);
            Assert.Equal(["文章圖片"], issueBefore.MissingFields.Select(f => f.LabelZh).ToList());

            // ── 缺漏時：公開端點（新聞詳情）schemaEligible 為 false ─────────────────────
            using var publicClient = fixture.CreateClient();
            var detailBefore = await GetPublicArticleAsync(publicClient, slug);
            Assert.False(detailBefore!.SchemaEligible);

            // ── 補上這篇專屬的 OG 圖片後：報表不再列出，公開端點恢復 true ────────────────
            var updateForm = new MultipartFormDataContent();
            var payloadJson = JsonSerializer.Serialize(new UpdateArticleRequest
            {
                Slug = slug,
                CategoryCode = "club",
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"GEO-05 測試 {slug}" } },
                ExpectedUpdatedAt = published.UpdatedAt,
            }, TestJson.WriteOptions);
            var payloadContent = new StringContent(payloadJson, Encoding.UTF8);
            payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            updateForm.Add(payloadContent, "payload");
            var ogImageContent = new ByteArrayContent(TestImages.SmallPng());
            ogImageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            updateForm.Add(ogImageContent, "ogImage", "og.png");

            var updateResponse = await editor.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", updateForm);
            var updateBody = await updateResponse.Content.ReadAsStringAsync();
            Assert.True(updateResponse.StatusCode == HttpStatusCode.OK, $"預期 200，實際 {updateResponse.StatusCode}：{updateBody}");
            var updated = JsonSerializer.Deserialize<AdminArticleDetailDto>(updateBody, TestJson.Options)!;

            var reportAfter = await GetSchemaReportAsync(superAdmin, "tcrfc");
            Assert.DoesNotContain(reportAfter.Items, i => i.SchemaTypeName == "Article" && i.Id == created.Id);

            var detailAfter = await GetPublicArticleAsync(publicClient, slug);
            Assert.True(detailAfter!.SchemaEligible);

            published = updated; // 給 finally 的刪除用最新的 updatedAt 當並行權杖。
        }
        finally
        {
            var deleteResponse = await editor.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(published.UpdatedAt.ToString("o"))}");
            Assert.True(deleteResponse.IsSuccessStatusCode, $"清理測試文章失敗：{deleteResponse.StatusCode}");

            await SetClubOgImageAsync("tcrfc", originalClubOgImage.Key, originalClubOgImage.Width, originalClubOgImage.Height);
        }
    }

    // ═════════════════════════════ 共用小工具 ═════════════════════════════

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<SchemaCompletenessReportDto> GetSchemaReportAsync(HttpClient client, string clubCode)
    {
        var response = await client.GetAsync($"/api/v1/admin/{clubCode}/seo/schema-completeness");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"預期 200，實際 {response.StatusCode}：{body}");
        return JsonSerializer.Deserialize<SchemaCompletenessReportDto>(body, TestJson.Options)!;
    }

    private static async Task<MatchDto?> GetPublicScheduleAsync(HttpClient client, Guid matchId)
    {
        var response = await client.GetAsync("/api/v1/tcrfc/schedule?pageSize=200&lang=zh");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MatchDto>>(TestJson.Options);
        return page!.Items.SingleOrDefault(m => m.Id == matchId);
    }

    private static async Task<ArticleDetailDto?> GetPublicArticleAsync(HttpClient client, string slug)
    {
        var response = await client.GetAsync($"/api/v1/tcrfc/news/{slug}?lang=zh");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<ArticleDetailDto>(TestJson.Options);
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetSeasonIdAsync(string clubCode, string seasonCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id
            WHERE c.code = @ClubCode AND s.code = @SeasonCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@SeasonCode", seasonCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetTeamIdAsync(string clubCode, string teamCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id
            WHERE c.code = @ClubCode AND t.code = @TeamCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@TeamCode", teamCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetCompetitionIdAsync(string clubCode, string competitionCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT co.id FROM competitions co JOIN clubs c ON c.id = co.club_id
            WHERE c.code = @ClubCode AND co.code = @CompetitionCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@CompetitionCode", competitionCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private readonly record struct ClubOgImage(string? Key, int? Width, int? Height);

    private static async Task<ClubOgImage> CaptureClubOgImageAsync(string clubCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT og_image_key, og_image_width, og_image_height FROM clubs WHERE code = @ClubCode";
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new ClubOgImage(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2));
    }

    private static async Task SetClubOgImageAsync(string clubCode, string? key, int? width, int? height)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clubs SET og_image_key = @Key, og_image_width = @Width, og_image_height = @Height
            WHERE code = @ClubCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@Key", (object?)key ?? DBNull.Value);
        command.Parameters.AddWithValue("@Width", (object?)width ?? DBNull.Value);
        command.Parameters.AddWithValue("@Height", (object?)height ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }
}
