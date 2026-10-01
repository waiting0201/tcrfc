using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.FanEvents;

/// <summary>
/// 8.2 球迷會活動公開端點（主站規劃書 §3.8）：列表、詳情（含活動回顧）、報名、取消報名。讀取不需要登入；
/// 報名：非會員也能報名一般活動，限付費會員的活動需要會員權杖。報名／取消依 IP 限流（接收個資的公開寫入）。
/// </summary>
public static class FanEventsEndpoints
{
    public static void MapFanEventsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/fan-events?phase=upcoming|past&lang=zh
        app.MapGet("/api/v1/{club}/fan-events", async (
            string club, string? phase, string? lang, IClubResolver clubs, FanEventsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListAsync(scope, phase, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListFanEvents")
        .WithTags("FanEvents")
        .Produces<IReadOnlyList<FanEventListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/fan-events/{slug}?lang=zh —— 帶會員權杖時會附上自己的報名狀態
        app.MapGet("/api/v1/{club}/fan-events/{slug}", async (
            string club, string slug, string? lang, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, FanEventsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var me = await authenticator.TryAsync(http, ct);
            var detail = await repository.GetAsync(scope, slug, me?.MemberId, RequestLocale.ToDbLocale(lang), ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
        .WithName("GetFanEvent")
        .WithTags("FanEvents")
        .Produces<FanEventDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/fan-events/{slug}/registrations
        app.MapPost("/api/v1/{club}/fan-events/{slug}/registrations", async (
            string club, string slug, FanEventRegisterRequest request, string? lang, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, FanEventsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var me = await authenticator.TryAsync(http, ct);
            var result = await repository.RegisterAsync(scope, slug, request ?? new FanEventRegisterRequest(null, null, null, null), me, RequestLocale.ToDbLocale(lang), ct);
            return Results.Created($"/api/v1/{club}/fan-events/{slug}", result);
        })
        .WithName("RegisterFanEvent")
        .WithTags("FanEvents")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<FanEventRegistrationResultDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);

        // DELETE /api/v1/{club}/fan-events/{slug}/registrations/me —— 會員取消自己的報名
        app.MapDelete("/api/v1/{club}/fan-events/{slug}/registrations/me", async (
            string club, string slug, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, FanEventsRepository repository, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            var scope = await clubs.ResolveAsync(club, ct);
            await repository.CancelMyRegistrationAsync(scope, slug, me.MemberId, ct);
            return Results.NoContent();
        })
        .WithName("CancelMyFanEventRegistration")
        .WithTags("FanEvents")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
