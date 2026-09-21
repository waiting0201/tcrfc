using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Players;

public static class PlayersEndpoints
{
    public static void MapPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/players?team=D1&lang=zh&page=1&pageSize=50
        app.MapGet("/api/v1/{club}/players", async (
            string club,
            string? team,
            string? lang,
            int? page,
            int? pageSize,
            IClubResolver clubResolver,
            PlayersRepository repository,
            CancellationToken cancellationToken) =>
        {
            // 🔴 club_id 強制生效的落點：拿不到已驗證的 ClubScope，後面的 repository 呼叫連編譯都過不了。
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);

            var result = await repository.ListAsync(scope, team, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListPlayers")
        .WithTags("Players")
        .Produces<PagedResult<PlayerDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
