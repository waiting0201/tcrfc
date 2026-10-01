namespace Tcrfc.Api.CharityPlatform.Invoices;

/// <summary>
/// 協會的電子發票／捐贈收據加值中心接縫（慈善規劃書 §5.5：「串接實作須抽象為<b>單一介面</b>（開立／作廢／折讓／查詢
/// 四個動作），日後換廠商時只換實作，捐款與報表模組不動」）。<b>廠商尚未指定、協會統編尚未取得（STATUS B-7）</b>，
/// 目前只有 <see cref="FakeInvoiceIssuer"/>。
///
/// 🔴 <b>字軌</b>：協會的發票字軌與俱樂部的<b>不得共用</b>（慈善 §4 前提、docs/14）。字軌（<c>invoice_prefix</c>）存在
/// 慈善庫的 <c>payment_channels</c>（協會自己的憑證），由呼叫端讀出後以 <see cref="InvoiceIssueRequest.TrackPrefix"/>
/// 傳入——本介面的實作不得自行從任何共用設定取字軌。
///
/// 🔴 <b>冪等</b>：<see cref="InvoiceIssueRequest.OrderNo"/> 是加值中心端的「關聯號碼」，實作必須保證同一個
/// <c>OrderNo</c> 重複開立只會得到同一張憑證（不重複開票，規劃書 §4.2 冪等性硬性要求）。
/// 技術性失敗（連線、逾時、廠商 5xx）丟 <see cref="InvoiceIssuerUnavailableException"/>，呼叫端會重試；
/// 廠商明確拒絕（欄位格式錯誤等）丟 <see cref="InvoiceRejectedException"/>，不重試、直接標記失敗進人工佇列。
/// </summary>
public interface IInvoiceIssuer
{
    Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken);

    /// <summary>作廢（當期內，§5.4）。已作廢的憑證不得重複作廢（呼叫端先檢查狀態）。</summary>
    Task VoidAsync(string invoiceNo, string reason, CancellationToken cancellationToken);

    /// <summary>折讓（跨期，§5.4）。</summary>
    Task AllowanceAsync(string invoiceNo, int amount, string reason, CancellationToken cancellationToken);

    /// <summary>查詢憑證在加值中心的狀態（對帳與人工補開前確認用）；查無回傳 <c>null</c>。</summary>
    Task<InvoiceQueryResult?> QueryAsync(string invoiceNo, CancellationToken cancellationToken);
}

public enum InvoiceKind
{
    /// <summary>電子發票（B2C）。</summary>
    B2cInvoice,

    /// <summary>捐贈收據。</summary>
    DonationReceipt,
}

/// <param name="OrderNo">加值中心端的關聯號碼，冪等依據。</param>
/// <param name="TrackPrefix">協會自己的字軌（來自慈善庫 <c>payment_channels.invoice_prefix</c>）。</param>
/// <param name="CarrierType"><c>mobile_carrier</c>／<c>love_code</c>／<c>tax_id</c>（只有 B2C 有意義）。</param>
/// <param name="CarrierId">手機條碼或捐贈碼（明文，只在這次呼叫的記憶體裡存在）。</param>
/// <param name="NationalId">捐贈收據的身分證字號（明文，同上；⛔ 不得寫進日誌）。</param>
public sealed record InvoiceIssueRequest(
    InvoiceKind Kind,
    string OrderNo,
    int Amount,
    string TrackPrefix,
    string BuyerName,
    string? CarrierType,
    string? CarrierId,
    string? TaxId,
    string? Title,
    string? NationalId,
    string? Address,
    bool IsAnnualSummary);

public sealed record InvoiceIssueResult(string InvoiceNo, DateTime IssuedAtUtc, string RawResponse);

public sealed record InvoiceQueryResult(string InvoiceNo, string Status, DateTime? IssuedAtUtc);

public sealed class InvoiceIssuerUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class InvoiceRejectedException(string message) : Exception(message);

public sealed class InvoiceIssuerNotConfiguredException(string message) : Exception(message);
