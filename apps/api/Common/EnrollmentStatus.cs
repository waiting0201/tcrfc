namespace Tcrfc.Api.Common;

/// <summary>
/// 梯次／試訓場次狀態與報名狀態的**穩定代碼＋雙語標籤**（Android 缺口 C2，2026-10-05）。
/// 資料庫與既有欄位（<c>status</c>）沿用規劃書的中文字面值（<c>CK_sessions_status</c>、<c>CK_registrations_status</c>，docs/12 §12 第 35 點），
/// <b>不改資料、不刪既有欄位</b>；DTO 另外新增 <c>statusCode</c>／<c>statusLabelZh</c>／<c>statusLabelEn</c>，用戶端依代碼分流、依語系顯示標籤，
/// 不再比對中文字面值。代碼值域收進 <c>shared/enums.json</c>（產生器解析本檔）。
/// 格式請勿改動：每筆一行 <c>["中文字面值"] = ("code", "中文標籤", "English label"),</c>——產生器用正規表示式解析。
/// </summary>
public static class EnrollmentStatus
{
    /// <summary>梯次（<c>sessions.status</c>）與試訓場次（<c>trials.status</c>）：開放／額滿／候補／已結束。</summary>
    public static readonly IReadOnlyDictionary<string, (string Code, string Zh, string En)> Slot = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
    {
        ["開放"] = ("open", "開放", "Open"),
        ["額滿"] = ("full", "額滿", "Full"),
        ["候補"] = ("waitlist", "候補", "Waitlist"),
        ["已結束"] = ("ended", "已結束", "Ended"),
    };

    /// <summary>報名（<c>registrations.status</c>）：待確認／已確認／已繳費／完成／取消／候補。</summary>
    public static readonly IReadOnlyDictionary<string, (string Code, string Zh, string En)> Registration = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
    {
        ["待確認"] = ("pending", "待確認", "Pending confirmation"),
        ["已確認"] = ("confirmed", "已確認", "Confirmed"),
        ["已繳費"] = ("paid", "已繳費", "Paid"),
        ["完成"] = ("completed", "完成", "Completed"),
        ["取消"] = ("cancelled", "取消", "Cancelled"),
        ["候補"] = ("waitlisted", "候補", "Waitlisted"),
    };

    public static (string Code, string Zh, string En) OfSlot(string? literal) => Resolve(Slot, literal);

    public static (string Code, string Zh, string En) OfRegistration(string? literal) => Resolve(Registration, literal);

    /// <summary>未知字面值（資料被改成值域外時）回傳 <c>unknown</c>＋原文字，不丟例外。</summary>
    private static (string Code, string Zh, string En) Resolve(IReadOnlyDictionary<string, (string Code, string Zh, string En)> map, string? literal)
        => literal is not null && map.TryGetValue(literal, out var hit) ? hit : ("unknown", literal ?? string.Empty, literal ?? string.Empty);
}
