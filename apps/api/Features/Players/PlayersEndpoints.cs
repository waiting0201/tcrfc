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

        // GET /api/v1/{club}/players/{slug}?lang=zh
        // App 深連結 tcrfc://player/{slug}（App 規劃書 §2.3）與官網 /zh/club/first-team/player/{slug} 的解析端點。
        // {slug} 也接受球員 id（Guid），見 PlayersRepository.GetBySlugAsync。
        app.MapGet("/api/v1/{club}/players/{slug}", async (
            string club,
            string slug,
            string? lang,
            IClubResolver clubResolver,
            PlayersRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var player = await repository.GetBySlugAsync(scope, slug, RequestLocale.ToDbLocale(lang), cancellationToken);
            return player is null ? Results.NotFound() : Results.Ok(player);
        })
        .WithName("GetPlayer")
        .WithTags("Players")
        .Produces<PlayerDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
