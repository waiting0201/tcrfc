namespace Tcrfc.Api.CharityPlatform.Mail;

/// <summary>
/// 慈善平台系統信（規劃書 §3.5 四封：捐款感謝／憑證通知／開立失敗通知／退款通知）的寄送接縫。
/// 寄信服務尚未選定（<c>docs/20 §7</c> 待確認項），目前只有 <see cref="FakeEmailSender"/>（不會真的寄出）。
/// 這個介面屬於慈善自己的命名空間，不與主站的寄信機制共用（慈善是獨立後台、獨立資料庫，且寄件人署名是協會）。
///
/// 技術性失敗丟 <see cref="EmailSendException"/>，呼叫端把它記成 <c>email_logs.status = 'failed'</c>，
/// <b>不得讓寄信失敗影響已經成功的收款</b>（寄信是收款之後的旁路）。
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <param name="TemplateCode"><c>donation_thanks</c>／<c>invoice_issued</c>／<c>invoice_failed</c>／<c>refund_notice</c>。</param>
public sealed record EmailMessage(string TemplateCode, string ToAddress, string Subject, string Body);

public sealed class EmailSendException(string message, Exception? inner = null) : Exception(message, inner);
