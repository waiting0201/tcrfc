using Dapper;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L1「行事曆總覽」——合併讀取 <c>matches</c>（C4）與 <c>calendar_custom_events</c>（L2）兩種來源，
/// 主站規劃書 §4.12「以月曆呈現全部賽事與自建的俱樂部活動，依隊別以色彩區分」。
///
/// 🔴 **本輪範圍縮減（S1-11，見 apps/api/README.md）**：規劃書 L1 原文同時列出「隊別分軌檢視」
/// 「拖曳調整日期回寫賽事」「衝突偵測」——這三項連同 L3／L4 一併排進 <c>STATUS.md</c> 的 <c>S2-6</c>
/// （「行事曆進階」），本輪只做**合併讀取**：把 <c>matches</c> 與 <c>calendar_custom_events</c>
/// 換算成同一種形狀回傳，前台／後台畫面要做分軌並排、拖曳改期、衝突警示是畫面邏輯與另一組寫入
/// 端點的工作，不在本輪端點的回應形狀裡預先決定。
///
/// 🔴 **為什麼讀取端不套用 <see cref="TeamRowScope"/>**：<c>matches</c> 本身已經是
/// <c>GET /api/v1/{club}/schedule</c>（13 賽事行事曆）任何人不需要登入就能看到的公開資訊——
/// 行事曆總覽只是換一種畫面（月曆／列表）呈現同一份資料，不會因為多了「總覽」這個入口就變成需要
/// 列級限制的敏感資料。規劃書「行事曆權限採跟隨來源模組」講的是**編輯**哪些事件（見
/// <c>team.match.*</c> 與 <c>calendar.custom_event.*</c> 兩組寫入權限碼各自的範圍），不是「能不能
/// 看到」，故 <c>calendar.view</c> 一律 <c>scope_type="all"</c>。S1-8 當時保留給 L 模組使用的
/// <c>own_teams</c> scope_type 盤點後在本輪讀取端仍然沒有實際用途，見 apps/api/README.md「S1-11」
/// 段「規劃書沒寫清楚、自行判斷」的完整說明。
/// </summary>
public sealed class AdminCalendarOverviewRepository(IClubSqlConnectionFactory connectionFactory)
{
    private sealed record MatchRow(
        Guid Id, DateTime MatchOn, string? Kickoff, string? HomeAway, string? Opponent,
        string? Status, Guid? VenueId);

    // ⚠️ RepeatUntil 用 DateTime? 不是 DateOnly?——SQL `date` 欄位經 Microsoft.Data.SqlClient
    // 回報的 CLR 型別一律是 DateTime，Dapper 的 record 建構子具現化要求型別逐一相符
    // （docs/18-work-errors.md E-20，逐字比照 Features/Schedule/MatchesRepository.cs 對
    // OriginalMatchOn 的既有註解）。Map() 時再轉成 DateOnly。
    private sealed record CustomEventRow(
        Guid Id, DateTime StartsAt, DateTime? EndsAt, bool IsAllDay, bool IsPublic,
        Guid? VenueId, string? EventTypeCode, string? Title, string? RepeatRule, DateTime? RepeatUntil);

    private sealed record TeamCodeRow(string SourceType, Guid SourceId, string Code);
    private sealed record VenueNameRow(Guid VenueId, string? Name);

    /// <summary>
    /// <paramref name="fromDate"/>（含）～<paramref name="toDateExclusive"/>（不含）範圍內的合併事件。
    /// 範圍是呼叫端的必要輸入（月曆檢視一次看一個月、列表檢視也該有界），避免一次撈出整個資料庫的
    /// 賽事與活動。
    /// </summary>
    public async Task<IReadOnlyList<AdminCalendarEventDto>> ListAsync(
        AdminClubScope scope, DateOnly fromDate, DateOnly toDateExclusive, string? teamCode, string? sourceType,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var matches = new List<MatchRow>();
        if (sourceType is null or "match")
        {
            const string matchSql = """
                SELECT DISTINCT m.id AS Id, m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway,
                       m.opponent AS Opponent, m.status AS Status, m.venue_id AS VenueId
                FROM matches m
                LEFT JOIN match_teams mt ON mt.match_id = m.id
                LEFT JOIN teams t ON t.id = mt.team_id
                WHERE m.club_id = @ClubId
                  AND m.match_on >= @FromDate AND m.match_on < @ToDateExclusive
                  AND (@TeamCode IS NULL OR t.code = @TeamCode)
                ORDER BY m.match_on
                """;
            matches = (await connection.QueryAsync<MatchRow>(new CommandDefinition(
                matchSql,
                new { scope.ClubId, FromDate = fromDate, ToDateExclusive = toDateExclusive, TeamCode = teamCode },
                cancellationToken: cancellationToken))).AsList();
        }

        var customEvents = new List<CustomEventRow>();
        if (sourceType is null or "custom")
        {
            // 候選範圍刻意放寬（起始時間在查詢範圍結束前、重複結束日在查詢範圍開始後或未設定），
            // 精確的每一次重複展開交給 RecurrenceExpander 在下方逐筆處理。
            const string customSql = """
                SELECT c.id AS Id, c.starts_at AS StartsAt, c.ends_at AS EndsAt, c.is_all_day AS IsAllDay,
                       c.is_public AS IsPublic, c.venue_id AS VenueId, et.code AS EventTypeCode,
                       i18n_zh.title AS Title, c.repeat_rule AS RepeatRule, c.repeat_until AS RepeatUntil
                FROM calendar_custom_events c
                LEFT JOIN event_types et ON et.id = c.event_type_id
                LEFT JOIN calendar_custom_events_i18n i18n_zh
                    ON i18n_zh.calendar_custom_event_id = c.id AND i18n_zh.locale = N'zh-Hant'
                WHERE c.club_id = @ClubId
                  AND c.starts_at < @ToDateExclusiveTs
                  AND (c.repeat_until IS NULL OR c.repeat_until >= @FromDate)
                  AND (@TeamCode IS NULL
                       OR (@TeamCode = N'club' AND NOT EXISTS (
                             SELECT 1 FROM calendar_event_teams cet WHERE cet.source_type = N'custom' AND cet.source_id = c.id))
                       OR EXISTS (
                             SELECT 1 FROM calendar_event_teams cet JOIN teams t ON t.id = cet.team_id
                             WHERE cet.source_type = N'custom' AND cet.source_id = c.id AND t.code = @TeamCode))
                """;
            customEvents = (await connection.QueryAsync<CustomEventRow>(new CommandDefinition(
                customSql,
                new
                {
                    scope.ClubId, FromDate = fromDate,
                    ToDateExclusiveTs = toDateExclusive.ToDateTime(TimeOnly.MinValue),
                    TeamCode = teamCode,
                },
                cancellationToken: cancellationToken))).AsList();
        }

        var matchIds = matches.Select(m => m.Id).ToList();
        var customIds = customEvents.Select(c => c.Id).ToList();
        var teamCodesBySource = await LoadTeamCodesAsync(connection, matchIds, customIds, cancellationToken);
        var venueIds = matches.Select(m => m.VenueId).Concat(customEvents.Select(c => c.VenueId))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var venueNameById = await LoadVenueNamesAsync(connection, venueIds, cancellationToken);

        var rangeFrom = fromDate.ToDateTime(TimeOnly.MinValue);
        var rangeToExclusive = toDateExclusive.ToDateTime(TimeOnly.MinValue);

        var results = new List<AdminCalendarEventDto>();

        foreach (var m in matches)
        {
            results.Add(new AdminCalendarEventDto
            {
                SourceType = "match",
                SourceId = m.Id,
                StartsAt = m.MatchOn,
                EndsAt = null,
                IsAllDay = string.IsNullOrEmpty(m.Kickoff),
                Title = m.Opponent ?? "(未定對手)",
                TeamCodes = teamCodesBySource.GetValueOrDefault(("match", m.Id), []),
                VenueName = m.VenueId is { } vid ? venueNameById.GetValueOrDefault(vid) : null,
                Status = m.Status,
                HomeAway = m.HomeAway,
            });
        }

        foreach (var c in customEvents)
        {
            // L2 重複規則展開——見 Common/RecurrenceExpander.cs 檔頭「行事曆是彙整層」的完整說明。
            // 例外日期本輪未在此查詢中一併撈出（管理總覽的候選集合已經很小，逐筆查一次可接受，
            // 避免在合併 SQL 裡再多一層 GROUP_CONCAT 風格的聚合)。
            var exceptions = await LoadExceptionDatesAsync(connection, c.Id, cancellationToken);
            var repeatUntil = c.RepeatUntil is { } ru ? DateOnly.FromDateTime(ru) : (DateOnly?)null;

            var occurrences = RecurrenceExpander.Expand(
                c.StartsAt, c.EndsAt, c.RepeatRule, repeatUntil, exceptions, rangeFrom, rangeToExclusive);

            foreach (var (starts, ends) in occurrences)
            {
                results.Add(new AdminCalendarEventDto
                {
                    SourceType = "custom",
                    SourceId = c.Id,
                    StartsAt = starts,
                    EndsAt = ends,
                    IsAllDay = c.IsAllDay,
                    Title = c.Title ?? "(未命名活動)",
                    TeamCodes = teamCodesBySource.GetValueOrDefault(("custom", c.Id), []),
                    VenueName = c.VenueId is { } vid ? venueNameById.GetValueOrDefault(vid) : null,
                    EventTypeCode = c.EventTypeCode,
                    IsPublic = c.IsPublic,
                });
            }
        }

        return results.OrderBy(r => r.StartsAt).ToList();
    }

    private static async Task<HashSet<DateOnly>> LoadExceptionDatesAsync(
        System.Data.IDbConnection connection, Guid customEventId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<DateOnly>(new CommandDefinition(
            "SELECT excluded_on FROM calendar_event_exceptions WHERE calendar_custom_event_id = @Id",
            new { Id = customEventId }, cancellationToken: cancellationToken));
        return rows.ToHashSet();
    }

    private static async Task<Dictionary<(string SourceType, Guid SourceId), IReadOnlyList<string>>> LoadTeamCodesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> matchIds, IReadOnlyList<Guid> customIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, Guid), IReadOnlyList<string>>();
        if (matchIds.Count == 0 && customIds.Count == 0)
        {
            return result;
        }

        // 🔴 match 的隊別關聯走 match_teams，不是 calendar_event_teams——後者只給 custom 用
        // （db/club-schema.sql「4.10 L 行事曆管理」的 calendar_events 視圖定義：match 分支從
        // matches 直接讀，隊別關聯本來就在既有的 match_teams）。兩種來源分開查，用 UNION ALL 合併
        // 成同一份「(SourceType, SourceId) → 隊別代碼」對照，不勉強套一份共用的來源表。
        var rows = await connection.QueryAsync<TeamCodeRow>(new CommandDefinition(
            """
            SELECT N'match' AS SourceType, mt.match_id AS SourceId, t.code AS Code
            FROM match_teams mt JOIN teams t ON t.id = mt.team_id
            WHERE mt.match_id IN @MatchIds
            UNION ALL
            SELECT cet.source_type AS SourceType, cet.source_id AS SourceId, t.code AS Code
            FROM calendar_event_teams cet JOIN teams t ON t.id = cet.team_id
            WHERE cet.source_type = N'custom' AND cet.source_id IN @CustomIds
            """,
            new
            {
                MatchIds = matchIds.Count > 0 ? matchIds : [Guid.Empty],
                CustomIds = customIds.Count > 0 ? customIds : [Guid.Empty],
            },
            cancellationToken: cancellationToken));

        foreach (var group in rows.GroupBy(r => (r.SourceType, r.SourceId)))
        {
            result[group.Key] = group.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        return result;
    }

    private static async Task<Dictionary<Guid, string?>> LoadVenueNamesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> venueIds, CancellationToken cancellationToken)
    {
        if (venueIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT v.id AS VenueId, COALESCE(req.name, zh.name) AS Name
            FROM venues v
            LEFT JOIN venues_i18n zh ON zh.venue_id = v.id AND zh.locale = N'zh-Hant'
            LEFT JOIN venues_i18n req ON req.venue_id = v.id AND req.locale = N'zh-Hant'
            WHERE v.id IN @VenueIds
            """;
        var rows = await connection.QueryAsync<VenueNameRow>(new CommandDefinition(
            sql, new { VenueIds = venueIds }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.VenueId, r => r.Name);
    }
}
