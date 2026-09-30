namespace Tcrfc.Api.Features.AdminShop;

public enum GatewayOutcome
{
    Succeeded,

    /// <summary>金流尚未串接（LINE Pay 商店號未到位，B-10）。</summary>
    NotConfigured,
    Failed,
}

public sealed record GatewayRefundRequest(string OrderNo, string? LinepayTransactionId, int Amount);

public sealed record GatewayRefundResult(GatewayOutcome Outcome, string? Reference = null, string? Message = null);

/// <summary>
/// LINE Pay 金流接縫（規劃書 §4.13：付款只用 LINE Pay、退款一律原路退回）。🔴 <b>B-10：LINE Pay 商店號未到位，本期不串接</b>——
/// 預設註冊 <see cref="NotConfiguredLinePayGateway"/>，任何呼叫都回「尚未串接」；日後串接時只需換掉這個實作，訂單狀態機、
/// 庫存與退款紀錄等後台邏輯不需改動。正式串接時呼叫端須符合 docs/17 §3：從有固定出口 IP 的執行環境發出請求、回呼驗簽且冪等。
/// </summary>
public interface ILinePayGateway
{
    Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken);
}

public sealed class NotConfiguredLinePayGateway : ILinePayGateway
{
    public Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new GatewayRefundResult(GatewayOutcome.NotConfigured, null, "LINE Pay 尚未串接（商店號尚未到位），系統暫時無法執行原路退款。"));
}

public enum InvoiceVoidKind
{
    Void,
    Allowance,
}

/// <summary>發票作廢或折讓的處理結果。<c>Manual</c> 為 <c>true</c> 表示服務尚未串接、需人工到發票服務端完成。</summary>
public sealed record InvoiceVoidResult(bool Manual, string? Message = null);

/// <summary>電子發票服務接縫（必開電子發票；退貨須作廢或折讓，稅法要求，不因對外政策而省略）。🔴 本期<b>不串接</b>：預設實作只回「需人工處理」。</summary>
public interface IEInvoiceService
{
    Task<InvoiceVoidResult> VoidOrAllowanceAsync(string invoiceNo, InvoiceVoidKind kind, int amount, CancellationToken cancellationToken);
}

public sealed class NotConfiguredEInvoiceService : IEInvoiceService
{
    public Task<InvoiceVoidResult> VoidOrAllowanceAsync(string invoiceNo, InvoiceVoidKind kind, int amount, CancellationToken cancellationToken)
        => Task.FromResult(new InvoiceVoidResult(true,
            $"電子發票服務尚未串接，已在系統登記{(kind == InvoiceVoidKind.Void ? "作廢" : "折讓")}，請至發票服務端手動處理發票 {invoiceNo}。"));
}
