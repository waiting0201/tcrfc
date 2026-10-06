using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>
/// I3 全域設定（規劃書 §4.9：Logo、品牌色、Favicon、Cookie 政策、隱私權政策、會員條款、維護模式開關）。
///
/// - <b>Logo／Favicon／品牌色</b>寫在 <c>clubs</c> 該俱樂部那一列（與 J4 俱樂部管理同一組欄位；J4 的圖片欄位維持唯讀，上傳入口在這裡）。
///   圖片走共用上傳元件（伺服器端重新編碼為 WebP、去 EXIF、產衍生檔，規劃書 §4.0）。
/// - <b>政策頁</b>內文存 <c>settings_i18n</c>（鍵見 <see cref="SiteSettingKeys"/>），<b>純文字</b>、每份繁中／英文各 ≤ <see cref="MaxPolicyLength"/> 字；
///   沒有 HTML 清理器，所以不收 HTML——前台必須以文字節點輸出。
/// - <b>維護模式</b>：<c>maintenance.enabled</c>（<c>"1"</c>）＋逐語系訊息。切換會寫敏感操作日誌（整站下線等級的動作）。
///   🔴 後端只提供旗標與訊息，<b>不會</b>自動攔截其他公開端點（會員登入、後台等仍可用）；維護頁（G-10）由前台依 <c>GET /site-settings</c> 的
///   <c>maintenance.enabled</c> 顯示。
/// 儲存後立即失效前台快取（<see cref="SiteSettingsRepository.CacheEntities"/>），維護模式不得延後生效。
/// </summary>
public sealed partial class AdminGlobalSettingsRepository(
    ClubDbContext db, ClubSettingsEditor settingsEditor, IQueryCache cache, IImagePublicUrlResolver imageUrls, SensitiveActionLogger audit)
{
    public const int MaxPolicyLength = 50_000;
    public const int MaxMaintenanceMessageLength = 500;

    private static readonly string[] AllKeys =
    [
        SiteSettingKeys.PolicyCookie, SiteSettingKeys.PolicyPrivacy, SiteSettingKeys.PolicyMemberTerms,
        SiteSettingKeys.MaintenanceEnabled, SiteSettingKeys.MaintenanceMessage,
    ];

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    public async Task<AdminGlobalSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == scope.ClubId, cancellationToken);
        var settings = await settingsEditor.LoadAsync(scope.ClubId, AllKeys, cancellationToken);
        return Build(club, settings);
    }

    /// <summary>目前三個圖片欄位的物件鍵——端點在換圖成功後用來清除舊物件。</summary>
    public async Task<(string? LogoLight, string? LogoDark, string? Favicon)> GetCurrentImageKeysAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == clubId, cancellationToken);
        return (club.LogoLightKey, club.LogoDarkKey, club.FaviconKey);
    }

    public async Task<AdminGlobalSettingsDto> UpdateAsync(
        AdminClubScope scope, UpdateAdminGlobalSettingsRequest request,
        ImageFieldUpdate logoLight, ImageFieldUpdate logoDark, ImageFieldUpdate favicon,
        OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var brandColor = NormalizeColor(request.BrandColor, "品牌主色", "brandColor");
        var brandSecondary = NormalizeColor(request.BrandSecondaryColor, "品牌輔色", "brandSecondaryColor");
        var cookie = ValidatePolicy(request.CookiePolicy, "Cookie 政策", "cookie");
        var privacy = ValidatePolicy(request.PrivacyPolicy, "隱私權政策", "privacy");
        var terms = ValidatePolicy(request.MemberTerms, "會員條款", "terms");
        var messageZh = LimitText(request.MaintenanceMessageZh, "維護頁訊息（繁中）", MaxMaintenanceMessageLength, "maintenanceMessageZh");
        var messageEn = LimitText(request.MaintenanceMessageEn, "維護頁訊息（英文）", MaxMaintenanceMessageLength, "maintenanceMessageEn");

        var club = await db.Clubs.FirstAsync(c => c.Id == scope.ClubId, cancellationToken);
        var settings = await settingsEditor.LoadAsync(scope.ClubId, AllKeys, cancellationToken);
        var wasMaintenance = ClubSettingsEditor.Value(settings, SiteSettingKeys.MaintenanceEnabled) == "1";

        var now = DateTime.UtcNow;
        club.BrandColor = brandColor;
        club.BrandSecondaryColor = brandSecondary;
        if (logoLight.Change)
        {
            orphans.Image(club.LogoLightKey);
            club.LogoLightKey = logoLight.Key;
        }

        if (logoDark.Change)
        {
            orphans.Image(club.LogoDarkKey);
            club.LogoDarkKey = logoDark.Key;
        }

        if (favicon.Change)
        {
            orphans.Image(club.FaviconKey);
            club.FaviconKey = favicon.Key;
        }

        club.UpdatedAt = now;
        club.UpdatedBy = operatorId;

        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.PolicyCookie, SiteSettingKeys.GroupPolicy, cookie.Zh, cookie.En, operatorId);
        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.PolicyPrivacy, SiteSettingKeys.GroupPolicy, privacy.Zh, privacy.En, operatorId);
        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.PolicyMemberTerms, SiteSettingKeys.GroupPolicy, terms.Zh, terms.En, operatorId);
        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.MaintenanceEnabled, SiteSettingKeys.GroupMaintenance,
            request.MaintenanceEnabled ? "1" : "0", operatorId);
        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.MaintenanceMessage, SiteSettingKeys.GroupMaintenance, messageZh, messageEn, operatorId);

        await db.SaveChangesAsync(cancellationToken);

        if (wasMaintenance != request.MaintenanceEnabled)
        {
            audit.Record(scope, request.MaintenanceEnabled ? "開啟維護模式" : "關閉維護模式", "全域設定", 1, "維護模式切換");
        }

        await InvalidateAsync(scope.ClubCode, cancellationToken);
        return Build(club, settings);
    }

    private async Task InvalidateAsync(string clubCode, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.Settings, clubCode, cancellationToken);
        await cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.Policies, clubCode, cancellationToken);
        // 公開俱樂部清單／詳情也帶 Logo 與品牌色（Features/Clubs）；它們依俱樂部分區或共用，兩個維度都清。
        foreach (var entity in new[] { "clubs-list", "club-detail" })
        {
            await cache.InvalidateAsync(entity, CacheDimensions.SharedClub, cancellationToken);
            await cache.InvalidateAsync(entity, clubCode, cancellationToken);
        }
    }

    private AdminGlobalSettingsDto Build(Data.EfEntities.Club club, IReadOnlyDictionary<string, Data.EfEntities.Setting> settings)
        => new()
        {
            Brand = new AdminBrandSettingsDto
            {
                LogoLightUrl = imageUrls.Resolve(club.LogoLightKey),
                LogoDarkUrl = imageUrls.Resolve(club.LogoDarkKey),
                FaviconUrl = imageUrls.Resolve(club.FaviconKey),
                BrandColor = club.BrandColor,
                BrandSecondaryColor = club.BrandSecondaryColor,
            },
            Policies = SiteSettingKeys.Policies.Select(p => new AdminPolicySettingDto
            {
                Code = p.Code,
                Title = p.TitleZh,
                BodyZh = ClubSettingsEditor.I18n(settings, p.SettingKey, RequestLocale.DefaultDbLocale),
                BodyEn = ClubSettingsEditor.I18n(settings, p.SettingKey, "en"),
                UpdatedAt = ClubSettingsEditor.UpdatedAt(settings, p.SettingKey),
            }).ToList(),
            Maintenance = new AdminMaintenanceSettingsDto
            {
                Enabled = ClubSettingsEditor.Value(settings, SiteSettingKeys.MaintenanceEnabled) == "1",
                MessageZh = ClubSettingsEditor.I18n(settings, SiteSettingKeys.MaintenanceMessage, RequestLocale.DefaultDbLocale),
                MessageEn = ClubSettingsEditor.I18n(settings, SiteSettingKeys.MaintenanceMessage, "en"),
                UpdatedAt = ClubSettingsEditor.UpdatedAt(settings, SiteSettingKeys.MaintenanceEnabled),
            },
        };

    private static string? NormalizeColor(string? value, string label, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return HexColor().IsMatch(text) ? text.ToUpperInvariant() : throw new AdminValidationException($"{label}必須是 #RRGGBB 格式的色碼（例如 #1A5F3A）。", field);
    }

    private static (string? Zh, string? En) ValidatePolicy(AdminPolicyInput? input, string label, string fieldPrefix)
        => (CleanPolicyText(input?.BodyZh, $"{label}（繁中）", fieldPrefix + "Zh"), CleanPolicyText(input?.BodyEn, $"{label}（英文）", fieldPrefix + "En"));

    private static string? CleanPolicyText(string? value, string label, string field)
    {
        var text = value?.Replace("\0", string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > MaxPolicyLength ? throw new AdminValidationException($"{label}不可超過 {MaxPolicyLength} 個字。", field) : text;
    }

    private static string? LimitText(string? value, string label, int max, string field) => AdminInput.OptionalText(value, label, max, field);
}
