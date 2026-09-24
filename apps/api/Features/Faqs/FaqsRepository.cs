using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Faqs;

/// <summary>
/// 公開讀取：B4 常見問題。<c>faqs.club_id</c> 是 9 張可為空表之一，套用
/// <see cref="ClubOrSharedSql"/>（「俱樂部專屬優先、回退共同」）；<c>faq_categories</c> 沒有
/// <c>club_id</c>，不分俱樂部（同 <c>article_categories</c>）。唯讀路徑走 Dapper，
/// 寫入（回饋、瀏覽數、零結果搜尋）比照 <c>Features/News/ArticlesRepository.IncrementViewCountAsync</c>：
/// 資料庫端直接遞增，不經過 <see cref="IQueryCache"/>。
/// </summary>
public sealed class FaqsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CategoriesEntity = "faq-categories";
    private const string ListEntity = "faqs";
    private const string DetailEntity = "faq-detail";
    private const string EmbedEntity = "faq-embed";

    private sealed record FaqCategoryRow(Guid Id, string Slug, int SortOrder, string? Name);

    /// <summary>全站共用，<see cref="CacheDimensions.SharedClub"/> 當俱樂部維度
    /// （<see cref="IQueryCache"/> 的 key 命名規則要求一定要有這個維度，即使資料本身不分俱樂部）。</summary>
    public async Task<IReadOnlyList<FaqCategoryDto>> ListCategoriesAsync(string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CategoriesEntity, CacheDimensions.SharedClub, dbLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                // 🔴 S1-7a：is_enabled=1 過濾——停用的分類不列入公開導覽（軟停用，見
                // AdminFaqCategoriesRepository 檔頭說明），既有題目與關聯不受影響。
                const string sql = """
                    SELECT fc.id AS Id, fc.slug AS Slug, fc.sort_order AS SortOrder,
                           fci.name AS Name, fci.locale AS Locale
                    FROM faq_categories fc
                    LEFT JOIN faq_categories_i18n fci ON fci.faq_category_id = fc.id AND fci.locale IN @Locales
                    WHERE fc.is_enabled = 1
                    ORDER BY fc.sort_order
                    """;
                var locales = dbLocale == RequestLocale.DefaultDbLocale
                    ? new[] { dbLocale }
                    : new[] { dbLocale, RequestLocale.DefaultDbLocale };

                var rows = (await connection.QueryAsync<FaqCategoryI18nJoinRow>(new CommandDefinition(
                    sql, new { Locales = locales }, cancellationToken: ct))).ToList();

                return rows
                    .GroupBy(r => r.Id)
                    .Select(g =>
                    {
                        var first = g.First();
                        var byLocale = g.Where(r => r.Locale is not null).ToDictionary(r => r.Locale!);
                        byLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);
                        byLocale.TryGetValue(dbLocale, out var requested);

                        return new FaqCategoryDto
                        {
                            Id = first.Id,
                            Slug = first.Slug,
                            SortOrder = first.SortOrder,
                            Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
                        };
                    })
                    .OrderBy(c => c.SortOrder)
                    .ToList() as IReadOnlyList<FaqCategoryDto>;
            },
            cancellationToken);
    }

    private sealed record FaqCategoryI18nJoinRow(Guid Id, string Slug, int SortOrder, string? Name, string? Locale);

    private sealed record FaqRow(Guid Id, bool IsShared, string Slug, int SortOrder);

    /// <summary>常見問題列表。<paramref name="categorySlug"/> 篩選用 <c>EXISTS</c>（一題可屬多分類，
    /// 同 <c>ArticlesRepository.ListAsync</c> 的標籤篩選寫法，避免 JOIN 造成同一題重複列）。
    /// <paramref name="keyword"/> 只比對繁中欄位（跟 <c>ArticlesRepository</c> 一致：zh-Hant 是必填
    /// 語系，任何一題都一定有這個側表列）。</summary>
    public async Task<PagedResult<FaqListItemDto>> ListAsync(
        ClubScope scope, string? categorySlug, string? keyword, string dbLocale,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var qualifier = $"{categorySlug ?? CacheDimensions.NoQualifier}:{keyword ?? CacheDimensions.NoQualifier}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            ListEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var countSql = $"""
                    SELECT COUNT(*)
                    FROM faqs f
                    WHERE {ClubOrSharedSql.WhereClubOrShared} AND f.status = 'published'
                      AND (@CategorySlug IS NULL OR EXISTS (
                          SELECT 1 FROM faq_category_links fcl JOIN faq_categories fc ON fc.id = fcl.faq_category_id
                          WHERE fcl.faq_id = f.id AND fc.slug = @CategorySlug))
                      AND (@Keyword IS NULL OR EXISTS (
                          SELECT 1 FROM faqs_i18n fi WHERE fi.faq_id = f.id AND fi.locale = @DefaultLocale
                            AND ((fi.question IS NOT NULL AND fi.question LIKE @KeywordPattern)
                              OR (fi.answer IS NOT NULL AND fi.answer LIKE @KeywordPattern))))
                    """;

                var listSql = $"""
                    SELECT f.id AS Id,
                           CASE WHEN f.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           f.slug AS Slug, f.sort_order AS SortOrder
                    FROM faqs f
                    WHERE {ClubOrSharedSql.WhereClubOrShared} AND f.status = 'published'
                      AND (@CategorySlug IS NULL OR EXISTS (
                          SELECT 1 FROM faq_category_links fcl JOIN faq_categories fc ON fc.id = fcl.faq_category_id
                          WHERE fcl.faq_id = f.id AND fc.slug = @CategorySlug))
                      AND (@Keyword IS NULL OR EXISTS (
                          SELECT 1 FROM faqs_i18n fi WHERE fi.faq_id = f.id AND fi.locale = @DefaultLocale
                            AND ((fi.question IS NOT NULL AND fi.question LIKE @KeywordPattern)
                              OR (fi.answer IS NOT NULL AND fi.answer LIKE @KeywordPattern))))
                    ORDER BY f.sort_order, f.row_seq
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                // LIKE 萬用字元逐字轉義（% _ [ 三個在 T-SQL LIKE 有特殊意義），避免使用者輸入
                // 這些字元時被誤判成萬用字元樣式，同時也是防注入（本欄位一律走參數化查詢，
                // 這裡轉義只影響 LIKE 語意，不是防 SQL Injection 的手段本身）。
                var keywordPattern = keyword is null ? null : $"%{EscapeLikePattern(keyword)}%";

                var parameters = new
                {
                    scope.ClubId, CategorySlug = categorySlug, Keyword = keyword, KeywordPattern = keywordPattern,
                    DefaultLocale = RequestLocale.DefaultDbLocale,
                    Offset = (page - 1) * pageSize, PageSize = pageSize,
                };

                var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<FaqRow>(new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var faqIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadFaqI18nAsync(connection, faqIds, dbLocale, ct);
                var categorySlugsById = await LoadFaqCategorySlugsAsync(connection, faqIds, ct);

                var items = rows.Select(r =>
                {
                    i18nById.TryGetValue(r.Id, out var i18n);
                    var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                    var requested = i18n?.GetValueOrDefault(dbLocale);

                    return new FaqListItemDto
                    {
                        Id = r.Id,
                        Slug = r.Slug,
                        IsShared = r.IsShared,
                        SortOrder = r.SortOrder,
                        Question = RequestLocale.Pick(requested?.Question, fallback?.Question),
                        Answer = RequestLocale.Pick(requested?.Answer, fallback?.Answer),
                        CategorySlugs = categorySlugsById.GetValueOrDefault(r.Id) ?? [],
                    };
                }).ToList();

                return new PagedResult<FaqListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    /// <summary>單題詳情。<paramref name="scope"/> 同時是守門——<c>faqs.slug</c> 是
    /// <c>(club_id, slug)</c> 複合唯一鍵，不像 <c>articles.slug</c> 全站唯一，但仍必須過濾，
    /// 否則猜到別俱樂部專屬題目的 slug 就能讀到內容。</summary>
    public async Task<FaqListItemDto?> GetBySlugAsync(ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            DetailEntity, scope.ClubCode, dbLocale, slug,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var sql = $"""
                    SELECT f.id AS Id,
                           CASE WHEN f.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           f.slug AS Slug, f.sort_order AS SortOrder
                    FROM faqs f
                    WHERE f.slug = @Slug AND {ClubOrSharedSql.WhereClubOrShared} AND f.status = 'published'
                    """;

                var faq = await connection.QuerySingleOrDefaultAsync<FaqRow>(new CommandDefinition(
                    sql, new { scope.ClubId, Slug = slug }, cancellationToken: ct));

                if (faq is null)
                {
                    return null;
                }

                var i18nById = await LoadFaqI18nAsync(connection, [faq.Id], dbLocale, ct);
                var categorySlugsById = await LoadFaqCategorySlugsAsync(connection, [faq.Id], ct);

                i18nById.TryGetValue(faq.Id, out var i18n);
                var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                var requested = i18n?.GetValueOrDefault(dbLocale);

                return new FaqListItemDto
                {
                    Id = faq.Id,
                    Slug = faq.Slug,
                    IsShared = faq.IsShared,
                    SortOrder = faq.SortOrder,
                    Question = RequestLocale.Pick(requested?.Question, fallback?.Question),
                    Answer = RequestLocale.Pick(requested?.Answer, fallback?.Answer),
                    CategorySlugs = categorySlugsById.GetValueOrDefault(faq.Id) ?? [],
                };
            },
            cancellationToken);
    }

    /// <summary>
    /// 依 G-12 掛載點代碼查詢「額外」指定出現在該掛載點的題目（S1-7a，<c>faq_embed_slot_links</c>）。
    /// 🔴 這裡**只回傳「逐題額外指定」的那一半**——「由分類自動對應」是應用層（前台頁面元件）的
    /// 固定路由決定，不存在資料庫裡（docs/12 §12 第 34 點：「刻意不建掛載點對應哪個分類的對照表」）。
    /// 前台頁面要湊出完整的「聯集」效果，作法是**同時**呼叫這支端點與既有的
    /// <c>GET /api/v1/{club}/faqs?category=&lt;該頁固定對應的分類 slug&gt;</c>，自行合併去重——
    /// 不是這支端點內部做聯集，因為「哪個掛載點對應哪個分類」這件事本身只存在於前台程式碼，
    /// 後端沒有資料可查。<paramref name="slotCode"/> 找不到（打錯字或字典沒有這個代碼）視同
    /// 空清單，不是 404——掛載點是否存在跟「這個掛載點目前有沒有題目」是兩件事，前台不需要
    /// 特別處理找不到掛載點的錯誤狀態。
    /// </summary>
    public async Task<IReadOnlyList<FaqListItemDto>> ListByEmbedSlotAsync(
        ClubScope scope, string slotCode, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            EmbedEntity, scope.ClubCode, dbLocale, slotCode,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var sql = $"""
                    SELECT f.id AS Id,
                           CASE WHEN f.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           f.slug AS Slug, f.sort_order AS SortOrder
                    FROM faqs f
                    JOIN faq_embed_slot_links fesl ON fesl.faq_id = f.id
                    JOIN faq_embed_slots fes ON fes.id = fesl.faq_embed_slot_id
                    WHERE {ClubOrSharedSql.WhereClubOrShared} AND f.status = 'published' AND fes.code = @SlotCode
                    ORDER BY fesl.sort_order, f.sort_order, f.row_seq
                    """;

                var parameters = new { scope.ClubId, SlotCode = slotCode };
                var rows = (await connection.QueryAsync<FaqRow>(new CommandDefinition(sql, parameters, cancellationToken: ct))).AsList();

                var faqIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadFaqI18nAsync(connection, faqIds, dbLocale, ct);
                var categorySlugsById = await LoadFaqCategorySlugsAsync(connection, faqIds, ct);

                return rows.Select(r =>
                {
                    i18nById.TryGetValue(r.Id, out var i18n);
                    var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                    var requested = i18n?.GetValueOrDefault(dbLocale);

                    return new FaqListItemDto
                    {
                        Id = r.Id,
                        Slug = r.Slug,
                        IsShared = r.IsShared,
                        SortOrder = r.SortOrder,
                        Question = RequestLocale.Pick(requested?.Question, fallback?.Question),
                        Answer = RequestLocale.Pick(requested?.Answer, fallback?.Answer),
                        CategorySlugs = categorySlugsById.GetValueOrDefault(r.Id) ?? [],
                    };
                }).ToList() as IReadOnlyList<FaqListItemDto>;
            },
            cancellationToken);
    }

    /// <summary>瀏覽數＋1。理由與寫法逐字對應 <c>ArticlesRepository.IncrementViewCountAsync</c>：
    /// 獨立端點、資料庫端遞增、不經 <see cref="IQueryCache"/>、不呼叫 <c>InvalidateAsync</c>。</summary>
    public async Task<bool> IncrementViewCountAsync(ClubScope scope, string slug, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string sql = """
            UPDATE faqs SET view_count = view_count + 1
            WHERE slug = @Slug AND (club_id = @ClubId OR club_id IS NULL) AND status = 'published'
            """;

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { scope.ClubId, Slug = slug }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    /// <summary>👍／👎 回饋。同瀏覽數的取捨：資料庫端遞增、不快取失效——
    /// 回饋統計是後台「成效數據」的參考指標，容忍最多一個 TTL 的顯示落後。</summary>
    public async Task<bool> SubmitFeedbackAsync(ClubScope scope, string slug, bool helpful, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var sql = helpful
            ? "UPDATE faqs SET helpful_count = helpful_count + 1 WHERE slug = @Slug AND (club_id = @ClubId OR club_id IS NULL) AND status = 'published'"
            : "UPDATE faqs SET unhelpful_count = unhelpful_count + 1 WHERE slug = @Slug AND (club_id = @ClubId OR club_id IS NULL) AND status = 'published'";

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { scope.ClubId, Slug = slug }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    /// <summary>
    /// 零結果搜尋關鍵字（<c>faq_search_misses</c>，成效統計而非日誌，見該表 db/club-schema.sql
    /// 註解與 apps/api/README.md「我的判斷」）。🔴 這支端點刻意獨立於 <see cref="ListAsync"/>
    /// 之外，不是「搜尋回傳 0 筆就自動記錄」——列表端點是快取讀取路徑，把寫入嵌進去會讓同一個
    /// entity 同時身兼讀與寫，且會讓「使用者還在打字、中途出現的暫時 0 筆」也被計入，語意不對；
    /// 呼叫時機交給前端：真正呈現「找不到結果」畫面給使用者看到的那一刻才呼叫。
    /// 先 <c>UPDATE</c>、0 筆才 <c>INSERT</c>（先查後寫，接受與既有標籤／分類建立同等級的低機率
    /// 競態視窗，跟本專案既有慣例一致，不做額外的鎖或 <c>MERGE</c>）。
    /// </summary>
    public async Task RecordSearchMissAsync(ClubScope scope, string keyword, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string updateSql = """
            UPDATE faq_search_misses
            SET hit_count = hit_count + 1, last_searched_at = SYSUTCDATETIME(), updated_at = SYSUTCDATETIME()
            WHERE club_id = @ClubId AND keyword = @Keyword
            """;

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            updateSql, new { scope.ClubId, Keyword = keyword }, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            const string insertSql = """
                INSERT INTO faq_search_misses (id, club_id, keyword, hit_count, last_searched_at)
                VALUES (NEWID(), @ClubId, @Keyword, 1, SYSUTCDATETIME())
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql, new { scope.ClubId, Keyword = keyword }, cancellationToken: cancellationToken));
        }
    }

    private static string EscapeLikePattern(string value)
        => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    private sealed record FaqI18nRow(Guid FaqId, string Locale, string? Question, string? Answer);

    private static async Task<Dictionary<Guid, Dictionary<string, FaqI18nRow>>> LoadFaqI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> faqIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (faqIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT faq_id AS FaqId, locale AS Locale, question AS Question, answer AS Answer
            FROM faqs_i18n
            WHERE faq_id IN @FaqIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<FaqI18nRow>(new CommandDefinition(
            sql, new { FaqIds = faqIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.FaqId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private sealed record FaqCategorySlugRow(Guid FaqId, string Slug);

    private static async Task<Dictionary<Guid, List<string>>> LoadFaqCategorySlugsAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> faqIds, CancellationToken cancellationToken)
    {
        if (faqIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT fcl.faq_id AS FaqId, fc.slug AS Slug
            FROM faq_category_links fcl
            JOIN faq_categories fc ON fc.id = fcl.faq_category_id
            WHERE fcl.faq_id IN @FaqIds
            """;

        var rows = await connection.QueryAsync<FaqCategorySlugRow>(new CommandDefinition(
            sql, new { FaqIds = faqIds }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.FaqId).ToDictionary(g => g.Key, g => g.Select(r => r.Slug).ToList());
    }
}
