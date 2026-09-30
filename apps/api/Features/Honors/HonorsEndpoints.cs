using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Honors;

/// <summary>公開讀取：C5 榮譽與里程碑（規劃書 §3.2 02 關於台中磐石，2.8 里程碑時間軸）。不需要登入。</summary>
public static class HonorsEndpoints
{
    public static void MapHonorsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/achievements?team=D1&lang=zh
        app.MapGet("/api/v1/{club}/achievements", async (
            string club, string? team, string? lang, IClubResolver clubResolver, HonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListAchievementsAsync(scope, string.IsNullOrWhiteSpace(team) ? null : team.Trim(), RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListAchievements").WithTags("Honors")
        .Produces<IReadOnlyList<AchievementDto>>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/milestones?lang=zh
        app.MapGet("/api/v1/{club}/milestones", async (
            string club, string? lang, IClubResolver clubResolver, HonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListMilestonesAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListMilestones").WithTags("Honors")
        .Produces<IReadOnlyList<MilestoneDto>>().Produces(StatusCodes.Status404NotFound);
    }
}
