using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>
/// 13 賽事行事曆的公開合併讀取（主站規劃書 §3.13）。合併 <c>matches</c>（C4，既有
/// <c>Features/Schedule/MatchesRepository</c> 完全未改動，本檔是給「13 賽事行事曆」這個新頁面用的
/// 另一種形狀，不是取代）與**公開**的 <c>calendar_custom_events</c>（<c>is_public = 1</c>）。
///
/// 🔴 **兩種查詢模式（本輪判斷，規劃書沒有把這兩種模式的參數形狀寫清楚）**：
/// 1. **列表模式**（<paramref name="fromDate"/>／<paramref name="toDateExclusive"/> 皆未提供）：
///    依 <paramref name="team"/> 決定內容——`team="club"` 回傳「俱樂部活動」分頁（規劃書 §3.13
///    「隊別分頁（第一層）……另有『全部』與『俱樂部活動』」），其餘（含未指定）回傳**只有賽事**的
///    「賽程 Fixtures／賽果 Results」分頁——這兩個分頁在規劃書原文就是賽事的概念（未來／過去），
///    俱樂部活動沒有「賽果」的語意，見下方 <c>ListMatchesAsync</c>／<c>ListClubEventsAsync</c>
///    各自的檔頭說明。
/// 2. **月曆模式**（提供 <paramref name="fromDate"/>／<paramref name="toDateExclusive"/>）：合併
///    賽事與俱樂部活動，L2 重複規則在這個模式即時展開（<see cref="RecurrenceExpander"/>），排序一律
///    由近到遠，不分賽程／賽果。
/// </summary>
public sealed class CalendarRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CacheEntity = "calendar";

    private sealed record MatchRow(
        Guid Id, Guid? CompetitionId, string SeasonCode, DateTime MatchOn, string? Kickoff,
        string? HomeAway, string? Opponent, string? CompetitionTag, string? Status,
        int? ScoreHome, int? ScoreAway, int? RoundNo, int? MatchNo,
        DateTime? OriginalMatchOn, string? OriginalKickoff, Guid? VenueId);

    // ⚠️ RepeatUntil 用 DateTime? 不是 DateOnly?——SQL `date` 欄位經 Microsoft.Data.SqlClient
    // 回報的 CLR 型別一律是 DateTime，Dapper 的 record 建構子具現化要求型別逐一相符
    // （docs/18-work-errors.md E-20）。傳給 RecurrenceExpander 前再轉成 DateOnly。
    private sealed record CustomEventRow(
        Guid Id, DateTime StartsAt, DateTime? EndsAt, bool IsAllDay, Guid? VenueId, string? EventTypeCode,
        string? CtaUrl, string? CoverKey, string? RepeatRule, DateTime? RepeatUntil);

    private sealed record I18nTextRow(Guid Id, string Locale, string? Text1, string? Text2);
    private sealed record TeamCodeRow(Guid Id, string Code);
    private sealed record CompetitionNameRow(Guid CompetitionId, string Locale, string? Name);

    public async Task<PagedResult<PublicCalendarEventDto>> ListAsync(
        ClubScope scope, DateOnly? fromDate, DateOnly? toDateExclusive, string? team, string mode,
        string? season, string? type, string? homeAway, string dbLocale, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var qualifier = $"{fromDate?.ToString() ?? "-"}:{toDateExclusive?.ToString() ?? "-"}:{team ?? "-"}:{mode}:" +
            $"{season ?? "-"}:{type ?? "-"}:{homeAway ?? "-"}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                if (fromDate is { } from && toDateExclusive is { } toExclusive)
                {
                    return await ListRangeAsync(connection, scope, from, toExclusive, team, season, type, homeAway, dbLocale, page, pageSize, ct);
                }

                return string.Equals(team, "club", StringComparison.OrdinalIgnoreCase)
                    ? await ListClubEventsAsync(connection, scope, dbLocale, page, pageSize, ct)
                    : await ListMatchesAsync(connection, scope, team, mode, season, type, homeAway, dbLocale, page, pageSize, ct);
            },
            cancellationToken);
    }

    /// <summary>賽程 Fixtures／賽果 Results 分頁——沒有 <c>from</c>／<c>to</c> 時的預設模式，
    /// 只回傳賽事（規劃書「賽程／賽果切換」本來就是賽事的未來／過去，俱樂部活動沒有這個概念）。</summary>
    private async Task<PagedResult<PublicCalendarEventDto>> ListMatchesAsync(
        System.Data.IDbConnection connection, ClubScope scope, string? team, string mode,
        string? season, string? type, string? homeAway, string dbLocale, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var isResults = string.Equals(mode, "results", StringComparison.OrdinalIgnoreCase);

        var countSql = $"""
            SELECT COUNT(*)
            FROM matches m
            JOIN seasons se ON se.id = m.season_id
            LEFT JOIN match_teams mt ON mt.match_id = m.id
            LEFT JOIN teams t ON t.id = mt.team_id
            WHERE m.club_id = @ClubId
              AND m.match_on {(isResults ? "<" : ">=")} @Today
              AND (@Team IS NULL OR t.code = @Team)
              AND (@Season IS NULL OR se.code = @Season)
              AND (@Type IS NULL OR m.competition = @Type)
              AND (@HomeAway IS NULL OR m.home_away = @HomeAway)
            """;
        var listSql = $"""
            SELECT DISTINCT m.id AS Id, m.competition_id AS CompetitionId, se.code AS SeasonCode,
                   m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway, m.opponent AS Opponent,
                   m.competition AS CompetitionTag, m.status AS Status, m.score_home AS ScoreHome,
                   m.score_away AS ScoreAway, m.round_no AS RoundNo, m.match_no AS MatchNo,
                   m.original_match_on AS OriginalMatchOn, m.original_kickoff AS OriginalKickoff, m.venue_id AS VenueId
            FROM matches m
            JOIN seasons se ON se.id = m.season_id
            LEFT JOIN match_teams mt ON mt.match_id = m.id
            LEFT JOIN teams t ON t.id = mt.team_id
            WHERE m.club_id = @ClubId
              AND m.match_on {(isResults ? "<" : ">=")} @Today
              AND (@Team IS NULL OR t.code = @Team)
              AND (@Season IS NULL OR se.code = @Season)
              AND (@Type IS NULL OR m.competition = @Type)
              AND (@HomeAway IS NULL OR m.home_away = @HomeAway)
            ORDER BY m.match_on {(isResults ? "DESC" : "ASC")}, m.kickoff {(isResults ? "DESC" : "ASC")}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var parameters = new
        {
            scope.ClubId, Today = todayUtc, Team = team, Season = season, Type = type, HomeAway = homeAway,
            Offset = (page - 1) * pageSize, PageSize = pageSize,
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<MatchRow>(new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken))).AsList();

        var items = await MapMatchesAsync(connection, rows, dbLocale, cancellationToken);
        return new PagedResult<PublicCalendarEventDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>「俱樂部活動」分頁——只回傳公開自建事件，粗略依「還沒結束」或「仍在重複中」篩選；
    /// ⚠️ **已知簡化**：重複規則事件在這個分頁只用原始 <c>starts_at</c> 排序，不逐一展開每一次
    /// 重複發生的時間（那需要月曆模式的 <c>from</c>／<c>to</c> 才有界限可以展開，見類別檔頭說明）。
    /// 前台若要看到某個重複活動「下一次」的確切時間，應改用月曆檢視。</summary>
    private async Task<PagedResult<PublicCalendarEventDto>> ListClubEventsAsync(
        System.Data.IDbConnection connection, ClubScope scope, string dbLocale, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var countSql = """
            SELECT COUNT(*) FROM calendar_custom_events c
            WHERE c.club_id = @ClubId AND c.is_public = 1
              AND (c.repeat_rule IS NOT NULL OR c.ends_at >= @Now OR (c.ends_at IS NULL AND c.starts_at >= @Now))
            """;
        var listSql = """
            SELECT c.id AS Id, c.starts_at AS StartsAt, c.ends_at AS EndsAt, c.is_all_day AS IsAllDay,
                   c.venue_id AS VenueId, et.code AS EventTypeCode, c.cta_url AS CtaUrl, c.cover_key AS CoverKey,
                   c.repeat_rule AS RepeatRule, c.repeat_until AS RepeatUntil
            FROM calendar_custom_events c
            LEFT JOIN event_types et ON et.id = c.event_type_id
            WHERE c.club_id = @ClubId AND c.is_public = 1
              AND (c.repeat_rule IS NOT NULL OR c.ends_at >= @Now OR (c.ends_at IS NULL AND c.starts_at >= @Now))
            ORDER BY c.starts_at ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;
        var parameters = new { scope.ClubId, Now = nowUtc, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<CustomEventRow>(new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken))).AsList();

        var items = await MapCustomEventsAsync(connection, rows, dbLocale, cancellationToken);
        return new PagedResult<PublicCalendarEventDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>月曆模式——合併賽事與公開自建事件，L2 重複規則即時展開，範圍上限 366 天
    /// （由呼叫端 <c>Features/Calendar/CalendarEndpoints.cs</c> 的 <c>NormalizeRange</c> 保證）。</summary>
    private async Task<PagedResult<PublicCalendarEventDto>> ListRangeAsync(
        System.Data.IDbConnection connection, ClubScope scope, DateOnly fromDate, DateOnly toDateExclusive,
        string? team, string? season, string? type, string? homeAway, string dbLocale, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        const string matchSql = """
            SELECT DISTINCT m.id AS Id, m.competition_id AS CompetitionId, se.code AS SeasonCode,
                   m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway, m.opponent AS Opponent,
                   m.competition AS CompetitionTag, m.status AS Status, m.score_home AS ScoreHome,
                   m.score_away AS ScoreAway, m.round_no AS RoundNo, m.match_no AS MatchNo,
                   m.original_match_on AS OriginalMatchOn, m.original_kickoff AS OriginalKickoff, m.venue_id AS VenueId
            FROM matches m
            JOIN seasons se ON se.id = m.season_id
            LEFT JOIN match_teams mt ON mt.match_id = m.id
            LEFT JOIN teams t ON t.id = mt.team_id
            WHERE m.club_id = @ClubId
              AND m.match_on >= @FromDate AND m.match_on < @ToDateExclusive
              AND (@Team IS NULL OR t.code = @Team)
              AND (@Season IS NULL OR se.code = @Season)
              AND (@Type IS NULL OR m.competition = @Type)
              AND (@HomeAway IS NULL OR m.home_away = @HomeAway)
            """;
        var matchRows = (await connection.QueryAsync<MatchRow>(new CommandDefinition(
            matchSql,
            new { scope.ClubId, FromDate = fromDate, ToDateExclusive = toDateExclusive, Team = team, Season = season, Type = type, HomeAway = homeAway },
            cancellationToken: cancellationToken))).AsList();

        const string customSql = """
            SELECT c.id AS Id, c.starts_at AS StartsAt, c.ends_at AS EndsAt, c.is_all_day AS IsAllDay,
                   c.venue_id AS VenueId, et.code AS EventTypeCode, c.cta_url AS CtaUrl, c.cover_key AS CoverKey,
                   c.repeat_rule AS RepeatRule, c.repeat_until AS RepeatUntil
            FROM calendar_custom_events c
            LEFT JOIN event_types et ON et.id = c.event_type_id
            WHERE c.club_id = @ClubId AND c.is_public = 1
              AND c.starts_at < @ToDateExclusiveTs
              AND (c.repeat_until IS NULL OR c.repeat_until >= @FromDate)
              AND (@Team IS NULL
                   OR (@Team = N'club' AND NOT EXISTS (
                         SELECT 1 FROM calendar_event_teams cet WHERE cet.source_type = N'custom' AND cet.source_id = c.id))
                   OR EXISTS (
                         SELECT 1 FROM calendar_event_teams cet JOIN teams t ON t.id = cet.team_id
                         WHERE cet.source_type = N'custom' AND cet.source_id = c.id AND t.code = @Team))
            """;
        var customRows = (await connection.QueryAsync<CustomEventRow>(new CommandDefinition(
            customSql,
            new { scope.ClubId, FromDate = fromDate, ToDateExclusiveTs = toDateExclusive.ToDateTime(TimeOnly.MinValue), Team = team },
            cancellationToken: cancellationToken))).AsList();

        var matchItems = await MapMatchesAsync(connection, matchRows, dbLocale, cancellationToken);

        var rangeFrom = fromDate.ToDateTime(TimeOnly.MinValue);
        var rangeToExclusive = toDateExclusive.ToDateTime(TimeOnly.MinValue);
        var customItems = new List<PublicCalendarEventDto>();
        foreach (var row in customRows)
        {
            var exceptions = await LoadExceptionDatesAsync(connection, row.Id, cancellationToken);
            var repeatUntil = row.RepeatUntil is { } ru ? DateOnly.FromDateTime(ru) : (DateOnly?)null;
            var occurrences = RecurrenceExpander.Expand(
                row.StartsAt, row.EndsAt, row.RepeatRule, repeatUntil, exceptions, rangeFrom, rangeToExclusive);
            if (occurrences.Count == 0)
            {
                continue;
            }

            var (dto, _) = await MapCustomEventBaseAsync(connection, row, dbLocale, cancellationToken);
            foreach (var (starts, ends) in occurrences)
            {
                customItems.Add(dto with { StartsAt = starts, EndsAt = ends });
            }
        }

        var all = matchItems.Concat(customItems).OrderBy(i => i.StartsAt).ToList();
        var (normalizedPage, normalizedPageSize) = (page, pageSize);
        var pageItems = all.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToList();

        return new PagedResult<PublicCalendarEventDto> { Items = pageItems, Page = page, PageSize = pageSize, TotalCount = all.Count };
    }

    private async Task<List<PublicCalendarEventDto>> MapMatchesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<MatchRow> rows, string dbLocale, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var matchIds = rows.Select(r => r.Id).ToList();
        var i18nByMatch = await LoadMatchI18nAsync(connection, matchIds, dbLocale, cancellationToken);
        var teamCodesByMatch = await LoadTeamCodesAsync(connection, "match", matchIds, cancellationToken);
        var competitionIds = rows.Select(r => r.CompetitionId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var competitionNameById = await LoadCompetitionNamesAsync(connection, competitionIds, dbLocale, cancellationToken);
        var venueIds = rows.Select(r => r.VenueId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var venueNameById = await LoadVenueNamesAsync(connection, venueIds, dbLocale, cancellationToken);

        return rows.Select(r =>
        {
            var (fallbackOpponent, fallbackVenue) = i18nByMatch.GetValueOrDefault(r.Id, (null, null));
            var opponent = RequestLocale.Pick(fallbackOpponent, r.Opponent);
            var competitionName = r.CompetitionId is { } cid ? competitionNameById.GetValueOrDefault(cid) : null;

            return new PublicCalendarEventDto
            {
                SourceType = "match",
                Id = r.Id,
                StartsAt = r.MatchOn,
                EndsAt = null,
                IsAllDay = string.IsNullOrEmpty(r.Kickoff),
                Title = opponent ?? "(未定對手)",
                TeamCodes = teamCodesByMatch.GetValueOrDefault(r.Id, []),
                VenueName = r.VenueId is { } vid ? (fallbackVenue ?? venueNameById.GetValueOrDefault(vid)) : fallbackVenue,
                SeasonCode = r.SeasonCode,
                CompetitionTag = r.CompetitionTag,
                CompetitionName = competitionName,
                Status = r.Status,
                HomeAway = r.HomeAway,
                ScoreHome = r.ScoreHome,
                ScoreAway = r.ScoreAway,
                RoundNo = r.RoundNo,
                MatchNo = r.MatchNo,
                OriginalMatchOn = r.OriginalMatchOn is { } om ? DateOnly.FromDateTime(om) : null,
                OriginalKickoff = r.OriginalKickoff,
            };
        }).ToList();
    }

    private async Task<List<PublicCalendarEventDto>> MapCustomEventsAsync(
        System.Data.IDbConnection connection, IReadOnlyList<CustomEventRow> rows, string dbLocale, CancellationToken cancellationToken)
    {
        var results = new List<PublicCalendarEventDto>();
        foreach (var row in rows)
        {
            var (dto, _) = await MapCustomEventBaseAsync(connection, row, dbLocale, cancellationToken);
            results.Add(dto);
        }

        return results;
    }

    private async Task<(PublicCalendarEventDto Dto, HashSet<DateOnly> Exceptions)> MapCustomEventBaseAsync(
        System.Data.IDbConnection connection, CustomEventRow row, string dbLocale, CancellationToken cancellationToken)
    {
        var i18n = await LoadCustomEventI18nAsync(connection, row.Id, dbLocale, cancellationToken);
        var teamCodes = (await LoadTeamCodesAsync(connection, "custom", [row.Id], cancellationToken)).GetValueOrDefault(row.Id, []);
        var venueName = row.VenueId is { } vid ? await LoadSingleVenueNameAsync(connection, vid, dbLocale, cancellationToken) : null;
        var exceptions = await LoadExceptionDatesAsync(connection, row.Id, cancellationToken);

        var dto = new PublicCalendarEventDto
        {
            SourceType = "custom",
            Id = row.Id,
            StartsAt = row.StartsAt,
            EndsAt = row.EndsAt,
            IsAllDay = row.IsAllDay,
            Title = i18n.Title ?? "(未命名活動)",
            TeamCodes = teamCodes,
            VenueName = venueName,
            EventTypeCode = row.EventTypeCode,
            Description = i18n.Description,
            CtaUrl = row.CtaUrl,
            CoverKey = row.CoverKey,
        };

        return (dto, exceptions);
    }

    private static async Task<HashSet<DateOnly>> LoadExceptionDatesAsync(
        System.Data.IDbConnection connection, Guid customEventId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<DateOnly>(new CommandDefinition(
            "SELECT excluded_on FROM calendar_event_exceptions WHERE calendar_custom_event_id = @Id",
            new { Id = customEventId }, cancellationToken: cancellationToken));
        return rows.ToHashSet();
    }

    private static async Task<Dictionary<Guid, (string? Opponent, string? Venue)>> LoadMatchI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> matchIds, string dbLocale, CancellationToken cancellationToken)
    {
        var locales = LocaleFallbackChain(dbLocale);
        const string sql = """
            SELECT match_id AS Id, locale AS Locale, opponent AS Text1, venue AS Text2
            FROM matches_i18n WHERE match_id IN @Ids AND locale IN @Locales
            """;
        var rows = await connection.QueryAsync<I18nTextRow>(new CommandDefinition(
            sql, new { Ids = matchIds, Locales = locales }, cancellationToken: cancellationToken));

        var byMatch = rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
        return matchIds.ToDictionary(id => id, id =>
        {
            var byLocale = byMatch.GetValueOrDefault(id);
            var requested = byLocale?.GetValueOrDefault(dbLocale);
            var fallback = byLocale?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
            return (RequestLocale.Pick(requested?.Text1, null), RequestLocale.Pick(requested?.Text2, fallback?.Text2));
        });
    }

    private static async Task<(string? Title, string? Description)> LoadCustomEventI18nAsync(
        System.Data.IDbConnection connection, Guid eventId, string dbLocale, CancellationToken cancellationToken)
    {
        var locales = LocaleFallbackChain(dbLocale);
        const string sql = """
            SELECT calendar_custom_event_id AS Id, locale AS Locale, title AS Text1, description AS Text2
            FROM calendar_custom_events_i18n WHERE calendar_custom_event_id = @Id AND locale IN @Locales
            """;
        var rows = (await connection.QueryAsync<I18nTextRow>(new CommandDefinition(
            sql, new { Id = eventId, Locales = locales }, cancellationToken: cancellationToken))).AsList();

        var byLocale = rows.ToDictionary(r => r.Locale, r => r);
        var requested = byLocale.GetValueOrDefault(dbLocale);
        var fallback = byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        return (RequestLocale.Pick(requested?.Text1, fallback?.Text1), RequestLocale.Pick(requested?.Text2, fallback?.Text2));
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTeamCodesAsync(
        System.Data.IDbConnection connection, string sourceType, IReadOnlyList<Guid> sourceIds, CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0)
        {
            return [];
        }

        var sql = sourceType == "match"
            ? "SELECT mt.match_id AS Id, t.code AS Code FROM match_teams mt JOIN teams t ON t.id = mt.team_id WHERE mt.match_id IN @Ids"
            : "SELECT cet.source_id AS Id, t.code AS Code FROM calendar_event_teams cet JOIN teams t ON t.id = cet.team_id " +
              "WHERE cet.source_type = N'custom' AND cet.source_id IN @Ids";
        var rows = await connection.QueryAsync<TeamCodeRow>(new CommandDefinition(
            sql, new { Ids = sourceIds }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, IReadOnlyList<string> (g) => g.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    private static async Task<Dictionary<Guid, string?>> LoadCompetitionNamesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> competitionIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (competitionIds.Count == 0)
        {
            return [];
        }

        var locales = LocaleFallbackChain(dbLocale);
        const string sql = """
            SELECT competition_id AS CompetitionId, locale AS Locale, name AS Name
            FROM competitions_i18n WHERE competition_id IN @Ids AND locale IN @Locales
            """;
        var rows = await connection.QueryAsync<CompetitionNameRow>(new CommandDefinition(
            sql, new { Ids = competitionIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.CompetitionId).ToDictionary(g => g.Key, g =>
        {
            var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
            return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
        });
    }

    private static async Task<Dictionary<Guid, string?>> LoadVenueNamesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> venueIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (venueIds.Count == 0)
        {
            return [];
        }

        var locales = LocaleFallbackChain(dbLocale);
        const string sql = "SELECT venue_id AS Id, locale AS Locale, name AS Text1, NULL AS Text2 FROM venues_i18n WHERE venue_id IN @Ids AND locale IN @Locales";
        var rows = await connection.QueryAsync<I18nTextRow>(new CommandDefinition(
            sql, new { Ids = venueIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g =>
        {
            var byLocale = g.ToDictionary(r => r.Locale, r => r.Text1);
            return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
        });
    }

    private static async Task<string?> LoadSingleVenueNameAsync(
        System.Data.IDbConnection connection, Guid venueId, string dbLocale, CancellationToken cancellationToken)
        => (await LoadVenueNamesAsync(connection, [venueId], dbLocale, cancellationToken)).GetValueOrDefault(venueId);

    private static string[] LocaleFallbackChain(string dbLocale) => dbLocale == RequestLocale.DefaultDbLocale
        ? [dbLocale]
        : [dbLocale, RequestLocale.DefaultDbLocale];
}
