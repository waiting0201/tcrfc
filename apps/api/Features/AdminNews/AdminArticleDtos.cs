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
}
