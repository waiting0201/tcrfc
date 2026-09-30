namespace Tcrfc.Api.Features.AdminShop;

/// <summary>站內商店（S1–S6）回應裡給畫面直接顯示的日常中文標籤（規劃書 §4.0：介面不顯示英文代碼）。
/// 資料庫存代碼，API 同時回傳代碼（供篩選與判斷）與標籤（供顯示）。</summary>
public static class ShopLabels
{
    public const string ClubOrderPrefixFallback = "SH";

    // ── 訂單狀態（資料庫本身就存中文，規劃書 §4.13 S3 狀態機）──
    public const string Pending = "待付款";
    public const string Paid = "已付款";
    public const string Preparing = "備貨中";
    public const string Shipped = "已出貨";
    public const string Completed = "已完成";
    public const string Cancelled = "已取消";
    public const string Returning = "退貨處理中";
    public const string Refunded = "已退款";

    public static readonly IReadOnlyList<string> OrderStatuses = [Pending, Paid, Preparing, Shipped, Completed, Cancelled, Returning, Refunded];

    public static readonly IReadOnlyDictionary<string, string> PaymentStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待付款",
        ["paid"] = "已付款",
        ["failed"] = "付款失敗",
        ["expired"] = "已逾時",
        ["refunded"] = "已退款",
    };

    public static readonly IReadOnlyDictionary<string, string> PaymentMethod = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["linepay"] = "LINE Pay",
        ["onsite"] = "現場收款",
    };

    public static readonly IReadOnlyDictionary<string, string> Delivery = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["home_delivery"] = "宅配",
        ["cvs_pickup"] = "超商取貨",
        ["onsite_pickup"] = "現場自取",
    };

    public static readonly IReadOnlyDictionary<string, string> Settlement = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待結算",
        ["settled"] = "已結算",
    };

    public static readonly IReadOnlyDictionary<string, string> Pickup = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["waiting"] = "待領取",
        ["picked_up"] = "已領取",
        ["overdue"] = "逾期",
    };

    public static readonly IReadOnlyDictionary<string, string> Refund = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["requested"] = "申請中",
        ["approved"] = "已核准",
        ["received"] = "已驗收退回品",
        ["processing"] = "退款處理中",
        ["refunded"] = "已退款",
        ["rejected"] = "已駁回",
    };

    public static readonly IReadOnlyDictionary<string, string> RefundMethod = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["linepay"] = "原路退回 LINE Pay",
        ["manual"] = "人工退款",
    };

    public static readonly IReadOnlyDictionary<string, string> Movement = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["stock_in"] = "進貨",
        ["stocktake"] = "盤點",
        ["damage"] = "報損",
        ["adjust"] = "調整",
        ["reserve"] = "下單保留",
        ["release"] = "釋回保留",
        ["sale"] = "售出扣減",
        ["cancel_restock"] = "取消回補",
        ["return_restock"] = "退貨回補",
    };

    public static readonly IReadOnlyDictionary<string, string> ProductStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "下架（草稿）",
        ["published"] = "上架",
        ["sold_out"] = "缺貨",
    };

    public static readonly IReadOnlyDictionary<string, string> VariantStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["active"] = "販售中",
        ["inactive"] = "停售",
    };

    public static readonly IReadOnlyDictionary<string, string> OutOfStock = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["show_unavailable"] = "顯示但不可購買",
        ["hide"] = "自動隱藏",
    };

    public static readonly IReadOnlyDictionary<string, string> CollectionStatus = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿",
        ["published"] = "已發布",
    };

    public static string Of(IReadOnlyDictionary<string, string> map, string? code)
        => code is not null && map.TryGetValue(code, out var label) ? label : code ?? "";

    /// <summary>訂單編號前綴（規劃書 §4.13 S3：依俱樂部加前綴 <c>TR-</c>／<c>BW-</c>）。</summary>
    public static string OrderPrefix(string clubCode) => clubCode.ToLowerInvariant() switch
    {
        "tcrfc" => "TR",
        "bw" => "BW",
        var other => other.ToUpperInvariant(),
    };

    /// <summary>供應收款主體的提示文字，S6 介面須標明（規劃書 §4.13：憑證填錯等於款項進錯法人）。</summary>
    public const string CollectingSubjectNotice = "本商店的收款主體是俱樂部，不是慈善捐款平台的主辦協會；金流與發票憑證請填入俱樂部自己的商店號與字軌。";
}
