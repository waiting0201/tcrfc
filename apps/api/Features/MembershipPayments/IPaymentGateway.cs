using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.MembershipPayments;

public sealed record PaymentReserveRequest(string OrderNo, int Amount, string ProductName);

/// <summary><see cref="TransactionId"/>：金流方的交易識別（LINE Pay 的 <c>transactionId</c>）；<see cref="PaymentUrl"/>：要把使用者導過去付款的網址。</summary>
public sealed record PaymentReservation(string TransactionId, string PaymentUrl);

public sealed record PaymentConfirmation(bool Success, string? FailureReason);

/// <summary>
/// 會籍付款的金流接縫（E 批，2026-10-01；App 規劃書 §5.4 兩段式流程、§5.2 收款主體）。
/// <b>LINE Pay 俱樂部商店號尚未取得（STATUS B-10）</b>，所以只定義介面，預設註冊 <see cref="NotConfiguredPaymentGateway"/>（呼叫就回 503「付款尚未啟用」，不假裝成功）；
/// 本機開發與測試註冊 <see cref="LocalFakePaymentGateway"/>，讓「建立訂單 → 請款 → 確認 → 開通」能端到端跑通。
/// 取得商店號後<b>只換 <c>Program.cs</c> 的註冊與這個介面的實作</b>，並依 docs/17 §3 登記出口 IP；訂單、冪等、開通邏輯一行都不用改。
/// 與商店的 <c>ILinePayGateway</c>（<c>Features/AdminShop</c>）刻意分開：兩者日後會共用同一個俱樂部商店號與同一組 HTTP 客戶端，但訂單模型與狀態機不同。
/// 🔴 硬規則（App 規劃書 §5.4）：<b>金額永遠由伺服器依方案重算後傳給金流方</b>；開通以伺服器端向金流方確認的結果為準，<b>不接受用戶端回報「付款成功」</b>。
/// </summary>
public interface IPaymentGateway
{
    /// <summary>付款方式代碼，寫入 <c>membership_orders.payment_method</c>（目前只有 <c>linepay</c>）。</summary>
    string Method { get; }

    /// <summary>false＝尚未串接，請款端點回 503。</summary>
    bool IsConfigured { get; }

    Task<PaymentReservation> ReserveAsync(PaymentReserveRequest request, CancellationToken cancellationToken);

    /// <summary>向金流方確認這筆交易（LINE Pay 的 Confirm API）。<paramref name="amount"/> 是伺服器自己算的金額。</summary>
    Task<PaymentConfirmation> ConfirmAsync(string transactionId, string orderNo, int amount, CancellationToken cancellationToken);
}

public sealed class NotConfiguredPaymentGateway : IPaymentGateway
{
    public string Method => "linepay";

    public bool IsConfigured => false;

    public Task<PaymentReservation> ReserveAsync(PaymentReserveRequest request, CancellationToken cancellationToken)
        => throw new FeatureNotConfiguredException("線上付款尚未啟用，請先依頁面指引完成付款，客服核對後會為你開通。", "payment_not_configured");

    public Task<PaymentConfirmation> ConfirmAsync(string transactionId, string orderNo, int amount, CancellationToken cancellationToken)
        => throw new FeatureNotConfiguredException("線上付款尚未啟用。", "payment_not_configured");
}

/// <summary>
/// 本機假金流：<b>只在 Development 註冊</b>（<c>Program.cs</c>；Production 環境即使設了 <c>PAYMENT_GATEWAY=fake</c> 也不會註冊，啟動時直接丟錯）。
/// 規則：請款回 <c>FAKE-{訂單編號}</c> 與假的付款網址；確認時交易識別必須就是 <c>FAKE-{訂單編號}</c>、金額必須大於 0，否則視為付款失敗——
/// 測試用這個規則模擬「付款成功」與「付款失敗」。<b>絕不會碰任何真實金流。</b>
/// </summary>
public sealed class LocalFakePaymentGateway : IPaymentGateway
{
    public string Method => "linepay";

    public bool IsConfigured => true;

    public Task<PaymentReservation> ReserveAsync(PaymentReserveRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new PaymentReservation($"FAKE-{request.OrderNo}", $"https://fake-linepay.invalid/pay/{request.OrderNo}"));

    public Task<PaymentConfirmation> ConfirmAsync(string transactionId, string orderNo, int amount, CancellationToken cancellationToken)
        => Task.FromResult(amount > 0 && string.Equals(transactionId, $"FAKE-{orderNo}", StringComparison.Ordinal)
            ? new PaymentConfirmation(true, null)
            : new PaymentConfirmation(false, "假金流：交易識別或金額不符"));
}
