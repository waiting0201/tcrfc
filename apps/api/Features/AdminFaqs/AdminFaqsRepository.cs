using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// B4 常見問題（<c>faqs</c>）後台寫入與後台讀取。形狀比照 <c>Features/AdminNews/AdminArticlesRepository.cs</c>
/// （俱樂部範圍、共用內容唯讀、雙語側表 upsert），差異：
///
/// 1. **沒有樂觀並行控制**——比照 <c>Features/AdminCompetitions/AdminCompetitionsRepository.cs</c>
///    （同樣是規劃書沒有要求排程發布的簡單狀態欄位型別），不像 <c>Article</c>／<c>Page</c> 那樣
///    用 <c>updated_at</c> 當並行權杖。這是本輪的取捨：FAQ 編輯的多人同時衝突風險與
///    Competition 同一等級（單一欄位表單、後台使用頻率低），不是新聞或頁面那種多段落長文；
///    需要時可依 <c>ClubDbContextCustomizations.cs</c> 既有寫法補上，不是型別上做不到。
/// 2. **狀態只有 <c>draft</c>／<c>published</c> 兩態、沒有獨立的發布／排程端點**——<c>faqs</c>
///    沒有 <c>published_at</c> 欄位（docs/14-invariants.md「S0-7g」），狀態轉換是 Update 請求
///    的一個平面欄位，不是像 <c>Article</c>／<c>Page</c> 那樣的獨立生命週期端點。
/// </summary>
public sealed class AdminFaqsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    private const string PublicListEntity = "faqs";
    private const string PublicDetailEntity = "faq-detail";
    private const string PublicEmbedEntity = "faq-embed";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal) { "draft", "published" };

    public async Task<PagedResult<AdminFaqListItemDto>> ListAsync(
        AdminClubScope scope, string? status, Guid? categoryId, string? keyword, string? sort,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Faqs.AsNoTracking()
            .Where(f => f.ClubId == scope.ClubId || f.ClubId == null);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (categoryId is Guid c)
        {
            query = query.Where(f => f.FaqCategories.Any(fc => fc.Id == c));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(f => f.FaqsI18ns.Any(i =>
                i.Locale == RequestLocale.DefaultDbLocale
                && ((i.Question != null && i.Question.Contains(keyword)) || (i.Answer != null && i.Answer.Contains(keyword)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // 🔴「低評價題目」清單（規劃書 B4「成效數據」）：sort=low_rating 依「不喜歡 - 喜歡」
        // 由多到少排序（負評相對最多的排最前），沒有另設門檻（例如至少要有幾筆回饋才算數）——
        // 規劃書沒有給門檻數字，這裡不杜撰一個，讓後台人員自己判斷零回饋跟真正負評的差別
        // （零回饋的題目 unhelpful-helpful=0，會落在排序中段，不會被誤判成負評最嚴重）。
        query = sort == "low_rating"
            ? query.OrderByDescending(f => f.UnhelpfulCount - f.HelpfulCount).ThenByDescending(f => f.UpdatedAt)
            : query.OrderBy(f => f.SortOrder).ThenByDescending(f => f.UpdatedAt);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                f.Slug,
                f.SortOrder,
                f.Status,
                f.ClubId,
                f.ViewCount,
                f.HelpfulCount,
                f.UnhelpfulCount,
                f.UpdatedAt,
                QuestionZh = f.FaqsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Question).FirstOrDefault(),
                QuestionEn = f.FaqsI18ns.Where(i => i.Locale == "en").Select(i => i.Question).FirstOrDefault(),
                Categories = f.FaqCategories.Select(fc => new
                {
                    fc.Id,
                    fc.Slug,
                    NameZh = fc.FaqCategoriesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                    NameEn = fc.FaqCategoriesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                }).ToList(),
                EmbedSlots = f.FaqEmbedSlotLinks
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new { l.FaqEmbedSlot.Id, l.FaqEmbedSlot.Code, l.FaqEmbedSlot.Name })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminFaqListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            SortOrder = r.SortOrder,
            Status = r.Status,
            IsShared = r.ClubId is null,
            ViewCount = r.ViewCount,
            HelpfulCount = r.HelpfulCount,
            UnhelpfulCount = r.UnhelpfulCount,
            UpdatedAt = r.UpdatedAt,
            QuestionZh = r.QuestionZh,
            QuestionEn = r.QuestionEn,
            Categories = r.Categories
                .Select(c => new AdminFaqCategoryRefDto { Id = c.Id, Slug = c.Slug, NameZh = c.NameZh, NameEn = c.NameEn })
                .ToList(),
            EmbedSlots = r.EmbedSlots
                .Select(s => new AdminFaqEmbedSlotRefDto { Id = s.Id, Code = s.Code, Name = s.Name })
                .ToList(),
        }).ToList();

        return new PagedResult<AdminFaqListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AdminFaqDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.AsNoTracking()
            .Include(f => f.FaqsI18ns)
            .Include(f => f.FaqCategories).ThenInclude(fc => fc.FaqCategoriesI18ns)
            .Include(f => f.FaqEmbedSlotLinks.OrderBy(l => l.SortOrder)).ThenInclude(l => l.FaqEmbedSlot)
            .FirstOrDefaultAsync(f => f.Id == id && (f.ClubId == scope.ClubId || f.ClubId == null), cancellationToken);

        return faq is null ? null : ToDetailDto(faq);
    }

    public async Task<AdminFaqDetailDto> CreateAsync(
        AdminClubScope scope, CreateFaqRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        FaqSlugPolicy.Validate(request.Slug);
        ValidateStatus(request.Status);
        ValidateContent(request.Content);
        var categories = await ResolveCategoriesAsync(request.CategoryIds, cancellationToken);
        var embedSlots = await ResolveEmbedSlotsAsync(request.EmbedSlotIds, cancellationToken);

        if (await dbContext.Faqs.AsNoTracking().AnyAsync(
                f => f.ClubId == scope.ClubId && f.Slug == request.Slug, cancellationToken))
        {
            throw new FaqSlugConflictException(request.Slug);
        }

        var now = DateTime.UtcNow;
        var faq = new Faq
        {
            Id = Guid.NewGuid(),
            ClubId = scope.ClubId, // 🔴 後台建立一律歸屬呼叫端當下的俱樂部，不能建立共用內容
                                    // （docs/14-invariants.md「共同內容只有超管能建立」，理由同
                                    // AdminArticlesRepository.CreateAsync 同一段註解）。
            Slug = request.Slug,
            SortOrder = request.SortOrder,
            Status = request.Status,
            ViewCount = 0,
            HelpfulCount = 0,
            UnhelpfulCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Faqs.Add(faq);
        AddOrReplaceI18n(faq, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(faq, "en", request.Content.En);
        }

        foreach (var category in categories)
        {
            faq.FaqCategories.Add(category);
        }

        AddEmbedSlotLinks(faq, embedSlots);

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return (await GetByIdAsync(scope, faq.Id, cancellationToken))!;
    }

    public async Task<AdminFaqDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateFaqRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        FaqSlugPolicy.Validate(request.Slug);
        ValidateStatus(request.Status);
        ValidateContent(request.Content);
        var categories = await ResolveCategoriesAsync(request.CategoryIds, cancellationToken);
        // 省略（null）＝維持不變，非 null（含空陣列）＝整份取代——比照 AdminStaffRepository.Teams
        // 的既有語意，跟 CategoryIds（一律整份取代、至少 1 個）刻意不同，見
        // CreateFaqRequest.EmbedSlotIds 的說明。
        var embedSlots = request.EmbedSlotIds is null
            ? null
            : await ResolveEmbedSlotsAsync(request.EmbedSlotIds, cancellationToken);

        var faq = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (faq is null)
        {
            return null;
        }

        if (!string.Equals(faq.Slug, request.Slug, StringComparison.Ordinal)
            && await dbContext.Faqs.AsNoTracking().AnyAsync(
                f => f.ClubId == scope.ClubId && f.Slug == request.Slug && f.Id != id, cancellationToken))
        {
            throw new FaqSlugConflictException(request.Slug);
        }

        faq.Slug = request.Slug;
        faq.SortOrder = request.SortOrder;
        faq.Status = request.Status;
        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = operatorId;

        AddOrReplaceI18n(faq, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = faq.FaqsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(faq, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        faq.FaqCategories.Clear();
        foreach (var category in categories)
        {
            faq.FaqCategories.Add(category);
        }

        if (embedSlots is not null)
        {
            dbContext.FaqEmbedSlotLinks.RemoveRange(faq.FaqEmbedSlotLinks);
            faq.FaqEmbedSlotLinks.Clear();
            AddEmbedSlotLinks(faq, embedSlots);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    // ── 批次操作（規劃書 B4「批次操作：批次改分類、批次顯示／隱藏、匯入／匯出 CSV」，主站
    // 規劃書行 1033）。形狀逐字比照 Features/AdminNews 既有的三支批次端點：不做逐筆並行權杖檢查
    // （批次操作的使用情境是「列表頁勾選多筆按一個按鈕」，沒有也不該要求先為每一筆蒐集
    // updated_at），能處理的處理、不能處理的列進 Skipped，不是全有全無（跟下面 CSV 匯入「整批
    // 驗證、任一列錯誤就整批不寫入」刻意是兩種不同的容錯策略——批次操作是「使用者在畫面上勾選
    // 已經看得到的既有資料」，CSV 匯入是「使用者上傳一份可能整份都打錯格式的外部檔案」，兩者
    // 對「部分失敗要不要接受」的合理期待不同）。 ──────────────────────────────────

    private const int MaxBatchSize = 200;

    public async Task<BatchFaqOperationResultDto> BatchChangeCategoryAsync(
        AdminClubScope scope, BatchChangeFaqCategoryRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateBatchIds(request.Ids);
        var categories = await ResolveCategoriesAsync(request.CategoryIds, cancellationToken);

        var skipped = new List<BatchFaqOperationSkippedItemDto>();
        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var id in request.Ids.Distinct())
        {
            var (faq, reason) = await TryLoadOwnFaqForBatchAsync(scope, id, cancellationToken);
            if (faq is null)
            {
                skipped.Add(new BatchFaqOperationSkippedItemDto { Id = id, Reason = reason! });
                continue;
            }

            faq.FaqCategories.Clear();
            foreach (var category in categories)
            {
                faq.FaqCategories.Add(category);
            }

            faq.UpdatedAt = now;
            faq.UpdatedBy = operatorId;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedCount > 0)
        {
            await InvalidatePublicCacheAsync(scope, cancellationToken);
        }

        return new BatchFaqOperationResultDto { UpdatedCount = updatedCount, Skipped = skipped };
    }

    /// <summary>批次顯示／隱藏共用實作。<paramref name="publish"/> 為 <c>true</c>＝顯示
    /// （<c>status = 'published'</c>），<c>false</c>＝隱藏（<c>status = 'draft'</c>）——兩態直接
    /// 互轉，不像 B2 新聞的批次發布／下架要檢查「目前狀態是不是草稿或排程中」，因為 FAQ 只有
    /// 這兩態，沒有第三態需要排除。</summary>
    public async Task<BatchFaqOperationResultDto> BatchSetVisibilityAsync(
        AdminClubScope scope, BatchFaqIdsRequest request, bool publish, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateBatchIds(request.Ids);

        var skipped = new List<BatchFaqOperationSkippedItemDto>();
        var updatedCount = 0;
        var now = DateTime.UtcNow;
        var targetStatus = publish ? "published" : "draft";

        foreach (var id in request.Ids.Distinct())
        {
            var (faq, reason) = await TryLoadOwnFaqForBatchAsync(scope, id, cancellationToken);
            if (faq is null)
            {
                skipped.Add(new BatchFaqOperationSkippedItemDto { Id = id, Reason = reason! });
                continue;
            }

            faq.Status = targetStatus;
            faq.UpdatedAt = now;
            faq.UpdatedBy = operatorId;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedCount > 0)
        {
            await InvalidatePublicCacheAsync(scope, cancellationToken);
        }

        return new BatchFaqOperationResultDto { UpdatedCount = updatedCount, Skipped = skipped };
    }

    private static void ValidateBatchIds(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            throw new AdminFaqValidationException("批次操作至少要選擇一題常見問題。");
        }

        if (ids.Count > MaxBatchSize)
        {
            throw new AdminFaqValidationException($"批次操作一次最多處理 {MaxBatchSize} 題，請分批操作。");
        }
    }

    /// <summary>批次操作專用的載入：不追蹤並行權杖，找不到／共用內容／跨俱樂部三種情況分別回傳
    /// 可讀原因，不是丟例外——逐字比照 <c>AdminArticlesRepository.TryLoadOwnArticleForBatchAsync</c>。</summary>
    private async Task<(Faq? Faq, string? SkipReason)> TryLoadOwnFaqForBatchAsync(
        AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs.Include(f => f.FaqCategories).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (faq is null)
        {
            return (null, "找不到這題常見問題。");
        }

        if (faq.ClubId is null)
        {
            return (null, "這是兩隊共用的常見問題，目前僅系統管理員可以編輯。");
        }

        if (faq.ClubId != scope.ClubId)
        {
            return (null, "這題常見問題不屬於這個俱樂部。");
        }

        return (faq, null);
    }

    // ── CSV 匯入／匯出（規劃書 B4，主站規劃書行 1033；docs/04-data-model.md §5、
    // docs/12b-database-tables.md §10.1 只確認「FAQ 題目要支援 CSV 匯入＋匯出」本身，沒有逐欄
    // 定義格式——本輪依任務指示「沒定義就採最小可行」新增以下格式，已在 apps/api/README.md
    // 回報，供 system-analyst 之後決定要不要正式寫進 docs/12。） ──────────────────────────

    /// <summary>
    /// CSV 欄位（表頭，逐字輸出，日常中文——docs/14-invariants.md「CSV 匯出的欄位標題同此規則」，
    /// 不得出現 <c>slug</c>／<c>status</c> 這類英文技術詞當標題）：
    /// <c>網址名稱,所屬分類,狀態,排序,中文問題,中文答案,英文問題,英文答案</c>。
    ///
    /// - **所屬分類**：多個分類用全形頓號「、」相接（不是半形逗號——半形逗號是 CSV 本身的分欄
    ///   符號，用頓號可以避免這一欄非得用雙引號包住不可，可讀性也更好）。值是分類的**中文名稱**
    ///   （不是 slug 或 GUID）——同樣是 docs/14 那條規則的延伸：讓後台人員用 Excel 打開時看得懂
    ///   欄位內容，不需要另外查一張「slug 對照表」。
    /// - **狀態**：`顯示`／`隱藏`（規劃書 B4 原文字面用詞），不是 `published`／`draft`。
    /// - **排序**：整數字串。
    /// - **英文問題／英文答案**：可留空＝這題沒有英文版。
    /// </summary>
    private static readonly string[] CsvHeader =
        ["網址名稱", "所屬分類", "狀態", "排序", "中文問題", "中文答案", "英文問題", "英文答案"];

    /// <summary>匯出這個俱樂部**自己的**常見問題（不含共用內容）。共用內容不屬於任何單一俱樂部，
    /// 匯出後若被原地改過再匯入，會在這個俱樂部底下多造一筆新的俱樂部專屬列，而不是真的改到
    /// 共用那一列（見 <see cref="ImportCsvAsync"/> 的「upsert 鍵」說明）——為了不讓匯出／匯入
    /// 這一組操作意外複製出重複資料，匯出範圍限縮成「這個俱樂部自己建立的題目」。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var faqs = await dbContext.Faqs.AsNoTracking()
            .Where(f => f.ClubId == scope.ClubId)
            .OrderBy(f => f.SortOrder)
            .Select(f => new
            {
                f.Slug,
                f.Status,
                f.SortOrder,
                QuestionZh = f.FaqsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Question).FirstOrDefault(),
                AnswerZh = f.FaqsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Answer).FirstOrDefault(),
                QuestionEn = f.FaqsI18ns.Where(i => i.Locale == "en").Select(i => i.Question).FirstOrDefault(),
                AnswerEn = f.FaqsI18ns.Where(i => i.Locale == "en").Select(i => i.Answer).FirstOrDefault(),
                CategoryNames = f.FaqCategories
                    .Select(c => c.FaqCategoriesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() ?? c.Slug)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var rows = new List<IEnumerable<string?>> { CsvHeader };
        rows.AddRange(faqs.Select(f => (IEnumerable<string?>)
        [
            f.Slug,
            string.Join("、", f.CategoryNames),
            f.Status == "published" ? "顯示" : "隱藏",
            f.SortOrder.ToString(),
            f.QuestionZh,
            f.AnswerZh,
            f.QuestionEn,
            f.AnswerEn,
        ]));

        return CsvUtils.BuildCsv(rows);
    }

    /// <summary>
    /// CSV 匯入。🔴 **整批驗證，任一列有錯就整批不寫入**（任務指示明文，跟上面的批次操作刻意
    /// 是不同的容錯策略，理由見本檔「批次操作」區段開頭的說明）：先把所有列**在不碰資料庫寫入
    /// 的前提下**驗證完（格式、必填、分類名稱能否解析），只要有一列不合格就直接回傳完整的
    /// <see cref="FaqCsvImportRowErrorDto"/> 清單、不呼叫任何一次 <c>Add</c>／欄位指派；
    /// 全部合格才進第二輪真正讀寫資料庫並呼叫一次 <see cref="ClubDbContext.SaveChangesAsync"/>。
    /// **不寫入任何 log 表**（CLAUDE.md 全域規定、docs/18 `E-44`）——匯入本身沒有稽核需求，
    /// 這是單純的內容批次建立／更新，跟會員名單匯出那種需要稽核軌跡的情境不同。
    ///
    /// **Upsert 鍵是 <c>(club_id, slug)</c>**（跟 <see cref="FaqSlugConflictException"/> 用的
    /// 唯一鍵一致）：CSV 裡的網址名稱如果已經是這個俱樂部現有的某一題，就整份取代那一題的內容
    /// （沿用 <see cref="UpdateAsync"/> 同一套「整份取代」語意，包含分類清單）；不存在就新增一題。
    /// 對照 <c>docs/04-data-model.md</c> 第 130 行「`id` / `slug` … 客戶素材匯入時作為對應鍵」。
    /// **永遠不會比對到共用內容**（查詢固定帶 <c>club_id = scope.ClubId</c>），理由同
    /// <see cref="ExportCsvAsync"/>。
    /// </summary>
    public async Task<FaqCsvImportResultDto> ImportCsvAsync(AdminClubScope scope, string csvContent, Guid? operatorId, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvContent);
        if (rows.Count == 0)
        {
            throw new AdminFaqValidationException("檔案是空的，找不到任何資料列。");
        }

        var header = rows[0];
        if (header.Count != CsvHeader.Length || !header.SequenceEqual(CsvHeader, StringComparer.Ordinal))
        {
            throw new AdminFaqValidationException(
                $"檔案格式不正確，表頭必須依序是「{string.Join("、", CsvHeader)}」。");
        }

        var categoryByName = await LoadCategoryByNameAsync(cancellationToken);
        var errors = new List<FaqCsvImportRowErrorDto>();
        var parsedRows = new List<(string Slug, List<Guid> CategoryIds, string Status, int SortOrder, string QuestionZh, string AnswerZh, string? QuestionEn, string? AnswerEn)>();
        var seenSlugs = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1; // 表頭是第 1 行，第一筆資料是第 2 行。
            var row = rows[i];

            if (row.Count != CsvHeader.Length)
            {
                errors.Add(new FaqCsvImportRowErrorDto { RowNumber = rowNumber, Reason = $"欄位數不正確，應為 {CsvHeader.Length} 欄，實際 {row.Count} 欄。" });
                continue;
            }

            var slug = row[0].Trim();
            var categoryNamesRaw = row[1].Trim();
            var statusText = row[2].Trim();
            var sortOrderText = row[3].Trim();
            var questionZh = row[4].Trim();
            var answerZh = row[5].Trim();
            var questionEn = row[6].Trim();
            var answerEn = row[7].Trim();

            var rowErrors = new List<string>();

            try
            {
                FaqSlugPolicy.Validate(slug);
            }
            catch (AdminFaqValidationException ex)
            {
                rowErrors.Add(ex.Message);
            }

            if (slug.Length > 0 && !seenSlugs.Add(slug))
            {
                rowErrors.Add($"網址名稱「{slug}」在檔案中重複出現，同一份檔案裡的網址名稱不能重複。");
            }

            var categoryIds = new List<Guid>();
            var categoryNames = categoryNamesRaw
                .Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (categoryNames.Count == 0)
            {
                rowErrors.Add("至少要選擇一個所屬分類。");
            }
            else
            {
                foreach (var name in categoryNames)
                {
                    if (categoryByName.TryGetValue(name, out var category))
                    {
                        categoryIds.Add(category.Id);
                    }
                    else
                    {
                        rowErrors.Add($"分類「{name}」不存在，請確認名稱與後台的主題分類完全一致。");
                    }
                }
            }

            var status = statusText switch
            {
                "顯示" => "published",
                "隱藏" => "draft",
                _ => null,
            };
            if (status is null)
            {
                rowErrors.Add("狀態欄位必須是「顯示」或「隱藏」。");
            }

            if (!int.TryParse(sortOrderText, out var sortOrder))
            {
                rowErrors.Add("排序欄位必須是整數。");
            }

            if (questionZh.Length == 0)
            {
                rowErrors.Add("中文問題為必填欄位。");
            }

            if (answerZh.Length == 0)
            {
                rowErrors.Add("中文答案為必填欄位。");
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new FaqCsvImportRowErrorDto { RowNumber = rowNumber, Reason = string.Join("；", rowErrors) });
                continue;
            }

            parsedRows.Add((slug, categoryIds, status!, sortOrder, questionZh, answerZh,
                questionEn.Length == 0 ? null : questionEn, answerEn.Length == 0 ? null : answerEn));
        }

        if (errors.Count > 0)
        {
            // 🔴 任一列有錯就整批不寫入：這裡完全沒有呼叫過 dbContext 的任何寫入方法，
            // 直接回傳即可，不需要額外的復原動作。
            return new FaqCsvImportResultDto { ImportedCount = 0, Errors = errors };
        }

        var now = DateTime.UtcNow;
        foreach (var parsed in parsedRows)
        {
            var existing = await dbContext.Faqs
                .Include(f => f.FaqsI18ns)
                .Include(f => f.FaqCategories)
                .FirstOrDefaultAsync(f => f.ClubId == scope.ClubId && f.Slug == parsed.Slug, cancellationToken);

            var faq = existing ?? new Faq
            {
                Id = Guid.NewGuid(),
                ClubId = scope.ClubId,
                Slug = parsed.Slug,
                ViewCount = 0,
                HelpfulCount = 0,
                UnhelpfulCount = 0,
                CreatedAt = now,
                CreatedBy = operatorId,
            };
            if (existing is null)
            {
                dbContext.Faqs.Add(faq);
            }

            faq.Status = parsed.Status;
            faq.SortOrder = parsed.SortOrder;
            faq.UpdatedAt = now;
            faq.UpdatedBy = operatorId;

            AddOrReplaceI18n(faq, RequestLocale.DefaultDbLocale, new AdminFaqLocaleContent { Question = parsed.QuestionZh, Answer = parsed.AnswerZh });
            var existingEn = faq.FaqsI18ns.FirstOrDefault(i => i.Locale == "en");
            if (parsed.QuestionEn is not null || parsed.AnswerEn is not null)
            {
                AddOrReplaceI18n(faq, "en", new AdminFaqLocaleContent { Question = parsed.QuestionEn, Answer = parsed.AnswerEn });
            }
            else if (existingEn is not null)
            {
                dbContext.Remove(existingEn);
            }

            faq.FaqCategories.Clear();
            foreach (var categoryId in parsed.CategoryIds)
            {
                // 這裡直接用已載入的分類實體集合（LoadCategoryByNameAsync 回傳的字典的值），
                // 避免每一列都各自查一次資料庫；分類數量固定是十幾筆，全部撈進記憶體成本可忽略。
                faq.FaqCategories.Add(categoryByName.Values.First(c => c.Id == categoryId));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return new FaqCsvImportResultDto { ImportedCount = parsedRows.Count, Errors = [] };
    }

    /// <summary>依中文名稱（zh-Hant，去除前後空白）對照到分類實體，供 CSV 解析用。**若有兩個分類
    /// 剛好同名，取 <c>sort_order</c> 較小的那個**（分類數量小、由後台人員維護，本輪判斷這個
    /// 簡化風險可接受，沒有在 schema 層強制分類名稱唯一）。</summary>
    private async Task<Dictionary<string, FaqCategory>> LoadCategoryByNameAsync(CancellationToken cancellationToken)
    {
        var categories = await dbContext.FaqCategories
            .Include(c => c.FaqCategoriesI18ns)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var byName = new Dictionary<string, FaqCategory>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            var name = category.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                byName.TryAdd(name, category); // TryAdd：同名取先出現（sort_order 較小）的那個。
            }
        }

        return byName;
    }

    /// <summary>回傳 <c>null</c>＝找不到（含跨俱樂部），<c>true</c>＝刪除成功。
    /// 共用內容唯讀例外由 <see cref="LoadTrackedForWriteAsync"/> 統一擋下。</summary>
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var faq = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (faq is null)
        {
            return null;
        }

        dbContext.Faqs.Remove(faq);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);
        return true;
    }

    /// <summary>寫入路徑專用的載入：追蹤中、含 i18n 與分類。共用內容（<c>club_id IS NULL</c>）
    /// 直接丟 <see cref="SharedFaqReadOnlyException"/>，跨俱樂部回 <c>null</c>（呼叫端 404，
    /// 不洩漏存在與否）。理由與寫法逐字對應 <c>AdminArticlesRepository.LoadTrackedForWriteAsync</c>。</summary>
    private async Task<Faq?> LoadTrackedForWriteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var faq = await dbContext.Faqs
            .Include(f => f.FaqsI18ns)
            .Include(f => f.FaqCategories)
            .Include(f => f.FaqEmbedSlotLinks)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (faq is null || (faq.ClubId != scope.ClubId && faq.ClubId is not null))
        {
            return null;
        }

        if (faq.ClubId is null)
        {
            throw new SharedFaqReadOnlyException();
        }

        return faq;
    }

    private async Task<List<FaqCategory>> ResolveCategoriesAsync(IReadOnlyList<Guid> categoryIds, CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            throw new AdminFaqValidationException("至少要選擇一個所屬分類。");
        }

        var distinctIds = categoryIds.Distinct().ToList();
        var categories = await dbContext.FaqCategories.Where(c => distinctIds.Contains(c.Id)).ToListAsync(cancellationToken);
        if (categories.Count != distinctIds.Count)
        {
            throw new AdminFaqValidationException("所屬分類包含不存在的項目，請重新整理分類清單後再試一次。");
        }

        return categories;
    }

    /// <summary>解析 G-12 掛載點 id 清單，<paramref name="embedSlotIds"/> 為 <c>null</c> 或空陣列
    /// 皆回傳空清單——跟 <see cref="ResolveCategoriesAsync"/> 不同，這裡沒有「至少 1 個」的下限
    /// （見 <c>CreateFaqRequest.EmbedSlotIds</c> 的說明）。</summary>
    private async Task<List<FaqEmbedSlot>> ResolveEmbedSlotsAsync(IReadOnlyList<Guid>? embedSlotIds, CancellationToken cancellationToken)
    {
        if (embedSlotIds is null || embedSlotIds.Count == 0)
        {
            return [];
        }

        var distinctIds = embedSlotIds.Distinct().ToList();
        var slots = await dbContext.FaqEmbedSlots.Where(s => distinctIds.Contains(s.Id)).ToListAsync(cancellationToken);
        if (slots.Count != distinctIds.Count)
        {
            throw new AdminFaqValidationException("指定的掛載點包含不存在的項目，請重新整理清單後再試一次。");
        }

        return slots;
    }

    /// <summary>把已解析好的掛載點加進 <paramref name="faq"/>，依清單順序寫入 <c>sort_order</c>——
    /// <c>faq_embed_slot_links</c> 本身沒有 <c>row_seq</c>，用呼叫端給的順序當排序依據。</summary>
    private void AddEmbedSlotLinks(Faq faq, IReadOnlyList<FaqEmbedSlot> embedSlots)
    {
        for (var i = 0; i < embedSlots.Count; i++)
        {
            var link = new FaqEmbedSlotLink { FaqId = faq.Id, FaqEmbedSlotId = embedSlots[i].Id, SortOrder = i };
            faq.FaqEmbedSlotLinks.Add(link);
            dbContext.FaqEmbedSlotLinks.Add(link);
        }
    }

    private void AddOrReplaceI18n(Faq faq, string locale, AdminFaqLocaleContent content)
    {
        var existing = faq.FaqsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new FaqsI18n { FaqId = faq.Id, Locale = locale };
            faq.FaqsI18ns.Add(existing);
            dbContext.FaqsI18ns.Add(existing);
        }

        existing.Question = content.Question;
        existing.Answer = content.Answer;
    }

    private static void ValidateStatus(string status)
    {
        if (!AllowedStatuses.Contains(status))
        {
            throw new AdminFaqValidationException(
                "狀態只能是「draft」（草稿／隱藏）或「published」（顯示）——這個型別不支援排程發布" +
                "（docs/14-invariants.md「S0-7g」：沒有 published_at 欄位可以記排定時間）。");
        }
    }

    private static void ValidateContent(AdminFaqContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Question))
        {
            throw new AdminFaqValidationException("中文問題為必填欄位。");
        }
        if (string.IsNullOrWhiteSpace(content.Zh.Answer))
        {
            throw new AdminFaqValidationException("中文答案為必填欄位。");
        }
    }

    private static AdminFaqDetailDto ToDetailDto(Faq faq)
    {
        var zh = faq.FaqsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = faq.FaqsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminFaqDetailDto
        {
            Id = faq.Id,
            Slug = faq.Slug,
            SortOrder = faq.SortOrder,
            Status = faq.Status,
            IsShared = faq.ClubId is null,
            ViewCount = faq.ViewCount,
            HelpfulCount = faq.HelpfulCount,
            UnhelpfulCount = faq.UnhelpfulCount,
            UpdatedAt = faq.UpdatedAt,
            Zh = new AdminFaqLocaleContent { Question = zh?.Question, Answer = zh?.Answer },
            En = en is null ? null : new AdminFaqLocaleContent { Question = en.Question, Answer = en.Answer },
            Categories = faq.FaqCategories.Select(c => new AdminFaqCategoryRefDto
            {
                Id = c.Id,
                Slug = c.Slug,
                NameZh = c.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
                NameEn = c.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == "en")?.Name,
            }).ToList(),
            EmbedSlots = faq.FaqEmbedSlotLinks
                .OrderBy(l => l.SortOrder)
                .Select(l => new AdminFaqEmbedSlotRefDto { Id = l.FaqEmbedSlot.Id, Code = l.FaqEmbedSlot.Code, Name = l.FaqEmbedSlot.Name })
                .ToList(),
        };
    }

    /// <summary>寫入成功後讓公開讀取快取失效（docs/17 §4「write-invalidate」），比照
    /// <c>AdminArticlesRepository.InvalidatePublicCacheAsync</c>。共同內容（<c>club_id IS NULL</c>）
    /// 對兩個俱樂部都可見，但這裡目前沒有機會發生（共用內容一律唯讀，走不到任何寫入路徑），
    /// 只失效呼叫端當下的俱樂部即可。</summary>
    private async Task InvalidatePublicCacheAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync(PublicListEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync(PublicDetailEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync(PublicEmbedEntity, scope.ClubCode, cancellationToken);
    }
}
