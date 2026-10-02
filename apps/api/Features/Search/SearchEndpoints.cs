using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Search;

/// <summary>G-02 全站搜尋公開端點。不需要登入；只讀、不寫入任何資料；套 <see cref="PublicRateLimitPolicies.Search"/> 限流。</summary>
public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/search?q=&type=news|faq|program|player|coach|charity&lang=zh&page=1&pageSize=20
        app.MapGet("/api/v1/{club}/search", async (
            string club, string? q, string? type, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, SearchRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 50);
            var result = await repository.SearchAsync(
                scope, q, string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToLowerInvariant(),
                RequestLocale.ToDbLocale(lang), normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("SearchSite")
        .WithTags("Search")
        .WithDescription("全站搜尋（新聞、FAQ、課程、球員、教練、慈善）。所有關鍵字都要命中；只回已發布內容。")
        .RequireRateLimiting(PublicRateLimitPolicies.Search)
        .Produces<SearchResponseDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);
    }
}
