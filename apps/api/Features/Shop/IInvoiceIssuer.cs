using System.Security.Cryptography;

namespace Tcrfc.Api.Features.Shop;

public sealed record InvoiceIssueItem(string Name, int Quantity, int UnitPrice);

/// <summary>開立一張商店電子發票的請求。🔴 <see cref="InvoicePrefix"/> 一律是<b>俱樂部</b>的字軌（<c>payment_channels</c> 中 <c>einvoice</c> 那一列），與慈善平台的協會字軌不得共用。</summary>
public sealed record InvoiceIssueRequest(
    string OrderNo, string InvoicePrefix, int Total, string? CarrierType, string? CarrierId, string? TaxId, string? DonationCode,
    IReadOnlyList<InvoiceIssueItem> Items);

public sealed record InvoiceIssueResult(bool Success, string? InvoiceNo, string? FailureReason);

/// <summary>
/// 站內商店電子發票開立接縫（F 批，2026-10-01；主站規劃書 §3.8 8.3「發票」、§4.13 S6）。
/// 🔴 <b>發票加值中心／發票服務尚未選定與申請（STATUS B-10）</b>，所以只定義介面：正式環境註冊 <see cref="NotConfiguredInvoiceIssuer"/>（回報失敗、不假裝開立）；
/// 本機開發註冊 <see cref="LocalFakeInvoiceIssuer"/>（<b>絕不碰任何發票服務</b>）。取得服務後<b>只換 <c>Program.cs</c> 的註冊與這個介面的實作</b>，
/// 結帳、付款、冪等、重試與通知信一行都不用改。
/// 與 <c>Features/AdminShop</c> 的 <c>IEInvoiceService</c>（退貨時作廢／折讓）刻意分開：一個管「開」、一個管「退」，實際串接同一家服務時可由同一個類別實作兩個介面。
/// 實作須<b>冪等</b>：同一個 <see cref="InvoiceIssueRequest.OrderNo"/> 重複呼叫不得開出兩張發票（真實服務以訂單編號當發票的關聯鍵）。
/// </summary>
public interface IInvoiceIssuer
{
    bool IsConfigured { get; }

    Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken);
}

public sealed class NotConfiguredInvoiceIssuer : IInvoiceIssuer
{
    public bool IsConfigured => false;

    public Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new InvoiceIssueResult(false, null, "電子發票服務尚未串接（STATUS B-10）。"));
}

/// <summary>
/// 本機假發票：<b>只在 Development 註冊</b>（<c>Program.cs</c>；Production 即使設了 <c>INVOICE_ISSUER=fake</c> 也不會註冊，啟動時直接丟錯）。
/// 發票號碼 ＝ 字軌 ＋ 8 位數字（依訂單編號雜湊產生，<b>同一訂單永遠得到同一個號碼</b>，滿足冪等）；沒有字軌（<c>InvoicePrefix</c> 為空）視為失敗，
/// 與真實情境一致——沒有字軌就開不出發票。
/// </summary>
public sealed class LocalFakeInvoiceIssuer : IInvoiceIssuer
{
    public bool IsConfigured => true;

    public Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoicePrefix))
        {
            return Task.FromResult(new InvoiceIssueResult(false, null, "尚未設定電子發票字軌。"));
        }

        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("fake-invoice:" + request.OrderNo));
        var number = (BitConverter.ToUInt32(hash, 0) % 100_000_000).ToString("D8");
        return Task.FromResult(new InvoiceIssueResult(true, request.InvoicePrefix.Trim().ToUpperInvariant() + number, null));
    }
}
