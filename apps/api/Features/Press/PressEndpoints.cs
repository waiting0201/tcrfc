using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Press;

/// <summary>公開讀取：B6 媒體專區（規劃書 §3.7 7.8）。不需要登入。下載端點會累計下載次數，
/// 因此掛輕量互動限流（比照瀏覽數）。</summary>
public static class PressEndpoints
{
    public static void MapPressEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/press?type=press_release|brand_kit|hires_image&lang=&page=&pageSize=
        app.MapGet("/api/v1/{club}/press", async (
            string club, string? type, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, PressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 24, maxPageSize: 100);
            var resourceType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
            if (resourceType is not null && !Features.AdminPress.AdminPressRepository.ResourceTypes.Contains(resourceType))
            {
                return Results.BadRequest(new { title = "查詢參數有誤", detail = "類別只能是 press_release、brand_kit 或 hires_image。" });
            }

            return Results.Ok(await repository.ListAsync(scope, resourceType, RequestLocale.ToDbLocale(lang), p, ps, cancellationToken));
        })
        .WithName("ListPressResources").WithTags("Press")
        .Produces<PagedResult<PressResourceDto>>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/press/{slug}/download → 累計下載次數後 302 轉址到檔案。
        app.MapGet("/api/v1/{club}/press/{slug}/download", async (
            string club, string slug, IClubResolver clubResolver, PressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var url = await repository.RegisterDownloadAsync(scope, slug, cancellationToken);
            return url is null ? Results.NotFound() : Results.Redirect(url);
        })
        .WithName("DownloadPressResource").WithTags("Press")
        .RequireRateLimiting(PublicRateLimitPolicies.LightInteraction)
        .Produces(StatusCodes.Status302Found).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status429TooManyRequests);
    }
}
