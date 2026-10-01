namespace Tcrfc.Api.Data.EfEntities;

// F 批（2026-10-01，S3-5 前台結帳）：訂單新增欄位。與 Order.cs（scaffold 產生檔）分開，避免重新 scaffold 時被覆寫。
public partial class Order
{
    /// <summary>買家 Email（非會員結帳必填；訂單成立信與 /order/lookup 的比對依據）。後台人工建單為空。</summary>
    public string? BuyerEmail { get; set; }

    /// <summary>前台結帳的冪等鍵（<c>(club_id, idempotency_key)</c> 篩選唯一）。</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>同一冪等鍵的請求指紋（SHA-256 十六進位）：同鍵不同內容或不同擁有者 → 409。</summary>
    public string? RequestFingerprint { get; set; }

    /// <summary>LINE Pay 請款後的付款網址（待付款期間才有值）。</summary>
    public string? PaymentUrl { get; set; }
}
