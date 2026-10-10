using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 全站 SEO 預設＋追蹤碼的讀寫（S1-12）。文字欄位**不新增資料表**，沿用既有 <c>settings</c>／
/// <c>settings_i18n</c>（跟 GEO 段落「設定值存 Setting」同一個原則，見 docs/05-i18n-seo.md §3）。
///
/// 設定鍵詞彙（本輪定案，規劃書沒有給字面值，之後任何模組要沿用同一套鍵值命名慣例
/// <c>&lt;setting_group&gt;.&lt;name&gt;</c>）：
/// - <c>setting_group='seo'</c>：<c>seo.title_template</c>（逐語系）、
///   <c>seo.default_description</c>（逐語系）、<c>seo.robots_custom_rules</c>（單一值，非語系）。
/// - <c>setting_group='tracking'</c>：<c>tracking.ga4_measurement_id</c>／
///   <c>tracking.gtm_container_id</c>／<c>tracking.meta_pixel_id</c>／<c>tracking.line_tag_id</c>
///   （皆單一值，這些 ID 本身不是人類語言）。
///
/// 🔴 **全站預設 OG 圖片刻意不走 <c>settings</c>**：<c>clubs.og_image_key</c>／<c>_width</c>／
/// <c>_height</c> 早已存在於資料庫（J4 品牌欄位），本輪查證後改用既有欄位而不是在 <c>settings</c>
/// 裡另外發明三個鍵重複儲存同一份資料——那會製造兩個可能互相矛盾的真實來源。寫入時直接更新
/// <c>Club</c> 實體，不經過 <c>Features/AdminClubs</c>（該模組的 <c>AdminClubDetailDto.OgImageKey</c>
/// 原本刻意唯讀，是因為當時的任務邊界要求不要動圖片上傳共用元件，跟本輪的任務邊界不同，
/// 見 <c>AdminClubDtos.cs</c> 的既有註解）。（主站規劃書 v3.20：<c>clubs</c> 的標誌、Favicon、
/// 品牌色欄位已刪除，由前台靜態資產與 CSS 定義；<c>og_image_*</c> 保留。）
/// </summary>
public sealed partial class AdminSeoSettingsRepository(ClubDbContext dbContext, IImagePublicUrlResolver imageUrlResolver, IQueryCache cache)
{
    private const string KeyTitleTemplate = "seo.title_template";
    private const string KeyDefaultDescription = "seo.default_description";
    private const string KeyRobotsCustomRules = "seo.robots_custom_rules";
    private const string KeyGa4 = "tracking.ga4_measurement_id";
    private const string KeyGtm = "tracking.gtm_container_id";
    private const string KeyMetaPixel = "tracking.meta_pixel_id";
    private const string KeyLineTag = "tracking.line_tag_id";

    private const string GroupSeo = "seo";
    private const string GroupTracking = "tracking";

    private static readonly string[] AllKeys =
    [
        KeyTitleTemplate, KeyDefaultDescription, KeyRobotsCustomRules,
        KeyGa4, KeyGtm, KeyMetaPixel, KeyLineTag,
    ];

    public async Task<AdminSeoSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking()
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToListAsync(cancellationToken);

        var club = await dbContext.Clubs.AsNoTracking().Include(c => c.ClubsI18ns)
            .FirstOrDefaultAsync(c => c.Id == scope.ClubId, cancellationToken);

        return BuildDto(settings, club);
    }

    /// <summary>文字欄位整份取代語意——呼叫端一律送出完整表單內容
    /// （見 <see cref="AdminSeoSettingsDto"/> 檔頭說明）。<paramref name="ogImageUpdate"/> 的三態
    /// 語意見 <see cref="ImageFieldUpdate"/>，套用對象是 <see cref="Club.OgImageKey"/>。
    /// <c>TitleTemplateZh</c>／<c>DefaultDescriptionZh</c> 為必填（CLAUDE.md 全域規定 4：中文必填、
    /// 英文可空但欄位存在），其餘欄位（含追蹤碼、robots.txt 自訂規則、OG 圖片）皆可為空。</summary>
    public async Task<AdminSeoSettingsDto> UpdateAsync(
        AdminClubScope scope, UpdateSeoSettingsRequest request, ImageFieldUpdate ogImageUpdate,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TitleTemplateZh))
        {
            throw new AdminSeoValidationException("標題樣板（中文）為必填欄位。", "titleTemplateZh");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultDescriptionZh))
        {
            throw new AdminSeoValidationException("預設描述（中文）為必填欄位。", "defaultDescriptionZh");
        }

        // 追蹤碼前台會直接拼進 <script>，只收各平台正式格式（稽核 E-1）；空白＝清除。
        var ga4 = ValidateTrackingId(request.Ga4MeasurementId, Ga4Pattern(), "GA4 評估 ID", "G-XXXXXXXXXX", "ga4MeasurementId", upper: true);
        var gtm = ValidateTrackingId(request.GtmContainerId, GtmPattern(), "Google Tag Manager 容器 ID", "GTM-XXXXXXX", "gtmContainerId", upper: true);
        var metaPixel = ValidateTrackingId(request.MetaPixelId, MetaPixelPattern(), "Meta Pixel ID", "純數字（約 15–16 碼）", "metaPixelId", upper: false);
        var lineTag = ValidateTrackingId(request.LineTagId, LineTagPattern(), "LINE Tag ID", "英文字母、數字與連字號", "lineTagId", upper: false);

        var settings = await dbContext.Settings
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        UpsertI18n(settings, KeyTitleTemplate, GroupSeo, scope.ClubId, request.TitleTemplateZh, request.TitleTemplateEn, operatorId);
        UpsertI18n(settings, KeyDefaultDescription, GroupSeo, scope.ClubId, request.DefaultDescriptionZh, request.DefaultDescriptionEn, operatorId);
        UpsertValue(settings, KeyRobotsCustomRules, GroupSeo, scope.ClubId, request.RobotsCustomRules, operatorId);
        UpsertValue(settings, KeyGa4, GroupTracking, scope.ClubId, ga4, operatorId);
        UpsertValue(settings, KeyGtm, GroupTracking, scope.ClubId, gtm, operatorId);
        UpsertValue(settings, KeyMetaPixel, GroupTracking, scope.ClubId, metaPixel, operatorId);
        UpsertValue(settings, KeyLineTag, GroupTracking, scope.ClubId, lineTag, operatorId);

        var ogAltZh = AdminInput.OptionalText(request.OgImageAltZh, "OG 圖片替代文字（中文）", 200, "ogImageAltZh");
        var ogAltEn = AdminInput.OptionalText(request.OgImageAltEn, "OG 圖片替代文字（英文）", 200, "ogImageAltEn");

        var clubForOg = await dbContext.Clubs.Include(c => c.ClubsI18ns).FirstOrDefaultAsync(c => c.Id == scope.ClubId, cancellationToken)
            ?? throw new AdminSeoValidationException("找不到俱樂部主檔，無法更新全站預設 OG 圖片。");

        if (ogImageUpdate.Change)
        {
            clubForOg.OgImageKey = ogImageUpdate.Key;
            clubForOg.OgImageWidth = ogImageUpdate.Key is null ? null : ogImageUpdate.Width;
            clubForOg.OgImageHeight = ogImageUpdate.Key is null ? null : ogImageUpdate.Height;
            clubForOg.UpdatedAt = DateTime.UtcNow;
            clubForOg.UpdatedBy = operatorId;
        }

        // Alt 在 clubs_i18n.og_image_alt；該語系的俱樂部名稱列（name NOT NULL）不存在時無法另建，只能擋下有值的請求。
        // 文字欄位一律整份取代（與本請求其他欄位一致）：沒送＝清空。
        foreach (var (locale, alt) in new[] { (RequestLocale.DefaultDbLocale, ogAltZh), ("en", ogAltEn) })
        {
            var row = clubForOg.ClubsI18ns.FirstOrDefault(i => i.Locale == locale);
            if (row is null)
            {
                if (alt is not null)
                {
                    throw new AdminSeoValidationException("尚未建立這個語系的俱樂部名稱，無法儲存 OG 圖片替代文字。", locale == "en" ? "ogImageAltEn" : "ogImageAltZh");
                }

                continue;
            }

            row.OgImageAlt = alt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(SeoRepository.SettingsEntity, scope.ClubCode, cancellationToken);
        // 俱樂部公開端點（Clubs）也輸出 OG 圖片網址、寬高與 Alt，一併失效。
        await cache.InvalidateAsync(Features.Clubs.ClubsRepository.DetailEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync(Features.Clubs.ClubsRepository.ListEntity, Caching.CacheDimensions.SharedClub, cancellationToken);

        return await GetAsync(scope, cancellationToken);
    }

    [GeneratedRegex(@"^G-[A-Z0-9]{4,20}$")]
    private static partial Regex Ga4Pattern();

    [GeneratedRegex(@"^GTM-[A-Z0-9]{4,12}$")]
    private static partial Regex GtmPattern();

    [GeneratedRegex(@"^[0-9]{5,20}$")]
    private static partial Regex MetaPixelPattern();

    // LINE Tag 的 ID 是 UUID 樣式（含連字號的英數字串），官方沒有公布更嚴格的格式，保守只收英數與連字號。
    [GeneratedRegex(@"^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex LineTagPattern();

    private static string? ValidateTrackingId(string? raw, Regex pattern, string label, string example, string field, bool upper)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (upper)
        {
            text = text.ToUpperInvariant();
        }

        if (!pattern.IsMatch(text))
        {
            throw new AdminSeoValidationException($"{label}格式不正確（範例：{example}）。", field);
        }

        return text;
    }

    /// <summary>呼叫端（<see cref="AdminSeoSettingsEndpoints"/>）需要先知道目前的 OG 圖片鍵，
    /// 才能在換圖成功後刪除舊物件（規劃書 §4.0「換圖與刪除」）。</summary>
    public async Task<string?> GetCurrentOgImageKeyAsync(Guid clubId, CancellationToken cancellationToken)
        => (await dbContext.Clubs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clubId, cancellationToken))?.OgImageKey;

    private AdminSeoSettingsDto BuildDto(IReadOnlyCollection<Setting> settings, Club? club)
    {
        string? Value(string key) => settings.FirstOrDefault(s => s.SettingKey == key)?.SettingValue;

        string? I18n(string key, string locale) => settings
            .FirstOrDefault(s => s.SettingKey == key)?.SettingsI18ns
            .FirstOrDefault(i => i.Locale == locale)?.Value;

        return new AdminSeoSettingsDto
        {
            TitleTemplateZh = I18n(KeyTitleTemplate, RequestLocale.DefaultDbLocale),
            TitleTemplateEn = I18n(KeyTitleTemplate, "en"),
            DefaultDescriptionZh = I18n(KeyDefaultDescription, RequestLocale.DefaultDbLocale),
            DefaultDescriptionEn = I18n(KeyDefaultDescription, "en"),
            RobotsCustomRules = Value(KeyRobotsCustomRules),
            Ga4MeasurementId = Value(KeyGa4),
            GtmContainerId = Value(KeyGtm),
            MetaPixelId = Value(KeyMetaPixel),
            LineTagId = Value(KeyLineTag),
            OgImageUrl = imageUrlResolver.Resolve(club?.OgImageKey),
            OgImageWidth = club?.OgImageWidth,
            OgImageHeight = club?.OgImageHeight,
            OgImageAltZh = club?.ClubsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.OgImageAlt,
            OgImageAltEn = club?.ClubsI18ns.FirstOrDefault(i => i.Locale == "en")?.OgImageAlt,
        };
    }

    private Setting GetOrCreate(Dictionary<string, Setting> settings, string key, string group, Guid clubId, Guid? operatorId)
    {
        if (settings.TryGetValue(key, out var existing))
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = operatorId;
            return existing;
        }

        var now = DateTime.UtcNow;
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            SettingKey = key,
            SettingGroup = group,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Settings.Add(setting);
        settings[key] = setting;
        return setting;
    }

    /// <summary>🔴 只有「這個鍵已經有列」或「這次要寫入非空白值」才會經過 <see cref="GetOrCreate"/>
    /// ——追蹤碼與 robots.txt 自訂規則都是選填欄位（不是每個俱樂部都申請了 GA4／GTM），若一律呼叫
    /// <see cref="GetOrCreate"/>，每次 PUT（即使呼叫端只是要改標題樣板）都會把全部七個
    /// <c>seo.*</c>／<c>tracking.*</c> 鍵各建一列空殼（<c>setting_value = NULL</c>），
    /// <c>settings</c> 表會塞滿從未真正被設定過的空列（S1-12 驗收退回後補做時由
    /// <c>Tcrfc.Api.Tests.AdminSeoImageTests</c> 的清理殘留實測發現，見該測試檔的說明）。</summary>
    private void UpsertValue(Dictionary<string, Setting> settings, string key, string group, Guid clubId, string? value, Guid? operatorId)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value);
        if (!hasValue && !settings.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrCreate(settings, key, group, clubId, operatorId);
        setting.SettingValue = hasValue ? value : null;
    }

    /// <summary>比照 <c>AdminArticlesRepository.AddOrReplaceI18n</c> 同一套「zh 必寫、en 省略即刪除
    /// 既有列」的寫法——這裡的呼叫端已經先驗證過 <paramref name="zhValue"/> 非空白，不會寫入空的
    /// 中文列。</summary>
    private void UpsertI18n(Dictionary<string, Setting> settings, string key, string group, Guid clubId, string zhValue, string? enValue, Guid? operatorId)
    {
        var setting = GetOrCreate(settings, key, group, clubId, operatorId);

        var zh = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        if (zh is null)
        {
            zh = new SettingsI18n { SettingId = setting.Id, Locale = RequestLocale.DefaultDbLocale };
            setting.SettingsI18ns.Add(zh);
            dbContext.SettingsI18ns.Add(zh);
        }

        zh.Value = zhValue;

        var en = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (!string.IsNullOrWhiteSpace(enValue))
        {
            if (en is null)
            {
                en = new SettingsI18n { SettingId = setting.Id, Locale = "en" };
                setting.SettingsI18ns.Add(en);
                dbContext.SettingsI18ns.Add(en);
            }

            en.Value = enValue;
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
            setting.SettingsI18ns.Remove(en);
        }
    }
}
