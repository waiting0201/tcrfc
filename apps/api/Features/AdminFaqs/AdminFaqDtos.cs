namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>單一語系可編輯內容。理由同 <c>Features/AdminNews/AdminArticleLocaleContent</c>：
/// 後台編輯要看到「這個語系實際存了什麼」，不做回退。</summary>
public sealed record AdminFaqLocaleContent
{
    public string? Question { get; init; }
    public string? Answer { get; init; }
}

/// <summary><c>Zh</c> 必填且 <see cref="AdminFaqLocaleContent.Question"/>／
/// <see cref="AdminFaqLocaleContent.Answer"/> 皆不得空白，<c>En</c> 可省略。</summary>
public sealed record AdminFaqContentInput
{
    public required AdminFaqLocaleContent Zh { get; init; }
    public AdminFaqLocaleContent? En { get; init; }
}

/// <summary>
/// 建立常見問題的請求。<c>faqs.status</c> 只接受 <c>draft</c>／<c>published</c>——這個型別沒有
/// <c>published_at</c> 欄位，不支援排程發布（docs/14-invariants.md「S0-7g」／`AdminCompetitionsRepository`
/// 同一種處理方式），狀態是這裡的一個平面欄位，不是獨立的發布／排程端點。
/// </summary>
public sealed record CreateFaqRequest
{
    /// <summary>畫面上叫「網址名稱」，對應 <c>faqs.slug</c>，格式見 <see cref="FaqSlugPolicy"/>。</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// 所屬分類（規劃書 B4「所屬分類（可複選）」）。🔴 本輪判斷至少 1 個——沒有任何分類的題目
    /// 在前台完全無法透過主題分類導覽找到（只能靠關鍵字搜尋），等同事實上不可見，比「這個功能
    /// 還沒做」更容易被誤以為是 bug，故要求至少 1 個。這是需要業務確認的判斷，非規劃書明文，
    /// 見 apps/api/README.md「我的判斷」。
    /// </summary>
    public required IReadOnlyList<Guid> CategoryIds { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>值域 <c>draft</c>／<c>published</c>。</summary>
    public string Status { get; init; } = "draft";

    /// <summary>
    /// G-12 快捷區塊「額外」指定出現的掛載點（S1-7a，<c>faq_embed_slot_links</c>），疊加在
    /// 「由分類自動對應」之上、不是取代——由分類自動對應是應用層（前台頁面元件）的固定路由決定，
    /// 不經過這裡（docs/12 §12 第 34 點）。省略或空陣列＝這題沒有額外指定任何掛載點，
    /// 完全合法，不像 <see cref="CategoryIds"/> 有「至少 1 個」的下限。
    /// </summary>
    public IReadOnlyList<Guid>? EmbedSlotIds { get; init; }

    public required AdminFaqContentInput Content { get; init; }
}

public sealed record UpdateFaqRequest
{
    public required string Slug { get; init; }
    public required IReadOnlyList<Guid> CategoryIds { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }

    /// <summary>省略＝維持不變、空陣列＝清空——跟 <c>AdminStaffRepository.Teams</c> 同一種既有語意
    /// （見 <see cref="CreateFaqRequest.EmbedSlotIds"/> 的說明）。</summary>
    public IReadOnlyList<Guid>? EmbedSlotIds { get; init; }

    public required AdminFaqContentInput Content { get; init; }
}

public sealed record AdminFaqCategoryRefDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>該題額外指定出現的 G-12 掛載點（S1-7a）。</summary>
public sealed record AdminFaqEmbedSlotRefDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>後台清單一列。不依語系回退，帶三態狀態與成效數據（規劃書 B4「成效數據」）。</summary>
public sealed record AdminFaqListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required bool IsShared { get; init; }
    public required int ViewCount { get; init; }
    public required int HelpfulCount { get; init; }
    public required int UnhelpfulCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? QuestionZh { get; init; }
    public string? QuestionEn { get; init; }
    public required IReadOnlyList<AdminFaqCategoryRefDto> Categories { get; init; }
    public required IReadOnlyList<AdminFaqEmbedSlotRefDto> EmbedSlots { get; init; }
}

public sealed record AdminFaqDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required bool IsShared { get; init; }
    public required int ViewCount { get; init; }
    public required int HelpfulCount { get; init; }
    public required int UnhelpfulCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required AdminFaqLocaleContent Zh { get; init; }
    public AdminFaqLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminFaqCategoryRefDto> Categories { get; init; }
    public required IReadOnlyList<AdminFaqEmbedSlotRefDto> EmbedSlots { get; init; }
}

// ── 批次操作與 CSV 匯入匯出（追加，規劃書 B4「批次操作：批次改分類、批次顯示／隱藏、匯入／
// 匯出 CSV」，主站規劃書行 1033）。形狀逐字比照 Features/AdminNews 既有的批次操作三支端點
// （BatchChangeCategoryRequest／BatchArticleIdsRequest／BatchOperationResultDto），
// 這裡各自宣告一份 Faq 專用版本而不是共用型別，跟本專案既有的「每個模組自己宣告 DTO」慣例一致。

/// <summary>批次改分類的請求。跟單篇更新的 <c>CategoryIds</c> 不同——這裡是「整批改成同一組分類」
/// （取代原有分類，不是新增或移除），對應規劃書「批次改分類」字面上「改」的語意，跟 B2 新聞
/// 批次改分類（單一分類取代）同一種簡化。</summary>
public sealed record BatchChangeFaqCategoryRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public required IReadOnlyList<Guid> CategoryIds { get; init; }
}

/// <summary>批次顯示／隱藏共用的請求形狀——只需要知道要處理哪些題目。</summary>
public sealed record BatchFaqIdsRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
}

/// <summary>批次操作裡沒有處理成功的一筆，附上人類看得懂的原因（找不到／共用內容唯讀／
/// 跨俱樂部……）。批次操作不是全有全無，能處理的處理，不能處理的列出來。</summary>
public sealed record BatchFaqOperationSkippedItemDto
{
    public required Guid Id { get; init; }
    public required string Reason { get; init; }
}

public sealed record BatchFaqOperationResultDto
{
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<BatchFaqOperationSkippedItemDto> Skipped { get; init; }
}

/// <summary>CSV 匯入單列的錯誤。<see cref="RowNumber"/> 是 CSV 檔案的**實體行號**（表頭算第 1 行，
/// 第一筆資料是第 2 行）——這樣使用者在文字編輯器或試算表軟體開啟原始檔案時，行號可以直接對得起來，
/// 不需要自己心算「扣掉表頭」。</summary>
public sealed record FaqCsvImportRowErrorDto
{
    public required int RowNumber { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// CSV 匯入結果。🔴 整批驗證、任一列有錯就整批不寫入（任務指示明文）：<see cref="Errors"/> 非空
/// 時 <see cref="ImportedCount"/> 恆為 0，且資料庫沒有任何一列被真的寫入或更新
/// （見 <see cref="AdminFaqsRepository.ImportCsvAsync"/> 上的完整說明）。
/// </summary>
public sealed record FaqCsvImportResultDto
{
    public required int ImportedCount { get; init; }
    public required IReadOnlyList<FaqCsvImportRowErrorDto> Errors { get; init; }
}

// ── 搜尋無結果關鍵字排行（S1-8，規劃書主站 §4.2 B4「搜尋無結果關鍵字紀錄」，行 1032；
// docs/18-work-errors.md `E-51`：本輪之前只有寫入端，後台讀不到）───────────────────────

/// <summary>一筆關鍵字排行。<see cref="Count"/> 是 <c>faq_search_misses.hit_count</c>——
/// 該表是彙總列不是逐次搜尋的日誌（見 <c>faq_search_misses</c> 表註解與
/// <c>AdminFaqsRepository.ListSearchMissesAsync</c> 上的說明），故這裡是「這個關鍵字有史以來
/// 被搜尋不到的總次數」，**不是**「最近 N 天內被搜尋的次數」——<c>days</c> 篩選只決定
/// 「這個關鍵字最後一次被搜尋到，是不是在這個天數範圍內」，不會讓 <see cref="Count"/>
/// 只計算範圍內的次數（綱要沒有逐次搜尋列可供加總）。</summary>
public sealed record AdminFaqSearchMissDto
{
    public required string Keyword { get; init; }
    public required int Count { get; init; }
    public required DateTime LastSearchedAt { get; init; }
}
