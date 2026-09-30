namespace Tcrfc.Api.Features.AdminAds;

/// <summary>E4–E6 回應裡給畫面直接顯示的日常中文標籤（規劃書 §4.0：介面不顯示英文代碼）。資料庫存英文代碼，API 同時回代碼與標籤。</summary>
public static class AdLabels
{
    public static readonly IReadOnlyDictionary<string, string> AdvertiserStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["negotiating"] = "洽談中", ["active"] = "合作中", ["ended"] = "已結束",
    };

    public static readonly IReadOnlyDictionary<string, string> CampaignStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿", ["pending_review"] = "待審核", ["scheduled"] = "已排程", ["running"] = "投放中",
        ["paused"] = "已暫停", ["ended"] = "已結束", ["closed"] = "已結案", ["voided"] = "已作廢",
    };

    public static readonly IReadOnlyDictionary<string, string> GoalType = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["guaranteed"] = "曝光保證", ["traffic"] = "導流",
    };

    public static readonly IReadOnlyDictionary<string, string> ReviewStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待審", ["approved"] = "通過", ["rejected"] = "退回",
    };

    public static readonly IReadOnlyDictionary<string, string> Theme = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["light"] = "淺色底", ["dark"] = "深色底", ["both"] = "深淺底通用",
    };

    public static readonly IReadOnlyDictionary<string, string> Platform = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ios"] = "iOS", ["android"] = "Android", ["unknown"] = "未知",
    };

    public static readonly IReadOnlyDictionary<string, string> Locale = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["zh-Hant"] = "繁體中文", ["en"] = "英文", ["unknown"] = "未知",
    };

    public static string Of(IReadOnlyDictionary<string, string> map, string? code)
        => code is null ? string.Empty : map.GetValueOrDefault(code, code);

    /// <summary>對外語系代碼 <c>zh</c>／<c>en</c> ↔ 資料庫 <c>zh-Hant</c>／<c>en</c>。</summary>
    public static string ToDbLocale(string? external) => Localization.RequestLocale.ToDbLocale(external);

    public static string ToExternalLocale(string? db) => db == "en" ? "en" : "zh";
}
