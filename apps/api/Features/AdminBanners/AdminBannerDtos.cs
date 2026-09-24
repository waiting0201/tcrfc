namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>單一語系可編輯內容。<c>banners_i18n</c> 五欄全部可為 <c>null</c>（db/club-schema.sql）。</summary>
public sealed record AdminBannerLocaleContent
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Cta1Label { get; init; }
    public string? Cta1Url { get; init; }
    public string? Cta2Label { get; init; }
    public string? Cta2Url { get; init; }
}

/// <summary><c>Zh</c> 必填（雙語規則本身不要求標題非空白——<c>banners_i18n</c> 的欄位本來就全部
/// 可為 <c>null</c>，一張只靠視覺的輪播圖不一定需要文字），<c>En</c> 可省略。</summary>
public sealed record AdminBannerContentInput
{
    public required AdminBannerLocaleContent Zh { get; init; }
    public AdminBannerLocaleContent? En { get; init; }
}

/// <summary>
/// 🔴 這是 <c>payload</c> 這個 multipart 欄位的 JSON 內容，不含圖片鍵——圖片透過同一次請求的
/// <c>file</c> 欄位一起送出，比照 <c>Features/AdminNews</c> 的既有契約（見
/// apps/api/README.md「多檔案 multipart 契約」段的既有說明；本模組只有單張圖，形狀更接近
/// B2 新聞封面圖那種「固定一個 file 欄位」，不是 B1 頁面那種多欄位路徑命名）。
/// </summary>
public sealed record CreateBannerRequest
{
    /// <summary>上架起訖時間（規劃書 B3「上架期間」）。皆可為 <c>null</c>＝不限制起訖，
    /// 一建立就可能出現在前台（只要 <c>EndAt</c> 也是 <c>null</c> 或還沒到）。</summary>
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }

    public required int SortOrder { get; init; }

    public required AdminBannerContentInput Content { get; init; }
}

/// <summary>
/// 更新請求。圖片是否更換由「這次請求有沒有帶 <c>file</c>」決定（見
/// <c>AdminBannersEndpoints</c>）——<c>banners.image_key</c> 是 <c>NOT NULL</c>，不像文章封面圖
/// 有「移除」這個選項，只有「換一張」或「維持原圖」兩態。
/// </summary>
public sealed record UpdateBannerRequest
{
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required AdminBannerContentInput Content { get; init; }
}

public sealed record AdminBannerListItemDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }
}

public sealed record AdminBannerDetailDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required AdminBannerLocaleContent Zh { get; init; }
    public AdminBannerLocaleContent? En { get; init; }
}
