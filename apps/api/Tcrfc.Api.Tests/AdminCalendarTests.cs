using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-11：L1 行事曆總覽（合併讀取 <c>matches</c>／<c>calendar_custom_events</c>）／L2 自建事件
/// 後台 CRUD。形狀比照 <c>AdminMatchesAndStandingsTests</c>／<c>AdminProgramsSessionsRegistrationsTests</c>，
/// 打真正 HTTP 管線與真正 <c>tcrfc_club_dev</c>。
///
/// 種子測試帳號（見 apps/api/README.md「種子測試帳號」與「S1-11」段的權限指派）：
/// <c>content.editor@tcrfc.test</c>（<c>content_editor</c>，僅授權 <c>tcrfc</c>，
/// <c>calendar.view</c>＋<c>calendar.custom_event.*</c> 全給，矩陣「自建事件」）；
/// <c>team.manager@tcrfc.test</c>（<c>team_competition</c>，僅授權 <c>tcrfc</c>，只有
/// <c>calendar.view</c>，矩陣「賽事事件」對應的是既有 <c>team.match.*</c>，不是本模組的權限碼）；
/// <c>viewer@tcrfc.test</c>（<c>viewer</c>，僅授權 <c>tcrfc</c>，<c>calendar.view</c>＋
/// <c>calendar.custom_event.view</c> 唯讀）；
/// <c>partner.club@tcrfc.test</c>（<c>partner_club_manager</c>，僅授權 <c>bw</c>，<c>own_clubs</c>，
/// 自建事件全給）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminCalendarTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════════ 權限矩陣 ═════════════════════════════

    [Fact]
    public async Task Calendar_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-10-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_跨俱樂部_擋下()
    {
        // content.editor@tcrfc.test 只被授權 tcrfc，沒有 bw。
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/bw/calendar/events?from=2026-10-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_檢視者能看總覽_但不能建立自建事件()
    {
        using var client = await CreateClientAsync("viewer@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-10-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var customListResponse = await client.GetAsync("/api/v1/admin/tcrfc/calendar/custom-events");
        Assert.Equal(HttpStatusCode.OK, customListResponse.StatusCode);

        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events", AdminArticleMultipart.Build(NewEventRequest()));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Calendar_競技球隊管理只有總覽權限_矩陣賽事事件對應既有teammatch權限碼()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-10-01&to=2026-11-01");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var customListResponse = await client.GetAsync("/api/v1/admin/tcrfc/calendar/custom-events");
        Assert.Equal(HttpStatusCode.Forbidden, customListResponse.StatusCode);
    }

    // ═════════════════════════════ L2 CRUD ═════════════════════════════

    [Fact]
    public async Task CustomEvent_建立成功_可取得_可更新_可刪除()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events",
            AdminArticleMultipart.Build(NewEventRequest(teamIds: [teamId])));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);
        Assert.NotNull(created);

        try
        {
            var getResponse = await client.GetAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);
            Assert.Equal("測試記者會", fetched!.Zh.Title);
            Assert.Equal([teamId], fetched.TeamIds);
            Assert.Equal(["D1"], fetched.TeamCodes);

            var updateRequest = new UpdateAdminCalendarCustomEventRequest
            {
                StartsAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc),
                IsAllDay = false,
                IsPublic = true,
                TeamIds = [],
                Content = new AdminCalendarEventContentInput { Zh = new AdminCalendarEventLocaleContent { Title = "測試記者會（更新後）" } },
            };
            var updateResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/calendar/custom-events/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);
            Assert.Equal("測試記者會（更新後）", updated!.Zh.Title);
            Assert.Empty(updated.TeamIds); // 空陣列＝清空為「俱樂部活動」
        }
        finally
        {
            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        var afterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        Assert.Equal(0, await CountEventTeamLinksAsync(created.Id));
    }

    [Fact]
    public async Task CustomEvent_中文標題必填_球隊不屬於本俱樂部回400()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");

        var blankTitle = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events",
            AdminArticleMultipart.Build(NewEventRequest() with { Content = new AdminCalendarEventContentInput { Zh = new AdminCalendarEventLocaleContent { Title = "" } } }));
        Assert.Equal(HttpStatusCode.BadRequest, blankTitle.StatusCode);

        var bwTeamId = await GetTeamIdAsync("bw", "BW1");
        var wrongClubTeam = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events",
            AdminArticleMultipart.Build(NewEventRequest(teamIds: [bwTeamId])));
        Assert.Equal(HttpStatusCode.BadRequest, wrongClubTeam.StatusCode);
    }

    [Fact]
    public async Task CustomEvent_跨俱樂部更新刪除回404_不洩漏存在與否()
    {
        using var editorClient = await CreateClientAsync("content.editor@tcrfc.test");
        var createResponse = await editorClient.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events", AdminArticleMultipart.Build(NewEventRequest()));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);

        try
        {
            using var partnerClient = await CreateClientAsync("partner.club@tcrfc.test"); // 僅授權 bw
            var getResponse = await partnerClient.GetAsync($"/api/v1/admin/bw/calendar/custom-events/{created!.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            var deleteResponse = await partnerClient.DeleteAsync($"/api/v1/admin/bw/calendar/custom-events/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }
        finally
        {
            await editorClient.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created!.Id}");
        }
    }

    [Fact]
    public async Task EventTypes_可列出六個起始分類()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/calendar/event-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<AdminEventTypeDto>>(TestJson.Options);
        Assert.NotNull(items);
        Assert.Contains(items!, t => t.Code == "press_conference");
        Assert.Contains(items!, t => t.Code == "other");
    }

    // ═════════════════════════════ L1 合併總覽 ═════════════════════════════

    [Fact]
    public async Task Overview_合併賽事與自建事件()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        var matchId = Guid.NewGuid();
        await InsertMatchAsync(matchId, seasonId, teamId, new DateOnly(2026, 10, 15));

        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events",
            AdminArticleMultipart.Build(NewEventRequest(startsAt: new DateTime(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc))));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);

        try
        {
            var response = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-10-01&to=2026-11-01");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = await response.Content.ReadFromJsonAsync<List<AdminCalendarEventDto>>(TestJson.Options);
            Assert.NotNull(items);
            Assert.Contains(items!, i => i.SourceType == "match" && i.SourceId == matchId);
            Assert.Contains(items!, i => i.SourceType == "custom" && i.SourceId == created!.Id);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created!.Id}");
            await DeleteMatchByIdAsync(matchId);
        }
    }

    [Fact]
    public async Task Overview_每週重複事件展開出多次()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");

        var payload = NewEventRequest(startsAt: new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc)) with
        {
            RepeatRule = "weekly",
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/calendar/custom-events", AdminArticleMultipart.Build(payload));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);

        try
        {
            var response = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-11-01&to=2026-12-01");
            var items = await response.Content.ReadFromJsonAsync<List<AdminCalendarEventDto>>(TestJson.Options);
            var occurrences = items!.Where(i => i.SourceType == "custom" && i.SourceId == created!.Id).ToList();
            // 11/7、11/14、11/21、11/28 落在 [11/1, 12/1)。
            Assert.Equal(4, occurrences.Count);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created!.Id}");
        }
    }

    [Fact]
    public async Task Overview_查詢範圍超過366天回400()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/calendar/events?from=2026-01-01&to=2028-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═════════════════════════════ helpers ═════════════════════════════

    private static CreateAdminCalendarCustomEventRequest NewEventRequest(
        DateTime? startsAt = null, IReadOnlyList<Guid>? teamIds = null) => new()
    {
        StartsAt = startsAt ?? new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc),
        IsAllDay = false,
        IsPublic = true,
        TeamIds = teamIds,
        Content = new AdminCalendarEventContentInput { Zh = new AdminCalendarEventLocaleContent { Title = "測試記者會" } },
    };

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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

    private static async Task InsertMatchAsync(Guid matchId, Guid seasonId, Guid teamId, DateOnly matchOn)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        var clubId = await GetTeamClubIdAsync(connection, teamId);

        await using (var insertMatch = connection.CreateCommand())
        {
            insertMatch.CommandText = """
                INSERT INTO matches (id, club_id, season_id, match_on, kickoff, home_away, opponent, status)
                VALUES (@Id, @ClubId, @SeasonId, @MatchOn, '19:00', N'主場', N'測試對手（行事曆合併測試）', 'scheduled');
                """;
            insertMatch.Parameters.AddWithValue("@Id", matchId);
            insertMatch.Parameters.AddWithValue("@ClubId", clubId);
            insertMatch.Parameters.AddWithValue("@SeasonId", seasonId);
            insertMatch.Parameters.AddWithValue("@MatchOn", matchOn.ToDateTime(TimeOnly.MinValue));
            await insertMatch.ExecuteNonQueryAsync();
        }

        await using var insertTeam = connection.CreateCommand();
        insertTeam.CommandText = "INSERT INTO match_teams (match_id, team_id) VALUES (@MatchId, @TeamId);";
        insertTeam.Parameters.AddWithValue("@MatchId", matchId);
        insertTeam.Parameters.AddWithValue("@TeamId", teamId);
        await insertTeam.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> GetTeamClubIdAsync(SqlConnection connection, Guid teamId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT club_id FROM teams WHERE id = @TeamId";
        command.Parameters.AddWithValue("@TeamId", teamId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteMatchByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM matches WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountEventTeamLinksAsync(Guid customEventId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM calendar_event_teams WHERE source_type = N'custom' AND source_id = @Id";
        command.Parameters.AddWithValue("@Id", customEventId);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
