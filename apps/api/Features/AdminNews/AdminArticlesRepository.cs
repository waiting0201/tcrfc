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
/// <see cref="Features.News.ArticlesRepository"/>——兩者刻意分開，不是這份檔案要取代那份，
/// 公開端點的行為與型別一行都沒有因為本次授權改動而變。
///
/// 🔴🔴🔴 2026-09-23（型別層強制授權）：**本類別每一個公開與私有方法一律收
/// <see cref="Security.AdminClubScope"/>，不收 <see cref="Security.ClubScope"/>**——後者任何人
/// 呼叫公開的 <see cref="Security.IClubResolver.ResolveAsync"/> 就拿得到、不需要登入也不需要
/// 授權。若這裡收的是 <c>ClubScope</c>，一支忘記呼叫
/// <see cref="Security.IAdminClubAuthorizer.AuthorizeAsync"/> 的新端點只要改呼叫
/// <c>IClubResolver</c> 就能編譯過、跑得動、繞過整套登入與授權——這正是 2026-09-23 使用者裁決
/// 要堵的洞。詳細設計理由見 apps/api/README.md「新增後台端點的必要形狀」。
/// </summary>
public sealed class AdminArticlesRepository(ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage)
{
    /// <summary>公開讀取 API 用的 entity 名稱，寫入成功後要讓這兩個快取失效（docs/17 §4「write-invalidate」）。</summary>
    private const string PublicListEntity = "articles";
    private const string PublicDetailEntity = "article-detail";

    /// <summary><c>value_tag_links.entity_type</c> 給文章用的值（S1-5 新增）。這張表是通用多型
    /// 關聯，目前全站唯一接上真正讀寫邏輯的呼叫端就是這裡，這個字面值是本輪定的慣例
    /// （小寫、單數、對應型別詞彙表的 <c>Article</c>），之後若有其他型別要掛核心價值標籤，
    /// 比照同一套命名（<c>player</c>／<c>program</c>……）即可。</summary>
    private const string ArticleEntityType = "article";

    /// <summary>五大核心價值標籤值域（規劃書 §1.2，<c>value_tag_links.value_tag</c> 的 CHECK 約束逐字照抄）。</summary>
    private static readonly HashSet<string> AllowedCoreValueTags = new(StringComparer.Ordinal)
    {
        "players_first", "excellence", "global_pathways", "community", "integrity",
    };

    /// <summary>文章多型關聯（<c>article_relations.target_type</c>）允許的五種（規劃書 B2「關聯
    /// （球員／球隊／賽事／課程／夥伴）」逐字對應，命名採型別詞彙表單數小寫）。</summary>
    private static readonly HashSet<string> AllowedRelationTargetTypes = new(StringComparer.Ordinal)
    {
        "player", "team", "match", "program", "partner",
    };

    // ───────────────────────────── 讀取（後台專用，含全部狀態） ─────────────────────────────

    /// <summary>
    /// 後台清單：回傳這個俱樂部專屬 ＋ 共用（<c>club_id IS NULL</c>）的文章，**不篩狀態**——
    /// 這正是本輪要補的缺口（STATUS.md S0-12：公開 API 只回已發布內容，後台驗證不到草稿／
    /// 排程／已停用）。全程 <c>AsNoTracking</c>，這條路徑不做寫入。
    /// </summary>
    public async Task<PagedResult<AdminArticleListItemDto>> ListAsync(
        AdminClubScope scope, string? status, string? categoryCode, string? keyword,
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
                a.ViewCount,
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
                // S1-5 新增：標籤跟著同一筆查詢帶出（相關子查詢／OUTER APPLY，SQL Server 對這種
                // 「分頁後再展開子集合」的形狀處理得很好，不需要另外用 AsSplitQuery）。
                Tags = a.Tags.Select(t => new
                {
                    t.Slug,
                    NameZh = t.TagsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                    NameEn = t.TagsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminArticleListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            CategoryCode = r.CategoryCode,
            CoverKey = r.CoverKey,
            IsFeatured = r.IsFeatured,
            ViewCount = r.ViewCount,
            Status = r.Status,
            PublishedAt = r.PublishedAt,
            IsShared = r.ClubId is null,
            UpdatedAt = r.UpdatedAt,
            TitleZh = r.TitleZh,
            TitleEn = r.TitleEn,
            Tags = r.Tags.Select(t => new AdminArticleTagDto { Slug = t.Slug, NameZh = t.NameZh, NameEn = t.NameEn }).ToList(),
        }).ToList();

        return new PagedResult<AdminArticleListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>後台單篇詳情。跨俱樂部（非共用、非本俱樂部）回傳 <c>null</c>（404），
    /// 不透露「這個 id 存在但屬於別的俱樂部」。</summary>
    public async Task<AdminArticleDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var article = await dbContext.Articles.AsNoTracking()
            .Include(a => a.ArticleCategory)
            .Include(a => a.ArticlesI18ns)
            .Include(a => a.Tags).ThenInclude(t => t.TagsI18ns)
            .Include(a => a.ArticleRelations)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (article is null || (article.ClubId is not null && article.ClubId != scope.ClubId))
        {
            return null;
        }

        // S1-5 新增：value_tag_links 是通用多型關聯表，Article 沒有對應的導覽屬性（沒有真正的
        // 外鍵可以宣告），這裡另外查一次，跟上面的 Include 分開。
        var coreValueTags = await dbContext.ValueTagLinks.AsNoTracking()
            .Where(v => v.EntityType == ArticleEntityType && v.EntityId == article.Id)
            .Select(v => v.ValueTag)
            .ToListAsync(cancellationToken);

        return ToDetailDto(article, coreValueTags);
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
        AdminClubScope scope, Guid articleId, CreateArticleRequest request, string? coverKey, Guid? operatorId, CancellationToken cancellationToken)
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

        // S1-5 新增：標籤／關聯在寫入資料列之前先驗證與解析完畢——驗證失敗（格式不對、核心價值
        // 標籤值域不合法、關聯目標不存在或跨俱樂部）一律在這裡就丟例外，不會走到後面已經
        // Add 了一半的資料列（呼叫端 AdminArticlesEndpoints 的封面圖片補償刪除邏輯也是靠
        // 這一類「驗證失敗就整段不落地」的順序才成立）。
        var tags = await ResolveTagsAsync(request.Tags ?? [], cancellationToken);
        var coreValueTags = ValidateCoreValueTags(request.CoreValueTags ?? []);
        var relations = await ValidateRelationsAsync(articleId, scope.ClubId, request.Relations ?? [], cancellationToken);

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

        // S1-5 新增：標籤／關聯掛回導覽屬性集合，核心價值標籤直接進 DbSet（沒有導覽屬性可掛，
        // 見 ArticleEntityType 上的說明）。三者跟文章本體、雙語側表在同一次 SaveChanges 交易內
        // 一起落地，不是分開兩次寫入。
        foreach (var tag in tags)
        {
            article.Tags.Add(tag);
        }

        foreach (var relation in relations)
        {
            article.ArticleRelations.Add(relation);
        }

        foreach (var valueTag in coreValueTags)
        {
            dbContext.ValueTagLinks.Add(new ValueTagLink { EntityType = ArticleEntityType, EntityId = article.Id, ValueTag = valueTag });
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
        AdminClubScope scope, Guid id, UpdateArticleRequest request, CoverKeyUpdate coverUpdate, Guid? operatorId, CancellationToken cancellationToken)
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

        // S1-5 新增：三個欄位一律「省略＝維持不變」（見 UpdateArticleRequest 上的說明），
        // 所以先各自判斷是否要處理，再各自解析／驗證——驗證失敗一樣要在改動任何欄位之前發生。
        var tagsToApply = request.Tags is null ? null : await ResolveTagsAsync(request.Tags, cancellationToken);
        var coreValueTagsToApply = request.CoreValueTags is null ? null : ValidateCoreValueTags(request.CoreValueTags);
        var relationsToApply = request.Relations is null
            ? null
            : await ValidateRelationsAsync(article.Id, article.ClubId!.Value, request.Relations, cancellationToken);

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

        // S1-5 新增：三個欄位分別套用，null＝這次請求沒有提到這個欄位，維持資料庫現況不動。
        if (tagsToApply is not null)
        {
            article.Tags.Clear();
            foreach (var tag in tagsToApply)
            {
                article.Tags.Add(tag);
            }
        }

        if (relationsToApply is not null)
        {
            article.ArticleRelations.Clear();
            foreach (var relation in relationsToApply)
            {
                article.ArticleRelations.Add(relation);
            }
        }

        if (coreValueTagsToApply is not null)
        {
            await SyncCoreValueTagsAsync(article.Id, coreValueTagsToApply, cancellationToken);
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
        AdminClubScope scope, Guid id, PublishArticleRequest request, Guid? operatorId, CancellationToken cancellationToken)
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

        // 🔴 用資料庫自己的「現在」，不是應用程式行程的 DateTime.UtcNow——理由見
        // Common/DatabaseClock.cs 檔頭（2026-09-24，S1-4 續作排查 PagesPublicEndpointTests
        // 間歇性失敗時發現這裡跟 Pages 是同一個根因，一併修正）：公開讀取用
        // published_at <= SYSUTCDATETIME() 判斷「已到發布時間」，若這裡寫入的時間戳來自另一個
        // 時鐘（應用程式行程的作業系統時鐘），兩個時鐘只要有任何飄移，剛發布的內容就可能暫時被
        // 判定為「還沒到發布時間」而查不到——查無資料不快取（IQueryCache 規則），但下一次請求
        // 仍會再打一次 SQL，一樣可能落在飄移窗內再次落空，直到資料庫時鐘追上應用程式時鐘為止。
        var dbNow = await DatabaseClock.GetUtcNowAsync(dbContext, cancellationToken);
        article.Status = "published";
        article.PublishedAt = dbNow;
        article.UpdatedAt = dbNow;
        article.UpdatedBy = operatorId;

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<AdminArticleDetailDto?> ScheduleAsync(
        AdminClubScope scope, Guid id, ScheduleArticleRequest request, Guid? operatorId, CancellationToken cancellationToken)
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
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, DateTime expectedUpdatedAt, CancellationToken cancellationToken)
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

    // ───────────────────────────── 批次操作（S1-5 新增） ─────────────────────────────

    /// <summary>一次最多處理的筆數——規劃書沒有給數字，防禦性上限，避免一次請求鎖太多列太久。</summary>
    private const int MaxBatchSize = 200;

    /// <summary>
    /// 批次改分類。🔴 **不做逐筆並行權杖檢查**——批次操作的使用情境是「列表頁勾選多筆按一個
    /// 按鈕」，呼叫端沒有（也不該要求畫面先為每一筆蒐集 <c>updated_at</c>）；能處理的處理、
    /// 不能處理的（找不到、跨俱樂部、共用內容唯讀）列進 <see cref="BatchOperationResultDto.Skipped"/>，
    /// 不是靠樂觀並行擋下這些情況。這是本輪的判斷，需要確認（見 apps/api/README.md）。
    /// </summary>
    public async Task<BatchOperationResultDto> BatchChangeCategoryAsync(
        AdminClubScope scope, BatchChangeCategoryRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateBatchIds(request.Ids);
        var category = await ResolveCategoryAsync(request.CategoryCode, cancellationToken);

        var skipped = new List<BatchOperationSkippedItemDto>();
        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var id in request.Ids.Distinct())
        {
            var (article, reason) = await TryLoadOwnArticleForBatchAsync(scope, id, cancellationToken);
            if (article is null)
            {
                skipped.Add(new BatchOperationSkippedItemDto { Id = id, Reason = reason! });
                continue;
            }

            article.ArticleCategoryId = category.Id;
            article.UpdatedAt = now;
            article.UpdatedBy = operatorId;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedCount > 0)
        {
            await InvalidatePublicCacheAsync(scope, cancellationToken);
        }

        return new BatchOperationResultDto { UpdatedCount = updatedCount, Skipped = skipped };
    }

    /// <summary>批次發布：只接受 <c>draft</c>／<c>scheduled</c> 出發，跟單篇 <see cref="PublishAsync"/>
    /// 同一條轉換規則（README「三態轉換規則」）。用資料庫自己的「現在」（<see cref="DatabaseClock"/>），
    /// 理由跟 <see cref="PublishAsync"/> 上的說明相同。</summary>
    public async Task<BatchOperationResultDto> BatchPublishAsync(
        AdminClubScope scope, BatchArticleIdsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateBatchIds(request.Ids);

        var skipped = new List<BatchOperationSkippedItemDto>();
        var updatedCount = 0;
        DateTime? dbNow = null;

        foreach (var id in request.Ids.Distinct())
        {
            var (article, reason) = await TryLoadOwnArticleForBatchAsync(scope, id, cancellationToken);
            if (article is null)
            {
                skipped.Add(new BatchOperationSkippedItemDto { Id = id, Reason = reason! });
                continue;
            }

            if (article.Status is not ("draft" or "scheduled"))
            {
                skipped.Add(new BatchOperationSkippedItemDto
                {
                    Id = id,
                    Reason = $"目前狀態是「{StatusLabel(article.Status)}」，只有草稿或排程發布中的文章可以批次發布。",
                });
                continue;
            }

            dbNow ??= await DatabaseClock.GetUtcNowAsync(dbContext, cancellationToken);
            article.Status = "published";
            article.PublishedAt = dbNow;
            article.UpdatedAt = dbNow.Value;
            article.UpdatedBy = operatorId;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedCount > 0)
        {
            await InvalidatePublicCacheAsync(scope, cancellationToken);
        }

        return new BatchOperationResultDto { UpdatedCount = updatedCount, Skipped = skipped };
    }

    /// <summary>
    /// 批次下架（規劃書 B2「批次發布／下架」的後半）。
    /// 🔴🔴🔴 **我的判斷，需要確認**：<c>articles.status</c> 的 CHECK 約束只有
    /// <c>draft</c>／<c>published</c>／<c>scheduled</c> 三態，資料庫沒有獨立的「已下架」狀態值
    /// （這是既有落差，見 README「已發現、未動手修改的既有落差」第 1 點：<c>apps/admin</c> 的
    /// <c>ContentStatus</c> 型別有 <c>disabled</c> 第四態，但資料庫沒有對應值域，新增值域是規格
    /// 變更要先改 <c>docs/12</c>，本輪任務邊界不能改綱要）。這裡把「下架」實作成**轉回
    /// <c>draft</c>**——公開 API 只顯示 <c>status = 'published'</c> 的文章，轉回草稿在對外行為上
    /// 就是「從公開站消失」，跟「下架」字面上要達成的效果一致；代價是「這篇文章從來沒發布過的草稿」
    /// 跟「這篇文章下架前發布過」在資料庫裡變成同一個狀態值，不再能單靠 <c>status</c> 分辨兩者
    /// （<c>published_at</c> 欄位仍保留下架前最後一次發布的時間戳，沒有被清空，這是唯一還能
    /// 分辨「曾經發布過」的線索）。**這是規劃書沒有明講、需要業務判斷確認的假設，跟 S0-7h
    /// 那兩項是同一種性質，只是時間點更晚**。
    /// </summary>
    public async Task<BatchOperationResultDto> BatchUnpublishAsync(
        AdminClubScope scope, BatchArticleIdsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateBatchIds(request.Ids);

        var skipped = new List<BatchOperationSkippedItemDto>();
        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var id in request.Ids.Distinct())
        {
            var (article, reason) = await TryLoadOwnArticleForBatchAsync(scope, id, cancellationToken);
            if (article is null)
            {
                skipped.Add(new BatchOperationSkippedItemDto { Id = id, Reason = reason! });
                continue;
            }

            if (article.Status is not ("published" or "scheduled"))
            {
                skipped.Add(new BatchOperationSkippedItemDto
                {
                    Id = id,
                    Reason = $"目前狀態是「{StatusLabel(article.Status)}」，只有已發布或排程發布中的文章可以批次下架。",
                });
                continue;
            }

            article.Status = "draft";
            article.UpdatedAt = now;
            article.UpdatedBy = operatorId;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedCount > 0)
        {
            await InvalidatePublicCacheAsync(scope, cancellationToken);
        }

        return new BatchOperationResultDto { UpdatedCount = updatedCount, Skipped = skipped };
    }

    private static void ValidateBatchIds(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            throw new AdminArticleValidationException("批次操作至少要選擇一篇文章。");
        }

        if (ids.Count > MaxBatchSize)
        {
            throw new AdminArticleValidationException($"批次操作一次最多處理 {MaxBatchSize} 篇文章，請分批操作。");
        }
    }

    /// <summary>批次操作專用的載入：不追蹤並行權杖（批次操作刻意不做逐筆並行檢查，見上方說明），
    /// 找不到／共用內容／跨俱樂部三種情況分別回傳可讀原因，不是丟例外——批次操作要能「部分成功」，
    /// 一筆的問題不能讓整批都不能處理。</summary>
    private async Task<(Article? Article, string? SkipReason)> TryLoadOwnArticleForBatchAsync(
        AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var article = await dbContext.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return (null, "找不到這篇文章。");
        }

        if (article.ClubId is null)
        {
            return (null, "這是兩隊共用的內容，目前僅系統管理員可以編輯。");
        }

        if (article.ClubId != scope.ClubId)
        {
            return (null, "這篇文章不屬於這個俱樂部。");
        }

        return (article, null);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    /// <summary>寫入路徑專用的載入：追蹤中、含 i18n。共用內容（<c>club_id IS NULL</c>）直接丟
    /// <see cref="SharedArticleReadOnlyException"/>，跨俱樂部回 <c>null</c>（讓呼叫端 404，不洩漏存在與否）。</summary>
    private async Task<Article?> LoadTrackedForWriteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        // S1-5 新增：Tags／ArticleRelations 一併載入，供 UpdateAsync 在有帶這兩個欄位時
        // 直接操作導覽屬性集合（Clear／Add），不用另外查一次。
        var article = await dbContext.Articles
            .Include(a => a.ArticlesI18ns)
            .Include(a => a.Tags)
            .Include(a => a.ArticleRelations)
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

    /// <summary>
    /// 標籤（S1-5 新增）：依 <c>Slug</c> 找既有標籤，找不到才新建。找到既有標籤時**忽略**輸入的
    /// <c>NameZh</c>／<c>NameEn</c>（標籤名稱由標籤自己的資料列管理，不因為某一篇文章的輸入
    /// 被覆寫，否則 A 文章存檔時打的名稱會悄悄改掉 B 文章也在用的同一個標籤顯示名稱）。
    /// 🔴 已知、接受的競態窗口：兩個請求同時建立同一個新 slug 的標籤時，<c>UQ_tags_slug</c>
    /// 會讓其中一個 <c>SaveChangesAsync</c> 失敗——這裡沒有額外的重試或攔截處理，跟本檔其他
    /// 「先查後寫」的重複檢查（slug、分類）採同一種風險容忍度，不是本輪遺漏。
    /// </summary>
    private async Task<List<Tag>> ResolveTagsAsync(IReadOnlyList<AdminArticleTagInput> inputs, CancellationToken cancellationToken)
    {
        var result = new List<Tag>();
        var seenSlugs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            TagSlugFormat.Validate(input.Slug);
            if (!seenSlugs.Add(input.Slug))
            {
                continue; // 同一次請求重複送同一個標籤，容錯忽略，不視為錯誤。
            }

            var tag = await dbContext.Tags.Include(t => t.TagsI18ns)
                .FirstOrDefaultAsync(t => t.Slug == input.Slug, cancellationToken);

            if (tag is null)
            {
                if (string.IsNullOrWhiteSpace(input.NameZh))
                {
                    throw new AdminArticleValidationException(
                        $"標籤「{input.Slug}」尚未建立，新增標籤時必須提供中文名稱。");
                }

                var now = DateTime.UtcNow;
                tag = new Tag { Id = Guid.NewGuid(), Slug = input.Slug, CreatedAt = now, UpdatedAt = now };
                tag.TagsI18ns.Add(new TagsI18n { TagId = tag.Id, Locale = RequestLocale.DefaultDbLocale, Name = input.NameZh });
                if (!string.IsNullOrWhiteSpace(input.NameEn))
                {
                    tag.TagsI18ns.Add(new TagsI18n { TagId = tag.Id, Locale = "en", Name = input.NameEn });
                }

                dbContext.Tags.Add(tag);
            }

            result.Add(tag);
        }

        return result;
    }

    /// <summary>核心價值標籤（S1-5 新增）：值域檢查＋去重複，不碰資料庫（呼叫端決定要新增還是取代）。</summary>
    private static List<string> ValidateCoreValueTags(IReadOnlyList<string> values)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (!AllowedCoreValueTags.Contains(value))
            {
                throw new AdminArticleValidationException(
                    $"核心價值標籤「{value}」不是合法值，合法值只有「以球員為本」「追求卓越」「國際發展」" +
                    "「社區共好」「誠信專業」（規劃書五大核心價值）對應的五個系統代碼。");
            }

            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    /// <summary>
    /// 更新時的核心價值標籤同步：整份取代（先刪光這篇文章現有的，再依新清單插入），
    /// 跟標籤／關聯用同一種「Clear 再重建」邏輯一致，差別只是這裡沒有導覽屬性可以
    /// <c>Clear()</c>，改成手動查出既有列再 <c>RemoveRange</c>。
    /// </summary>
    private async Task SyncCoreValueTagsAsync(Guid articleId, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ValueTagLinks
            .Where(v => v.EntityType == ArticleEntityType && v.EntityId == articleId)
            .ToListAsync(cancellationToken);
        dbContext.ValueTagLinks.RemoveRange(existing);

        foreach (var value in values)
        {
            dbContext.ValueTagLinks.Add(new ValueTagLink { EntityType = ArticleEntityType, EntityId = articleId, ValueTag = value });
        }
    }

    /// <summary>
    /// 關聯（S1-5 新增，<c>article_relations</c>）：驗證 <c>TargetType</c> 在允許值域內、
    /// <c>TargetId</c> 在對應資料表裡真的存在，**且屬於跟這篇文章同一個俱樂部**——這是「多型
    /// 關聯的跨俱樂部隔離」規則唯一的落點。任何一筆不合法就整包 400，不做「部分成功」。
    /// </summary>
    private async Task<List<ArticleRelation>> ValidateRelationsAsync(
        Guid articleId, Guid articleClubId, IReadOnlyList<AdminArticleRelationInput> inputs, CancellationToken cancellationToken)
    {
        var result = new List<ArticleRelation>();
        var seen = new HashSet<(string TargetType, Guid TargetId)>();

        foreach (var input in inputs)
        {
            if (!AllowedRelationTargetTypes.Contains(input.TargetType))
            {
                throw new AdminArticleValidationException(
                    $"關聯類型「{input.TargetType}」不支援，只能關聯球員、球隊、賽事、課程或夥伴其中一種。");
            }

            if (!seen.Add((input.TargetType, input.TargetId)))
            {
                continue; // 同一次請求重複送同一筆關聯，容錯忽略。
            }

            var exists = input.TargetType switch
            {
                "player" => await dbContext.Players.AsNoTracking()
                    .AnyAsync(p => p.Id == input.TargetId && p.ClubId == articleClubId, cancellationToken),
                "team" => await dbContext.Teams.AsNoTracking()
                    .AnyAsync(t => t.Id == input.TargetId && t.ClubId == articleClubId, cancellationToken),
                "match" => await dbContext.Matches.AsNoTracking()
                    .AnyAsync(m => m.Id == input.TargetId && m.ClubId == articleClubId, cancellationToken),
                "program" => await dbContext.Programs.AsNoTracking()
                    .AnyAsync(p => p.Id == input.TargetId && p.ClubId == articleClubId, cancellationToken),
                "partner" => await dbContext.Partners.AsNoTracking()
                    .AnyAsync(p => p.Id == input.TargetId && p.ClubId == articleClubId, cancellationToken),
                _ => false,
            };

            if (!exists)
            {
                throw new AdminArticleValidationException(
                    $"找不到這筆關聯的目標資料（{RelationTargetTypeLabel(input.TargetType)}），" +
                    "或者它不屬於這篇文章所屬的俱樂部——關聯目標必須跟文章屬於同一個俱樂部。");
            }

            result.Add(new ArticleRelation { ArticleId = articleId, TargetType = input.TargetType, TargetId = input.TargetId });
        }

        return result;
    }

    private static string RelationTargetTypeLabel(string targetType) => targetType switch
    {
        "player" => "球員",
        "team" => "球隊",
        "match" => "賽事",
        "program" => "課程",
        "partner" => "夥伴",
        _ => targetType,
    };

    /// <summary>置頂精選同時最多 3 篇（B2 規格）。🔴 判斷範圍是「這個俱樂部自己的文章」，
    /// 規格沒有明講是全站還是逐俱樂部限制，這是本輪的判斷——見 apps/api/README.md 說明。</summary>
    private async Task EnsureFeaturedCapAsync(AdminClubScope scope, Guid? excludeArticleId, CancellationToken cancellationToken)
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

    private static AdminArticleDetailDto ToDetailDto(Article article, IReadOnlyList<string> coreValueTags)
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
            ViewCount = article.ViewCount,
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
            Tags = article.Tags
                .Select(t => new AdminArticleTagDto
                {
                    Slug = t.Slug,
                    NameZh = t.TagsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
                    NameEn = t.TagsI18ns.FirstOrDefault(i => i.Locale == "en")?.Name,
                })
                .ToList(),
            CoreValueTags = coreValueTags,
            Relations = article.ArticleRelations
                .Select(r => new AdminArticleRelationInput { TargetType = r.TargetType, TargetId = r.TargetId })
                .ToList(),
        };
    }

    private async Task InvalidatePublicCacheAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        // docs/17-deployment.md §4「寫入：write-invalidate，不是 write-update」——
        // 先寫 SQL（上面 SaveChangesAsync 已交易成功）再失效，順序不可顛倒。
        // 🔴 共用內容本來就不會被這個 repository 寫到（LoadTrackedForWriteAsync 擋下），
        // 所以這裡只需要失效呼叫端自己俱樂部的快取命名空間，不需要遍歷其他俱樂部。
        await cache.InvalidateAsync(PublicListEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync(PublicDetailEntity, scope.ClubCode, cancellationToken);
    }
}
