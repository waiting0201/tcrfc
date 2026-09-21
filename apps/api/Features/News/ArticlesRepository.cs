using Dapper;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.News;

public sealed class ArticlesRepository(IClubSqlConnectionFactory connectionFactory)
{
    private sealed record ArticleListRow(
        Guid Id, bool IsShared, string Slug, string CategoryCode, string? CoverKey, bool IsFeatured, DateTime? PublishedAt);

    private sealed record ArticleDetailRow(
        Guid Id, bool IsShared, string Slug, string CategoryCode, string? CoverKey, bool IsFeatured,
        int ViewCount, DateTime? PublishedAt);

    private sealed record ArticleI18nRow(Guid ArticleId, string Locale, string? Title, string? Summary, string? SeoTitle, string? SeoDescription);

    // 單篇詳情專用：比 ArticleI18nRow 多一個 Body（含 JSON 內容，列表查詢不需要，不放進共用型別
    // 避免每次列表都多拉一個可能很大的欄位）。
    // ⚠️ Dapper 的 record 建構子具現化要求 SELECT 的欄位順序與數量跟建構子完全對齊
    // （docs/18-work-errors.md E-20），所以這裡跟 SQL 的 SELECT 清單逐一比對過。
    private sealed record ArticleDetailI18nRow(
        Guid ArticleId, string Locale, string? Title, string? Summary, string? SeoTitle, string? SeoDescription, string? Body);

    /// <summary>
    /// 新聞列表。⛔ 公開讀取 API 只回傳 <c>status = 'published'</c> 且已到發布時間的文章——
    /// 草稿與排程中的文章即使能被猜到 slug 也不對外，這是刻意的業務規則，不是遺漏。
    /// <c>articles.club_id</c> 是 9 張可為空表之一，套用「俱樂部專屬優先、回退共同」。
    /// </summary>
    public async Task<PagedResult<ArticleListItemDto>> ListAsync(
        ClubScope scope, string? categoryCode, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var countSql = $"""
            SELECT COUNT(*)
            FROM articles a
            JOIN article_categories ac ON ac.id = a.article_category_id
            WHERE {ClubOrSharedSql.WhereClubOrShared}
              AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
              AND (@CategoryCode IS NULL OR ac.code = @CategoryCode)
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
            scope.ClubId, CategoryCode = categoryCode, Offset = (page - 1) * pageSize, PageSize = pageSize,
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<ArticleListRow>(
            new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken))).AsList();

        var articleIds = rows.Select(r => r.Id).ToList();
        var i18nById = await LoadArticleI18nAsync(connection, articleIds, dbLocale, cancellationToken);
        var categoryCodes = rows.Select(r => r.CategoryCode).Distinct().ToList();
        var categoryNameByCode = await LoadCategoryNamesAsync(connection, categoryCodes, dbLocale, cancellationToken);

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
            };
        }).ToList();

        return new PagedResult<ArticleListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>
    /// 單篇新聞。🔴 <paramref name="scope"/> 同時是「這篇文章看不看得到」的守門——
    /// <c>articles.slug</c> 全站唯一（跨俱樂部），若不過濾 club_id，猜到別俱樂部專屬文章的 slug
    /// 就能讀到內容，等於繞過俱樂部邊界。WHERE 子句與列表查詢用同一份
    /// <see cref="Data.ClubOrSharedSql"/> 常數，不是另外重寫的邏輯。
    /// </summary>
    public async Task<ArticleDetailDto?> GetBySlugAsync(
        ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var sql = $"""
            SELECT a.id AS Id,
                   CASE WHEN a.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                   a.slug AS Slug, ac.code AS CategoryCode, a.cover_key AS CoverKey,
                   a.is_featured AS IsFeatured, a.view_count AS ViewCount, a.published_at AS PublishedAt
            FROM articles a
            JOIN article_categories ac ON ac.id = a.article_category_id
            WHERE a.slug = @Slug AND {ClubOrSharedSql.WhereClubOrShared}
              AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
            """;

        var article = await connection.QuerySingleOrDefaultAsync<ArticleDetailRow>(new CommandDefinition(
            sql, new { scope.ClubId, Slug = slug }, cancellationToken: cancellationToken));

        if (article is null)
        {
            return null;
        }

        const string i18nSql = """
            SELECT article_id AS ArticleId, locale AS Locale, title AS Title, summary AS Summary,
                   seo_title AS SeoTitle, seo_description AS SeoDescription, CAST(body AS nvarchar(max)) AS Body
            FROM articles_i18n
            WHERE article_id = @ArticleId AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var i18nRows = (await connection.QueryAsync<ArticleDetailI18nRow>(new CommandDefinition(
            i18nSql, new { ArticleId = article.Id, Locales = locales }, cancellationToken: cancellationToken))).ToList();

        var byLocale = i18nRows.ToDictionary(r => r.Locale);
        byLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);
        byLocale.TryGetValue(dbLocale, out var requested);

        var categoryName = (await LoadCategoryNamesAsync(connection, [article.CategoryCode], dbLocale, cancellationToken))
            .GetValueOrDefault(article.CategoryCode);

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
        };
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
}
