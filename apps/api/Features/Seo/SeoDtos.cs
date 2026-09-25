using Tcrfc.Api.Features.AdminSeo;

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

/// <summary>公開讀取的 <c>llms.txt</c> 內容（`GEO-01`，S1-12a）。形狀對應後台
/// <c>Features/AdminSeo/AdminLlmsContentDto</c>，欄位一致，供 <c>apps/web</c> 的
/// <c>server/routes/llms.txt.ts</c>／<c>llms-en.txt.ts</c> 消費。任一欄位為 <c>null</c> 表示
/// 管理員尚未填寫，前台自行套用內建預設文字（見兩支路由檔頭說明），不是這個端點的責任。</summary>
public sealed record PublicLlmsContentDto
{
    public string? PositioningZh { get; init; }
    public string? PositioningEn { get; init; }
    public string? KeyPagesZh { get; init; }
    public string? KeyPagesEn { get; init; }
    public string? FactsSummaryZh { get; init; }
    public string? FactsSummaryEn { get; init; }
    public string? LicenseZh { get; init; }
    public string? LicenseEn { get; init; }
    public string? ContactZh { get; init; }
    public string? ContactEn { get; init; }
}

/// <summary>公開讀取的 AI 爬蟲授權設定（`GEO-02`，S1-12b），供 <c>apps/web</c> 的
/// <c>server/routes/robots.txt.ts</c> 組出各 <c>User-agent:</c> 區塊。<see cref="ExcludePaths"/>
/// **已經是強制排除路徑 ∪ 後台自行再加的路徑**的合併結果（見
/// <see cref="SeoRepository.GetCrawlerSettingsAsync"/> 檔頭）——前台不需要、也不應該自己再算一次
/// 強制清單，直接用這裡回傳的完整清單即可。</summary>
public sealed record PublicCrawlerSettingsDto
{
    public required IReadOnlyList<CrawlerAgentDto> UserAgents { get; init; }
    public required IReadOnlyList<string> ExcludePaths { get; init; }
}
