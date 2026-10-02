using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Trials;

/// <summary>3.3 球員機會／4.7 加入學院 的試訓場次公開讀取＋線上報名端點（主站規劃書 §3.3）。全部不需要登入。</summary>
public static class TrialsEndpoints
{
    public static void MapTrialsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/trials?teamCode=&lang=zh —— 尚未結束、日期未過的試訓場次（最多 100 筆，依日期由近到遠）。
        app.MapGet("/api/v1/{club}/trials", async (
            string club, string? teamCode, string? lang,
            IClubResolver clubResolver, TrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var trials = await repository.ListAsync(scope, teamCode, RequestLocale.ToDbLocale(lang), cancellationToken);
            return Results.Ok(trials);
        })
        .WithName("ListTrials")
        .WithTags("Trials")
        .Produces<IReadOnlyList<PublicTrialDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/trials/{trialId}/registrations
        app.MapPost("/api/v1/{club}/trials/{trialId:guid}/registrations", async (
            string club, Guid trialId, SubmitTrialRegistrationRequest request, HttpContext httpContext,
            IClubResolver clubResolver, MemberAuthenticator memberAuthenticator, TrialsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            // 與課程報名相同：帶有效會員權杖（行動 App）就記 member_id；沒帶或權杖無效視為訪客報名。
            var member = await memberAuthenticator.TryAsync(httpContext, cancellationToken);
            var result = await repository.SubmitRegistrationAsync(scope, trialId, request, member?.MemberId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("SubmitTrialRegistration")
        .WithTags("Trials")
        .RequireRateLimiting(PublicRateLimitPolicies.TrialRegistration)
        .Produces<TrialRegistrationSubmittedDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);
    }
}
