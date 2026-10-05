using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Competitions;

public static class CompetitionsEndpoints
{
    public static void MapCompetitionsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/competitions?season=2026-27&lang=zh —— 賽事系列清單（App 規劃書 §9.2；賽程第三層篩選）。
        app.MapGet("/api/v1/{club}/competitions", async (
            string club,
            string? season,
            string? lang,
            IClubResolver clubResolver,
            CompetitionsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, season, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListCompetitions")
        .WithTags("Competitions")
        .Produces<IReadOnlyList<CompetitionDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
