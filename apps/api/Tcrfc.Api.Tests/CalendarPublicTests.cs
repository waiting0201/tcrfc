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
/// <c>ScheduleOriginalDateTests</c>（打真正 HTTP 管線與真正 <c>tcrfc_club</c>，測資自己寫入、
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

    // ───────── A-9／B-6：展開重複規則、team 篩選、活動類型 ─────────

    [Fact]
    public async Task 俱樂部活動分頁_每週重複活動展開_排除例外日_帶原id與唯一key()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var eventId = Guid.NewGuid();
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var skipped = DateOnly.FromDateTime(start.AddDays(7));

        try
        {
            await InsertCustomEventAsync(eventId, clubId, start, isPublic: true, title: "每週活動A9",
                repeatRule: "weekly", repeatUntil: DateOnly.FromDateTime(start.AddDays(22)));
            await ExecAsync("INSERT INTO calendar_event_exceptions (calendar_custom_event_id, excluded_on) VALUES (@Id, @D);",
                ("@Id", eventId), ("@D", skipped.ToDateTime(TimeOnly.MinValue)));

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=club&pageSize=101", TestJson.Options);

            var mine = result!.Items.Where(i => i.Id == eventId).OrderBy(i => i.StartsAt).ToList();
            // start、+14、+21（+28 超過 repeat_until；+7 是例外日）
            Assert.Equal(3, mine.Count);
            Assert.Equal(new[] { start, start.AddDays(14), start.AddDays(21) }, mine.Select(i => i.StartsAt).ToArray());
            Assert.All(mine, i => Assert.True(i.IsRecurring));
            Assert.Equal(3, mine.Select(i => i.OccurrenceId).Distinct().Count());
        }
        finally
        {
            await DeleteCustomEventAsync(eventId);
        }
    }

    [Fact]
    public async Task 月曆模式_區間邊界_to不含_from含()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var eventId = Guid.NewGuid();
        // 每週重複，起點 2031-03-03 00:00（上限 repeat 不設）
        var start = new DateTime(2031, 3, 3, 0, 0, 0, DateTimeKind.Utc);

        try
        {
            await InsertCustomEventAsync(eventId, clubId, start, isPublic: true, title: "邊界活動A9", repeatRule: "weekly");

            using var client = fixture.CreateClient();
            // [3/10, 3/17) 只含 3/10 一次；3/3 在 from 之前、3/17 等於 to（不含）。
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?from=2031-03-10&to=2031-03-17&pageSize=102", TestJson.Options);

            var mine = result!.Items.Where(i => i.Id == eventId).ToList();
            var only = Assert.Single(mine);
            Assert.Equal(new DateTime(2031, 3, 10, 0, 0, 0, DateTimeKind.Utc), only.StartsAt);
        }
        finally
        {
            await DeleteCustomEventAsync(eventId);
        }
    }

    [Fact]
    public async Task team篩選_club只回未掛球隊_球隊代碼只回掛該隊()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var (_, _, teamId) = await LookupTcrfcSeasonTeamAsync();
        var clubEvent = Guid.NewGuid();
        var teamEvent = Guid.NewGuid();
        var when = new DateTime(2031, 6, 10, 10, 0, 0, DateTimeKind.Utc);

        try
        {
            await InsertCustomEventAsync(clubEvent, clubId, when, isPublic: true, title: "俱樂部活動A9");
            await InsertCustomEventAsync(teamEvent, clubId, when, isPublic: true, title: "D1活動A9");
            await ExecAsync("INSERT INTO calendar_event_teams (source_type, source_id, team_id) VALUES (N'custom', @Id, @T);",
                ("@Id", teamEvent), ("@T", teamId));

            using var client = fixture.CreateClient();
            var club = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=club&from=2031-06-01&to=2031-07-01&pageSize=103", TestJson.Options);
            Assert.Contains(club!.Items, i => i.Id == clubEvent);
            Assert.DoesNotContain(club.Items, i => i.Id == teamEvent);

            var d1 = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=D1&from=2031-06-01&to=2031-07-01&pageSize=104", TestJson.Options);
            Assert.Contains(d1!.Items, i => i.Id == teamEvent);
            Assert.DoesNotContain(d1.Items, i => i.Id == clubEvent);
        }
        finally
        {
            await ExecAsync("DELETE FROM calendar_event_teams WHERE source_id IN (@A, @B);", ("@A", clubEvent), ("@B", teamEvent));
            await DeleteCustomEventAsync(clubEvent);
            await DeleteCustomEventAsync(teamEvent);
        }
    }

    [Fact]
    public async Task 活動類型不公開_其下公開活動不輸出_公開類型帶名稱色彩圖示()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var hiddenType = Guid.NewGuid();
        var openType = Guid.NewGuid();
        var hiddenEvent = Guid.NewGuid();
        var openEvent = Guid.NewGuid();
        var when = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        try
        {
            await ExecAsync("INSERT INTO event_types (id, code, colour, icon, is_public) VALUES (@Id, @C, N'#112233', N'star', 0);",
                ("@Id", hiddenType), ("@C", "h" + suffix));
            await ExecAsync("INSERT INTO event_types (id, code, colour, icon, is_public) VALUES (@Id, @C, N'#C8102E', N'ball', 1);",
                ("@Id", openType), ("@C", "o" + suffix));
            await ExecAsync("INSERT INTO event_types_i18n (event_type_id, locale, name) VALUES (@Id, N'zh-Hant', N'公開類型A9'), (@Id, N'en', N'Open Type');",
                ("@Id", openType));
            await InsertCustomEventAsync(hiddenEvent, clubId, when, isPublic: true, title: "隱藏類型活動A9");
            await InsertCustomEventAsync(openEvent, clubId, when, isPublic: true, title: "公開類型活動A9");
            await ExecAsync("UPDATE calendar_custom_events SET event_type_id = @T WHERE id = @Id;", ("@T", hiddenType), ("@Id", hiddenEvent));
            await ExecAsync("UPDATE calendar_custom_events SET event_type_id = @T WHERE id = @Id;", ("@T", openType), ("@Id", openEvent));

            using var client = fixture.CreateClient();
            var zh = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=club&pageSize=105", TestJson.Options);
            Assert.DoesNotContain(zh!.Items, i => i.Id == hiddenEvent);
            var open = Assert.Single(zh.Items, i => i.Id == openEvent);
            Assert.Equal("公開類型A9", open.EventTypeName);
            Assert.Equal("#C8102E", open.EventTypeColour);
            Assert.Equal("ball", open.EventTypeIcon);

            var en = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?team=club&lang=en&pageSize=106", TestJson.Options);
            Assert.Equal("Open Type", Assert.Single(en!.Items, i => i.Id == openEvent).EventTypeName);
        }
        finally
        {
            await DeleteCustomEventAsync(hiddenEvent);
            await DeleteCustomEventAsync(openEvent);
            await ExecAsync("DELETE FROM event_types_i18n WHERE event_type_id IN (@A, @B);", ("@A", hiddenType), ("@B", openType));
            await ExecAsync("DELETE FROM event_types WHERE id IN (@A, @B);", ("@A", hiddenType), ("@B", openType));
        }
    }

    [Fact]
    public async Task 月曆模式_重複活動的例外日不出現()
    {
        var clubId = await LookupClubIdAsync("tcrfc");
        var eventId = Guid.NewGuid();
        var start = new DateTime(2032, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        try
        {
            await InsertCustomEventAsync(eventId, clubId, start, isPublic: true, title: "例外日活動A9", repeatRule: "weekly");
            await ExecAsync("INSERT INTO calendar_event_exceptions (calendar_custom_event_id, excluded_on) VALUES (@Id, @D);",
                ("@Id", eventId), ("@D", new DateTime(2032, 1, 12)));

            using var client = fixture.CreateClient();
            var result = await client.GetFromJsonAsync<PagedResult<PublicCalendarEventDto>>(
                "/api/v1/tcrfc/calendar/events?from=2032-01-01&to=2032-01-27&pageSize=107", TestJson.Options);

            var days = result!.Items.Where(i => i.Id == eventId).Select(i => i.StartsAt.Day).OrderBy(d => d).ToArray();
            Assert.Equal(new[] { 5, 19, 26 }, days);
        }
        finally
        {
            await DeleteCustomEventAsync(eventId);
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

    private static async Task InsertCustomEventAsync(
        Guid id, Guid clubId, DateTime startsAt, bool isPublic, string title,
        string? repeatRule = null, DateOnly? repeatUntil = null)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.CommandText = """
                INSERT INTO calendar_custom_events (id, club_id, starts_at, is_all_day, is_public, repeat_rule, repeat_until)
                VALUES (@Id, @ClubId, @StartsAt, 0, @IsPublic, @RepeatRule, @RepeatUntil);
                """;
            insertEvent.Parameters.AddWithValue("@RepeatRule", (object?)repeatRule ?? DBNull.Value);
            insertEvent.Parameters.AddWithValue("@RepeatUntil", repeatUntil is { } ru ? ru.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
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

    private static async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
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
