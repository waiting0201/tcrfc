namespace Tcrfc.Api.Features.Seo;

/// <summary>公開讀取的全站 SEO 預設＋追蹤碼（S1-12）。內容全部是「本來就會出現在頁面原始碼／
/// HTTP 回應」的資訊（標題樣板、描述、追蹤碼 ID、robots.txt 規則），公開端點不需要登入，
/// 前台（Nuxt）用來組 <c>&lt;title&gt;</c>／<c>&lt;meta name="description"&gt;</c>、注入追蹤碼
/// 腳本、組 <c>/robots.txt</c>。形狀對應後台 <c>Features/AdminSeo/AdminSeoSettingsDto</c>，
/// 欄位一致，只是這一份不需要授權即可讀取。</summary>
public sealed record PublicSeoSettingsDto
{
    public string? TitleTemplateZh { get; init; }
    public string? TitleTemplateEn { get; init; }
    public string? DefaultDescriptionZh { get; init; }
    public string? DefaultDescriptionEn { get; init; }
    public string? RobotsCustomRules { get; init; }
    public string? Ga4MeasurementId { get; init; }
    public string? GtmContainerId { get; init; }
    public string? MetaPixelId { get; init; }
    public string? LineTagId { get; init; }

    /// <summary>全站預設 OG 圖片完整網址（S1-12 驗收退回後補做，<c>Club.OgImageKey</c>）。
    /// 靜態單元頁（前台既有 80 頁，未串接 <c>pages</c>／<c>articles</c> 動態內容）沒有其他
    /// OG 圖片來源可用時，直接用這裡的值；有逐篇動態內容的頁面（新聞詳情、未來的 B1 頁面）已經
    /// 在各自的公開端點把「這個頁面專屬 &gt; 全站預設」的優先序算好，不需要再組合一次。</summary>
    public string? OgImageUrl { get; init; }

    public int? OgImageWidth { get; init; }
    public int? OgImageHeight { get; init; }
}

/// <summary>單筆生效中的 301 轉址（<c>is_active = 1</c>）。前台（Nuxt server middleware）用來
/// 判斷「這個請求路徑要不要送 301」，見 apps/api/README.md「S1-12」段的前台串接說明。</summary>
public sealed record PublicRedirectDto
{
    public required string FromPath { get; init; }
    public required string ToPath { get; init; }
}

/// <summary>一筆 Sitemap 項目。<see cref="LastModifiedAt"/> 供 <c>&lt;lastmod&gt;</c> 使用，
/// 可為空（規劃書沒有要求所有內容都一定要有異動時間，見 apps/api/README.md）。</summary>
public sealed record SitemapEntryDto
{
    public required string Path { get; init; }
    public DateTime? LastModifiedAt { get; init; }
}
