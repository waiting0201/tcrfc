using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Seo;

/// <summary>公開讀取端點（S1-12），供 <c>apps/web</c>（Nuxt）串接：全站 SEO 預設與追蹤碼、
/// 生效中的 301 轉址、Sitemap 項目。三者皆不需要登入——這些資訊本來就會出現在公開頁面的原始碼
/// 或 HTTP 行為裡（標題、描述、追蹤碼、轉址、Sitemap 本身就是給搜尋引擎與瀏覽器看的）。</summary>
public static class SeoEndpoints
{
    public static void MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/seo/settings
        app.MapGet("/api/v1/{club}/seo/settings", async (
            string club, IClubResolver clubResolver, SeoRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.GetSettingsAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetSeoSettings")
        .WithTags("Seo")
        .Produces<PublicSeoSettingsDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/seo/redirects
        app.MapGet("/api/v1/{club}/seo/redirects", async (
            string club, IClubResolver clubResolver, SeoRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.GetActiveRedirectsAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetActiveRedirects")
        .WithTags("Seo")
        .Produces<IReadOnlyList<PublicRedirectDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/seo/sitemap-entries
        app.MapGet("/api/v1/{club}/seo/sitemap-entries", async (
            string club, IClubResolver clubResolver, SeoRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.GetSitemapEntriesAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetSitemapEntries")
        .WithTags("Seo")
        .Produces<IReadOnlyList<SitemapEntryDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
