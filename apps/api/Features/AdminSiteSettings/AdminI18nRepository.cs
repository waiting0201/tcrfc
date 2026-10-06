using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>
/// I4 多語系管理（規劃書 §4.9）：啟用語系、翻譯狀態總覽、Fallback 規則、日期／數字格式。字串翻譯表見 <see cref="AdminUiStringsRepository"/>。
///
/// - <b>語系表 <c>locales</c> 是全站共用主檔</b>（沒有 <c>club_id</c>）；本檔只改既有列的名稱／啟用／備援／排序，<b>不提供新增語系</b>：
///   <see cref="RequestLocale.ToDbLocale"/> 目前只認 <c>en</c>，新增語系需要改程式，與規劃書 G-01「新增語系時不需改動程式」的目標仍有落差（README 待決）。
/// - <b>Fallback 規則、日期、數字格式是每俱樂部一份</b>（<c>settings</c>），鍵見 <see cref="SiteSettingKeys"/>。
///   日期格式只收 <c>YYYY MMMM MMM MM M DD D</c> 與分隔字元 <c>空白 / - . , 年 月 日</c> 組成的樣式；數字格式只收 <c>1,234.56</c> 這類範例字串
///   （千分位 <c>, . 空白</c> 或無，小數點 <c>. ,</c>）——不收自由格式字串，避免前台格式化函式吃到任意輸入。
/// - 「字型設定」規劃書只有一行字、沒有可選項目定義，且字型由設計系統（design tokens）決定，<b>本次不做</b>（README 待決）。
/// </summary>
public sealed partial class AdminI18nRepository(
    ClubDbContext db, ClubSettingsEditor settingsEditor, TranslationStatusReader statusReader, IQueryCache cache)
{
    private const int TranslationPageMax = 100;

    [GeneratedRegex(@"^(?:YYYY|MMMM|MMM|MM|M|DD|D|[ /\-.,年月日])+$")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^1[,. ]?234[.,]56$")]
    private static partial Regex NumberPattern();

    // ── 語系 ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<AdminLocaleDto>> ListLocalesAsync(CancellationToken cancellationToken)
        => (await db.Locales.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Code).ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<AdminLocaleDto?> UpdateLocaleAsync(
        AdminClubScope scope, string code, UpdateAdminLocaleRequest request, CancellationToken cancellationToken)
    {
        var all = await db.Locales.ToListAsync(cancellationToken);
        var locale = all.FirstOrDefault(l => l.Code == code);
        if (locale is null)
        {
            return null;
        }

        var name = AdminInput.RequireText(request.Name, "語系名稱", 64, "name");
        if (request.SortOrder < 0)
        {
            throw new AdminValidationException("排序不可為負數。", "sortOrder");
        }

        var fallback = string.IsNullOrWhiteSpace(request.FallbackCode) ? null : request.FallbackCode.Trim();
        if (locale.IsDefault)
        {
            if (!request.IsEnabled)
            {
                throw new AdminValidationException("預設語系不能停用。", "isEnabled");
            }

            if (fallback is not null)
            {
                throw new AdminValidationException("預設語系不需要設定備援語系。", "fallbackCode");
            }
        }
        else if (fallback is not null)
        {
            var target = all.FirstOrDefault(l => l.Code == fallback)
                ?? throw new AdminValidationException("備援語系不存在。", "fallbackCode");
            if (target.Code == locale.Code)
            {
                throw new AdminValidationException("備援語系不能是自己。", "fallbackCode");
            }

            if (!target.IsEnabled)
            {
                throw new AdminValidationException("備援語系必須是啟用中的語系。", "fallbackCode");
            }

            // 備援鏈不得成環（A→B→A）。
            var cursor = target;
            for (var hops = 0; cursor.FallbackCode is not null && hops < all.Count; hops++)
            {
                if (cursor.FallbackCode == locale.Code)
                {
                    throw new AdminValidationException("備援語系設定會形成循環。", "fallbackCode");
                }

                cursor = all.First(l => l.Code == cursor.FallbackCode);
            }
        }

        if (!request.IsEnabled && all.Any(l => l.Code != locale.Code && l.IsEnabled && l.FallbackCode == locale.Code))
        {
            throw new AdminValidationException("還有其他語系把它當作備援語系，請先改掉那些語系的備援設定再停用。", "isEnabled");
        }

        locale.Name = name;
        locale.IsEnabled = request.IsEnabled;
        locale.FallbackCode = locale.IsDefault ? null : fallback;
        locale.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        // 語系表是全站共用主檔：所有俱樂部的站台設定快取（含啟用語系清單）都要失效，不只目前這個俱樂部。
        foreach (var clubCode in await db.Clubs.AsNoTracking().Select(c => c.Code).ToListAsync(cancellationToken))
        {
            await cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.Settings, clubCode, cancellationToken);
        }

        return ToDto(locale);
    }

    private static AdminLocaleDto ToDto(Locale l) => new()
    {
        Code = l.Code, Name = l.Name, IsDefault = l.IsDefault, FallbackCode = l.FallbackCode, IsEnabled = l.IsEnabled, SortOrder = l.SortOrder,
    };

    // ── Fallback 規則與格式 ────────────────────────────────────────────────────────
    private static readonly string[] SettingKeys =
        [SiteSettingKeys.I18nFallbackMode, SiteSettingKeys.I18nDateFormat, SiteSettingKeys.I18nNumberFormat];

    public async Task<AdminI18nSettingsDto> GetSettingsAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await settingsEditor.LoadAsync(scope.ClubId, SettingKeys, cancellationToken);
        return BuildSettings(settings);
    }

    public async Task<AdminI18nSettingsDto> UpdateSettingsAsync(
        AdminClubScope scope, UpdateAdminI18nSettingsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var mode = string.IsNullOrWhiteSpace(request.FallbackMode) ? SiteSettingKeys.FallbackShowDefault : request.FallbackMode.Trim();
        if (mode != SiteSettingKeys.FallbackShowDefault && mode != SiteSettingKeys.FallbackHide)
        {
            throw new AdminValidationException("未翻譯時的處理方式只能是「顯示繁中」或「隱藏該頁」。", "fallbackMode");
        }

        var dateZh = ValidateDate(request.DateFormatZh, "日期格式（繁中）", "dateFormatZh");
        var dateEn = ValidateDate(request.DateFormatEn, "日期格式（英文）", "dateFormatEn");
        var numberZh = ValidateNumber(request.NumberFormatZh, "數字格式（繁中）", "numberFormatZh");
        var numberEn = ValidateNumber(request.NumberFormatEn, "數字格式（英文）", "numberFormatEn");

        var settings = await settingsEditor.LoadAsync(scope.ClubId, SettingKeys, cancellationToken);
        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.I18nFallbackMode, SiteSettingKeys.GroupI18n, mode, operatorId);
        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.I18nDateFormat, SiteSettingKeys.GroupI18n, dateZh, dateEn, operatorId);
        settingsEditor.SetI18n(settings, scope.ClubId, SiteSettingKeys.I18nNumberFormat, SiteSettingKeys.GroupI18n, numberZh, numberEn, operatorId);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.Settings, scope.ClubCode, cancellationToken);
        return BuildSettings(settings);
    }

    private static AdminI18nSettingsDto BuildSettings(IReadOnlyDictionary<string, Setting> settings) => new()
    {
        FallbackMode = ClubSettingsEditor.Value(settings, SiteSettingKeys.I18nFallbackMode) == SiteSettingKeys.FallbackHide
            ? SiteSettingKeys.FallbackHide
            : SiteSettingKeys.FallbackShowDefault,
        DateFormatZh = ClubSettingsEditor.I18n(settings, SiteSettingKeys.I18nDateFormat, RequestLocale.DefaultDbLocale),
        DateFormatEn = ClubSettingsEditor.I18n(settings, SiteSettingKeys.I18nDateFormat, "en"),
        NumberFormatZh = ClubSettingsEditor.I18n(settings, SiteSettingKeys.I18nNumberFormat, RequestLocale.DefaultDbLocale),
        NumberFormatEn = ClubSettingsEditor.I18n(settings, SiteSettingKeys.I18nNumberFormat, "en"),
    };

    private static string? ValidateDate(string? value, string label, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > 32 || !DatePattern().IsMatch(text) || !text.Contains("YYYY", StringComparison.Ordinal)
            || !text.Contains('M') || !text.Contains('D'))
        {
            throw new AdminValidationException($"{label}只能由年（YYYY）、月（M、MM、MMM、MMMM）、日（D、DD）與分隔字元（空白 / - . , 年 月 日）組成，且三者都要有，例如 YYYY/MM/DD。", field);
        }

        return text;
    }

    private static string? ValidateNumber(string? value, string label, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return NumberPattern().IsMatch(text) ? text : throw new AdminValidationException($"{label}請用範例格式，例如 1,234.56、1.234,56 或 1 234,56。", field);
    }

    // ── 翻譯狀態總覽 ───────────────────────────────────────────────────────────────
    public async Task<AdminTranslationOverviewDto> OverviewAsync(
        AdminClubScope scope, string? type, string? missingLocale, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, TranslationPageMax);
        var locales = await db.Locales.AsNoTracking().Where(l => l.IsEnabled)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Code).Select(l => l.Code).ToListAsync(cancellationToken);
        if (locales.Count == 0)
        {
            locales = [RequestLocale.DefaultDbLocale];
        }

        if (!string.IsNullOrWhiteSpace(type) && TranslationStatusReader.Catalog.All(c => c.Type != type))
        {
            throw new AdminValidationException("內容類別不正確。");
        }

        var missing = string.IsNullOrWhiteSpace(missingLocale) ? null : missingLocale.Trim();
        if (missing is not null && !locales.Contains(missing))
        {
            throw new AdminValidationException("語系不正確或尚未啟用。");
        }

        var rows = await statusReader.ReadAsync(scope.ClubId, TranslationStatusReader.Catalog, locales, cancellationToken);
        var summary = TranslationStatusReader.Catalog.Select(c =>
        {
            var ofType = rows.Where(r => r.Type == c.Type).ToList();
            return new AdminTranslationSummaryDto
            {
                Type = c.Type,
                TypeLabel = c.LabelZh,
                Total = ofType.Count,
                Missing = locales.ToDictionary(l => l, l => ofType.Count(r => !r.DoneLocales.Contains(l)), StringComparer.Ordinal),
            };
        }).ToList();

        IEnumerable<TranslationStatusReader.Row> filtered = rows;
        if (!string.IsNullOrWhiteSpace(type))
        {
            filtered = filtered.Where(r => r.Type == type);
        }

        if (missing is not null)
        {
            filtered = filtered.Where(r => !r.DoneLocales.Contains(missing));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            filtered = filtered.Where(r => r.Label.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        var labels = TranslationStatusReader.Catalog.ToDictionary(c => c.Type, c => c.LabelZh, StringComparer.Ordinal);
        var items = list.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).Select(r => new AdminTranslationRowDto
        {
            Type = r.Type,
            TypeLabel = labels[r.Type],
            Id = r.Id,
            Label = r.Label,
            IsShared = r.IsShared,
            Done = locales.ToDictionary(l => l, l => r.DoneLocales.Contains(l), StringComparer.Ordinal),
        }).ToList();

        return new AdminTranslationOverviewDto
        {
            Locales = locales, Summary = summary, Items = items, Page = Math.Max(page, 1), PageSize = pageSize, TotalCount = list.Count,
        };
    }
}
