using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Common;

namespace Tcrfc.Api.CharityPlatform.Invoices;

/// <summary>
/// <see cref="IInvoiceIssuer"/> 的本機假實作：憑證號碼由字軌與單號決定性地組出（<c>{字軌}-{單號}</c>），所以對同一個
/// <c>OrderNo</c> 重複開立天然冪等。<b>沒有任何真實的加值中心呼叫</b>，號碼不是合法的電子發票號碼。
/// 🔴 只在 <see cref="CharityFakeGuard"/> 允許的環境運作（正式環境憑空開立發票等於偽造憑證）。
/// </summary>
public sealed class FakeInvoiceIssuer(IHostEnvironment environment, IConfiguration configuration) : IInvoiceIssuer
{
    public Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
    {
        EnsureAllowed();

        var invoiceNo = $"{request.TrackPrefix}-{request.OrderNo}";
        var raw = JsonSerializer.Serialize(new { provider = "fake", invoiceNo, kind = request.Kind.ToString() });
        return Task.FromResult(new InvoiceIssueResult(invoiceNo, DateTime.UtcNow, raw));
    }

    public Task VoidAsync(string invoiceNo, string reason, CancellationToken cancellationToken)
    {
        EnsureAllowed();
        return Task.CompletedTask;
    }

    public Task AllowanceAsync(string invoiceNo, int amount, string reason, CancellationToken cancellationToken)
    {
        EnsureAllowed();
        return Task.CompletedTask;
    }

    public Task<InvoiceQueryResult?> QueryAsync(string invoiceNo, CancellationToken cancellationToken)
    {
        EnsureAllowed();
        return Task.FromResult<InvoiceQueryResult?>(new InvoiceQueryResult(invoiceNo, "issued", null));
    }

    private void EnsureAllowed()
    {
        if (!CharityFakeGuard.IsAllowed(environment, configuration))
        {
            throw new InvoiceIssuerNotConfiguredException(
                "協會的電子發票管道尚未設定，目前環境不允許使用模擬發票（避免在正式環境憑空開立憑證）。");
        }
    }
}
