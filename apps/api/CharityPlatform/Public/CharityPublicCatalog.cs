using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Public;

/// <summary>
/// 公開唯讀：掃碼落地頁的店家、項目卡片牆、項目詳情、站台文案。<b>不經過任何快取</b>（慈善平台完全不接 Redis，
/// docs/16 §7：資料量數千至數萬筆，直接查即可，多一層快取只會多一個出錯的地方；也避免快取繞過獨立資料庫的邊界）。
/// 全部走 <c>AsNoTracking</c> 投影，只取回要輸出的欄位。
/// </summary>
public sealed class CharityPublicCatalog(CharityDbContext db, ICharityImageStorage imageUrls)
{
    public const string DefaultMinAmountKey = "donation.default_min_amount";
    public const string DefaultMaxAmountKey = "donation.default_max_amount";
    public const string CreditListRuleKey = "donation.credit_list_display_rule";

    // 規劃書沒有給全站預設上下限的數字；設定缺漏時的保底值（CHECK 要求金額 > 0）。
    private const int FallbackMinAmount = 1;
    private const int FallbackMaxAmount = 1_000_000;

    /// <summary>
    /// 依 <c>store_slug</c> 解析<b>有效</b>的店家：狀態為合作中，且今天（台灣日期）落在合作起訖之內。
    /// 不存在、已停止、不在合作期間一律回傳 <c>null</c>（視同無店家歸屬）。
    /// </summary>
    public async Task<PublicStoreDto?> ResolveActiveStoreAsync(string? storeSlug, string dbLocale, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeSlug))
        {
            return null;
        }

        var today = TaiwanClock.Today;
        var row = await db.DonationStores.AsNoTracking()
            .Where(s => s.StoreSlug == storeSlug && s.Status == "active"
                        && (s.StartOn == null || s.StartOn <= today)
                        && (s.EndOn == null || s.EndOn >= today))
            .Select(s => new
            {
                s.StoreSlug,
                s.LogoKey,
                Requested = s.DonationStoresI18ns.Where(i => i.Locale == dbLocale).Select(i => new { i.Name, i.LogoAlt }).FirstOrDefault(),
                Default = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => new { i.Name, i.LogoAlt }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var name = RequestLocale.Pick(row.Requested?.Name, row.Default?.Name);
        if (name is null)
        {
            return null; // 沒有任何語系的店名：無法呈現，視同無效
        }

        return new PublicStoreDto
        {
            Slug = row.StoreSlug,
            Name = name,
            LogoUrl = imageUrls.Resolve(row.LogoKey),
            LogoAlt = RequestLocale.Pick(row.Requested?.LogoAlt, row.Default?.LogoAlt),
            IsFallback = dbLocale != RequestLocale.DefaultDbLocale && string.IsNullOrWhiteSpace(row.Requested?.Name),
        };
    }

    /// <summary>建單時用：同一套「有效店家」判斷（合作中且在合作期間），回傳店家主鍵與<b>當下</b>分潤率。
    /// 沒帶或對不到有效店家回傳 <c>null</c>——視同無店家歸屬，捐款照常成立、店家分潤為 0（規劃書 §2.2 第 4、5 點）。</summary>
    public async Task<(Guid Id, decimal SharePct)?> ResolveActiveStoreRefAsync(string? storeSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeSlug))
        {
            return null;
        }

        var today = TaiwanClock.Today;
        var row = await db.DonationStores.AsNoTracking()
            .Where(s => s.StoreSlug == storeSlug && s.Status == "active"
                        && (s.StartOn == null || s.StartOn <= today)
                        && (s.EndOn == null || s.EndOn >= today))
            .Select(s => new { s.Id, s.StoreSharePct })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : (row.Id, row.StoreSharePct);
    }

    public async Task<IReadOnlyList<PublicProjectCardDto>> ListPublishedProjectsAsync(string dbLocale, CancellationToken cancellationToken)
    {
        var rows = await db.DonationProjects.AsNoTracking()
            .Where(p => p.Status == "published")
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Seq)
            .Select(p => new
            {
                p.ProjectSlug,
                p.CoverKey,
                p.SortOrder,
                Requested = p.DonationProjectsI18ns.Where(i => i.Locale == dbLocale).Select(i => new { i.Name, i.OneLiner, i.CoverAlt }).FirstOrDefault(),
                Default = p.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => new { i.Name, i.OneLiner, i.CoverAlt }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new { Row = r, Name = RequestLocale.Pick(r.Requested?.Name, r.Default?.Name) })
            .Where(x => x.Name is not null) // 沒有任何名稱的項目不輸出（資料不完整，不要在前台露出空白卡片）
            .Select(x => new PublicProjectCardDto
            {
                Slug = x.Row.ProjectSlug,
                Name = x.Name!,
                OneLiner = RequestLocale.Pick(x.Row.Requested?.OneLiner, x.Row.Default?.OneLiner),
                CoverUrl = imageUrls.Resolve(x.Row.CoverKey),
                CoverAlt = RequestLocale.Pick(x.Row.Requested?.CoverAlt, x.Row.Default?.CoverAlt),
                SortOrder = x.Row.SortOrder,
                IsFallback = dbLocale != RequestLocale.DefaultDbLocale && string.IsNullOrWhiteSpace(x.Row.Requested?.Name),
            })
            .ToList();
    }

    public async Task<PublicProjectDetailDto?> GetPublishedProjectAsync(string slug, string dbLocale, CancellationToken cancellationToken)
    {
        var row = await db.DonationProjects.AsNoTracking()
            .Where(p => p.ProjectSlug == slug && p.Status == "published")
            .Select(p => new
            {
                p.ProjectSlug,
                p.CoverKey,
                p.MinAmount,
                p.MaxAmount,
                p.InvoiceMode,
                p.CharityNameSnapshot,
                p.CharityProgramNameSnapshot,
                p.CharityProgramRefCode,
                AmountOptions = p.DonationAmountOptions.OrderBy(o => o.Amount).Select(o => o.Amount).ToList(),
                Requested = p.DonationProjectsI18ns.Where(i => i.Locale == dbLocale)
                    .Select(i => new { i.Name, i.OneLiner, i.Description, i.FundUsage, i.CoverAlt }).FirstOrDefault(),
                Default = p.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale)
                    .Select(i => new { i.Name, i.OneLiner, i.Description, i.FundUsage, i.CoverAlt }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var name = RequestLocale.Pick(row.Requested?.Name, row.Default?.Name);
        if (name is null)
        {
            return null;
        }

        var (defaultMin, defaultMax) = await GetDefaultAmountRangeAsync(cancellationToken);

        return new PublicProjectDetailDto
        {
            Slug = row.ProjectSlug,
            Name = name,
            OneLiner = RequestLocale.Pick(row.Requested?.OneLiner, row.Default?.OneLiner),
            Description = ParseJson(RequestLocale.Pick(row.Requested?.Description, row.Default?.Description)),
            FundUsage = RequestLocale.Pick(row.Requested?.FundUsage, row.Default?.FundUsage),
            CoverUrl = imageUrls.Resolve(row.CoverKey),
            CoverAlt = RequestLocale.Pick(row.Requested?.CoverAlt, row.Default?.CoverAlt),
            AmountOptions = row.AmountOptions,
            MinAmount = row.MinAmount ?? defaultMin,
            MaxAmount = row.MaxAmount ?? defaultMax,
            InvoiceMode = row.InvoiceMode,
            CharityName = row.CharityNameSnapshot,
            CharityProgramName = row.CharityProgramNameSnapshot,
            CharityProgramRefCode = row.CharityProgramRefCode,
            IsFallback = dbLocale != RequestLocale.DefaultDbLocale && string.IsNullOrWhiteSpace(row.Requested?.Name),
        };
    }

    public async Task<PublicSettingsDto> GetSettingsAsync(string dbLocale, CancellationToken cancellationToken)
    {
        string[] textKeys = ["donation.home_intro", "donation.thank_you_message_template", "donation.notice", "donation.privacy_policy"];

        var texts = await db.Settings.AsNoTracking()
            .Where(s => textKeys.Contains(s.SettingKey))
            .Select(s => new
            {
                s.SettingKey,
                Requested = s.SettingsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Value).FirstOrDefault(),
                Default = s.SettingsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Value).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        string? Text(string key) => texts.Where(t => t.SettingKey == key).Select(t => RequestLocale.Pick(t.Requested, t.Default)).FirstOrDefault();
        var fallback = dbLocale != RequestLocale.DefaultDbLocale
                       && texts.Any(t => string.IsNullOrWhiteSpace(t.Requested) && !string.IsNullOrWhiteSpace(t.Default));

        var (min, max) = await GetDefaultAmountRangeAsync(cancellationToken);
        var rule = await db.Settings.AsNoTracking().Where(s => s.SettingKey == CreditListRuleKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);

        return new PublicSettingsDto
        {
            HomeIntro = Text("donation.home_intro"),
            ThankYouTemplate = Text("donation.thank_you_message_template"),
            Notice = Text("donation.notice"),
            PrivacyPolicy = Text("donation.privacy_policy"),
            DefaultMinAmount = min,
            DefaultMaxAmount = max,
            CreditListEnabled = !string.Equals(rule, "off", StringComparison.Ordinal),
            IsFallback = fallback,
        };
    }

    /// <summary>全站預設單筆金額上下限（<c>settings</c>）；缺漏或不是正整數時退回保底值。</summary>
    public async Task<(int Min, int Max)> GetDefaultAmountRangeAsync(CancellationToken cancellationToken)
    {
        var values = await db.Settings.AsNoTracking()
            .Where(s => s.SettingKey == DefaultMinAmountKey || s.SettingKey == DefaultMaxAmountKey)
            .Select(s => new { s.SettingKey, s.Value })
            .ToListAsync(cancellationToken);

        int Read(string key, int fallback)
            => int.TryParse(values.FirstOrDefault(v => v.SettingKey == key)?.Value, out var parsed) && parsed > 0 ? parsed : fallback;

        var min = Read(DefaultMinAmountKey, FallbackMinAmount);
        var max = Read(DefaultMaxAmountKey, FallbackMaxAmount);
        return max < min ? (FallbackMinAmount, FallbackMaxAmount) : (min, max);
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null; // 內文不是合法 JSON（資料瑕疵）：不讓整頁 500，當作沒有內文
        }
    }
}
