using System.Text;

namespace Tcrfc.Api.Features.Email;

/// <summary>
/// 表單（10 表單中心）送出後的兩種信（稽核 A-4）：給後台設定的「收件通知 Email」的內部通知，
/// 與給送件者的「自動回覆」。
/// 🔴 內部通知只放題目與答案給營運同仁處理用，不放同意條款欄位；答案長度設上限，避免被灌爆信件。
/// 自動回覆的文案由後台 <c>forms_i18n.auto_reply_body</c> 維護，這裡只負責主旨與外框。
/// </summary>
public static class FormEmailTemplates
{
    private const int MaxAnswerLength = 2000;

    public sealed record Answer(string Label, string Value);

    public static EmailMessage StaffNotification(string to, string clubName, string formNameZh, Guid enquiryId, IReadOnlyList<Answer> answers)
    {
        var body = new StringBuilder()
            .AppendLine($"{clubName} 官網收到一筆新的「{formNameZh}」。")
            .AppendLine("請登入後台的「詢問收件匣」處理。")
            .AppendLine()
            .AppendLine($"收件編號：{enquiryId}")
            .AppendLine();
        foreach (var a in answers)
        {
            var value = a.Value.Length > MaxAnswerLength ? a.Value[..MaxAnswerLength] + "…" : a.Value;
            body.AppendLine($"{a.Label}：{value}");
        }

        return new EmailMessage(to, $"【{clubName}】官網新的{formNameZh}", body.ToString(), "form_notify");
    }

    public static EmailMessage AutoReply(string to, string clubName, string autoReplyBody, string lang)
        => lang == "en"
            ? new EmailMessage(to, $"[{clubName}] We have received your submission", autoReplyBody.TrimEnd() + "\n", "form_auto_reply")
            : new EmailMessage(to, $"【{clubName}】我們已收到你的來信", autoReplyBody.TrimEnd() + "\n", "form_auto_reply");
}
