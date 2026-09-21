using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Schedule;

public static class MatchesEndpoints
{
    public static void MapMatchesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/schedule?team=D1&season=2026-27&status=scheduled&lang=zh&page=1&pageSize=20
        app.MapGet("/api/v1/{club}/schedule", async (
            string club,
            string? team,
            string? season,
            string? status,
            string? lang,
            int? page,
            int? pageSize,
            IClubResolver clubResolver,
            MatchesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            var result = await repository.ListAsync(scope, team, season, status, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListSchedule")
        .WithTags("Schedule")
        .Produces<PagedResult<MatchDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
