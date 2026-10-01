using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Common;

namespace Tcrfc.Api.CharityPlatform.Payments;

/// <summary>
/// <see cref="IPaymentGateway"/> 的本機假實作：讓「建單 → 付款 → 確認 → 開票 → 寄信」端到端跑得通，
/// <b>沒有任何真實金流</b>。付款網址指回前台自己的模擬付款頁（<c>/{lang}/pay/{orderNo}</c>，CH-2b 已做），
/// 由前台模擬頁在「付款成功／取消」時呼叫本 API 的確認／取消端點。
///
/// 行為刻意簡單且<b>無狀態</b>（行程重啟後既有的假交易仍可確認）：
/// 交易識別碼以 <c>FAKE-</c> 開頭；<c>FAKE-DECLINE-</c> 開頭的視為金流拒絕（供手動測「付款失敗」分支）；
/// 其餘一律確認成功。退款一律成功。
/// 🔴 只在 <see cref="CharityFakeGuard"/> 允許的環境運作，正式環境一律丟 <see cref="PaymentGatewayNotConfiguredException"/>。
/// </summary>
public sealed class FakePaymentGateway(IHostEnvironment environment, IConfiguration configuration) : IPaymentGateway
{
    public const string TransactionPrefix = "FAKE-";
    public const string DeclinePrefix = "FAKE-DECLINE-";

    public Task<PaymentRequestResult> RequestPaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        EnsureAllowed();

        var transactionId = TransactionPrefix + Guid.NewGuid().ToString("N");
        var baseUrl = CharityOptions.ResolvePublicBaseUrl(configuration, environment) ?? "http://charity.localhost";
        var lang = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        var paymentUrl = $"{baseUrl}/{lang}/pay/{Uri.EscapeDataString(request.OrderNo)}?transactionId={Uri.EscapeDataString(transactionId)}";

        var raw = JsonSerializer.Serialize(new { provider = "fake", transactionId, paymentUrl, amount = request.Amount });
        return Task.FromResult(new PaymentRequestResult(transactionId, paymentUrl, raw));
    }

    public Task<PaymentConfirmResult> ConfirmPaymentAsync(PaymentConfirmRequest request, CancellationToken cancellationToken)
    {
        EnsureAllowed();

        var declined = request.TransactionId.StartsWith(DeclinePrefix, StringComparison.Ordinal)
                       || !request.TransactionId.StartsWith(TransactionPrefix, StringComparison.Ordinal);
        var raw = JsonSerializer.Serialize(new { provider = "fake", transactionId = request.TransactionId, confirmed = !declined });
        return Task.FromResult(new PaymentConfirmResult(
            declined ? PaymentConfirmOutcome.Declined : PaymentConfirmOutcome.Confirmed,
            declined ? "FAKE_DECLINED" : "0000", raw));
    }

    public Task<PaymentRefundResult> RefundPaymentAsync(PaymentRefundRequest request, CancellationToken cancellationToken)
    {
        EnsureAllowed();

        var raw = JsonSerializer.Serialize(new { provider = "fake", transactionId = request.TransactionId, refunded = true });
        return Task.FromResult(new PaymentRefundResult(true, "0000", raw));
    }

    private void EnsureAllowed()
    {
        if (!CharityFakeGuard.IsAllowed(environment, configuration))
        {
            throw new PaymentGatewayNotConfiguredException(
                "協會的 LINE Pay 尚未設定，目前環境不允許使用模擬金流（避免在正式環境憑空確認收款）。");
        }
    }
}
