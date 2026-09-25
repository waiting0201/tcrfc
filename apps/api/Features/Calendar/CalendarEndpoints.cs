using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>13 賽事行事曆公開端點（主站規劃書 §3.13）。不需要登入——賽事本身既有
/// <c>GET /api/v1/{club}/schedule</c> 就是公開的，本檔是給行事曆頁的合併形狀，不是新的授權邊界。</summary>
public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/calendar/events?team=D1&mode=fixtures&season=2026-27&type=league&homeAway=主場&from=&to=&lang=&page=&pageSize=
        app.MapGet("/api/v1/{club}/calendar/events", async (
            string club, string? team, string? mode, string? season, string? type, string? homeAway,
            DateOnly? from, DateOnly? to, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, CalendarRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var normalizedMode = string.Equals(mode, "results", StringComparison.OrdinalIgnoreCase) ? "results" : "fixtures";

            var (fromDate, toDateExclusive) = NormalizeRange(from, to);

            var result = await repository.ListAsync(
                scope, fromDate, toDateExclusive, team, normalizedMode, season, type, homeAway,
                dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListCalendarEvents")
        .WithTags("Calendar")
        .Produces<PagedResult<PublicCalendarEventDto>>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/matches/{id}/ics —— 單場賽事「加入我的行事曆」下載。
        app.MapGet("/api/v1/{club}/matches/{id:guid}/ics", async (
            string club, Guid id, string? lang, IClubResolver clubResolver, CalendarIcsRepository icsRepository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var icsEvent = await icsRepository.GetMatchIcsAsync(scope, id, dbLocale, cancellationToken);
            if (icsEvent is null)
            {
                return Results.NotFound();
            }

            var content = IcsBuilder.BuildSingleEvent(icsEvent);
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return Results.File(bytes, "text/calendar; charset=utf-8", $"match-{id}.ics");
        })
        .WithName("DownloadMatchIcs")
        .WithTags("Calendar")
        .Produces(StatusCodes.Status200OK, contentType: "text/calendar")
        .Produces(StatusCodes.Status404NotFound);
    }

    /// <summary>只提供 <c>from</c> 或只提供 <c>to</c> 其中一個視為輸入錯誤（月曆模式需要完整區間）；
    /// 兩者皆未提供時回傳 <c>(null, null)</c>，交給 <see cref="CalendarRepository"/> 走列表模式。
    /// 範圍上限 366 天，理由同 <c>Features/AdminCalendar/AdminCalendarEndpoints.NormalizeRange</c>。</summary>
    private static (DateOnly? From, DateOnly? ToExclusive) NormalizeRange(DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null)
        {
            return (null, null);
        }

        if (from is null || to is null)
        {
            throw new CalendarQueryValidationException("月曆檢視需要同時提供 from 與 to。");
        }

        if (to <= from)
        {
            throw new CalendarQueryValidationException("結束日期必須晚於起始日期。");
        }

        if (to.Value.DayNumber - from.Value.DayNumber > 366)
        {
            throw new CalendarQueryValidationException("查詢範圍最長 366 天，請縮小 from／to 區間。");
        }

        return (from, to);
    }
}

public sealed class CalendarQueryValidationException(string message) : Exception(message);
