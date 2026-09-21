using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.News;

public static class ArticlesEndpoints
{
    public static void MapArticlesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/news?category=match&lang=zh&page=1&pageSize=20
        app.MapGet("/api/v1/{club}/news", async (
            string club,
            string? category,
            string? lang,
            int? page,
            int? pageSize,
            IClubResolver clubResolver,
            ArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            var result = await repository.ListAsync(scope, category, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListNews")
        .WithTags("News")
        .Produces<PagedResult<ArticleListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/news/{slug}?lang=zh
        app.MapGet("/api/v1/{club}/news/{slug}", async (
            string club,
            string slug,
            string? lang,
            IClubResolver clubResolver,
            ArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var article = await repository.GetBySlugAsync(scope, slug, dbLocale, cancellationToken);
            return article is null ? Results.NotFound() : Results.Ok(article);
        })
        .WithName("GetNewsArticle")
        .WithTags("News")
        .Produces<ArticleDetailDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
