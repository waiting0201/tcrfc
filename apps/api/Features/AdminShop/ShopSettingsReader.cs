using System.Globalization;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>商店設定的鍵（<c>settings</c> 表，設定群組 <c>shop</c>，每個俱樂部各一份）。S6 商店設定與其他商店模組共用。</summary>
public static class ShopSettingKeys
{
    public const string Group = "shop";
    public const string ShippingFee = "shop.shipping_fee";
    public const string FreeShippingThreshold = "shop.free_shipping_threshold";
    public const string ExcludedRegions = "shop.excluded_regions";
    public const string LowStockThreshold = "shop.low_stock_threshold";
    public const string PendingTimeoutMinutes = "shop.pending_timeout_minutes";
    public const string EntryTitle = "shop.entry_title";
    public const string EntryIntro = "shop.entry_intro";
    public const string PolicyNotice = "shop.policy_notice";
    public const string PolicyShipping = "shop.policy_shipping";
    public const string PolicyReturns = "shop.policy_returns";
    public const string PolicyTerms = "shop.policy_terms";

    // 收款主體（俱樂部）層級：金流環境與發票重試設定，存在收款主體俱樂部名下。
    public const string PaymentEnvironment = "shop.payment_environment";
    public const string InvoiceRetryMax = "shop.invoice_retry_max";
    public const string InvoiceRetryIntervalMinutes = "shop.invoice_retry_interval_minutes";

    public static readonly string[] Numeric =
        [ShippingFee, FreeShippingThreshold, ExcludedRegions, LowStockThreshold, PendingTimeoutMinutes];

    public static readonly string[] Texts = [EntryTitle, EntryIntro, PolicyNotice, PolicyShipping, PolicyReturns, PolicyTerms];

    public const int DefaultLowStockThreshold = 5;
    public const int DefaultPendingTimeoutMinutes = 30;
}

/// <summary>運費與庫存門檻等「數值型」商店設定的讀取（結帳與後台建單共用）。</summary>
public sealed class ShopSettingsReader(ClubSettingsStore store)
{
    public sealed record ShippingRule(int Fee, int? FreeThreshold);

    public async Task<ShippingRule> GetShippingAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var map = await store.GetManyAsync(clubId, [ShopSettingKeys.ShippingFee, ShopSettingKeys.FreeShippingThreshold], cancellationToken);
        return new ShippingRule(ParseInt(map, ShopSettingKeys.ShippingFee) ?? 0, ParseInt(map, ShopSettingKeys.FreeShippingThreshold));
    }

    public async Task<int> GetLowStockThresholdAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var map = await store.GetManyAsync(clubId, [ShopSettingKeys.LowStockThreshold], cancellationToken);
        return ParseInt(map, ShopSettingKeys.LowStockThreshold) ?? ShopSettingKeys.DefaultLowStockThreshold;
    }

    public async Task<int> GetPendingTimeoutMinutesAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var map = await store.GetManyAsync(clubId, [ShopSettingKeys.PendingTimeoutMinutes], cancellationToken);
        return ParseInt(map, ShopSettingKeys.PendingTimeoutMinutes) ?? ShopSettingKeys.DefaultPendingTimeoutMinutes;
    }

    /// <summary>運費計算：現場自取免運；小計達免運門檻免運；否則固定運費。</summary>
    public static int ComputeShippingFee(ShippingRule rule, string deliveryMethod, int subtotal)
        => deliveryMethod == "onsite_pickup" || (rule.FreeThreshold is int t && subtotal >= t) ? 0 : rule.Fee;

    private static int? ParseInt(IReadOnlyDictionary<string, string?> map, string key)
        => map.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
