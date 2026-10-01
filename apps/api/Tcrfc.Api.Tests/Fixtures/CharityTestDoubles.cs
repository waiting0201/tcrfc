using System.Collections.Concurrent;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Security;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 可編排行為的假金流：讓測試能走到「Confirm 結果未知」「金流拒絕」「退款失敗」這些正式假實作
/// （<see cref="FakePaymentGateway"/>）不會產生的分支。每個測試開始時呼叫 <see cref="Reset"/>
/// （整個測試組件停用平行化，單一主機的單例可以安全共用）。
/// </summary>
public sealed class ScriptedPaymentGateway : IPaymentGateway
{
    public enum Mode { Ok, Declined, Unavailable }

    private int _requestCalls;
    private int _confirmCalls;
    private int _refundCalls;

    public Mode RequestMode { get; set; } = Mode.Ok;
    public Mode ConfirmMode { get; set; } = Mode.Ok;
    public bool RefundSucceeds { get; set; } = true;
    public bool RefundUnavailable { get; set; }

    /// <summary>退款呼叫的人工延遲，讓並發測試能確定性地讓多個請求重疊在「金流退款進行中」這個視窗。</summary>
    public TimeSpan RefundDelay { get; set; } = TimeSpan.Zero;

    public int RequestCalls => _requestCalls;
    public int ConfirmCalls => _confirmCalls;
    public int RefundCalls => _refundCalls;
    public ConcurrentQueue<PaymentRequest> Requests { get; } = new();
    public ConcurrentQueue<PaymentConfirmRequest> Confirms { get; } = new();

    public void Reset()
    {
        RequestMode = Mode.Ok;
        ConfirmMode = Mode.Ok;
        RefundSucceeds = true;
        RefundUnavailable = false;
        RefundDelay = TimeSpan.Zero;
        Interlocked.Exchange(ref _requestCalls, 0);
        Interlocked.Exchange(ref _confirmCalls, 0);
        Interlocked.Exchange(ref _refundCalls, 0);
        Requests.Clear();
        Confirms.Clear();
    }

    public Task<PaymentRequestResult> RequestPaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCalls);
        Requests.Enqueue(request);
        if (RequestMode == Mode.Unavailable)
        {
            throw new PaymentGatewayUnavailableException("scripted: request unavailable");
        }

        var tx = $"TEST-TX-{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentRequestResult(tx, $"https://pay.test.invalid/{request.OrderNo}/{tx}", "{\"scripted\":true}"));
    }

    public async Task<PaymentConfirmResult> ConfirmPaymentAsync(PaymentConfirmRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _confirmCalls);
        Confirms.Enqueue(request);
        await Task.Yield();
        return ConfirmMode switch
        {
            Mode.Unavailable => throw new PaymentGatewayUnavailableException("scripted: confirm unavailable"),
            Mode.Declined => new PaymentConfirmResult(PaymentConfirmOutcome.Declined, "9999", "{\"scripted\":\"declined\"}"),
            _ => new PaymentConfirmResult(PaymentConfirmOutcome.Confirmed, "0000", "{\"scripted\":\"confirmed\"}"),
        };
    }

    public async Task<PaymentRefundResult> RefundPaymentAsync(PaymentRefundRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _refundCalls);
        if (RefundDelay > TimeSpan.Zero)
        {
            await Task.Delay(RefundDelay, cancellationToken);
        }

        if (RefundUnavailable)
        {
            throw new PaymentGatewayUnavailableException("scripted: refund unavailable");
        }

        return new PaymentRefundResult(RefundSucceeds, RefundSucceeds ? "0000" : "9998", "{\"scripted\":true}");
    }
}

public sealed class ScriptedInvoiceIssuer : IInvoiceIssuer
{
    public enum Mode { Ok, Unavailable, Rejected }

    private int _issueCalls;
    private int _voidCalls;
    private int _allowanceCalls;

    public Mode IssueMode { get; set; } = Mode.Ok;
    public bool VoidFails { get; set; }

    public int IssueCalls => _issueCalls;
    public int VoidCalls => _voidCalls;
    public int AllowanceCalls => _allowanceCalls;
    public ConcurrentQueue<InvoiceIssueRequest> Requests { get; } = new();

    public void Reset()
    {
        IssueMode = Mode.Ok;
        VoidFails = false;
        Interlocked.Exchange(ref _issueCalls, 0);
        Interlocked.Exchange(ref _voidCalls, 0);
        Interlocked.Exchange(ref _allowanceCalls, 0);
        Requests.Clear();
    }

    public Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _issueCalls);
        Requests.Enqueue(request);
        return IssueMode switch
        {
            Mode.Unavailable => throw new InvoiceIssuerUnavailableException("scripted: unavailable"),
            Mode.Rejected => throw new InvoiceRejectedException("scripted: rejected"),
            _ => Task.FromResult(new InvoiceIssueResult($"{request.TrackPrefix}-{request.OrderNo}", DateTime.UtcNow, "{\"scripted\":true}")),
        };
    }

    public Task VoidAsync(string invoiceNo, string reason, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _voidCalls);
        return VoidFails ? throw new InvoiceIssuerUnavailableException("scripted: void failed") : Task.CompletedTask;
    }

    public Task AllowanceAsync(string invoiceNo, int amount, string reason, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _allowanceCalls);
        return VoidFails ? throw new InvoiceIssuerUnavailableException("scripted: allowance failed") : Task.CompletedTask;
    }

    public Task<InvoiceQueryResult?> QueryAsync(string invoiceNo, CancellationToken cancellationToken)
        => Task.FromResult<InvoiceQueryResult?>(new InvoiceQueryResult(invoiceNo, "issued", null));
}

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();
    public bool Fail { get; set; }

    public void Reset()
    {
        Fail = false;
        Sent.Clear();
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new EmailSendException("scripted: send failed");
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>可編排的人機驗證：預設通過；<see cref="Result"/> 設成 <c>false</c> 模擬驗證不過。記錄收到的權杖與來源 IP。</summary>
public sealed class ScriptedTurnstileVerifier : ITurnstileVerifier
{
    public bool Result { get; set; } = true;
    public ConcurrentQueue<(string? Token, string? RemoteIp)> Calls { get; } = new();

    public void Reset()
    {
        Result = true;
        Calls.Clear();
    }

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        Calls.Enqueue((token, remoteIp));
        return Task.FromResult(Result);
    }
}
