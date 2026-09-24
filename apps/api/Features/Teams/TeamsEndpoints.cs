using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Teams;

public static class TeamsEndpoints
{
    public static void MapTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/teams?lang=zh
        app.MapGet("/api/v1/{club}/teams", async (
            string club,
            string? lang,
            IClubResolver clubResolver,
            TeamsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var result = await repository.ListAsync(scope, dbLocale, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListTeams")
        .WithTags("Teams")
        .Produces<IReadOnlyList<TeamDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
