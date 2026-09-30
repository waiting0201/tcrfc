using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Sponsors;

/// <summary>公開讀取：E2 贊助商與贊助方案（規劃書 §3.9 9.2、9.4）。不需要登入。</summary>
public static class SponsorsEndpoints
{
    public static void MapSponsorsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/sponsors?lang=zh —— 依等級（主贊助→官方贊助→支持夥伴）排序，含贊助故事與贊助活動。
        app.MapGet("/api/v1/{club}/sponsors", async (
            string club, string? lang, IClubResolver clubResolver, SponsorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListSponsorsAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListSponsors").WithTags("Sponsors")
        .Produces<IReadOnlyList<SponsorDto>>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/sponsor-packages?lang=zh —— 9 種贊助方案卡片（只回已發布）。
        app.MapGet("/api/v1/{club}/sponsor-packages", async (
            string club, string? lang, IClubResolver clubResolver, SponsorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListPackagesAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListSponsorPackages").WithTags("Sponsors")
        .Produces<IReadOnlyList<SponsorPackageDto>>().Produces(StatusCodes.Status404NotFound);
    }
}
