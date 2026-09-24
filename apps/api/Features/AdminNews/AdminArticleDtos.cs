namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 單一語系的可編輯內容。<c>Title</c> 以外全部可為 <c>null</c>。
/// 🔴 這裡刻意跟公開讀取 API 的 <c>ArticleDetailDto</c> 分開——公開 API 回傳的是「已依語系回退
/// 挑值後」的單一字串，後台編輯需要的是「這個語系實際存了什麼」（不回退），兩者語意不同，
/// 混用同一個型別會讓後台編輯畫面把回退後的中文誤存回英文欄位。
/// </summary>
public sealed record AdminArticleLocaleContent
{
    public string? Title { get; init; }
    public string? Summary { get; init; }

    /// <summary>對應 <c>articles_i18n.body</c>（實際型別是 <c>nvarchar(max)</c>，見 README
    /// 「body 欄位的型別落差」——不強制驗證是 JSON，目前 apps/admin 的 mockup 送的是純文字，
    /// 不是區塊編輯器的 JSON 結構）。</summary>
    public string? Body { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
}

/// <summary>
/// 建立／更新文章時的雙語內容輸入。<c>Zh</c> 必填（且 <see cref="AdminArticleLocaleContent.Title"/>
/// 不得空白，CLAUDE.md 全域規定 4），<c>En</c> 可省略（省略＝這篇文章目前沒有英文版）。
/// </summary>
public sealed record AdminArticleContentInput
{
    public required AdminArticleLocaleContent Zh { get; init; }
    public AdminArticleLocaleContent? En { get; init; }
}

/// <summary>
/// 標籤輸入（S1-5 新增）。<c>Slug</c> 對應既有標籤就直接沿用（<c>NameZh</c>／<c>NameEn</c>
/// 一律忽略，標籤名稱一旦建立由標籤自己管理，不因為某一篇文章的輸入而被悄悄改掉）；
/// <c>Slug</c> 在 <c>tags</c> 找不到就新建一個，這時候 <c>NameZh</c> 是必填（新標籤要有中文名稱
/// 才有意義給人看，<see cref="AdminArticleValidationException"/> 擋不給的情況），<c>NameEn</c>
/// 可省略（英文可空，CLAUDE.md 全域規定 4）。格式規則見 <see cref="TagSlugFormat"/>。
/// </summary>
public sealed record AdminArticleTagInput
{
    public required string Slug { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>標籤輸出（後台編輯頁回填用）。不做語系回退——跟 <see cref="AdminArticleLocaleContent"/>
/// 同一個理由，後台要看到「這個語系實際存了什麼」。</summary>
public sealed record AdminArticleTagDto
{
    public required string Slug { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>
/// 文章多型關聯（<c>article_relations</c>，S1-5 新增）：<c>TargetType</c> 只接受規劃書 B2
/// 明文列出的五種——<c>player</c>／<c>team</c>／<c>match</c>／<c>program</c>／<c>partner</c>
/// （對應球員／球隊／賽事／課程／夥伴，見 <c>AdminArticlesRepository.AllowedRelationTargetTypes</c>）。
/// <c>TargetId</c> 必須是**這篇文章所屬俱樂部**底下真實存在的那一種實體——跨俱樂部或不存在
/// 一律 400（<see cref="AdminArticleValidationException"/>），這是「多型關聯的跨俱樂部隔離」
/// 這條規則在程式碼裡唯一的落點。輸入與輸出共用同一個形狀，不需要分開兩個型別。
/// </summary>
public sealed record AdminArticleRelationInput
{
    public required string TargetType { get; init; }
    public required Guid TargetId { get; init; }
}

/// <summary>
/// 🔴🔴🔴 S0-8 修正（規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」）：這是 <c>payload</c> 這個
/// multipart 欄位的 JSON 內容，**不含封面圖片鍵**——封面圖片透過同一次請求的 <c>file</c> 欄位
/// 一起送出，由 <see cref="AdminArticlesEndpoints"/> 處理上傳並把結果寫進資料列，呼叫端不會、
/// 也不能自己指定物件鍵字串（見 apps/api/README.md「圖片上傳共用元件」整節的新契約）。
/// </summary>
public sealed record CreateArticleRequest
{
    /// <summary>畫面上叫「網址名稱」（docs/03 §後台設計通則④），對應 <c>articles.slug</c>。</summary>
    public required string Slug { get; init; }

    /// <summary>對應 <c>article_categories.code</c>（7.1–7.8 分類代碼），不是分類的 GUID。</summary>
    public required string CategoryCode { get; init; }

    public bool IsFeatured { get; init; }

    public required AdminArticleContentInput Content { get; init; }

    /// <summary>
    /// 標籤（S1-5 新增）。省略（<c>null</c>）＝這篇文章不掛任何標籤，等同傳空陣列——
    /// 建立時沒有「維持原樣」這回事，這點跟 <see cref="UpdateArticleRequest.Tags"/> 不同。
    /// </summary>
    public IReadOnlyList<AdminArticleTagInput>? Tags { get; init; }

    /// <summary>
    /// 核心價值標籤（S1-5 新增）。值域見規劃書 §1.2 五大核心價值：<c>players_first</c>／
    /// <c>excellence</c>／<c>global_pathways</c>／<c>community</c>／<c>integrity</c>
    /// （<c>value_tag_links.value_tag</c> 的 CHECK 約束）。省略＝不掛任何核心價值標籤。
    /// </summary>
    public IReadOnlyList<string>? CoreValueTags { get; init; }

    /// <summary>關聯（S1-5 新增）。省略＝這篇文章不關聯任何球員／球隊／賽事／課程／夥伴。</summary>
    public IReadOnlyList<AdminArticleRelationInput>? Relations { get; init; }
}

/// <summary>
/// 同上，這是 PUT 請求 <c>payload</c> 欄位的 JSON 內容。封面圖片的三態改變見
/// <see cref="RemoveCover"/> 與 <see cref="CoverKeyUpdate"/>。
/// </summary>
public sealed record UpdateArticleRequest
{
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
    public bool IsFeatured { get; init; }
    public required AdminArticleContentInput Content { get; init; }

    /// <summary>
    /// 勾選「移除封面圖片」。🔴 跟這次請求的 <c>file</c> 欄位互斥——兩個都有視為請求矛盾，
    /// <see cref="AdminArticlesEndpoints"/> 回 400（<see cref="AdminArticleValidationException"/>）。
    /// 兩者都沒有＝維持目前的封面圖片不變（<see cref="CoverKeyUpdate.Keep"/>）。
    /// </summary>
    public bool RemoveCover { get; init; }

    /// <summary>
    /// 樂觀並行控制權杖：呼叫端上次讀到的 <c>updated_at</c>（<see cref="AdminArticleDetailDto.UpdatedAt"/>）。
    /// 跟資料庫目前的值對不起來就回 409，⛔ 不做「後寫的贏」。
    /// </summary>
    public required DateTime ExpectedUpdatedAt { get; init; }

    /// <summary>
    /// 標籤（S1-5 新增）。🔴 **省略（<c>null</c>）＝維持目前的標籤不變**，跟雙語內容整份取代的
    /// 語意不同——前端既有畫面（<c>NewsEditView.vue</c>）目前完全沒有標籤輸入框，若比照內容欄位
    /// 「省略＝清空」，任何既有標籤在下一次改標題這類跟標籤無關的存檔就會被整批清掉，
    /// 是比「這個欄位還沒做」更糟的資料損毀。空陣列（<c>[]</c>）才是「明確清空所有標籤」。
    /// </summary>
    public IReadOnlyList<AdminArticleTagInput>? Tags { get; init; }

    /// <summary>核心價值標籤（S1-5 新增）。省略語意同 <see cref="Tags"/>：維持不變，不是清空。</summary>
    public IReadOnlyList<string>? CoreValueTags { get; init; }

    /// <summary>關聯（S1-5 新增）。省略語意同 <see cref="Tags"/>：維持不變，不是清空。</summary>
    public IReadOnlyList<AdminArticleRelationInput>? Relations { get; init; }
}

public sealed record PublishArticleRequest
{
    public required DateTime ExpectedUpdatedAt { get; init; }
}

public sealed record ScheduleArticleRequest
{
    public required DateTime ExpectedUpdatedAt { get; init; }

    /// <summary>排程發布時間（UTC）。必須晚於呼叫當下，否則 400。</summary>
    public required DateTime PublishAt { get; init; }
}

/// <summary>後台清單一列。跟公開 API 的 <c>ArticleListItemDto</c> 不同之處：不依語系回退、
/// 帶 <c>status</c> 三態（含草稿與排程）、帶 <see cref="UpdatedAt"/> 供之後打開編輯頁時當並行權杖起點。</summary>
public sealed record AdminArticleListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
    public string? CoverKey { get; init; }
    public required bool IsFeatured { get; init; }

    /// <summary>值域 <c>draft</c>／<c>published</c>／<c>scheduled</c>（<c>articles.status</c> 的 CHECK 約束）。
    /// ⛔ 沒有 <c>disabled</c>（下架）——apps/admin 目前的 mockup 型別有第四態，但資料庫沒有對應欄位值，
    /// 這是本輪發現、回報但沒有動手加的落差，見 README。</summary>
    public required string Status { get; init; }

    public DateTime? PublishedAt { get; init; }
    public required bool IsShared { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }

    /// <summary>標籤（S1-5 新增）。列表頁沿用同一筆查詢附帶回傳，方便後台列表顯示標籤晶片。</summary>
    public required IReadOnlyList<AdminArticleTagDto> Tags { get; init; }

    /// <summary>瀏覽數（S1-5 新增，規劃書 B2「瀏覽數統計」）。唯讀，只會透過公開端點
    /// （<c>POST /api/v1/{club}/news/{slug}/views</c>）遞增，這裡單純回填讓後台看得到。</summary>
    public required int ViewCount { get; init; }
}

/// <summary>後台單篇詳情（編輯頁用）。<see cref="UpdatedAt"/> 是下一次寫入要帶回來的並行權杖。</summary>
public sealed record AdminArticleDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
    public string? CoverKey { get; init; }
    public required bool IsFeatured { get; init; }
    public required string Status { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required bool IsShared { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required AdminArticleLocaleContent Zh { get; init; }
    public AdminArticleLocaleContent? En { get; init; }

    /// <summary>瀏覽數（S1-5 新增）。說明同 <see cref="AdminArticleListItemDto.ViewCount"/>。</summary>
    public required int ViewCount { get; init; }

    /// <summary>標籤（S1-5 新增）。</summary>
    public required IReadOnlyList<AdminArticleTagDto> Tags { get; init; }

    /// <summary>核心價值標籤（S1-5 新增）。值域見 <see cref="CreateArticleRequest.CoreValueTags"/>。</summary>
    public required IReadOnlyList<string> CoreValueTags { get; init; }

    /// <summary>關聯（S1-5 新增）。</summary>
    public required IReadOnlyList<AdminArticleRelationInput> Relations { get; init; }
}

// ── 批次操作（S1-5 新增，規劃書 B2「批次操作：改分類、批次發布／下架」）─────────────────

/// <summary>批次改分類的請求。</summary>
public sealed record BatchChangeCategoryRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }

    /// <summary>對應 <c>article_categories.code</c>，跟單篇更新一樣不是分類的 GUID。</summary>
    public required string CategoryCode { get; init; }
}

/// <summary>批次發布／批次下架共用的請求形狀——只需要知道要處理哪些文章。</summary>
public sealed record BatchArticleIdsRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
}

/// <summary>批次操作裡沒有處理成功的一筆，附上人類看得懂的原因（找不到／共用內容唯讀／
/// 狀態不允許……）。批次操作**不是全有全無**：可以處理的照樣處理，處理不了的列在這裡，
/// 不會因為其中一筆不合法就讓整批都失敗——這是批次操作（勾選多筆按一個按鈕）跟單篇編輯
/// （有明確的並行權杖與畫面可以顯示個別錯誤）在使用情境上的差異。</summary>
public sealed record BatchOperationSkippedItemDto
{
    public required Guid Id { get; init; }
    public required string Reason { get; init; }
}

/// <summary>批次操作的回應：處理成功的筆數，以及每一筆處理不了的原因。</summary>
public sealed record BatchOperationResultDto
{
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<BatchOperationSkippedItemDto> Skipped { get; init; }
}
