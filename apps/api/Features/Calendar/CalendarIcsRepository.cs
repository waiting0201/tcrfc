using Dapper;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>
/// 單場賽事 <c>.ics</c> 下載（主站規劃書 §3.13「加入我的行事曆：單一賽事下載 .ics」）。
///
/// 🔴 **時區換算，全站僅此一處**：<c>matches.match_on</c>（<c>date</c>）＋ <c>kickoff</c>
/// （<c>nvarchar(8)</c>，如 <c>"19:00"</c>）依 <c>docs/12-database-schema.md</c> §12 第 31 點是
/// **「當地牆上時間」的展示值，不是可換算時區的時間戳**——本俱樂部主場都在台灣，這裡把牆上時間視為
/// <c>Asia/Taipei</c>（UTC+8，無夏令時間，換算是固定 -8 小時減法，不需要 <c>TimeZoneInfo</c>
/// 查表）換算成 UTC 才能交給 <see cref="IcsBuilder"/> 輸出符合 RFC 5545 的 <c>DTSTART</c>。
/// <c>calendar_custom_events.starts_at</c>／<c>ends_at</c> 不在這條規則內——那兩欄是一般
/// <c>datetime2(3)</c>，依 §1 型別詞彙表本來就已經是 UTC 時間戳，不需要（也不應該）再減 8 小時，
/// 本輪只實作賽事的 <c>.ics</c>（規劃書明確要求的是單一賽事，L2 自建事件的 <c>.ics</c> 留給日後
/// 有實際需求時比照本檔案的模式擴充）。
/// </summary>
public sealed class CalendarIcsRepository(IClubSqlConnectionFactory connectionFactory)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    private sealed record MatchIcsRow(
        Guid Id, DateTime MatchOn, string? Kickoff, string? HomeAway, string? Opponent, string? Status,
        DateTime CreatedAt, DateTime UpdatedAt, Guid? VenueId);

    public async Task<IcsEvent?> GetMatchIcsAsync(ClubScope scope, Guid matchId, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string sql = """
            SELECT m.id AS Id, m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway,
                   m.opponent AS Opponent, m.status AS Status, m.created_at AS CreatedAt, m.updated_at AS UpdatedAt,
                   m.venue_id AS VenueId
            FROM matches m
            WHERE m.id = @MatchId AND m.club_id = @ClubId
            """;
        var row = await connection.QuerySingleOrDefaultAsync<MatchIcsRow>(new CommandDefinition(
            sql, new { MatchId = matchId, scope.ClubId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var locales = dbLocale == RequestLocale.DefaultDbLocale ? new[] { dbLocale } : new[] { dbLocale, RequestLocale.DefaultDbLocale };
        var i18nRows = (await connection.QueryAsync<(string Locale, string? Opponent, string? Venue)>(new CommandDefinition(
            "SELECT locale AS Locale, opponent AS Opponent, venue AS Venue FROM matches_i18n WHERE match_id = @MatchId AND locale IN @Locales",
            new { MatchId = matchId, Locales = locales }, cancellationToken: cancellationToken))).AsList();
        var byLocale = i18nRows.ToDictionary(r => r.Locale, r => r);
        var requested = byLocale.GetValueOrDefault(dbLocale);
        var fallback = byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale);

        var opponent = RequestLocale.Pick(requested.Opponent, row.Opponent) ?? "(未定對手)";
        var venueFromI18n = RequestLocale.Pick(requested.Venue, fallback.Venue);
        string? venueName = venueFromI18n;
        if (venueName is null && row.VenueId is { } venueId)
        {
            venueName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT COALESCE(req.name, zh.name) FROM venues v " +
                "LEFT JOIN venues_i18n zh ON zh.venue_id = v.id AND zh.locale = N'zh-Hant' " +
                "LEFT JOIN venues_i18n req ON req.venue_id = v.id AND req.locale = @Locale " +
                "WHERE v.id = @VenueId",
                new { VenueId = venueId, Locale = dbLocale }, cancellationToken: cancellationToken));
        }

        var isAllDay = string.IsNullOrEmpty(row.Kickoff);
        var startsAtUtc = ToUtc(row.MatchOn, row.Kickoff);
        // 沒有儲存賽事時長，比照一般足球比賽含中場約 2 小時估算——單純用於 .ics 的 DTEND，
        // 不影響任何資料庫欄位或其他端點的回應。
        var endsAtUtc = isAllDay ? (DateTime?)null : startsAtUtc.AddHours(2);

        var summary = row.HomeAway switch
        {
            "主場" => $"台中磐石 vs {opponent}",
            "客場" => $"{opponent} vs 台中磐石",
            _ => $"台中磐石 vs {opponent}",
        };

        return new IcsEvent
        {
            Uid = $"match-{row.Id}@tcrfc",
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            IsAllDay = isAllDay,
            Summary = summary,
            Location = venueName,
            Status = row.Status == "cancelled" ? "CANCELLED" : "CONFIRMED",
            CreatedAtUtc = row.CreatedAt,
            UpdatedAtUtc = row.UpdatedAt,
        };
    }

    private static DateTime ToUtc(DateTime matchOn, string? kickoff)
    {
        var timeOfDay = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(kickoff) && TimeOnly.TryParse(kickoff, out var parsed))
        {
            timeOfDay = parsed.ToTimeSpan();
        }

        var taipeiWallTime = matchOn.Date + timeOfDay;
        return DateTime.SpecifyKind(taipeiWallTime - TaipeiOffset, DateTimeKind.Utc);
    }
}
