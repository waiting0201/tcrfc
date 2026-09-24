using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.News;

public static class ArticlesEndpoints
{
    public static void MapArticlesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/news?category=match&tag=<slug>&lang=zh&page=1&pageSize=20
        // 🔴 S1-5 新增 tag 篩選（規劃書 3.7 前台「標籤篩選」）。
        app.MapGet("/api/v1/{club}/news", async (
            string club,
            string? category,
            string? tag,
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

            var result = await repository.ListAsync(scope, category, tag, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
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

        // POST /api/v1/{club}/news/{slug}/views  → 瀏覽數＋1（S1-5 新增，規劃書 B2「瀏覽數統計」）。
        // 🔴 公開、不需要登入（跟其餘 News 端點一樣），呼叫端是前台頁面渲染完成後另外呼叫一次，
        // 不是掛在上面兩個 GET 的讀取路徑上——理由見 ArticlesRepository.IncrementViewCountAsync。
        // 回應刻意固定 204（不回傳最新瀏覽數）：瀏覽數本來就可能因為快取而延後顯示，這支端點的
        // 回應如果精確回傳「資料庫這一刻的瀏覽數」，反而會讓呼叫端誤以為這個數字比畫面上顯示的
        // （來自快取的 GET 回應）更準確，兩個數字互相打架不是好設計；要精確就不該用快取，這是
        // 已經做過的取捨（docs/17 §4 五類不得讀快取不含瀏覽數）。
        app.MapPost("/api/v1/{club}/news/{slug}/views", async (
            string club,
            string slug,
            IClubResolver clubResolver,
            ArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var incremented = await repository.IncrementViewCountAsync(scope, slug, cancellationToken);
            return incremented ? Results.NoContent() : Results.NotFound();
        })
        .WithName("IncrementNewsArticleViewCount")
        .WithTags("News")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
