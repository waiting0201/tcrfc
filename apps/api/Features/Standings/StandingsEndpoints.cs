using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Standings;

/// <summary>公開讀取：賽事積分榜與球員數據（主站規劃書 §3.3 3.1、§4.3 C2／C4；藍鯨規劃書 §5 藍鯨自己的賽事資料）。全部不需要登入、純讀取。</summary>
public static class StandingsEndpoints
{
    public static void MapStandingsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/standings?season=2026/27
        app.MapGet("/api/v1/{club}/standings", async (
            string club, string? season, IClubResolver clubs, StandingsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.GetStandingsAsync(scope, season, ct));
        })
        .WithName("GetStandings").WithTags("Standings")
        .Produces<StandingsDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/stats/players?season=&team=D1&lang=
        app.MapGet("/api/v1/{club}/stats/players", async (
            string club, string? season, string? team, string? lang, IClubResolver clubs, StandingsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.GetPlayerStatsAsync(scope, season, string.IsNullOrWhiteSpace(team) ? null : team.Trim(), RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("GetPlayerStats").WithTags("Standings")
        .Produces<PlayerStatsDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/players/{id}/stats —— 球員詳情頁的逐季數據。
        app.MapGet("/api/v1/{club}/players/{id:guid}/stats", async (
            string club, Guid id, IClubResolver clubs, StandingsRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var stats = await repository.GetPlayerCareerAsync(scope, id, ct);
            return stats is null ? Results.NotFound() : Results.Ok(stats);
        })
        .WithName("GetPlayerCareerStats").WithTags("Standings")
        .Produces<PlayerCareerStatsDto>().Produces(StatusCodes.Status404NotFound);
    }
}
