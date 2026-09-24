using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Faqs;

/// <summary>公開讀取＋輕量互動端點：B4 常見問題（規劃書 3.12）。全部不需要登入。</summary>
public static class FaqsEndpoints
{
    public static void MapFaqsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/faq-categories?lang=zh —— 全站共用，不分俱樂部路由段（faq_categories 沒有 club_id）。
        app.MapGet("/api/v1/faq-categories", async (
            string? lang, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var categories = await repository.ListCategoriesAsync(dbLocale, cancellationToken);
            return Results.Ok(categories);
        })
        .WithName("ListFaqCategories")
        .WithTags("Faqs")
        .Produces<IReadOnlyList<FaqCategoryDto>>();

        // GET /api/v1/{club}/faqs?category=<slug>&keyword=&lang=zh&page=&pageSize=
        app.MapGet("/api/v1/{club}/faqs", async (
            string club, string? category, string? keyword, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);

            var result = await repository.ListAsync(scope, category, keyword, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListFaqs")
        .WithTags("Faqs")
        .Produces<PagedResult<FaqListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/faqs/{slug}?lang=zh
        app.MapGet("/api/v1/{club}/faqs/{slug}", async (
            string club, string slug, string? lang, IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var faq = await repository.GetBySlugAsync(scope, slug, dbLocale, cancellationToken);
            return faq is null ? Results.NotFound() : Results.Ok(faq);
        })
        .WithName("GetFaq")
        .WithTags("Faqs")
        .Produces<FaqListItemDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/faqs/embeds/{slotCode}?lang=zh —— G-12 掛載點「額外」指定的題目
        // （S1-7a）。只回這一半（逐題額外指定），「由分類自動對應」由前台頁面另外呼叫既有的
        // ?category= 篩選湊出聯集，見 FaqsRepository.ListByEmbedSlotAsync 檔頭說明。
        app.MapGet("/api/v1/{club}/faqs/embeds/{slotCode}", async (
            string club, string slotCode, string? lang, IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var faqs = await repository.ListByEmbedSlotAsync(scope, slotCode, dbLocale, cancellationToken);
            return Results.Ok(faqs);
        })
        .WithName("ListFaqsByEmbedSlot")
        .WithTags("Faqs")
        .Produces<IReadOnlyList<FaqListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/faqs/{slug}/views  → 瀏覽數＋1，理由同 News 的既有端點。
        app.MapPost("/api/v1/{club}/faqs/{slug}/views", async (
            string club, string slug, IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var incremented = await repository.IncrementViewCountAsync(scope, slug, cancellationToken);
            return incremented ? Results.NoContent() : Results.NotFound();
        })
        .WithName("IncrementFaqViewCount")
        .WithTags("Faqs")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/faqs/{slug}/feedback  body: { "helpful": true|false }
        app.MapPost("/api/v1/{club}/faqs/{slug}/feedback", async (
            string club, string slug, FaqFeedbackRequest request,
            IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var recorded = await repository.SubmitFeedbackAsync(scope, slug, request.Helpful, cancellationToken);
            return recorded ? Results.NoContent() : Results.NotFound();
        })
        .WithName("SubmitFaqFeedback")
        .WithTags("Faqs")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/faqs/search-misses  body: { "keyword": "..." }
        // 🔴 前台真正呈現「找不到結果」畫面給使用者看到的那一刻才呼叫，見 FaqsRepository.RecordSearchMissAsync。
        app.MapPost("/api/v1/{club}/faqs/search-misses", async (
            string club, FaqSearchMissRequest request,
            IClubResolver clubResolver, FaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            // 🔴 S1-8：寫入前正規化（大小寫、前後空白、全半形），見 SearchKeywordNormalizer 上的說明——
            // faq_search_misses 是彙總列，正規化只能在寫入前做，讀取端事後補救不了。
            var keyword = SearchKeywordNormalizer.Normalize(request.Keyword);
            if (keyword.Length == 0)
            {
                return Results.NoContent(); // 空字串不值得記錄，靜默忽略即可，不需要讓前端處理 400。
            }

            await repository.RecordSearchMissAsync(scope, keyword, cancellationToken);
            return Results.NoContent();
        })
        .WithName("RecordFaqSearchMiss")
        .WithTags("Faqs")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
