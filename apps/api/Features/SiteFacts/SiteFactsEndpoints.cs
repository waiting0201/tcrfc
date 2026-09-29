using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.SiteFacts;

/// <summary>公開讀取端點（S1-12d），供 <c>apps/web</c>（Nuxt）串接：成立年份、主場與場地、
/// 所屬聯賽、梯隊組成、聯絡方式。不需要登入——這些都是規劃書 §7 <c>GEO-03</c> 點名要讓 AI 與
/// 訪客都能穩定讀到的公開事實。</summary>
public static class SiteFactsEndpoints
{
    public static void MapSiteFactsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/site-facts?lang=zh|en
        app.MapGet("/api/v1/{club}/site-facts", async (
            string club, string? lang, IClubResolver clubResolver, SiteFactsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var result = await repository.GetAsync(scope, dbLocale, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetSiteFacts")
        .WithTags("SiteFacts")
        .Produces<PublicSiteFactsDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
