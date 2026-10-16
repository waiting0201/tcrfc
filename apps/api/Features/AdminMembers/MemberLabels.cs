namespace Tcrfc.Api.Features.AdminMembers;

/// <summary>會員系統（K1–K4）回應裡給畫面直接顯示的日常中文標籤（規劃書 §4.0：介面不顯示英文代碼）。
/// 資料庫存英文代碼，API 同時回傳代碼（供篩選與判斷）與標籤（供顯示）。</summary>
public static class MemberLabels
{
    /// <summary>
    /// 會籍層級（v3.29，客戶 8.2）：只有 <c>standard</c>（磐石會員）與 <c>premium</c>（尊尚會員）。<b>註冊帳號不是會籍</b>，沒有「一般會員」這一級。
    /// 這是層級的預設名稱；會籍所屬方案有自己的名稱（藍鯨的方案由藍鯨自訂），畫面顯示優先用方案名稱，見 <see cref="TierName"/>。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Tier = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["standard"] = "磐石會員",
        ["premium"] = "尊尚會員",
    };

    /// <summary>
    /// 抽獎名單快照的層級顯示名稱：新名單是 <c>standard</c>／<c>premium</c>；<b>舊制（v3.29 以前）凍結的名單</b>快照是 <c>fan_club</c>／<c>registered</c>，
    /// 凍結名單不改寫（名單雜湊含層級字串），所以顯示時保留舊制名稱，不能直接丟掉或當成錯誤。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SnapshotTier = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["standard"] = "磐石會員",
        ["premium"] = "尊尚會員",
        ["fan_club"] = "球迷會員（舊制）",
        ["registered"] = "一般會員（舊制）",
    };

    /// <summary>層級預設名稱（英文）。客戶未提供英文名，Rock Member／Prestige Member 是開發團隊的工作翻譯（見主站規劃書 §3.14 註記）。</summary>
    public static readonly IReadOnlyDictionary<string, string> TierEn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["standard"] = "Rock Member",
        ["premium"] = "Prestige Member",
    };

    /// <summary>層級的顯示名稱：有方案名稱用方案名稱，沒有就用層級預設名稱。</summary>
    public static string TierName(string tier, bool en, string? planName = null)
        => !string.IsNullOrWhiteSpace(planName) ? planName : (en ? TierEn : Tier).GetValueOrDefault(tier, tier);

    public static readonly IReadOnlyDictionary<string, string> MembershipStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待確認",
        ["active"] = "有效",
        ["expired"] = "已到期",
        ["cancelled"] = "已取消",
    };

    /// <summary>帳號狀態（含由「未驗證 Email」推得的 unverified）。</summary>
    public static readonly IReadOnlyDictionary<string, string> AccountStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["active"] = "啟用",
        ["suspended"] = "停用",
        ["unverified"] = "未驗證",
        ["deleted"] = "已刪除",
    };

    public static readonly IReadOnlyDictionary<string, string> SignupSource = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["web"] = "官網註冊",
        ["line"] = "LINE",
        ["admin"] = "現場入會",
        ["app"] = "行動 App",
    };

    public static readonly IReadOnlyDictionary<string, string> Jersey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待處理",
        ["shipped"] = "已寄出",
        ["received"] = "已領取",
    };

    public static readonly IReadOnlyDictionary<string, string> Delivery = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ship"] = "寄送",
        ["pickup"] = "到場領取",
    };

    public static readonly IReadOnlyDictionary<string, string> PaymentMethod = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["linepay"] = "LINE Pay",
        ["onsite"] = "現場收款",
    };

    public static readonly IReadOnlyDictionary<string, string> Locale = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["zh-Hant"] = "繁體中文",
        ["en"] = "English",
    };

    public static string? Of(IReadOnlyDictionary<string, string> map, string? code)
        => code is not null && map.TryGetValue(code, out var label) ? label : code;

    /// <summary>會籍有效與否以「到期日」為準（同抽獎資格的判定，見 docs/12b §6.4）：
    /// 狀態欄是人工覆寫（待確認／已取消／批次到期），日期過了但還沒被批次處理的仍算已到期。</summary>
    public static string EffectiveMembershipStatus(string status, DateOnly? endOn, DateOnly today)
        => status == "active" && endOn is DateOnly end && end < today ? "expired" : status;
}
