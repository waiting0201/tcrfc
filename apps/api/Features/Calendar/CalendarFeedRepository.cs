using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>
/// L4 訂閱：公開的 webcal／iCal feed（主站規劃書 §3.13「依隊別訂閱」、§4.12 L4）。全站一份、每支隊別一份；
/// 行事曆客戶端會定期重新抓取，賽程異動自動同步。
///
/// <b>內容</b>：賽事（含取消與延賽，狀態隨 <c>STATUS</c> 同步）、前台公開的自建活動（重複規則展開）、
/// 已同步至行事曆的試訓（全天）。範圍為今天前 90 天到後 400 天。隊別 feed 只含該隊別的事件；全站 feed 含全部
/// （只涉及「不公開的隊別」的事件不出現）。L3 把某隊別設為不公開時，那支隊別的 feed 回 404。
/// 賽事時間的換算與 <c>CalendarIcsRepository</c> 相同（當地牆上時間視為 <c>Asia/Taipei</c>，換算成 UTC）；自建活動時間戳本來就是 UTC。
///
/// <b>訂閱數統計</b>（規劃書 §4.12 L4「各隊別各有多少人訂閱」）：每次抓取記錄「哪個 feed、哪一天、哪個來源」——來源是
/// IP＋User-Agent 的 HMAC 雜湊（不存原始 IP，去識別化），同一來源同一天只記一筆（先用行程內記憶體去重，避免每次抓取都寫資料庫）。
/// 90 天以前的紀錄定期清除。這是<b>估計值</b>：行事曆服務（例如 Google 行事曆）由自己的伺服器代為抓取，多位訂閱者會被算成同一個來源。
/// </summary>
public sealed class CalendarFeedRepository(IClubSqlConnectionFactory connectionFactory, IConfiguration configuration)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan MatchDuration = TimeSpan.FromHours(2);

    // 行程內去重：key = 俱樂部|feed|來源雜湊，value = 已記錄的日期。超過上限就整批清掉（只是少一次去重，不影響正確性）。
    private static readonly ConcurrentDictionary<string, DateOnly> RecordedToday = new();
    private const int RecordedCacheLimit = 50_000;
    private static DateTime _lastPurgeUtc = DateTime.MinValue;

    private sealed record MatchRow(
        Guid Id, DateTime MatchOn, string? Kickoff, string? HomeAway, string? Opponent, string? Status,
        DateTime CreatedAt, DateTime UpdatedAt, string? VenueName);
    private sealed record CustomRow(
        Guid Id, DateTime StartsAt, DateTime? EndsAt, bool IsAllDay, string? RepeatRule, DateTime? RepeatUntil, string? Title, string? Description,
        string? CtaUrl, string? VenueName, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record TrialRow(Guid Id, DateTime TrialOn, string? Audience, string? VenueName, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record ExceptionRow(Guid EventId, DateTime ExcludedOn);

    /// <summary><paramref name="teamCode"/> 為 <c>null</c>＝全站 feed。隊別不存在或 L3 設為不公開時回傳 <c>null</c>（404）。</summary>
    public async Task<string?> BuildAsync(ClubScope scope, string? teamCode, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        string? teamName = null;
        if (teamCode is not null)
        {
            teamName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                """
                SELECT COALESCE(NULLIF(dn.display_name, N''), NULLIF(ti.name, N''), t.code)
                FROM teams t
                LEFT JOIN calendar_team_settings s ON s.team_id = t.id
                LEFT JOIN calendar_team_settings_i18n dn ON dn.calendar_team_setting_id = s.id AND dn.locale = @Locale
                LEFT JOIN teams_i18n ti ON ti.team_id = t.id AND ti.locale = @Locale
                WHERE t.club_id = @ClubId AND t.code = @TeamCode AND COALESCE(s.is_public, 1) = 1
                """,
                new { scope.ClubId, TeamCode = teamCode, Locale = dbLocale }, cancellationToken: cancellationToken));
            if (teamName is null)
            {
                return null;
            }
        }

        var clubName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 name FROM clubs_i18n WHERE club_id = @ClubId ORDER BY CASE WHEN locale = @Locale THEN 0 ELSE 1 END",
            new { scope.ClubId, Locale = dbLocale }, cancellationToken: cancellationToken)) ?? scope.ClubCode;

        var today = DateTime.UtcNow.Date;
        var fromDate = today.AddDays(-90);
        var toDate = today.AddDays(400);
        var parameters = new { scope.ClubId, TeamCode = teamCode, Locale = dbLocale, From = fromDate, To = toDate };

        // 全站 feed：「只涉及不公開隊別」的事件不出現（事件有隊別且全部都是不公開才排除），條件寫在下面各個查詢裡。

        var matches = (await connection.QueryAsync<MatchRow>(new CommandDefinition(
            """
            SELECT m.id AS Id, m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway,
                   COALESCE(NULLIF(mi.opponent, N''), m.opponent) AS Opponent, m.status AS Status,
                   m.created_at AS CreatedAt, m.updated_at AS UpdatedAt,
                   COALESCE(NULLIF(vi.name, N''), vz.name) AS VenueName
            FROM matches m
            LEFT JOIN matches_i18n mi ON mi.match_id = m.id AND mi.locale = @Locale
            LEFT JOIN venues_i18n vi ON vi.venue_id = m.venue_id AND vi.locale = @Locale
            LEFT JOIN venues_i18n vz ON vz.venue_id = m.venue_id AND vz.locale = N'zh-Hant'
            WHERE m.club_id = @ClubId AND m.match_on >= @From AND m.match_on < @To
              AND (@TeamCode IS NULL OR EXISTS (SELECT 1 FROM match_teams mt JOIN teams t ON t.id = mt.team_id WHERE mt.match_id = m.id AND t.code = @TeamCode))
              AND (@TeamCode IS NOT NULL
                   OR NOT EXISTS (SELECT 1 FROM match_teams mt1 WHERE mt1.match_id = m.id)
                   OR EXISTS (SELECT 1 FROM match_teams mt2 LEFT JOIN calendar_team_settings s2 ON s2.team_id = mt2.team_id
                              WHERE mt2.match_id = m.id AND COALESCE(s2.is_public, 1) = 1))
            ORDER BY m.match_on
            """,
            parameters, cancellationToken: cancellationToken))).AsList();

        var customs = (await connection.QueryAsync<CustomRow>(new CommandDefinition(
            """
            SELECT c.id AS Id, c.starts_at AS StartsAt, c.ends_at AS EndsAt, c.is_all_day AS IsAllDay, c.repeat_rule AS RepeatRule,
                   c.repeat_until AS RepeatUntil, COALESCE(NULLIF(ci.title, N''), cz.title) AS Title,
                   COALESCE(NULLIF(ci.description, N''), cz.description) AS Description, c.cta_url AS CtaUrl,
                   COALESCE(NULLIF(vi.name, N''), vz.name) AS VenueName, c.created_at AS CreatedAt, c.updated_at AS UpdatedAt
            FROM calendar_custom_events c
            LEFT JOIN calendar_custom_events_i18n ci ON ci.calendar_custom_event_id = c.id AND ci.locale = @Locale
            LEFT JOIN calendar_custom_events_i18n cz ON cz.calendar_custom_event_id = c.id AND cz.locale = N'zh-Hant'
            LEFT JOIN venues_i18n vi ON vi.venue_id = c.venue_id AND vi.locale = @Locale
            LEFT JOIN venues_i18n vz ON vz.venue_id = c.venue_id AND vz.locale = N'zh-Hant'
            WHERE c.club_id = @ClubId AND c.is_public = 1
              AND c.starts_at < @To AND (c.repeat_until IS NULL OR c.repeat_until >= @From)
              AND (@TeamCode IS NULL OR EXISTS (SELECT 1 FROM calendar_event_teams cet JOIN teams t ON t.id = cet.team_id
                                                WHERE cet.source_type = N'custom' AND cet.source_id = c.id AND t.code = @TeamCode))
              AND (@TeamCode IS NOT NULL
                   OR NOT EXISTS (SELECT 1 FROM calendar_event_teams e1 WHERE e1.source_type = N'custom' AND e1.source_id = c.id)
                   OR EXISTS (SELECT 1 FROM calendar_event_teams e2 LEFT JOIN calendar_team_settings s2 ON s2.team_id = e2.team_id
                              WHERE e2.source_type = N'custom' AND e2.source_id = c.id AND COALESCE(s2.is_public, 1) = 1))
            """,
            parameters, cancellationToken: cancellationToken))).AsList();

        var exceptions = customs.Count == 0
            ? new Dictionary<Guid, HashSet<DateOnly>>()
            : (await connection.QueryAsync<ExceptionRow>(new CommandDefinition(
                "SELECT calendar_custom_event_id AS EventId, excluded_on AS ExcludedOn FROM calendar_event_exceptions WHERE calendar_custom_event_id IN @Ids",
                new { Ids = customs.Select(c => c.Id).ToList() }, cancellationToken: cancellationToken)))
                .GroupBy(e => e.EventId).ToDictionary(g => g.Key, g => g.Select(e => DateOnly.FromDateTime(e.ExcludedOn)).ToHashSet());

        var trials = (await connection.QueryAsync<TrialRow>(new CommandDefinition(
            """
            SELECT t.id AS Id, t.trial_on AS TrialOn, COALESCE(NULLIF(ti.audience, N''), tz.audience) AS Audience,
                   COALESCE(NULLIF(vi.name, N''), vz.name) AS VenueName, t.created_at AS CreatedAt, t.updated_at AS UpdatedAt
            FROM trials t
            LEFT JOIN trials_i18n ti ON ti.trial_id = t.id AND ti.locale = @Locale
            LEFT JOIN trials_i18n tz ON tz.trial_id = t.id AND tz.locale = N'zh-Hant'
            LEFT JOIN venues_i18n vi ON vi.venue_id = t.venue_id AND vi.locale = @Locale
            LEFT JOIN venues_i18n vz ON vz.venue_id = t.venue_id AND vz.locale = N'zh-Hant'
            WHERE t.club_id = @ClubId AND t.sync_to_calendar = 1 AND t.status <> N'已結束' AND t.trial_on >= @From AND t.trial_on < @To
              AND (@TeamCode IS NULL OR EXISTS (SELECT 1 FROM teams tm WHERE tm.id = t.team_id AND tm.code = @TeamCode))
            """,
            parameters, cancellationToken: cancellationToken))).AsList();

        var events = new List<IcsEvent>();
        foreach (var m in matches)
        {
            events.Add(MatchEvent(m, clubName, dbLocale));
        }

        foreach (var c in customs)
        {
            var title = c.Title ?? "(未命名活動)";
            var occurrences = RecurrenceExpander.Expand(
                c.StartsAt, c.EndsAt, c.RepeatRule, c.RepeatUntil is { } ru ? DateOnly.FromDateTime(ru) : null,
                exceptions.GetValueOrDefault(c.Id) ?? [], fromDate, toDate);
            foreach (var (starts, ends) in occurrences)
            {
                var isRecurring = !string.IsNullOrEmpty(c.RepeatRule);
                events.Add(new IcsEvent
                {
                    Uid = isRecurring ? $"custom-{c.Id}-{starts:yyyyMMdd}@tcrfc" : $"custom-{c.Id}@tcrfc",
                    StartsAtUtc = DateTime.SpecifyKind(starts, DateTimeKind.Utc),
                    EndsAtUtc = ends is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null,
                    IsAllDay = c.IsAllDay, Summary = title, Location = c.VenueName, Description = c.Description, Url = c.CtaUrl,
                    Status = "CONFIRMED", CreatedAtUtc = c.CreatedAt, UpdatedAtUtc = c.UpdatedAt,
                });
            }
        }

        foreach (var t in trials)
        {
            events.Add(new IcsEvent
            {
                Uid = $"trial-{t.Id}@tcrfc", StartsAtUtc = DateTime.SpecifyKind(t.TrialOn.Date, DateTimeKind.Utc), IsAllDay = true,
                Summary = t.Audience is { Length: > 0 } a ? (dbLocale == "en" ? $"Trial: {a}" : $"試訓：{a}") : (dbLocale == "en" ? "Trial" : "試訓"),
                Location = t.VenueName, Status = "CONFIRMED", CreatedAtUtc = t.CreatedAt, UpdatedAtUtc = t.UpdatedAt,
            });
        }

        var calendarName = teamName is null ? clubName : $"{clubName} {teamName}";
        return IcsBuilder.BuildCalendar(calendarName, events.OrderBy(e => e.StartsAtUtc));
    }

    private static IcsEvent MatchEvent(MatchRow m, string clubName, string dbLocale)
    {
        var opponent = m.Opponent ?? (dbLocale == "en" ? "TBD" : "(未定對手)");
        var isAway = m.HomeAway is "AWAY" or "客場";
        var summary = isAway ? $"{opponent} vs {clubName}" : $"{clubName} vs {opponent}";
        var isAllDay = string.IsNullOrEmpty(m.Kickoff);
        var timeOfDay = TimeSpan.Zero;
        if (!isAllDay && TimeOnly.TryParse(m.Kickoff, out var parsed))
        {
            timeOfDay = parsed.ToTimeSpan();
        }
        else
        {
            isAllDay = true;
        }

        var startUtc = isAllDay
            ? DateTime.SpecifyKind(m.MatchOn.Date, DateTimeKind.Utc)
            : DateTime.SpecifyKind(m.MatchOn.Date + timeOfDay - TaipeiOffset, DateTimeKind.Utc);
        return new IcsEvent
        {
            Uid = $"match-{m.Id}@tcrfc", StartsAtUtc = startUtc, EndsAtUtc = isAllDay ? null : startUtc + MatchDuration, IsAllDay = isAllDay,
            Summary = summary, Location = m.VenueName, Status = m.Status == "cancelled" ? "CANCELLED" : "CONFIRMED",
            CreatedAtUtc = m.CreatedAt, UpdatedAtUtc = m.UpdatedAt,
        };
    }

    // ═══════════════════ 訂閱數統計 ═══════════════════

    public async Task RecordFetchAsync(ClubScope scope, string feedKey, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hash = ClientHash(httpContext);
        var cacheKey = $"{scope.ClubId:N}|{feedKey}|{hash}";
        if (RecordedToday.TryGetValue(cacheKey, out var recorded) && recorded == today)
        {
            return;
        }

        if (RecordedToday.Count > RecordedCacheLimit)
        {
            RecordedToday.Clear();
        }

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM calendar_feed_fetches WHERE club_id = @ClubId AND feed_key = @FeedKey AND fetched_on = @Today AND client_hash = @Hash)
              INSERT INTO calendar_feed_fetches (club_id, feed_key, fetched_on, client_hash) VALUES (@ClubId, @FeedKey, @Today, @Hash)
            """,
            new { scope.ClubId, FeedKey = feedKey, Today = today.ToDateTime(TimeOnly.MinValue), Hash = hash }, cancellationToken: cancellationToken));
        RecordedToday[cacheKey] = today;

        // 90 天以前的紀錄：每六小時最多清一次（行程內計時，不需要排程器）。
        if (DateTime.UtcNow - _lastPurgeUtc > TimeSpan.FromHours(6))
        {
            _lastPurgeUtc = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM calendar_feed_fetches WHERE fetched_on < @Cutoff",
                new { Cutoff = today.AddDays(-90).ToDateTime(TimeOnly.MinValue) }, cancellationToken: cancellationToken));
        }
    }

    private string ClientHash(HttpContext httpContext)
    {
        var key = configuration["JWT_SIGNING_KEY_CLUB"] ?? "tcrfc-calendar-feed";
        var input = $"{ClientIpResolver.Resolve(httpContext)}|{httpContext.Request.Headers.UserAgent}";
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes("calendar-feed|" + key), Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
