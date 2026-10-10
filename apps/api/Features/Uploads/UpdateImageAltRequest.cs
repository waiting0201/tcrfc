namespace Tcrfc.Api.Features.Uploads;

/// <summary>
/// 多圖子表（圖集、漫畫內頁等，沒有 <c>_i18n</c> 側表）單張圖片的替代文字更新（主站規劃書 §4.0 圖片欄位組「雙語 Alt 文字」）。
/// 對應子表的 <c>image_alt_zh</c>／<c>image_alt_en</c>；各 ≤200 字，空白存 <c>null</c>（前台英文空白回退中文）。
/// 上傳圖片端點本身不接 Alt：先上傳、再用 <c>PUT .../images/{imageId}</c>（漫畫為 <c>.../pages/{pageId}</c>）補。
/// </summary>
public sealed record UpdateImageAltRequest
{
    /// <summary>中文替代文字。</summary>
    public string? AltZh { get; init; }

    /// <summary>英文替代文字。</summary>
    public string? AltEn { get; init; }
}

/// <summary>多圖子表替代文字（並排 <c>image_alt_zh</c>／<c>image_alt_en</c>）的共用驗證與語系選取。</summary>
public static class GalleryImageAlt
{
    /// <summary>驗證並正規化兩個語系的替代文字（trim、空白存 null、各 ≤200 字，過長回 400 並指出欄位）。</summary>
    public static (string? Zh, string? En) Normalize(UpdateImageAltRequest request)
        => (Common.AdminInput.OptionalText(request.AltZh, "圖片替代文字（中文）", 200, "altZh"),
            Common.AdminInput.OptionalText(request.AltEn, "圖片替代文字（英文）", 200, "altEn"));

    /// <summary>依請求語系取替代文字，英文空白回退中文。</summary>
    public static string? Pick(string dbLocale, string? altZh, string? altEn)
        => dbLocale == "en" ? Localization.RequestLocale.Pick(altEn, altZh) : altZh;
}
