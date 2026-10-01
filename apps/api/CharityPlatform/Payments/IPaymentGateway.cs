namespace Tcrfc.Api.CharityPlatform.Payments;

/// <summary>
/// 協會的 LINE Pay Online API 接縫（慈善規劃書 §4）。<b>收款主體是台灣足球策略發展協會，不是俱樂部</b>——
/// 這個介面屬於慈善平台自己的命名空間，<b>不得與主站（俱樂部商店）的金流介面共用</b>：商店號、憑證、出口 IP 登記
/// 都是各自獨立的（docs/17 §3、慈善 §4 前提）。協會的 LINE Pay 商店號尚未申請（STATUS B-7），因此目前只有
/// <see cref="FakePaymentGateway"/>；正式實作只需實作本介面並以 DI 取代，捐款流程與後台不動。
///
/// 兩段式流程（§4.2）：<see cref="RequestPaymentAsync"/> 取得付款網址 → 使用者付款 → 返回後
/// <see cref="ConfirmPaymentAsync"/> 確認扣款（<c>confirmUrlType = CLIENT</c>：由伺服器端主動呼叫 Confirm，
/// 不需要為金流回呼開放連入）。
///
/// 🔴 <b>例外語意是介面契約的一部分</b>：
/// <list type="bullet">
/// <item>金流<b>明確</b>回覆失敗（拒絕、逾時未付、使用者取消）→ 回傳結果物件（<see cref="PaymentConfirmOutcome.Declined"/>），不丟例外。</item>
/// <item>技術性失敗（連線中斷、逾時、5xx、回應無法解析）→ 丟 <see cref="PaymentGatewayUnavailableException"/>。
/// 對 <b>Confirm</b> 而言這代表「<b>不知道有沒有扣款</b>」——呼叫端必須把捐款單留在待人工處理的狀態
/// （慈善 §4.3「已扣款但 Confirm 失敗」最嚴重的例外），<b>絕不可當成失敗而讓使用者重付</b>。</item>
/// <item>尚未設定（沒有商店號）→ 丟 <see cref="PaymentGatewayNotConfiguredException"/>。</item>
/// </list>
/// 實作必須對同一個交易重複 <c>Confirm</c> 保持冪等（LINE Pay 對已完成交易再次 Confirm 會回「已確認」，
/// 實作應轉成 <see cref="PaymentConfirmOutcome.Confirmed"/>）。
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentRequestResult> RequestPaymentAsync(PaymentRequest request, CancellationToken cancellationToken);

    Task<PaymentConfirmResult> ConfirmPaymentAsync(PaymentConfirmRequest request, CancellationToken cancellationToken);

    /// <summary>全額退款（§4.4：部分退款不在範圍）。一律由後台人員發起。</summary>
    Task<PaymentRefundResult> RefundPaymentAsync(PaymentRefundRequest request, CancellationToken cancellationToken);
}

/// <param name="OrderNo">內部單號，對應金流端的 orderId（金流端要求唯一）。</param>
/// <param name="ProductName">顯示在 LINE Pay 付款頁的品名（項目名稱）。</param>
/// <param name="Lang">使用者語系（<c>zh</c>／<c>en</c>），決定返回網址的語系前綴。</param>
/// <param name="Attempt">這張捐款單第幾次發起付款（從 1 起算）。LINE Pay 要求 orderId 唯一，重試沿用原單時，
/// 正式實作要用它組出不重複的金流端 orderId（例如 <c>{OrderNo}-{Attempt}</c>），不能把同一個 orderId 送兩次。</param>
public sealed record PaymentRequest(string OrderNo, int Attempt, int Amount, string ProductName, string Lang, string ConfirmUrl, string CancelUrl);

/// <param name="TransactionId">金流端交易識別碼（寫入 <c>donation_payments.transaction_id</c>）。</param>
/// <param name="PaymentUrl">要把使用者導向的付款網址。</param>
/// <param name="RawResponse">金流端原始回應（寫入 <c>donation_payments.raw_response</c>，只存不查；⛔ 不得含任何憑證或卡號）。</param>
public sealed record PaymentRequestResult(string TransactionId, string PaymentUrl, string RawResponse);

public sealed record PaymentConfirmRequest(string OrderNo, string TransactionId, int Amount);

public enum PaymentConfirmOutcome
{
    /// <summary>金流端確認扣款成功。</summary>
    Confirmed,

    /// <summary>金流端明確回覆沒有扣款（拒絕、交易已逾時、使用者取消）。</summary>
    Declined,
}

public sealed record PaymentConfirmResult(PaymentConfirmOutcome Outcome, string? GatewayCode, string RawResponse);

public sealed record PaymentRefundRequest(string OrderNo, string TransactionId, int Amount);

public sealed record PaymentRefundResult(bool Succeeded, string? GatewayCode, string RawResponse);

/// <summary>技術性失敗：連線、逾時、5xx、無法解析的回應。對 Confirm 代表「結果未知」，見 <see cref="IPaymentGateway"/>。</summary>
public sealed class PaymentGatewayUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>金流尚未設定（沒有商店號或憑證），或目前環境不允許使用假實作。</summary>
public sealed class PaymentGatewayNotConfiguredException(string message) : Exception(message);
