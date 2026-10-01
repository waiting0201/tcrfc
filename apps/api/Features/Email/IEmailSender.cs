using System.Text;

namespace Tcrfc.Api.Features.Email;

/// <summary>一封要寄出的系統信。<see cref="Kind"/> 只用於日誌與本機輸出檔名（<c>verify</c>／<c>reset</c>／<c>membership_activated</c>），不是資料庫值域。</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string Kind);

/// <summary>
/// 系統信寄送接縫（E 批，2026-10-01；規劃書 §3.14「Email 通知（僅五封）」）。
/// 全系統原本沒有任何寄信通路（<c>email_logs</c> 只是紀錄表），會員前台的註冊驗證、密碼重設、會籍開通確認都需要它，
/// 所以先定義介面、預設註冊「尚未串接」實作，開發環境用寫檔實作；<b>正式的寄信供應商留待部署時決定</b>
/// （Azure Communication Services Email／SendGrid／SMTP…），只換 <c>Program.cs</c> 的註冊與實作，呼叫端不動——見 docs/17 §3「E 批的接縫」。
/// <see cref="SendAsync"/> 回傳 false＝這封信<b>沒有寄出</b>（未串接或供應商拒收）；呼叫端必須如實回報，不得假裝成功。
/// </summary>
public interface IEmailSender
{
    /// <summary>true＝真的會把信送出去（包含「本機寫檔」）；false＝尚未串接。</summary>
    bool IsConfigured { get; }

    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>預設實作：寄信供應商尚未選定。只記一行警告（不含收件人與內容），回傳 false。</summary>
public sealed class NotConfiguredEmailSender(ILogger<NotConfiguredEmailSender> logger) : IEmailSender
{
    public bool IsConfigured => false;

    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning("寄信供應商尚未串接，{Kind} 信件未寄出（docs/17 §3「E 批的接縫」）。", message.Kind);
        return Task.FromResult(false);
    }
}

/// <summary>
/// 本機開發用：把信寫成文字檔（預設 <c>{暫存目錄}/tcrfc-email-outbox</c>，可用 <c>EMAIL_OUTBOX_PATH</c> 指定），讓開發者能點到驗證連結。
/// ⚠️ 信件內容含一次性權杖，<b>只在 Development 註冊</b>（<c>Program.cs</c>；<c>Production</c> 環境即使設了 <c>EMAIL_SENDER=localfile</c> 也不會註冊）；
/// 日誌只記檔名，不記收件人與內容。
/// </summary>
public sealed class LocalFileEmailSender(IConfiguration configuration, ILogger<LocalFileEmailSender> logger) : IEmailSender
{
    public const string OutboxPathConfigKey = "EMAIL_OUTBOX_PATH";

    public bool IsConfigured => true;

    public string OutboxDirectory => configuration[OutboxPathConfigKey] is { Length: > 0 } path
        ? path
        : Path.Combine(Path.GetTempPath(), "tcrfc-email-outbox");

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(OutboxDirectory);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{message.Kind}-{Guid.NewGuid().ToString("N")[..8]}.txt";
        var path = Path.Combine(OutboxDirectory, fileName);
        var content = new StringBuilder()
            .AppendLine($"To: {message.To}")
            .AppendLine($"Subject: {message.Subject}")
            .AppendLine()
            .Append(message.TextBody)
            .ToString();
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken);
        logger.LogInformation("本機開發：{Kind} 信件已寫入 {File}", message.Kind, fileName);
        return true;
    }
}
