using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Clubs;

public static class ClubsEndpoints
{
    public static void MapClubsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/clubs").WithTags("Clubs");

        // GET /api/v1/clubs?lang=zh — 俱樂部清單（前台站台切換器、sitemap 用）。
        group.MapGet("/", async (
            string? lang,
            ClubsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var clubs = await repository.ListAsync(dbLocale, cancellationToken);
            return Results.Ok(clubs);
        })
        .WithName("ListClubs")
        .Produces<IReadOnlyList<ClubDto>>();

        // GET /api/v1/clubs/{club}?lang=zh — 單一俱樂部主檔（GEO-03 事實單一來源）。
        group.MapGet("/{club}", async (
            string club,
            string? lang,
            IClubResolver clubResolver,
            ClubsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var dto = await repository.GetAsync(scope, dbLocale, cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .WithName("GetClub")
        .Produces<ClubDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
