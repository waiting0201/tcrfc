using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Calendar;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-11：13 賽事行事曆公開讀取端點——合併 <c>matches</c>（既有
/// <c>Features/Schedule/MatchesRepository</c> 完全未改動）與**公開**的 <c>calendar_custom_events</c>
/// （<c>is_public = 1</c>），以及單場賽事 <c>.ics</c> 下載。不需要登入，形狀比照
/// <c>ScheduleOriginalDateTests</c>（打真正 HTTP 管線與真正 <c>tcrfc_club_dev</c>，測資自己寫入、
/// 自己刪除，不寫進 <c>db/seed</c>）。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CalendarPublicTests(ApiFixture fixture)
{
    [Fact]
    public async Task 列表模式_預設賽程分頁只回傳未來賽事()
    {
        var (clubId, seasonId, teamId) = await LookupTcrfcSeasonTeamAsync();
        var futureMatchId = Guid.NewGuid();
        var pastMatchId = Guid.NewGuid();

        try
        {
            await InsertMatchAsync(futureMatchId, clubId, seasonId, teamId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), "scheduled");
            await InsertMatchAsync(pastMatchId, clubId, seasonId, teamId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)), "played");

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=D1&pageSize=200", TestJson.Options);

            Assert.NotNull(result);
            Assert.Contains(result!.Items, i => i.Id == futureMatchId);
            Assert.DoesNotContain(result.Items, i => i.Id == pastMatchId);
        }
        finally
        {
            await DeleteMatchAsync(futureMatchId);
            await DeleteMatchAsync(pastMatchId);
        }
    }

    [Fact]
    public async Task 列表模式_賽果分頁只回傳過去賽事()
    {
        var (clubId, seasonId, teamId) = await LookupTcrfcSeasonTeamAsync();
        var pastMatchId = Guid.NewGuid();

        try
        {
            await InsertMatchAsync(pastMatchId, clubId, seasonId, teamId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), "played");

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=D1&mode=results&pageSize=200", TestJson.Options);

            Assert.NotNull(result);
            Assert.Contains(result!.Items, i => i.Id == pastMatchId);
        }
        finally
        {
            await DeleteMatchAsync(pastMatchId);
        }
    }

    [Fact]
    public async Task 俱樂部活動分頁只回傳公開自建事件_不含私密活動()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var publicEventId = Guid.NewGuid();
        var privateEventId = Guid.NewGuid();

        try
        {
            await InsertCustomEventAsync(publicEventId, clubId, DateTime.UtcNow.AddDays(10), isPublic: true, title: "公開活動");
            await InsertCustomEventAsync(privateEventId, clubId, DateTime.UtcNow.AddDays(10), isPublic: false, title: "私密活動");

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=club&pageSize=200", TestJson.Options);

            Assert.NotNull(result);
            Assert.Contains(result!.Items, i => i.Id == publicEventId);
            Assert.DoesNotContain(result.Items, i => i.Id == privateEventId);
        }
        finally
        {
            await DeleteCustomEventAsync(publicEventId);
            await DeleteCustomEventAsync(privateEventId);
        }
    }

    [Fact]
    public async Task 月曆模式_合併賽事與公開自建事件_排除私密活動()
    {
        var (clubId, seasonId, teamId) = await LookupTcrfcSeasonTeamAsync();
        var matchId = Guid.NewGuid();
        var publicEventId = Guid.NewGuid();
        var privateEventId = Guid.NewGuid();

        try
        {
            await InsertMatchAsync(matchId, clubId, seasonId, teamId, new DateOnly(2026, 12, 5), "scheduled");
            await InsertCustomEventAsync(publicEventId, clubId, new DateTime(2026, 12, 10, 10, 0, 0, DateTimeKind.Utc), isPublic: true, title: "公開活動");
            await InsertCustomEventAsync(privateEventId, clubId, new DateTime(2026, 12, 12, 10, 0, 0, DateTimeKind.Utc), isPublic: false, title: "私密活動");

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?from=2026-12-01&to=2027-01-01&pageSize=200", TestJson.Options);

            Assert.NotNull(result);
            Assert.Contains(result!.Items, i => i.SourceType == "match" && i.Id == matchId);
            Assert.Contains(result.Items, i => i.SourceType == "custom" && i.Id == publicEventId);
            Assert.DoesNotContain(result.Items, i => i.Id == privateEventId);
        }
        finally
        {
            await DeleteMatchAsync(matchId);
            await DeleteCustomEventAsync(publicEventId);
            await DeleteCustomEventAsync(privateEventId);
        }
    }

    [Fact]
    public async Task 只給from或只給to回400()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/tcrfc/calendar/events?from=2026-12-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 下載單場賽事ics_格式正確()
    {
        var (clubId, seasonId, teamId) = await LookupTcrfcSeasonTeamAsync();
        var matchId = Guid.NewGuid();

        try
        {
            await InsertMatchAsync(matchId, clubId, seasonId, teamId, new DateOnly(2026, 10, 3), "scheduled", kickoff: "19:00", opponent: "測試對手ICS");

            using var client = fixture.CreateClient();
            var response = await client.GetAsync($"/api/v1/tcrfc/matches/{matchId}/ics");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/calendar", response.Content.Headers.ContentType!.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("BEGIN:VCALENDAR\r\n", content);
            Assert.Contains("BEGIN:VEVENT\r\n", content);
            Assert.Contains($"UID:match-{matchId}@tcrfc\r\n", content);
            // 台灣 19:00（UTC+8）換算 UTC 應為 11:00。
            Assert.Contains("DTSTART:20261003T110000Z\r\n", content);
            Assert.Contains("測試對手ICS", content);
        }
        finally
        {
            await DeleteMatchAsync(matchId);
        }
    }

    [Fact]
    public async Task 下載不存在的賽事ics回404()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/tcrfc/matches/{Guid.NewGuid()}/ics");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═════════════════════════════ helpers ═════════════════════════════

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> LookupClubIdAsync(string clubCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM clubs WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", clubCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<(Guid ClubId, Guid SeasonId, Guid TeamId)> LookupTcrfcSeasonTeamAsync()
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id AS ClubId, s.id AS SeasonId, t.id AS TeamId
            FROM clubs c
            JOIN seasons s ON s.club_id = c.id AND s.code = N'2026-27'
            JOIN teams t ON t.club_id = c.id AND t.code = N'D1'
            WHERE c.code = N'tcrfc'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }

    private static async Task InsertMatchAsync(
        Guid matchId, Guid clubId, Guid seasonId, Guid teamId, DateOnly matchOn, string status,
        string kickoff = "19:00", string opponent = "測試對手（行事曆公開測試）")
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        await using (var insertMatch = connection.CreateCommand())
        {
            insertMatch.CommandText = """
                INSERT INTO matches (id, club_id, season_id, match_on, kickoff, home_away, opponent, status)
                VALUES (@Id, @ClubId, @SeasonId, @MatchOn, @Kickoff, N'主場', @Opponent, @Status);
                """;
            insertMatch.Parameters.AddWithValue("@Id", matchId);
            insertMatch.Parameters.AddWithValue("@ClubId", clubId);
            insertMatch.Parameters.AddWithValue("@SeasonId", seasonId);
            insertMatch.Parameters.AddWithValue("@MatchOn", matchOn.ToDateTime(TimeOnly.MinValue));
            insertMatch.Parameters.AddWithValue("@Kickoff", kickoff);
            insertMatch.Parameters.AddWithValue("@Opponent", opponent);
            insertMatch.Parameters.AddWithValue("@Status", status);
            await insertMatch.ExecuteNonQueryAsync();
        }

        await using var insertTeam = connection.CreateCommand();
        insertTeam.CommandText = "INSERT INTO match_teams (match_id, team_id) VALUES (@MatchId, @TeamId);";
        insertTeam.Parameters.AddWithValue("@MatchId", matchId);
        insertTeam.Parameters.AddWithValue("@TeamId", teamId);
        await insertTeam.ExecuteNonQueryAsync();
    }

    private static async Task DeleteMatchAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM matches WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertCustomEventAsync(Guid id, Guid clubId, DateTime startsAt, bool isPublic, string title)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.CommandText = """
                INSERT INTO calendar_custom_events (id, club_id, starts_at, is_all_day, is_public)
                VALUES (@Id, @ClubId, @StartsAt, 0, @IsPublic);
                """;
            insertEvent.Parameters.AddWithValue("@Id", id);
            insertEvent.Parameters.AddWithValue("@ClubId", clubId);
            insertEvent.Parameters.AddWithValue("@StartsAt", startsAt);
            insertEvent.Parameters.AddWithValue("@IsPublic", isPublic);
            await insertEvent.ExecuteNonQueryAsync();
        }

        await using var insertI18n = connection.CreateCommand();
        insertI18n.CommandText = "INSERT INTO calendar_custom_events_i18n (calendar_custom_event_id, locale, title) VALUES (@Id, N'zh-Hant', @Title);";
        insertI18n.Parameters.AddWithValue("@Id", id);
        insertI18n.Parameters.AddWithValue("@Title", title);
        await insertI18n.ExecuteNonQueryAsync();
    }

    private static async Task DeleteCustomEventAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM calendar_custom_events WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }
}
