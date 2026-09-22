using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 🔴 後台新聞（B2）寫入通則的落點——其他模組之後照抄的就是這個檔案的形狀：
/// 驗證（分類存在、標題非空白、排程時間在未來）、雙語側表 upsert、俱樂部範圍（含「共用內容唯讀」）、
/// 樂觀並行控制（<c>updated_at</c> 當並行權杖，靠 EF Core <c>IsConcurrencyToken</c>，見
/// <see cref="Data.ClubDbContextCustomizations"/>）、狀態轉換（draft／scheduled → published／scheduled）。
///
/// ⚠️ 寫入一律走 EF Core（<see cref="ClubDbContext"/>），唯讀查詢仍是 Dapper 的
/// <see cref="Features.News.ArticlesRepository"/>——兩者刻意分開，不是這份檔案要取代那份。
/// </summary>
public sealed class AdminArticlesRepository(ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage)
{
    /// <summary>公開讀取 API 用的 entity 名稱，寫入成功後要讓這兩個快取失效（docs/17 §4「write-invalidate」）。</summary>
    private const string PublicListEntity = "articles";
    private const string PublicDetailEntity = "article-detail";

    // ───────────────────────────── 讀取（後台專用，含全部狀態） ─────────────────────────────

    /// <summary>
    /// 後台清單：回傳這個俱樂部專屬 ＋ 共用（<c>club_id IS NULL</c>）的文章，**不篩狀態**——
    /// 這正是本輪要補的缺口（STATUS.md S0-12：公開 API 只回已發布內容，後台驗證不到草稿／
    /// 排程／已停用）。全程 <c>AsNoTracking</c>，這條路徑不做寫入。
    /// </summary>
    public async Task<PagedResult<AdminArticleListItemDto>> ListAsync(
        ClubScope scope, string? status, string? categoryCode, string? keyword,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Articles.AsNoTracking()
            .Where(a => a.ClubId == scope.ClubId || a.ClubId == null);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            query = query.Where(a => a.ArticleCategory.Code == categoryCode);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 只搜尋中文標題（zh-Hant 是必填語系，任何一篇文章都一定有這個側表列）。
            query = query.Where(a => a.ArticlesI18ns.Any(i =>
                i.Locale == RequestLocale.DefaultDbLocale && i.Title != null && i.Title.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.UpdatedAt)
            .ThenByDescending(a => a.RowSeq)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Slug,
                CategoryCode = a.ArticleCategory.Code,
                a.CoverKey,
                a.IsFeatured,
                a.Status,
                a.PublishedAt,
                a.ClubId,
                a.UpdatedAt,
                TitleZh = a.ArticlesI18ns
                    .Where(i => i.Locale == RequestLocale.DefaultDbLocale)
                    .Select(i => i.Title)
                    .FirstOrDefault(),
                TitleEn = a.ArticlesI18ns
                    .Where(i => i.Locale == "en")
                    .Select(i => i.Title)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminArticleListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            CategoryCode = r.CategoryCode,
            CoverKey = r.CoverKey,
            IsFeatured = r.IsFeatured,
            Status = r.Status,
            PublishedAt = r.PublishedAt,
            IsShared = r.ClubId is null,
            UpdatedAt = r.UpdatedAt,
            TitleZh = r.TitleZh,
            TitleEn = r.TitleEn,
        }).ToList();

        return new PagedResult<AdminArticleListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>後台單篇詳情。跨俱樂部（非共用、非本俱樂部）回傳 <c>null</c>（404），
    /// 不透露「這個 id 存在但屬於別的俱樂部」。</summary>
    public async Task<AdminArticleDetailDto?> GetByIdAsync(ClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var article = await dbContext.Articles.AsNoTracking()
            .Include(a => a.ArticleCategory)
            .Include(a => a.ArticlesI18ns)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null || (article.ClubId is not null && article.ClubId != scope.ClubId))
        {
            return null;
        }

        return ToDetailDto(article);
    }

    // ───────────────────────────── 寫入 ─────────────────────────────

    /// <summary>
    /// 🔴🔴🔴 S0-8 修正：<paramref name="articleId"/> 由呼叫端（<see cref="AdminArticlesEndpoints"/>）
    /// 先產生，不是這裡臨時決定——因為單一請求契約下，封面圖片要在寫入資料列**之前**就先上傳
    /// 成功（規劃書 §4.0「寫入成功才更新資料列」的另一半：blob 要先寫、資料列後寫），
    /// 物件鍵路徑需要知道「這張圖屬於哪一筆將要建立的資料列」，id 因此必須提前決定。
    /// <paramref name="coverKey"/> 是呼叫端已經上傳成功的物件鍵（沒有夾檔案時為 <c>null</c>）——
    /// 這個方法本身完全不碰物件儲存，只負責把已知結果寫進資料列，職責跟舊版一致。
    /// </summary>
    public async Task<AdminArticleDetailDto> CreateAsync(
        ClubScope scope, Guid articleId, CreateArticleRequest request, string? coverKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateContent(request.Content);
        SlugPolicy.Validate(request.Slug);

        var category = await ResolveCategoryAsync(request.CategoryCode, cancellationToken);

        if (await dbContext.Articles.AsNoTracking().AnyAsync(a => a.Slug == request.Slug, cancellationToken))
        {
            throw new ArticleSlugConflictException(request.Slug);
        }

        if (request.IsFeatured)
        {
            await EnsureFeaturedCapAsync(scope, excludeArticleId: null, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var article = new Article
        {
            Id = articleId,
            ClubId = scope.ClubId, // 🔴 後台建立的文章一律歸屬呼叫端當下的俱樂部，不能建立共用（club_id NULL）
                                    // 內容——docs/14-invariants.md「共同內容...只有超管能建立」，
                                    // 目前沒有超管角色可以繞過，這裡直接不給這條路。
            Slug = request.Slug,
            ArticleCategoryId = category.Id,
            CoverKey = coverKey,
            IsFeatured = request.IsFeatured,
            Status = "draft", // 🔴 一律從草稿開始，狀態轉換是獨立端點（Publish／Schedule），不接受這裡帶入
            PublishedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Articles.Add(article);
        AddOrReplaceI18n(article, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(article, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return (await GetByIdAsync(scope, article.Id, cancellationToken))!;
    }

    /// <summary>
    /// 🔴🔴🔴 S0-8 修正：<paramref name="coverUpdate"/> 取代舊版直接讀 <c>request.CoverKey</c>——
    /// 呼叫端（<see cref="AdminArticlesEndpoints"/>）已經把「這次請求要不要上傳新圖片／要不要清空」
    /// 解成明確的三態（見 <see cref="CoverKeyUpdate"/>），這裡只負責套用，不重新判斷語意，
    /// 也完全不碰物件儲存的上傳——**新圖片在呼叫這個方法之前就已經上傳成功**（規劃書 §4.0
    /// 「寫入 blob 成功才更新資料列」）。
    /// </summary>
    public async Task<AdminArticleDetailDto?> UpdateAsync(
        ClubScope scope, Guid id, UpdateArticleRequest request, CoverKeyUpdate coverUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateContent(request.Content);
        SlugPolicy.Validate(request.Slug);

        var article = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (article is null)
        {
            return null;
        }

        var category = await ResolveCategoryAsync(request.CategoryCode, cancellationToken);

        if (!string.Equals(article.Slug, request.Slug, StringComparison.Ordinal)
            && await dbContext.Articles.AsNoTracking().AnyAsync(a => a.Slug == request.Slug && a.Id != id, cancellationToken))
        {
            throw new ArticleSlugConflictException(request.Slug);
        }

        if (request.IsFeatured && !article.IsFeatured)
        {
            await EnsureFeaturedCapAsync(scope, excludeArticleId: id, cancellationToken);
        }

        ApplyConcurrencyToken(article, request.ExpectedUpdatedAt);

        // 🔴 換圖成功才刪舊物件（規劃書 §4.0）：這裡先記住「換之前」的鍵，等 DB 寫入真的成功
        // 之後才刪除——刪除順序不能提前，否則若後續的並行檢查／SaveChanges 失敗，舊圖已經被
        // 刪掉但資料庫其實還指著它，會變成資料列引用一個不存在的物件鍵。
        var previousCoverKey = article.CoverKey;
        // CoverKeyUpdate.Keep 時直接沿用目前的值——effectiveCoverKey 會跟 previousCoverKey 相等，
        // 下面「換了才刪舊物件」的比較自然不會觸發刪除，不需要另外寫一條「沒變就跳過」的分支。
        var effectiveCoverKey = coverUpdate.Change ? coverUpdate.NewKey : article.CoverKey;

        article.Slug = request.Slug;
        article.ArticleCategoryId = category.Id;
        article.CoverKey = effectiveCoverKey;
        article.IsFeatured = request.IsFeatured;
        article.UpdatedAt = DateTime.UtcNow;
        article.UpdatedBy = operatorId;

        // 內容欄位視為「整份取代」：zh 一律覆寫，en 省略＝清掉既有英文版（見 DTO 上的註解）。
        AddOrReplaceI18n(article, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = article.ArticlesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(article, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        if (!string.Equals(previousCoverKey, effectiveCoverKey, StringComparison.Ordinal))
        {
            // fail-open：IImageStorageService.DeleteAsync 內部自己吞例外並記警告日誌，
            // 這裡不需要（也不應該）用 try/catch 再包一層讓一個非關鍵的清理步驟影響回應。
            await imageStorage.DeleteAsync(previousCoverKey, cancellationToken);
        }

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<AdminArticleDetailDto?> PublishAsync(
        ClubScope scope, Guid id, PublishArticleRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var article = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (article is null)
        {
            return null;
        }

        if (article.Status is not ("draft" or "scheduled"))
        {
            throw new ArticleInvalidStatusTransitionException(
                $"目前狀態是「{StatusLabel(article.Status)}」，只有草稿或排程發布中的文章可以發布。");
        }

        ApplyConcurrencyToken(article, request.ExpectedUpdatedAt);

        article.Status = "published";
        article.PublishedAt = DateTime.UtcNow;
        article.UpdatedAt = DateTime.UtcNow;
        article.UpdatedBy = operatorId;

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<AdminArticleDetailDto?> ScheduleAsync(
        ClubScope scope, Guid id, ScheduleArticleRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (request.PublishAt <= DateTime.UtcNow)
        {
            throw new AdminArticleValidationException("排程發布時間必須晚於現在。");
        }

        var article = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (article is null)
        {
            return null;
        }

        if (article.Status is not ("draft" or "scheduled"))
        {
            throw new ArticleInvalidStatusTransitionException(
                $"目前狀態是「{StatusLabel(article.Status)}」，只有草稿或排程發布中的文章可以重新排程。");
        }

        ApplyConcurrencyToken(article, request.ExpectedUpdatedAt);

        article.Status = "scheduled";
        article.PublishedAt = request.PublishAt;
        article.UpdatedAt = DateTime.UtcNow;
        article.UpdatedBy = operatorId;

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>回傳 <c>null</c>＝找不到（含跨俱樂部），<c>true</c>＝刪除成功。
    /// 共用內容唯讀例外由 <see cref="LoadTrackedForWriteAsync"/> 統一擋下。</summary>
    public async Task<bool?> DeleteAsync(ClubScope scope, Guid id, DateTime expectedUpdatedAt, CancellationToken cancellationToken)
    {
        var article = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (article is null)
        {
            return null;
        }

        ApplyConcurrencyToken(article, expectedUpdatedAt);
        var coverKey = article.CoverKey;
        dbContext.Articles.Remove(article);

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        // 刪除資料列一併刪除其圖片物件（規劃書 §4.0「換圖與刪除」）。
        await imageStorage.DeleteAsync(coverKey, cancellationToken);

        return true;
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    /// <summary>寫入路徑專用的載入：追蹤中、含 i18n。共用內容（<c>club_id IS NULL</c>）直接丟
    /// <see cref="SharedArticleReadOnlyException"/>，跨俱樂部回 <c>null</c>（讓呼叫端 404，不洩漏存在與否）。</summary>
    private async Task<Article?> LoadTrackedForWriteAsync(ClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var article = await dbContext.Articles
            .Include(a => a.ArticlesI18ns)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null)
        {
            return null;
        }

        if (article.ClubId is null)
        {
            throw new SharedArticleReadOnlyException();
        }

        if (article.ClubId != scope.ClubId)
        {
            return null;
        }

        return article;
    }

    /// <summary>
    /// EF Core 樂觀並行的標準用法：把追蹤中實體「<c>UpdatedAt</c> 這個並行權杖屬性」的原始值
    /// 設成呼叫端宣稱看到的版本（<paramref name="expectedUpdatedAt"/>），SaveChanges 產生的
    /// <c>UPDATE</c>／<c>DELETE</c> 會帶 <c>WHERE updated_at = @原始值</c>，0 筆命中就丟
    /// <see cref="DbUpdateConcurrencyException"/>（<see cref="SaveWithConcurrencyHandlingAsync"/> 接住轉 409）。
    /// 並行權杖本身在 <see cref="Data.ClubDbContextCustomizations"/> 設定。
    /// </summary>
    private void ApplyConcurrencyToken(Article article, DateTime expectedUpdatedAt)
        => dbContext.Entry(article).Property(a => a.UpdatedAt).OriginalValue = expectedUpdatedAt;

    private async Task SaveWithConcurrencyHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArticleConcurrencyConflictException();
        }
    }

    private void AddOrReplaceI18n(Article article, string locale, AdminArticleLocaleContent content)
    {
        var existing = article.ArticlesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new ArticlesI18n { ArticleId = article.Id, Locale = locale };
            article.ArticlesI18ns.Add(existing);
            dbContext.ArticlesI18ns.Add(existing);
        }

        existing.Title = content.Title;
        existing.Summary = content.Summary;
        existing.Body = content.Body;
        existing.SeoTitle = content.SeoTitle;
        existing.SeoDescription = content.SeoDescription;
    }

    private async Task<ArticleCategory> ResolveCategoryAsync(string categoryCode, CancellationToken cancellationToken)
    {
        var category = await dbContext.ArticleCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == categoryCode, cancellationToken);

        return category ?? throw new AdminArticleValidationException($"找不到分類代碼「{categoryCode}」。");
    }

    /// <summary>置頂精選同時最多 3 篇（B2 規格）。🔴 判斷範圍是「這個俱樂部自己的文章」，
    /// 規格沒有明講是全站還是逐俱樂部限制，這是本輪的判斷——見 apps/api/README.md 說明。</summary>
    private async Task EnsureFeaturedCapAsync(ClubScope scope, Guid? excludeArticleId, CancellationToken cancellationToken)
    {
        var featuredCount = await dbContext.Articles.AsNoTracking()
            .Where(a => a.ClubId == scope.ClubId && a.IsFeatured && a.Id != excludeArticleId)
            .CountAsync(cancellationToken);

        if (featuredCount >= 3)
        {
            throw new ArticleFeaturedLimitExceededException();
        }
    }

    private static void ValidateContent(AdminArticleContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Title))
        {
            throw new AdminArticleValidationException("中文標題為必填欄位。");
        }
    }

    private static string StatusLabel(string status) => status switch
    {
        "draft" => "草稿",
        "published" => "已發布",
        "scheduled" => "排程發布中",
        _ => status,
    };

    private static AdminArticleDetailDto ToDetailDto(Article article)
    {
        var zh = article.ArticlesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = article.ArticlesI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminArticleDetailDto
        {
            Id = article.Id,
            Slug = article.Slug,
            CategoryCode = article.ArticleCategory.Code,
            CoverKey = article.CoverKey,
            IsFeatured = article.IsFeatured,
            Status = article.Status,
            PublishedAt = article.PublishedAt,
            IsShared = article.ClubId is null,
            UpdatedAt = article.UpdatedAt,
            Zh = new AdminArticleLocaleContent
            {
                Title = zh?.Title,
                Summary = zh?.Summary,
                Body = zh?.Body,
                SeoTitle = zh?.SeoTitle,
                SeoDescription = zh?.SeoDescription,
            },
            En = en is null ? null : new AdminArticleLocaleContent
            {
                Title = en.Title,
                Summary = en.Summary,
                Body = en.Body,
                SeoTitle = en.SeoTitle,
                SeoDescription = en.SeoDescription,
            },
        };
    }

    private async Task InvalidatePublicCacheAsync(ClubScope scope, CancellationToken cancellationToken)
    {
        // docs/17-deployment.md §4「寫入：write-invalidate，不是 write-update」——
        // 先寫 SQL（上面 SaveChangesAsync 已交易成功）再失效，順序不可顛倒。
        // 🔴 共用內容本來就不會被這個 repository 寫到（LoadTrackedForWriteAsync 擋下），
        // 所以這裡只需要失效呼叫端自己俱樂部的快取命名空間，不需要遍歷其他俱樂部。
        await cache.InvalidateAsync(PublicListEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync(PublicDetailEntity, scope.ClubCode, cancellationToken);
    }
}
