using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S6 商店設定（規劃書 §4.13 S6）：運費設定（<b>俱樂部層級</b>：單一固定運費＋免運門檻、離島與不配送地區，不做重量或級距計費）、
/// 低庫存與待付款保留時間、商店入口與政策內容（購物須知、運送說明、退換貨政策、交易條款，中／英）。
/// 沿用 <c>settings</c>／<c>settings_i18n</c>（群組 <c>shop</c>，鍵見 <see cref="ShopSettingKeys"/>），不新增資料表；<b>整份取代</b>語意。
/// 🔴 介面須標明本商店的收款主體是俱樂部（<see cref="ShopLabels.CollectingSubjectNotice"/>）。刻意不注入快取服務。
/// </summary>
public sealed class AdminShopSettingsRepository(ClubDbContext db, ClubTextSettings texts)
{
    private static readonly string[] AllKeys = [.. ShopSettingKeys.Numeric, .. ShopSettingKeys.Texts];

    internal async Task<AdminCollectingSubjectDto> CollectingSubjectAsync(CancellationToken cancellationToken)
    {
        var row = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, Name = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() })
            .FirstOrDefaultAsync(cancellationToken);
        return new AdminCollectingSubjectDto { ClubId = row?.Id ?? Guid.Empty, Name = row?.Name, Notice = ShopLabels.CollectingSubjectNotice };
    }

    public async Task<AdminShopSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var map = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey)).ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, cancellationToken);
        return Build(map, await CollectingSubjectAsync(cancellationToken));
    }

    public async Task<AdminShopSettingsDto> UpdateAsync(AdminClubScope scope, UpdateAdminShopSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.ShippingFee < 0)
        {
            throw new AdminValidationException("運費不可為負數。", "shippingFee");
        }

        if (request.FreeShippingThreshold is < 0)
        {
            throw new AdminValidationException("免運門檻不可為負數。", "freeShippingThreshold");
        }

        if (request.LowStockThreshold is < 0 or > 100000)
        {
            throw new AdminValidationException("低庫存門檻必須介於 0 與 100000 之間。", "lowStockThreshold");
        }

        if (request.PendingTimeoutMinutes is < 5 or > 1440)
        {
            throw new AdminValidationException("待付款保留時間必須介於 5 與 1440 分鐘之間。", "pendingTimeoutMinutes");
        }

        var regions = (request.ExcludedRegions ?? []).Select(r => r?.Trim() ?? "").Where(r => r.Length > 0).Distinct().ToList();
        if (regions.Count > 60 || regions.Any(r => r.Length > 32))
        {
            throw new AdminValidationException("不配送地區最多 60 個，每個名稱不可超過 32 個字。", "excludedRegions");
        }

        var map = await texts.LoadAsync(scope.ClubId, AllKeys, cancellationToken);
        var op = scope.Identity.AdminUserId;
        var g = ShopSettingKeys.Group;
        texts.SetValue(map, scope.ClubId, ShopSettingKeys.ShippingFee, g, request.ShippingFee.ToString(CultureInfo.InvariantCulture), op);
        texts.SetValue(map, scope.ClubId, ShopSettingKeys.FreeShippingThreshold, g, request.FreeShippingThreshold?.ToString(CultureInfo.InvariantCulture), op);
        texts.SetValue(map, scope.ClubId, ShopSettingKeys.ExcludedRegions, g, regions.Count == 0 ? null : JsonSerializer.Serialize(regions), op);
        texts.SetValue(map, scope.ClubId, ShopSettingKeys.LowStockThreshold, g, request.LowStockThreshold?.ToString(CultureInfo.InvariantCulture), op);
        texts.SetValue(map, scope.ClubId, ShopSettingKeys.PendingTimeoutMinutes, g, request.PendingTimeoutMinutes?.ToString(CultureInfo.InvariantCulture), op);
        SetText(map, scope, ShopSettingKeys.EntryTitle, request.EntryTitle, "商店入口標題", 200, "entryTitle");
        SetText(map, scope, ShopSettingKeys.EntryIntro, request.EntryIntro, "商店入口說明", 5000, "entryIntro");
        SetText(map, scope, ShopSettingKeys.PolicyNotice, request.PolicyNotice, "購物須知", 20000, "notice");
        SetText(map, scope, ShopSettingKeys.PolicyShipping, request.PolicyShipping, "運送說明", 20000, "shipping");
        SetText(map, scope, ShopSettingKeys.PolicyReturns, request.PolicyReturns, "退換貨政策", 20000, "returns");
        SetText(map, scope, ShopSettingKeys.PolicyTerms, request.PolicyTerms, "交易條款", 20000, "terms");
        await db.SaveChangesAsync(cancellationToken);
        return Build(map, await CollectingSubjectAsync(cancellationToken));
    }

    private void SetText(Dictionary<string, Data.EfEntities.Setting> map, AdminClubScope scope, string key, AdminShopBilingualText? value, string label, int max, string field)
        => texts.SetText(map, scope.ClubId, key, ShopSettingKeys.Group, AdminInput.OptionalText(value?.Zh, $"中文{label}", max, field + "Zh"),
            AdminInput.OptionalText(value?.En, $"英文{label}", max, field + "En"), scope.Identity.AdminUserId);

    private static AdminShopSettingsDto Build(IReadOnlyDictionary<string, Data.EfEntities.Setting> map, AdminCollectingSubjectDto subject)
    {
        int? Int(string key) => int.TryParse(ClubTextSettings.GetValue(map, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        AdminShopBilingualText Text(string key)
        {
            var (zh, en) = ClubTextSettings.Get(map, key);
            return new AdminShopBilingualText { Zh = zh, En = en };
        }

        IReadOnlyList<string> regions = [];
        if (ClubTextSettings.GetValue(map, ShopSettingKeys.ExcludedRegions) is { Length: > 0 } raw)
        {
            try
            {
                regions = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            }
            catch (JsonException)
            {
                regions = [];
            }
        }

        return new AdminShopSettingsDto
        {
            CollectingSubject = subject, ShippingFee = Int(ShopSettingKeys.ShippingFee) ?? 0, FreeShippingThreshold = Int(ShopSettingKeys.FreeShippingThreshold),
            ExcludedRegions = regions, LowStockThreshold = Int(ShopSettingKeys.LowStockThreshold) ?? ShopSettingKeys.DefaultLowStockThreshold,
            PendingTimeoutMinutes = Int(ShopSettingKeys.PendingTimeoutMinutes) ?? ShopSettingKeys.DefaultPendingTimeoutMinutes,
            EntryTitle = Text(ShopSettingKeys.EntryTitle), EntryIntro = Text(ShopSettingKeys.EntryIntro), PolicyNotice = Text(ShopSettingKeys.PolicyNotice),
            PolicyShipping = Text(ShopSettingKeys.PolicyShipping), PolicyReturns = Text(ShopSettingKeys.PolicyReturns), PolicyTerms = Text(ShopSettingKeys.PolicyTerms),
            UpdatedAt = ClubTextSettings.LatestUpdate(map),
        };
    }
}
