using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Seo;

/// <summary>
/// 公開讀取（S1-12，H 搜尋與 AI 能見度的前台落點）：全站 SEO 預設與追蹤碼、生效中的 301 轉址、
/// Sitemap 項目。三者皆與語系無關（設定值同時回傳 zh／en 兩份；轉址與 Sitemap 網址本身不分語系，
/// 目前站上也只有 zh 頁面存在，見 <c>apps/web/server/routes/sitemap.xml.ts</c> 既有註解），
/// 快取 key 的 locale 維度一律用 <see cref="CacheDimensions.AnyLocale"/>。
///
/// 🔴 **寫入端（<c>Features/AdminSeo</c>）刻意不呼叫 <see cref="IQueryCache.InvalidateAsync"/>**——
/// 比照 <c>IQueryCache</c> 介面文件本身的既有說明（「目前後台還不存在，沒有任何寫入層會呼叫這個
/// 方法……讀取端完全靠 TTL 兜底過期」），本輪維持與其餘既有唯讀 repository 一致的取捨：管理員
/// 改設定或轉址後，最多延後一個 TTL（預設 300 秒）才會反映到公開端點。這不是遺漏，是跟現有五組
/// repository 一致的既定行為，之後若要做 write-invalidate，直接在
/// <c>Features/AdminSeo</c> 各自的寫入方法補呼叫即可，這裡的讀取介面不需要改。
/// </summary>
public sealed class SeoRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrlResolver)
{
    private const string SettingsEntity = "seo-settings";
    private const string RedirectsEntity = "seo-redirects";
    private const string SitemapEntity = "seo-sitemap-entries";

    private static readonly string[] SettingKeys =
    [
        "seo.title_template", "seo.default_description", "seo.robots_custom_rules",
        "tracking.ga4_measurement_id", "tracking.gtm_container_id", "tracking.meta_pixel_id", "tracking.line_tag_id",
    ];

    private sealed record SettingValueRow(string SettingKey, string? SettingValue);
    private sealed record SettingI18nRow(string SettingKey, string Locale, string? Value);
    private sealed record RedirectRow(string FromPath, string ToPath);
    private sealed record SitemapRow(string Slug, DateTime UpdatedAt);
    private sealed record ClubOgImageRow(string? OgImageKey, int? OgImageWidth, int? OgImageHeight);

    public async Task<PublicSeoSettingsDto> GetSettingsAsync(ClubScope scope, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            SettingsEntity, scope.ClubCode, CacheDimensions.AnyLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string valueSql = """
                    SELECT setting_key AS SettingKey, setting_value AS SettingValue
                    FROM settings
                    WHERE club_id = @ClubId AND setting_key IN @Keys
                    """;
                const string i18nSql = """
                    SELECT s.setting_key AS SettingKey, si.locale AS Locale, si.value AS Value
                    FROM settings s
                    JOIN settings_i18n si ON si.setting_id = s.id
                    WHERE s.club_id = @ClubId AND s.setting_key IN @Keys
                    """;

                var valueRows = (await connection.QueryAsync<SettingValueRow>(new CommandDefinition(
                    valueSql, new { scope.ClubId, Keys = SettingKeys }, cancellationToken: ct))).ToList();
                var i18nRows = (await connection.QueryAsync<SettingI18nRow>(new CommandDefinition(
                    i18nSql, new { scope.ClubId, Keys = SettingKeys }, cancellationToken: ct))).ToList();

                const string clubSql = """
                    SELECT og_image_key AS OgImageKey, og_image_width AS OgImageWidth, og_image_height AS OgImageHeight
                    FROM clubs WHERE id = @ClubId
                    """;
                var club = await connection.QuerySingleOrDefaultAsync<ClubOgImageRow>(new CommandDefinition(
                    clubSql, new { scope.ClubId }, cancellationToken: ct));

                string? Value(string key) => valueRows.FirstOrDefault(r => r.SettingKey == key)?.SettingValue;
                string? I18n(string key, string locale) => i18nRows
                    .FirstOrDefault(r => r.SettingKey == key && r.Locale == locale)?.Value;

                return new PublicSeoSettingsDto
                {
                    TitleTemplateZh = I18n("seo.title_template", RequestLocale.DefaultDbLocale),
                    TitleTemplateEn = I18n("seo.title_template", "en"),
                    DefaultDescriptionZh = I18n("seo.default_description", RequestLocale.DefaultDbLocale),
                    DefaultDescriptionEn = I18n("seo.default_description", "en"),
                    RobotsCustomRules = Value("seo.robots_custom_rules"),
                    Ga4MeasurementId = Value("tracking.ga4_measurement_id"),
                    GtmContainerId = Value("tracking.gtm_container_id"),
                    MetaPixelId = Value("tracking.meta_pixel_id"),
                    LineTagId = Value("tracking.line_tag_id"),
                    OgImageUrl = imageUrlResolver.Resolve(club?.OgImageKey),
                    OgImageWidth = club?.OgImageWidth,
                    OgImageHeight = club?.OgImageHeight,
                };
            },
            cancellationToken);
    }

    /// <summary>只回傳 <c>is_active = 1</c> 的規則——停用的轉址不該讓前台真的轉走使用者。</summary>
    public async Task<IReadOnlyList<PublicRedirectDto>> GetActiveRedirectsAsync(ClubScope scope, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            RedirectsEntity, scope.ClubCode, CacheDimensions.AnyLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string sql = """
                    SELECT from_path AS FromPath, to_path AS ToPath
                    FROM redirects
                    WHERE club_id = @ClubId AND is_active = 1
                    """;

                var rows = (await connection.QueryAsync<RedirectRow>(new CommandDefinition(
                    sql, new { scope.ClubId }, cancellationToken: ct))).ToList();

                return (IReadOnlyList<PublicRedirectDto>)rows
                    .Select(r => new PublicRedirectDto { FromPath = r.FromPath, ToPath = r.ToPath })
                    .ToList();
            },
            cancellationToken);
    }

    /// <summary>
    /// Sitemap 項目。🔴 **目前只涵蓋 <c>Article</c>（新聞逐篇頁）**——這是本輪唯一真的有
    /// 逐篇動態路由的內容型別；<c>Page</c> 雖然規格上是「網站的靜態頁面路由」，但目前
    /// <c>apps/web</c> 的既有單元頁仍是 mockup 搬遷的靜態 Vue 檔案，不是查 <c>pages</c> 表渲染
    /// （見 apps/api/README.md「B1 頁面管理」既有落差說明）——此時把 <c>Page</c> 併入 Sitemap
    /// 會列出前台實際不存在對應內容的網址，等 B1 真正接上前台動態路由後再擴充，見
    /// apps/api/README.md「S1-12」段「規劃書沒寫清楚、自行判斷」。
    /// 排除 <c>is_noindex</c> 與 <c>is_excluded_from_sitemap</c> 任一為真的文章（GEO-05
    /// 「資料不足或不該收錄時不輸出」同一個精神）。
    /// </summary>
    public async Task<IReadOnlyList<SitemapEntryDto>> GetSitemapEntriesAsync(ClubScope scope, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            SitemapEntity, scope.ClubCode, CacheDimensions.AnyLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var sql = $"""
                    SELECT a.slug AS Slug, a.updated_at AS UpdatedAt
                    FROM articles a
                    WHERE {ClubOrSharedSql.WhereClubOrShared}
                      AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
                      AND a.is_noindex = 0 AND a.is_excluded_from_sitemap = 0
                    """;

                var rows = (await connection.QueryAsync<SitemapRow>(new CommandDefinition(
                    sql, new { scope.ClubId }, cancellationToken: ct))).ToList();

                return (IReadOnlyList<SitemapEntryDto>)rows
                    .Select(r => new SitemapEntryDto { Path = $"/zh/news/{r.Slug}/", LastModifiedAt = r.UpdatedAt })
                    .ToList();
            },
            cancellationToken);
    }
}
