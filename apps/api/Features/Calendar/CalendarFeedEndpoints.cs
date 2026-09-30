using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>13 賽事行事曆的訂閱 feed 與前台設定（公開端點，不需要登入）。主站規劃書 §3.13、§4.12 L3／L4。</summary>
public static class CalendarFeedEndpoints
{
    public static void MapCalendarFeedEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/calendar/feed.ics?team=D1&lang=zh —— webcal 訂閱用。省略 team（或 all）＝全站。
        app.MapGet("/api/v1/{club}/calendar/feed.ics", async (
            string club, string? team, string? lang, HttpContext httpContext, IClubResolver clubResolver,
            CalendarFeedRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var teamCode = string.IsNullOrWhiteSpace(team) || string.Equals(team, "all", StringComparison.OrdinalIgnoreCase) ? null : team.Trim();
            var content = await repository.BuildAsync(scope, teamCode, RequestLocale.ToDbLocale(lang), cancellationToken);
            if (content is null)
            {
                return Results.NotFound();
            }

            await repository.RecordFetchAsync(scope, teamCode ?? "all", httpContext, cancellationToken);
            httpContext.Response.Headers.CacheControl = "public, max-age=900";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(content), "text/calendar; charset=utf-8");
        })
        .WithName("CalendarFeed")
        .WithTags("Calendar")
        .RequireRateLimiting(PublicRateLimitPolicies.LightInteraction)
        .Produces(StatusCodes.Status200OK, contentType: "text/calendar")
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/calendar/settings?lang=zh —— 前台預設檢視、可選隊別與賽事類型。
        app.MapGet("/api/v1/{club}/calendar/settings", async (
            string club, string? lang, IClubResolver clubResolver, CalendarSettingsPublicRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("GetCalendarSettings")
        .WithTags("Calendar")
        .Produces<PublicCalendarSettingsDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
