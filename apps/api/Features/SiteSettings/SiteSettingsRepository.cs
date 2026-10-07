using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.SiteSettings;

/// <summary>
/// I 網站設定的公開讀取（規劃書 §4.9）：站台全域設定（品牌／維護模式／語系與格式）、政策頁、介面字串、場地。
/// 寫入端在 <c>Features/AdminSiteSettings</c>、<c>Features/AdminVenues</c>，<b>儲存後都會呼叫 <see cref="IQueryCache.InvalidateAsync"/></b>
/// 讓維護模式等設定立即生效（不同於 <c>SiteFactsRepository</c> 的「最多延後一個 TTL」取捨——維護頁開關延後五分鐘是不可接受的）。
/// 快取鍵常數集中在 <see cref="CacheEntities"/>，寫入端引用同一份。
/// </summary>
public sealed partial class SiteSettingsRepository(ClubDbContext db, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public static class CacheEntities
    {
        public const string Settings = "site-settings-global";
        public const string Policies = "site-policies";
        public const string UiStrings = "ui-strings";
    }

    private static readonly string[] SettingKeys =
    [
        SiteSettingKeys.MaintenanceEnabled, SiteSettingKeys.MaintenanceMessage,
        SiteSettingKeys.I18nFallbackMode, SiteSettingKeys.I18nDateFormat, SiteSettingKeys.I18nNumberFormat,
        SiteSettingKeys.PolicyCookie, SiteSettingKeys.PolicyPrivacy, SiteSettingKeys.PolicyMemberTerms,
    ];

    [GeneratedRegex(@"^1(?<th>[,. ]?)234(?<dec>[.,])56$")]
    private static partial Regex NumberSample();

    // ── 站台全域設定 ──────────────────────────────────────────────────────────────
    public async Task<PublicSiteSettingsDto> GetSiteSettingsAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(CacheEntities.Settings, scope.ClubCode, dbLocale, CacheDimensions.NoQualifier, async ct =>
        {
            var settings = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
                .Where(s => s.ClubId == scope.ClubId && SettingKeys.Contains(s.SettingKey))
                .ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, ct);
            var locales = await db.Locales.AsNoTracking().Where(l => l.IsEnabled).OrderBy(l => l.SortOrder).ThenBy(l => l.Code).ToListAsync(ct);

            string? Resolve(string key)
                => settings.TryGetValue(key, out var s)
                    ? RequestLocale.Pick(
                        s.SettingsI18ns.FirstOrDefault(i => i.Locale == dbLocale)?.Value,
                        s.SettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Value)
                    : null;

            var numberFormat = Resolve(SiteSettingKeys.I18nNumberFormat);
            var (thousands, decimalSep) = ParseNumberFormat(numberFormat);
            var fallback = settings.TryGetValue(SiteSettingKeys.I18nFallbackMode, out var fb) && fb.SettingValue == SiteSettingKeys.FallbackHide
                ? SiteSettingKeys.FallbackHide
                : SiteSettingKeys.FallbackShowDefault;

            return new PublicSiteSettingsDto
            {
                Maintenance = new PublicMaintenanceDto
                {
                    Enabled = settings.TryGetValue(SiteSettingKeys.MaintenanceEnabled, out var m) && m.SettingValue == "1",
                    Message = Resolve(SiteSettingKeys.MaintenanceMessage),
                },
                Languages = locales.Select(l => new PublicLanguageDto
                {
                    Code = l.Code, Name = l.Name, IsDefault = l.IsDefault, FallbackCode = l.FallbackCode,
                }).ToList(),
                FallbackMode = fallback,
                Formats = new PublicFormatsDto
                {
                    DateFormat = Resolve(SiteSettingKeys.I18nDateFormat),
                    NumberFormat = numberFormat,
                    ThousandsSeparator = thousands,
                    DecimalSeparator = decimalSep,
                },
                Policies = SiteSettingKeys.Policies.Select(p => new PublicPolicyIndexDto
                {
                    Code = p.Code,
                    Title = dbLocale == "en" ? p.TitleEn : p.TitleZh,
                    HasContent = settings.TryGetValue(p.SettingKey, out var s) && s.SettingsI18ns.Any(i => !string.IsNullOrWhiteSpace(i.Value)),
                }).ToList(),
            };
        }, cancellationToken);

    /// <summary>由範例字串（<c>1,234.56</c>）拆出千分位與小數點符號；格式不符回 (null, null)，前台用語系預設。</summary>
    public static (string? Thousands, string? Decimal) ParseNumberFormat(string? sample)
    {
        if (string.IsNullOrWhiteSpace(sample))
        {
            return (null, null);
        }

        var match = NumberSample().Match(sample.Trim());
        return match.Success ? (match.Groups["th"].Value, match.Groups["dec"].Value) : (null, null);
    }

    // ── 政策頁 ───────────────────────────────────────────────────────────────────
    public async Task<PublicPolicyDto?> GetPolicyAsync(ClubScope scope, string code, string dbLocale, CancellationToken cancellationToken)
    {
        var definition = SiteSettingKeys.Policies.FirstOrDefault(p => p.Code == code);
        if (definition is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(CacheEntities.Policies, scope.ClubCode, dbLocale, code, async ct =>
        {
            var setting = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
                .FirstOrDefaultAsync(s => s.ClubId == scope.ClubId && s.SettingKey == definition.SettingKey, ct);
            var requested = setting?.SettingsI18ns.FirstOrDefault(i => i.Locale == dbLocale)?.Value;
            var fallback = setting?.SettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Value;
            var body = RequestLocale.Pick(requested, fallback);
            if (setting is null || body is null)
            {
                return null;
            }

            return new PublicPolicyDto
            {
                Code = code,
                Title = dbLocale == "en" ? definition.TitleEn : definition.TitleZh,
                Body = body,
                UpdatedAt = setting.UpdatedAt,
                IsFallbackLocale = dbLocale != RequestLocale.DefaultDbLocale && string.IsNullOrWhiteSpace(requested),
            };
        }, cancellationToken);
    }

    // ── 介面字串 ─────────────────────────────────────────────────────────────────
    public async Task<PublicUiStringsDto> GetUiStringsAsync(string dbLocale, string? group, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(CacheEntities.UiStrings, CacheDimensions.SharedClub, dbLocale, group ?? CacheDimensions.NoQualifier, async ct =>
        {
            var query = db.UiStrings.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(group))
            {
                query = query.Where(s => s.StringGroup == group);
            }

            var rows = await query.Select(s => new
            {
                s.StringKey,
                Requested = s.UiStringTranslations.Where(t => t.Locale == dbLocale).Select(t => t.Value).FirstOrDefault(),
                Default = s.UiStringTranslations.Where(t => t.Locale == RequestLocale.DefaultDbLocale).Select(t => t.Value).FirstOrDefault(),
            }).ToListAsync(ct);

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                var value = RequestLocale.Pick(r.Requested, r.Default);
                if (value is not null)
                {
                    map[r.StringKey] = value;
                }
            }

            return new PublicUiStringsDto { Locale = dbLocale, Strings = map };
        }, cancellationToken);

    // ── 場地（Location & Map、訓練地點）──────────────────────────────────────────────
    /// <summary>這個俱樂部用得到的場地：站台事實登記的主場，加上本俱樂部賽事／梯次／試訓引用的場地。</summary>
    public async Task<IReadOnlyList<PublicVenueDto>> ListVenuesAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        var homeValue = await db.Settings.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId && s.SettingKey == "site.home_venue_ids")
            .Select(s => s.SettingValue).FirstOrDefaultAsync(cancellationToken);
        var homeIds = (homeValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Guid.TryParse(t, out var id) ? id : (Guid?)null).Where(id => id is not null).Select(id => id!.Value).ToList();

        var def = RequestLocale.DefaultDbLocale;
        var rows = await db.Venues.AsNoTracking()
            .Where(v => homeIds.Contains(v.Id)
                        || v.Sessions.Any(s => s.ClubId == scope.ClubId)
                        || v.Trials.Any(t => t.ClubId == scope.ClubId)
                        || v.Matches.Any(m => m.ClubId == scope.ClubId))
            .OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq)
            .Select(v => new
            {
                v.Id, v.Lat, v.Lng, v.PhotoKey, v.PhotoWidth, v.PhotoHeight,
                NameReq = v.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                NameDef = v.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                AddrReq = v.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Address).FirstOrDefault(),
                AddrDef = v.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Address).FirstOrDefault(),
                DirReq = v.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Directions).FirstOrDefault(),
                DirDef = v.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Directions).FirstOrDefault(),
                AltReq = v.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.PhotoAlt).FirstOrDefault(),
                AltDef = v.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.PhotoAlt).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PublicVenueDto
        {
            Id = r.Id,
            Name = RequestLocale.Pick(r.NameReq, r.NameDef),
            Address = RequestLocale.Pick(r.AddrReq, r.AddrDef),
            Directions = RequestLocale.Pick(r.DirReq, r.DirDef),
            Lat = r.Lat,
            Lng = r.Lng,
            PhotoUrl = imageUrls.Resolve(r.PhotoKey),
            PhotoWidth = r.PhotoKey is null ? null : r.PhotoWidth,
            PhotoHeight = r.PhotoKey is null ? null : r.PhotoHeight,
            PhotoAlt = r.PhotoKey is null ? null : RequestLocale.Pick(r.AltReq, r.AltDef),
            IsHome = homeIds.Contains(r.Id),
        }).OrderByDescending(v => v.IsHome).ToList();
    }
}
