using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.News;

public sealed class ArticlesRepository(
    IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrlResolver)
{
    private const string ListEntity = "articles";
    private const string DetailEntity = "article-detail";


    private sealed record ArticleListRow(
        Guid Id, bool IsShared, string Slug, string CategoryCode, string? CoverKey, bool IsFeatured, DateTime? PublishedAt);

    private sealed record ArticleDetailRow(
        Guid Id, bool IsShared, string Slug, string CategoryCode, string? CoverKey, bool IsFeatured,
        int ViewCount, DateTime? PublishedAt, string? CanonicalPath, bool IsNoindex,
        string? OgImageKey, int? OgImageWidth, int? OgImageHeight);

    private sealed record ArticleI18nRow(Guid ArticleId, string Locale, string? Title, string? Summary, string? SeoTitle, string? SeoDescription);

    // 單篇詳情專用：比 ArticleI18nRow 多一個 Body（含 JSON 內容，列表查詢不需要，不放進共用型別
    // 避免每次列表都多拉一個可能很大的欄位）、SeoKeywords 與 OgImageAlt（S1-12 新增，同理列表
    // 查詢不需要）。
    // ⚠️ Dapper 的 record 建構子具現化要求 SELECT 的欄位順序與數量跟建構子完全對齊
    // （docs/18-work-errors.md E-20），所以這裡跟 SQL 的 SELECT 清單逐一比對過。
    private sealed record ArticleDetailI18nRow(
        Guid ArticleId, string Locale, string? Title, string? Summary, string? SeoTitle, string? SeoDescription,
        string? Body, string? SeoKeywords, string? OgImageAlt);

    /// <summary>全站預設 OG 圖片（S1-12 驗收退回後補做，<c>Club.OgImageKey</c>），單頁優先序的
    /// 第二層，見 <see cref="ResolveOgImageAsync"/>。</summary>
    private sealed record ClubOgImageRow(string? OgImageKey, int? OgImageWidth, int? OgImageHeight);

    /// <summary>
    /// 新聞列表。⛔ 公開讀取 API 只回傳 <c>status = 'published'</c> 且已到發布時間的文章——
    /// 草稿與排程中的文章即使能被猜到 slug 也不對外，這是刻意的業務規則，不是遺漏。
    /// <c>articles.club_id</c> 是 9 張可為空表之一，套用「俱樂部專屬優先、回退共同」。
    /// **快取**：qualifier 涵蓋 <paramref name="categoryCode"/>／<paramref name="tagSlug"/>
    /// （S1-5 新增）／<paramref name="page"/>／<paramref name="pageSize"/>。
    /// 🔴 **排程發布的語意後果**：「已到發布時間」（<c>a.published_at &lt;= SYSUTCDATETIME()</c>）
    /// 這件事本身沒有任何寫入事件——後台排定 10:00 發布一篇文章，沒有人會在 10:00 那一刻呼叫
    /// <see cref="IQueryCache.InvalidateAsync"/>。**TTL 是這個情境目前唯一的失效機制**：文章
    /// 實際對外可見的時間點最多延後一個 TTL（預設 300 秒，見 <see cref="Caching.RedisQueryCache"/>）。
    /// 這不是 bug，是本次任務範圍的已知取捨（見 apps/api/README.md「排程發布與快取」）。
    /// </summary>
    public async Task<PagedResult<ArticleListItemDto>> ListAsync(
        ClubScope scope, string? categoryCode, string? tagSlug, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var qualifier = $"{categoryCode ?? CacheDimensions.NoQualifier}:{tagSlug ?? CacheDimensions.NoQualifier}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            ListEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                // S1-5 新增：標籤篩選用 EXISTS 子查詢，不是 JOIN——一篇文章可能掛多個標籤，
                // JOIN article_tags 會讓同一篇文章重複出現在結果列，分頁筆數因此對不起來；
                // EXISTS 天生只回傳「符不符合」，不會製造重複列。
                var countSql = $"""
                    SELECT COUNT(*)
                    FROM articles a
                    JOIN article_categories ac ON ac.id = a.article_category_id
                    WHERE {ClubOrSharedSql.WhereClubOrShared}
                      AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
                      AND (@CategoryCode IS NULL OR ac.code = @CategoryCode)
                      AND (@TagSlug IS NULL OR EXISTS (
                          SELECT 1 FROM article_tags at JOIN tags t ON t.id = at.tag_id
                          WHERE at.article_id = a.id AND t.slug = @TagSlug))
                    """;

                var listSql = $"""
                    SELECT a.id AS Id,
                           CASE WHEN a.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           a.slug AS Slug, ac.code AS CategoryCode, a.cover_key AS CoverKey,
                           a.is_featured AS IsFeatured, a.published_at AS PublishedAt
                    FROM articles a
                    JOIN article_categories ac ON ac.id = a.article_category_id
                    WHERE {ClubOrSharedSql.WhereClubOrShared}
                      AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
                      AND (@CategoryCode IS NULL OR ac.code = @CategoryCode)
                      AND (@TagSlug IS NULL OR EXISTS (
                          SELECT 1 FROM article_tags at JOIN tags t ON t.id = at.tag_id
                          WHERE at.article_id = a.id AND t.slug = @TagSlug))
                    -- 同一天發布的多篇文章要有穩定的次要排序鍵，否則同一天內的順序不保證。
                    -- 🔴 次要鍵刻意是 row_seq ASC，不是 DESC：row_seq 是 IDENTITY(1,1)，插入順序
                    -- 跟種子腳本讀 site/src/data/news.json 的陣列順序一致（db/seed/generate-club-seed-sql.py
                    -- 依序插入）；mockup（site/dist）同一天內的文章一律照 JSON 陣列的先後順序顯示
                    -- （2026-09-21 用 compare-dom.mjs 實跑 zh/news/club、zh/news/index 等頁核對過：
                    -- 2025-04-11 那天 061→063、2024-12-18 那天 079→080，皆為 row_seq 遞增），
                    -- 用 DESC 會把同一天內的順序整組反過來，害「主站與 mockup 一模一樣」的驗收關卡失敗。
                    ORDER BY a.published_at DESC, a.row_seq ASC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                var parameters = new
                {
                    scope.ClubId, CategoryCode = categoryCode, TagSlug = tagSlug,
                    Offset = (page - 1) * pageSize, PageSize = pageSize,
                };

                var totalCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<ArticleListRow>(
                    new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var articleIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadArticleI18nAsync(connection, articleIds, dbLocale, ct);
                var categoryCodes = rows.Select(r => r.CategoryCode).Distinct().ToList();
                var categoryNameByCode = await LoadCategoryNamesAsync(connection, categoryCodes, dbLocale, ct);
                var tagsById = await LoadArticleTagsAsync(connection, articleIds, dbLocale, ct);

                var items = rows.Select(r =>
                {
                    i18nById.TryGetValue(r.Id, out var i18n);
                    var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                    var requested = i18n?.GetValueOrDefault(dbLocale);

                    return new ArticleListItemDto
                    {
                        Id = r.Id,
                        IsShared = r.IsShared,
                        Slug = r.Slug,
                        CategoryCode = r.CategoryCode,
                        CategoryName = categoryNameByCode.GetValueOrDefault(r.CategoryCode),
                        CoverKey = r.CoverKey,
                        IsFeatured = r.IsFeatured,
                        PublishedAt = r.PublishedAt,
                        Title = RequestLocale.Pick(requested?.Title, fallback?.Title),
                        Summary = RequestLocale.Pick(requested?.Summary, fallback?.Summary),
                        Tags = tagsById.GetValueOrDefault(r.Id) ?? [],
                    };
                }).ToList();

                return new PagedResult<ArticleListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    /// <summary>
    /// 單篇新聞。🔴 <paramref name="scope"/> 同時是「這篇文章看不看得到」的守門——
    /// <c>articles.slug</c> 全站唯一（跨俱樂部），若不過濾 club_id，猜到別俱樂部專屬文章的 slug
    /// 就能讀到內容，等於繞過俱樂部邊界。WHERE 子句與列表查詢用同一份
    /// <see cref="Data.ClubOrSharedSql"/> 常數，不是另外重寫的邏輯。
    /// **快取**：qualifier 是 <paramref name="slug"/>。🔴 **查無資料（404）不快取**——
    /// <see cref="IQueryCache.GetOrCreateAsync{T}"/> 對 <c>null</c> 回傳值一律不寫入快取，這裡是
    /// 刻意依賴的行為：草稿文章排程發布後，若曾經被打過（例如猜測 slug 或提早分享連結）而快取住
    /// 一個 404，一旦真正發布就必須立刻查得到，不能被 TTL 內的負向快取多擋一段時間。
    /// 同一份排程發布的 TTL 延遲說明見 <see cref="ListAsync"/> 上的說明，兩者適用同一個 TTL，
    /// 但 404 negative caching 完全不受影響（因為根本不會被快取）。
    /// </summary>
    public async Task<ArticleDetailDto?> GetBySlugAsync(
        ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            DetailEntity, scope.ClubCode, dbLocale, slug,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var sql = $"""
                    SELECT a.id AS Id,
                           CASE WHEN a.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           a.slug AS Slug, ac.code AS CategoryCode, a.cover_key AS CoverKey,
                           a.is_featured AS IsFeatured, a.view_count AS ViewCount, a.published_at AS PublishedAt,
                           a.canonical_path AS CanonicalPath, a.is_noindex AS IsNoindex,
                           a.og_image_key AS OgImageKey, a.og_image_width AS OgImageWidth, a.og_image_height AS OgImageHeight
                    FROM articles a
                    JOIN article_categories ac ON ac.id = a.article_category_id
                    WHERE a.slug = @Slug AND {ClubOrSharedSql.WhereClubOrShared}
                      AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
                    """;

                var article = await connection.QuerySingleOrDefaultAsync<ArticleDetailRow>(new CommandDefinition(
                    sql, new { scope.ClubId, Slug = slug }, cancellationToken: ct));

                if (article is null)
                {
                    return null;
                }

                const string i18nSql = """
                    SELECT article_id AS ArticleId, locale AS Locale, title AS Title, summary AS Summary,
                           seo_title AS SeoTitle, seo_description AS SeoDescription, CAST(body AS nvarchar(max)) AS Body,
                           seo_keywords AS SeoKeywords, og_image_alt AS OgImageAlt
                    FROM articles_i18n
                    WHERE article_id = @ArticleId AND locale IN @Locales
                    """;
                var locales = dbLocale == RequestLocale.DefaultDbLocale
                    ? new[] { dbLocale }
                    : new[] { dbLocale, RequestLocale.DefaultDbLocale };

                var i18nRows = (await connection.QueryAsync<ArticleDetailI18nRow>(new CommandDefinition(
                    i18nSql, new { ArticleId = article.Id, Locales = locales }, cancellationToken: ct))).ToList();

                var byLocale = i18nRows.ToDictionary(r => r.Locale);
                byLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);
                byLocale.TryGetValue(dbLocale, out var requested);

                var categoryName = (await LoadCategoryNamesAsync(connection, [article.CategoryCode], dbLocale, ct))
                    .GetValueOrDefault(article.CategoryCode);

                var tags = (await LoadArticleTagsAsync(connection, [article.Id], dbLocale, ct))
                    .GetValueOrDefault(article.Id) ?? [];
                var coreValueTags = await LoadArticleCoreValueTagsAsync(connection, article.Id, ct);
                var relations = await LoadArticleRelationsAsync(connection, article.Id, ct);

                var ogImage = await ResolveOgImageAsync(connection, article, scope.ClubId, ct);
                var ogImageAlt = RequestLocale.Pick(requested?.OgImageAlt, fallback?.OgImageAlt);

                return new ArticleDetailDto
                {
                    Id = article.Id,
                    IsShared = article.IsShared,
                    Slug = article.Slug,
                    CategoryCode = article.CategoryCode,
                    CategoryName = categoryName,
                    CoverKey = article.CoverKey,
                    IsFeatured = article.IsFeatured,
                    ViewCount = article.ViewCount,
                    PublishedAt = article.PublishedAt,
                    Title = RequestLocale.Pick(requested?.Title, fallback?.Title),
                    Summary = RequestLocale.Pick(requested?.Summary, fallback?.Summary),
                    BodyJson = RequestLocale.Pick(requested?.Body, fallback?.Body),
                    SeoTitle = RequestLocale.Pick(requested?.SeoTitle, fallback?.SeoTitle),
                    SeoDescription = RequestLocale.Pick(requested?.SeoDescription, fallback?.SeoDescription),
                    SeoKeywords = RequestLocale.Pick(requested?.SeoKeywords, fallback?.SeoKeywords),
                    CanonicalPath = article.CanonicalPath,
                    IsNoindex = article.IsNoindex,
                    OgImageUrl = ogImage.Url,
                    OgImageWidth = ogImage.Width,
                    OgImageHeight = ogImage.Height,
                    // 只有「這篇文章自己有專屬 OG 圖片」時才有意義輸出 alt——全站預設圖與封面圖
                    // 回退時沒有對應的替代文字來源，見 ResolveOgImageAsync 的判斷。
                    OgImageAlt = ogImage.Key == article.OgImageKey ? ogImageAlt : null,
                    Tags = tags,
                    CoreValueTags = coreValueTags,
                    Relations = relations,
                };
            },
            cancellationToken);
    }

    private readonly record struct ResolvedOgImage(string? Url, string? Key, int? Width, int? Height);

    /// <summary>
    /// OG 圖片優先序（S1-12 驗收退回後補做，主站規劃書 §4.8 H「單頁 SEO：…OG 圖文…」）：
    /// **這篇文章專屬的 OG 圖片 &gt; 全站預設 OG 圖片（<c>Club.OgImageKey</c>） &gt; 這篇文章的
    /// 封面圖片（<c>cover_key</c>）**。全站預設圖與封面圖回退時**不輸出 alt**——兩者都沒有對應的
    /// 替代文字來源（<c>Club</c> 沒有 OG 圖片替代文字欄位，<c>cover_key</c> 本身就沒有 alt 欄位，
    /// 是既有落差，見 docs/14-invariants.md「其餘既有圖片欄位仍是同樣的缺口」），呼叫端
    /// （<see cref="GetBySlugAsync"/>）依 <see cref="ResolvedOgImage.Key"/> 是否等於文章自己的
    /// <c>OgImageKey</c> 判斷要不要一併輸出 alt。
    /// </summary>
    private async Task<ResolvedOgImage> ResolveOgImageAsync(
        System.Data.IDbConnection connection, ArticleDetailRow article, Guid clubId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(article.OgImageKey))
        {
            return new ResolvedOgImage(imageUrlResolver.Resolve(article.OgImageKey), article.OgImageKey, article.OgImageWidth, article.OgImageHeight);
        }

        const string clubSql = """
            SELECT og_image_key AS OgImageKey, og_image_width AS OgImageWidth, og_image_height AS OgImageHeight
            FROM clubs WHERE id = @ClubId
            """;
        var club = await connection.QuerySingleOrDefaultAsync<ClubOgImageRow>(new CommandDefinition(
            clubSql, new { ClubId = clubId }, cancellationToken: cancellationToken));

        if (club is not null && !string.IsNullOrEmpty(club.OgImageKey))
        {
            return new ResolvedOgImage(imageUrlResolver.Resolve(club.OgImageKey), club.OgImageKey, club.OgImageWidth, club.OgImageHeight);
        }

        if (!string.IsNullOrEmpty(article.CoverKey))
        {
            return new ResolvedOgImage(imageUrlResolver.Resolve(article.CoverKey), article.CoverKey, null, null);
        }

        return new ResolvedOgImage(null, null, null, null);
    }

    /// <summary>
    /// 瀏覽數＋1（S1-5 新增，規劃書 B2「瀏覽數統計」）。🔴 **刻意不經過 <see cref="IQueryCache"/>**：
    /// 這是一個獨立的公開端點（前台渲染完頁面後另外呼叫一次），不是掛在
    /// <see cref="GetBySlugAsync"/> 的讀取路徑上「順便」累加——掛在讀取路徑上會讓每一次公開讀取
    /// 都變成一次寫入，違反 docs/17 §4「讀寫分離、寫入才碰主檔」的精神，也會讓本來可以完全命中
    /// 快取的高流量讀取路徑被迫多一趟資料庫寫入。**也刻意不呼叫 <see cref="IQueryCache.InvalidateAsync"/>**：
    /// 瀏覽數不在 docs/17 §4「五類不得讀快取」之列，容忍最多一個 TTL（預設 300 秒）的顯示落後是
    /// 可接受的取捨——如果每次瀏覽都讓整個文章詳情快取失效，等於瀏覽數這個低重要性欄位拖垮了
    /// 標題、內文這些高重要性欄位的快取命中率，本末倒置。
    /// 直接用 <c>UPDATE ... SET view_count = view_count + 1</c>（資料庫端遞增，不是「讀出來
    /// +1 再寫回去」，避免高併發下的更新遺失）。只有 <c>published</c> 且已到發布時間的文章才會
    /// 被加到，找不到符合條件的文章回傳 <c>false</c>（呼叫端轉 404，不洩漏「這個 slug 存在但
    /// 還沒發布」）。
    /// </summary>
    public async Task<bool> IncrementViewCountAsync(ClubScope scope, string slug, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string sql = """
            UPDATE articles
            SET view_count = view_count + 1
            WHERE slug = @Slug AND (club_id = @ClubId OR club_id IS NULL)
              AND status = 'published' AND (published_at IS NULL OR published_at <= SYSUTCDATETIME())
            """;

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { scope.ClubId, Slug = slug }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    private static async Task<Dictionary<Guid, Dictionary<string, ArticleI18nRow>>> LoadArticleI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> articleIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (articleIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT article_id AS ArticleId, locale AS Locale, title AS Title, summary AS Summary,
                   seo_title AS SeoTitle, seo_description AS SeoDescription
            FROM articles_i18n
            WHERE article_id IN @ArticleIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<ArticleI18nRow>(new CommandDefinition(
            sql, new { ArticleIds = articleIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.ArticleId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private static async Task<Dictionary<string, string?>> LoadCategoryNamesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<string> categoryCodes, string dbLocale, CancellationToken cancellationToken)
    {
        if (categoryCodes.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT ac.code AS Code, aci.locale AS Locale, aci.name AS Name
            FROM article_categories ac
            JOIN article_categories_i18n aci ON aci.article_category_id = ac.id
            WHERE ac.code IN @Codes AND aci.locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = (await connection.QueryAsync<(string Code, string Locale, string? Name)>(new CommandDefinition(
            sql, new { Codes = categoryCodes, Locales = locales }, cancellationToken: cancellationToken))).ToList();

        return rows
            .GroupBy(r => r.Code)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
                    return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
                });
    }

    /// <summary><c>value_tag_links.entity_type</c> 給文章用的值——跟後台
    /// <c>AdminArticlesRepository.ArticleEntityType</c> 是同一個字面值，兩邊各自宣告一份常數
    /// （唯讀 Dapper 路徑跟寫入 EF Core 路徑本來就是兩個獨立的 repository，不共用型別），
    /// 改動時要兩邊一起改。</summary>
    private const string ArticleEntityType = "article";

    private sealed record ArticleTagRow(Guid ArticleId, string Slug, string Locale, string? Name);

    /// <summary>標籤（S1-5 新增）：一次查出多篇文章的標籤，依語系回退挑出顯示名稱。</summary>
    private static async Task<Dictionary<Guid, List<ArticleTagDto>>> LoadArticleTagsAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> articleIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (articleIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT at.article_id AS ArticleId, t.slug AS Slug, ti.locale AS Locale, ti.name AS Name
            FROM article_tags at
            JOIN tags t ON t.id = at.tag_id
            JOIN tags_i18n ti ON ti.tag_id = t.id
            WHERE at.article_id IN @ArticleIds AND ti.locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = (await connection.QueryAsync<ArticleTagRow>(new CommandDefinition(
            sql, new { ArticleIds = articleIds, Locales = locales }, cancellationToken: cancellationToken))).ToList();

        return rows
            .GroupBy(r => r.ArticleId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(r => r.Slug)
                    .Select(tagGroup =>
                    {
                        var byLocale = tagGroup.ToDictionary(r => r.Locale, r => r.Name);
                        return new ArticleTagDto
                        {
                            Slug = tagGroup.Key,
                            Name = RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale)),
                        };
                    })
                    .ToList());
    }

    /// <summary>核心價值標籤（S1-5 新增）。不分語系——值本身是系統代碼，不是自由文字。</summary>
    private static async Task<List<string>> LoadArticleCoreValueTagsAsync(
        System.Data.IDbConnection connection, Guid articleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT value_tag FROM value_tag_links WHERE entity_type = @EntityType AND entity_id = @ArticleId
            """;

        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            sql, new { EntityType = ArticleEntityType, ArticleId = articleId }, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    /// <summary>Dapper 具現化用的原始列——跟本檔其餘 <c>*Row</c> record 同一種寫法（位置參數建構子），
    /// 不直接用 <see cref="ArticleRelationDto"/>（<c>required</c> 屬性）給 Dapper 具現化，維持本檔
    /// 「Row 用來對應 SQL、Dto 用來對外」這條既有分工（docs/18-work-errors.md E-20 同一個精神）。</summary>
    private sealed record ArticleRelationRow(string TargetType, Guid TargetId);

    /// <summary>關聯（S1-5 新增）。</summary>
    private static async Task<List<ArticleRelationDto>> LoadArticleRelationsAsync(
        System.Data.IDbConnection connection, Guid articleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT target_type AS TargetType, target_id AS TargetId FROM article_relations WHERE article_id = @ArticleId
            """;

        var rows = await connection.QueryAsync<ArticleRelationRow>(new CommandDefinition(
            sql, new { ArticleId = articleId }, cancellationToken: cancellationToken));

        return rows.Select(r => new ArticleRelationDto { TargetType = r.TargetType, TargetId = r.TargetId }).ToList();
    }
}
