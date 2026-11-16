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

    /// <summary>模擬加值中心的回應時間（毫秒）。並行測試用它拉長「呼叫加值中心」的窗口，讓競態穩定出現。</summary>
    public int IssueDelayMilliseconds { get; set; }

    /// <summary>模擬加值中心作廢／折讓的回應時間（毫秒），用途同 <see cref="IssueDelayMilliseconds"/>。</summary>
    public int VoidDelayMilliseconds { get; set; }

    public int IssueCalls => _issueCalls;
    public int VoidCalls => _voidCalls;
    public int AllowanceCalls => _allowanceCalls;
    public ConcurrentQueue<InvoiceIssueRequest> Requests { get; } = new();

    public void Reset()
    {
        IssueMode = Mode.Ok;
        IssueDelayMilliseconds = 0;
        VoidDelayMilliseconds = 0;
        VoidFails = false;
        Interlocked.Exchange(ref _issueCalls, 0);
        Interlocked.Exchange(ref _voidCalls, 0);
        Interlocked.Exchange(ref _allowanceCalls, 0);
        Requests.Clear();
    }

    public async Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _issueCalls);
        Requests.Enqueue(request);
        if (IssueDelayMilliseconds > 0)
        {
            await Task.Delay(IssueDelayMilliseconds, cancellationToken);
        }

        return IssueMode switch
        {
            Mode.Unavailable => throw new InvoiceIssuerUnavailableException("scripted: unavailable"),
            Mode.Rejected => throw new InvoiceRejectedException("scripted: rejected"),
            _ => new InvoiceIssueResult($"{request.TrackPrefix}-{request.OrderNo}", DateTime.UtcNow, "{\"scripted\":true}"),
        };
    }

    public async Task VoidAsync(string invoiceNo, string reason, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _voidCalls);
        if (VoidDelayMilliseconds > 0)
        {
            await Task.Delay(VoidDelayMilliseconds, cancellationToken);
        }

        if (VoidFails)
        {
            throw new InvoiceIssuerUnavailableException("scripted: void failed");
        }
    }

    public async Task AllowanceAsync(string invoiceNo, int amount, string reason, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _allowanceCalls);
        if (VoidDelayMilliseconds > 0)
        {
            await Task.Delay(VoidDelayMilliseconds, cancellationToken);
        }

        if (VoidFails)
        {
            throw new InvoiceIssuerUnavailableException("scripted: allowance failed");
        }
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

/// <summary>
/// 可編排的每日對帳來源：測試自己決定「金流端那天有哪些交易」，才走得到三種差異與「取不到明細」。
/// 來源代號固定 <c>ct-recon</c>（清理依 <c>source LIKE 'ct-%'</c> 刪除測試產生的對帳批次，不碰種子的 <c>linepay</c> 批次）。
/// </summary>
public sealed class ScriptedReconciliationSource : IPaymentReconciliationSource
{
    public const string Name = "ct-recon";

    public string SourceName => Name;

    public bool Unavailable { get; set; }

    public List<GatewayTransaction> Transactions { get; } = [];

    public void Reset()
    {
        Unavailable = false;
        Transactions.Clear();
    }

    public Task<IReadOnlyList<GatewayTransaction>> ListTransactionsAsync(DateOnly taiwanDate, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new PaymentGatewayUnavailableException("scripted: reconciliation source unavailable");
        }

        return Task.FromResult<IReadOnlyList<GatewayTransaction>>(Transactions.ToList());
    }
}

/// <summary>
/// 可編排的「主站公開清單」：慈善後台「從官網更新清單」（K-2）以 HTTP 呼叫主站，測試不真的打網路，
/// 由這個假的 <see cref="HttpMessageHandler"/> 回應。預設回一份空清單；測試用 <see cref="SetDirectory"/> 指定內容，
/// 或 <see cref="SetStatus"/>／<see cref="SetRaw"/>／<see cref="ThrowOnSend"/> 製造主站回錯、回壞資料、連不上。
/// </summary>
public sealed class ScriptedMainSite : HttpMessageHandler
{
    private int _calls;
    private readonly object _gate = new();
    private Func<HttpResponseMessage> _respond = () => Json("{\"charities\":[],\"programs\":[]}");

    public int Calls => _calls;
    public List<string> RequestedUris { get; } = [];

    /// <summary>每次回應前的人工延遲（讓並發測試能讓多個同步重疊在鎖的視窗內）。</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public void Reset()
    {
        lock (_gate)
        {
            _respond = () => Json("{\"charities\":[],\"programs\":[]}");
            RequestedUris.Clear();
        }

        _calls = 0;
        Delay = TimeSpan.Zero;
    }

    private static HttpResponseMessage Json(string body)
        => new(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    public void SetRaw(string body, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK)
        => _respond = () => new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    public void SetStatus(System.Net.HttpStatusCode status) => _respond = () => new HttpResponseMessage(status);

    public void ThrowOnSend(Exception exception) => _respond = () => throw exception;

    /// <summary>以契約形狀（camelCase、<c>status = published</c>）編排清單。</summary>
    public void SetDirectory(IEnumerable<MainSiteCharity> charities, IEnumerable<MainSiteProgram> programs)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            charities = charities.Select(c => new { id = c.Id, slug = c.Slug, status = c.Status, nameZh = c.NameZh, nameEn = c.NameEn }),
            programs = programs.Select(p => new { id = p.Id, slug = p.Slug, charityId = p.CharityId, status = p.Status, nameZh = p.NameZh, nameEn = p.NameEn }),
        });
        SetRaw(body);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        lock (_gate)
        {
            RequestedUris.Add(request.RequestUri!.ToString());
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        Func<HttpResponseMessage> respond;
        lock (_gate)
        {
            respond = _respond;
        }

        return respond();
    }
}

public sealed record MainSiteCharity(Guid Id, string Slug, string NameZh, string? NameEn, string Status = "published");

public sealed record MainSiteProgram(Guid Id, string Slug, Guid CharityId, string NameZh, string? NameEn, string Status = "published");

/// <summary>把所有具名 <see cref="HttpClient"/> 都導向 <see cref="ScriptedMainSite"/>（測試主機沒有任何真正的外部呼叫）。</summary>
public sealed class ScriptedHttpClientFactory(ScriptedMainSite handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
}
