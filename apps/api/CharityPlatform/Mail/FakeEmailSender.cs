using Tcrfc.Api.CharityPlatform.Common;

namespace Tcrfc.Api.CharityPlatform.Mail;

/// <summary>
/// <see cref="IEmailSender"/> 的本機假實作：<b>不會真的寄出任何信</b>，只把「寄給誰（遮罩後）、哪一封」寫進日誌，
/// 讓流程端到端跑得通。在 <see cref="CharityFakeGuard"/> 不允許的環境（正式環境）一律丟 <see cref="EmailSendException"/>——
/// 信沒有寄出就要誠實記成 <c>email_logs.status = 'failed'</c>，不能假裝已寄出（捐款人會以為收到感謝信與憑證通知）。
/// 日誌只含遮罩後的收件者與樣板代碼，<b>不含信件本文</b>（本文含單號、金額、姓名）。
/// </summary>
public sealed class FakeEmailSender(
    IHostEnvironment environment, IConfiguration configuration, ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!CharityFakeGuard.IsAllowed(environment, configuration))
        {
            throw new EmailSendException("寄信服務尚未設定，信件未寄出。");
        }

        logger.LogInformation("[假寄信] 樣板 {TemplateCode} → {MaskedRecipient}（未真的寄出）",
            message.TemplateCode, Tcrfc.Api.Common.PiiMasking.MaskEmail(message.ToAddress));
        return Task.CompletedTask;
    }
}
