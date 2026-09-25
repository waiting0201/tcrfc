using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// `GEO-01` <c>llms.txt</c> 內容維護（S1-12a）的讀寫。**不新增資料表**，沿用既有
/// <c>settings</c>／<c>settings_i18n</c>（跟 <see cref="AdminSeoSettingsRepository"/> 同一個
/// <c>setting_group='geo'</c> 底下的一組鍵，五個區塊各自逐語系）：
/// <c>geo.llms_positioning</c>／<c>geo.llms_key_pages</c>／<c>geo.llms_facts_summary</c>／
/// <c>geo.llms_license</c>／<c>geo.llms_contact</c>。
///
/// 🔴 **「隨發布重產」的落實方式**：這裡沒有「發布」這個額外動作——公開端點
/// （<see cref="Seo.SeoRepository.GetLlmsContentAsync"/>）每次都直接查（或依 TTL 兜底）目前的
/// <c>settings</c> 內容，<c>apps/web</c> 的 <c>/llms.txt</c>／<c>/llms-en.txt</c> 也是每個請求都
/// 重新組字串、不是建置期產生的靜態檔案。管理員在這裡按下「儲存」，就是規劃書講的「隨發布重產」
/// ——不需要另外觸發一次建置或部署，`GEO-01`「不以人工改檔」的意思在這個實作下自動成立（沒有檔案
/// 可以讓人工去改，內容只存在資料庫裡）。跟既有 <c>seo.setting.*</c> 一樣，公開端點若接上
/// <c>IQueryCache</c>，最多延後一個 TTL（預設 300 秒）才會反映，理由與既定行為同
/// <see cref="Seo.SeoRepository"/> 檔頭「寫入端刻意不呼叫 InvalidateAsync」的既有說明。
/// </summary>
public sealed class AdminGeoLlmsRepository(ClubDbContext dbContext)
{
    private const string KeyPositioning = "geo.llms_positioning";
    private const string KeyKeyPages = "geo.llms_key_pages";
    private const string KeyFactsSummary = "geo.llms_facts_summary";
    private const string KeyLicense = "geo.llms_license";
    private const string KeyContact = "geo.llms_contact";

    private const string Group = "geo";

    private static readonly string[] AllKeys = [KeyPositioning, KeyKeyPages, KeyFactsSummary, KeyLicense, KeyContact];

    public async Task<AdminLlmsContentDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking()
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToListAsync(cancellationToken);

        return BuildDto(settings);
    }

    /// <summary>整份取代語意（同 <see cref="AdminSeoSettingsRepository"/> 檔頭說明），
    /// **五個區塊皆可為空**——理由見 <see cref="AdminLlmsContentDto"/> 檔頭。</summary>
    public async Task<AdminLlmsContentDto> UpdateAsync(
        AdminClubScope scope, UpdateLlmsContentRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        UpsertOptionalI18n(settings, KeyPositioning, scope.ClubId, request.PositioningZh, request.PositioningEn, operatorId);
        UpsertOptionalI18n(settings, KeyKeyPages, scope.ClubId, request.KeyPagesZh, request.KeyPagesEn, operatorId);
        UpsertOptionalI18n(settings, KeyFactsSummary, scope.ClubId, request.FactsSummaryZh, request.FactsSummaryEn, operatorId);
        UpsertOptionalI18n(settings, KeyLicense, scope.ClubId, request.LicenseZh, request.LicenseEn, operatorId);
        UpsertOptionalI18n(settings, KeyContact, scope.ClubId, request.ContactZh, request.ContactEn, operatorId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(scope, cancellationToken);
    }

    private static AdminLlmsContentDto BuildDto(IReadOnlyCollection<Setting> settings)
    {
        string? I18n(string key, string locale) => settings
            .FirstOrDefault(s => s.SettingKey == key)?.SettingsI18ns
            .FirstOrDefault(i => i.Locale == locale)?.Value;

        return new AdminLlmsContentDto
        {
            PositioningZh = I18n(KeyPositioning, RequestLocale.DefaultDbLocale),
            PositioningEn = I18n(KeyPositioning, "en"),
            KeyPagesZh = I18n(KeyKeyPages, RequestLocale.DefaultDbLocale),
            KeyPagesEn = I18n(KeyKeyPages, "en"),
            FactsSummaryZh = I18n(KeyFactsSummary, RequestLocale.DefaultDbLocale),
            FactsSummaryEn = I18n(KeyFactsSummary, "en"),
            LicenseZh = I18n(KeyLicense, RequestLocale.DefaultDbLocale),
            LicenseEn = I18n(KeyLicense, "en"),
            ContactZh = I18n(KeyContact, RequestLocale.DefaultDbLocale),
            ContactEn = I18n(KeyContact, "en"),
        };
    }

    private Setting GetOrCreate(Dictionary<string, Setting> settings, string key, Guid clubId, Guid? operatorId)
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
            SettingGroup = Group,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Settings.Add(setting);
        settings[key] = setting;
        return setting;
    }

    /// <summary>🔴 兩個語系都空白、且這個鍵目前完全沒有列時，不建立空殼列——同一個教訓見
    /// <see cref="AdminSeoSettingsRepository.UpsertValue"/> 檔頭（S1-12 驗收退回後補做時實測發現
    /// 的既有問題）。這裡比那邊多一層：**中文也可以是空白**（跟必填的標題樣板不同），所以連
    /// <c>zh</c> 這個語系列本身都可能不存在，不是「zh 一定寫、en 選填」的既有慣例。</summary>
    private void UpsertOptionalI18n(
        Dictionary<string, Setting> settings, string key, Guid clubId, string? zhValue, string? enValue, Guid? operatorId)
    {
        var hasZh = !string.IsNullOrWhiteSpace(zhValue);
        var hasEn = !string.IsNullOrWhiteSpace(enValue);

        if (!hasZh && !hasEn && !settings.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrCreate(settings, key, clubId, operatorId);

        SetLocaleValue(setting, RequestLocale.DefaultDbLocale, zhValue);
        SetLocaleValue(setting, "en", enValue);
    }

    private void SetLocaleValue(Setting setting, string locale, string? value)
    {
        var existing = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (existing is null)
            {
                existing = new SettingsI18n { SettingId = setting.Id, Locale = locale };
                setting.SettingsI18ns.Add(existing);
                dbContext.SettingsI18ns.Add(existing);
            }

            existing.Value = value;
        }
        else if (existing is not null)
        {
            dbContext.Remove(existing);
            setting.SettingsI18ns.Remove(existing);
        }
    }
}
