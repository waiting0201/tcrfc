using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Comics;

/// <summary>8.1 漫畫公開讀取端點（主站規劃書 §3.8）。全部不需要登入、全免費；台中藍鯨一律 403（不設漫畫）。唯一的寫入是閱讀數＋1（依 IP 限流）。</summary>
public static class ComicsEndpoints
{
    public static void MapComicsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/{club}/comic/about", async (
            string club, string? lang, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.GetAboutAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("GetComicAbout")
        .WithTags("Comics")
        .Produces<ComicAboutPublicDto>()
        .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/{club}/comic/characters", async (
            string club, string? lang, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListCharactersAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListComicCharacters")
        .WithTags("Comics")
        .Produces<IReadOnlyList<ComicCharacterPublicDto>>()
        .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/{club}/comic/episodes", async (
            string club, string? lang, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListEpisodesAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListComicEpisodes")
        .WithTags("Comics")
        .Produces<IReadOnlyList<ComicEpisodeListItemDto>>()
        .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/{club}/comic/episodes/latest", async (
            string club, string? lang, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var latest = await repository.GetLatestAsync(scope, RequestLocale.ToDbLocale(lang), ct);
            return latest is null ? Results.NotFound() : Results.Ok(latest);
        })
        .WithName("GetLatestComicEpisode")
        .WithTags("Comics")
        .Produces<ComicEpisodeListItemDto>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/{club}/comic/episodes/{episodeNo:int}", async (
            string club, int episodeNo, string? lang, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var episode = await repository.GetEpisodeAsync(scope, episodeNo, RequestLocale.ToDbLocale(lang), ct);
            return episode is null ? Results.NotFound() : Results.Ok(episode);
        })
        .WithName("GetComicEpisode")
        .WithTags("Comics")
        .Produces<ComicEpisodeDetailDto>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/comic/episodes/{episodeNo}/views → 閱讀數＋1（前台閱讀器載入後另外呼叫一次，固定 204）
        app.MapPost("/api/v1/{club}/comic/episodes/{episodeNo:int}/views", async (
            string club, int episodeNo, IClubResolver clubs, ComicsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return await repository.IncrementViewCountAsync(scope, episodeNo, ct) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("IncrementComicEpisodeViewCount")
        .WithTags("Comics")
        .RequireRateLimiting(PublicRateLimitPolicies.LightInteraction)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
