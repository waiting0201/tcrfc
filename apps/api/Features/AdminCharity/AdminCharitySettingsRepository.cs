using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCharity;

/// <summary>
/// B5「捐款導流設定」與「參與方式設定」（規劃書 §4.2 B5、§3.11「球迷捐款：導向慈善捐款平台」、CH-6）。
/// 沿用既有 <c>settings</c>／<c>settings_i18n</c>（設定群組 <c>charity</c>，鍵命名慣例同 <c>Features/AdminSiteFacts</c>），
/// 不新增資料表。每個俱樂部各一份（<c>club_id</c> 必填）。
/// 鍵：<c>charity.donation_url</c>（單值）、<c>charity.donation_cta</c>（逐語系）、<c>charity.corporate_cta</c>（逐語系）、
/// <c>charity.corporate_url</c>（單值）、<c>charity.fan_cta</c>（逐語系）。
/// 🔴 規劃書 §3.11：慈善捐款平台主辦與收款主體是「台灣足球策略發展協會」，不是俱樂部——設定了捐款平台網址時，
/// 中文導流文案**必須點明**協會名稱，否則 400（防止使用者誤以為捐款給台中磐石）。
/// </summary>
public sealed class AdminCharitySettingsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    internal const string Group = "charity";
    internal const string KeyDonationUrl = "charity.donation_url";
    internal const string KeyDonationCta = "charity.donation_cta";
    internal const string KeyCorporateCta = "charity.corporate_cta";
    internal const string KeyCorporateUrl = "charity.corporate_url";
    internal const string KeyFanCta = "charity.fan_cta";
    internal static readonly string[] AllKeys = [KeyDonationUrl, KeyDonationCta, KeyCorporateCta, KeyCorporateUrl, KeyFanCta];

    /// <summary>導流文案必須出現的收受者名稱（規劃書 §3.11 明文）。</summary>
    internal const string RequiredReceiverName = "台灣足球策略發展協會";

    public async Task<AdminCharitySettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey)).ToListAsync(cancellationToken);
        return Build(settings);
    }

    public async Task<AdminCharitySettingsDto> UpdateAsync(
        AdminClubScope scope, AdminCharitySettingsDto request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var donationUrl = AdminInput.OptionalText(request.DonationUrl, "捐款平台網址", 500);
        if (donationUrl is not null
            && (!Uri.TryCreate(donationUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AdminValidationException("捐款平台網址必須是以 https:// 開頭的完整網址。");
        }

        var donationCtaZh = AdminInput.OptionalText(request.DonationCta?.Zh, "捐款按鈕文案", 200);
        if (donationUrl is not null)
        {
            if (donationCtaZh is null)
            {
                throw new AdminValidationException("設定捐款平台網址時，必須同時填寫捐款按鈕的中文文案。");
            }

            if (!donationCtaZh.Contains(RequiredReceiverName, StringComparison.Ordinal))
            {
                throw new AdminValidationException($"捐款按鈕的中文文案必須說明捐款由「{RequiredReceiverName}」收受，避免讓人誤以為是捐款給台中磐石。");
            }
        }

        var corporateUrl = AdminInput.OptionalText(request.CorporateUrl, "企業合作連結", 500);
        if (corporateUrl is not null && !IsSiteRelativePath(corporateUrl)
            && !(Uri.TryCreate(corporateUrl, UriKind.Absolute, out var cUri) && cUri.Scheme == Uri.UriSchemeHttps))
        {
            throw new AdminValidationException("企業合作連結必須是站內路徑（以 / 開頭）或 https:// 開頭的完整網址。");
        }

        var settings = await dbContext.Settings.Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey)).ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        UpsertValue(settings, KeyDonationUrl, scope.ClubId, donationUrl, operatorId);
        UpsertValue(settings, KeyCorporateUrl, scope.ClubId, corporateUrl, operatorId);
        UpsertI18n(settings, KeyDonationCta, scope.ClubId, donationCtaZh, AdminInput.OptionalText(request.DonationCta?.En, "捐款按鈕英文文案", 200), operatorId);
        UpsertI18n(settings, KeyCorporateCta, scope.ClubId,
            AdminInput.OptionalText(request.CorporateCta?.Zh, "企業合作按鈕文案", 200), AdminInput.OptionalText(request.CorporateCta?.En, "企業合作按鈕英文文案", 200), operatorId);
        UpsertI18n(settings, KeyFanCta, scope.ClubId,
            AdminInput.OptionalText(request.FanCta?.Zh, "球迷捐款按鈕文案", 200), AdminInput.OptionalText(request.FanCta?.En, "球迷捐款按鈕英文文案", 200), operatorId);

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return await GetAsync(scope, cancellationToken);
    }

    internal static bool IsSiteRelativePath(string value) => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);

    internal static AdminCharitySettingsDto Build(IReadOnlyCollection<Setting> settings)
    {
        Setting? Find(string key) => settings.FirstOrDefault(s => s.SettingKey == key);
        AdminCharityBilingualTextDto? Bilingual(string key)
        {
            var s = Find(key);
            if (s is null)
            {
                return null;
            }

            var zh = s.SettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Value;
            var en = s.SettingsI18ns.FirstOrDefault(i => i.Locale == "en")?.Value;
            return zh is null && en is null ? null : new AdminCharityBilingualTextDto { Zh = zh, En = en };
        }

        return new AdminCharitySettingsDto
        {
            DonationUrl = Find(KeyDonationUrl)?.SettingValue,
            DonationCta = Bilingual(KeyDonationCta),
            CorporateCta = Bilingual(KeyCorporateCta),
            CorporateUrl = Find(KeyCorporateUrl)?.SettingValue,
            FanCta = Bilingual(KeyFanCta),
            UpdatedAt = settings.Count == 0 ? null : settings.Max(s => s.UpdatedAt),
        };
    }

    private Setting GetOrAdd(Dictionary<string, Setting> settings, string key, Guid clubId, Guid? operatorId)
    {
        if (!settings.TryGetValue(key, out var setting))
        {
            var now = DateTime.UtcNow;
            setting = new Setting
            {
                Id = Guid.NewGuid(), ClubId = clubId, SettingKey = key, SettingGroup = Group, CreatedAt = now, UpdatedAt = now,
                CreatedBy = operatorId, UpdatedBy = operatorId,
            };
            dbContext.Settings.Add(setting);
            settings[key] = setting;
        }
        else
        {
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = operatorId;
        }

        return setting;
    }

    private void UpsertValue(Dictionary<string, Setting> settings, string key, Guid clubId, string? value, Guid? operatorId)
    {
        if (value is null && !settings.ContainsKey(key))
        {
            return;
        }

        GetOrAdd(settings, key, clubId, operatorId).SettingValue = value;
    }

    private void UpsertI18n(Dictionary<string, Setting> settings, string key, Guid clubId, string? zh, string? en, Guid? operatorId)
    {
        if (zh is null && en is null && !settings.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrAdd(settings, key, clubId, operatorId);
        SetLocale(setting, RequestLocale.DefaultDbLocale, zh);
        SetLocale(setting, "en", en);
    }

    private void SetLocale(Setting setting, string locale, string? value)
    {
        var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (value is null)
        {
            if (row is not null)
            {
                dbContext.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new SettingsI18n { SettingId = setting.Id, Locale = locale };
            setting.SettingsI18ns.Add(row);
            dbContext.SettingsI18ns.Add(row);
        }

        row.Value = value;
    }
}
