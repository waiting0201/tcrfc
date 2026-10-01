using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Mail;

/// <summary>系統信樣板代碼（<c>email_templates.code</c>／<c>email_logs.type</c>，值域 4 個，規劃書 §3.5）。</summary>
public static class EmailTemplateCodes
{
    public const string DonationThanks = "donation_thanks";
    public const string InvoiceIssued = "invoice_issued";
    public const string InvoiceFailed = "invoice_failed";
    public const string RefundNotice = "refund_notice";
}

/// <summary>
/// 系統信：套樣板（<c>email_templates</c>＋<c>email_templates_i18n</c>）、寄送、寫 <c>email_logs</c>（規劃書 §3.5：「所有寄送須寫入
/// <c>EmailLog</c>」）。
///
/// 🔴 <b>寄信失敗絕不能影響已經成功的收款或退款</b>：<see cref="SendAsync"/> 吞下所有寄送例外，記成
/// <c>email_logs.status = 'failed'</c>＋警告日誌（不含信件本文與完整 Email）並回傳 <c>false</c>。
/// 🔴 信件語系固定繁體中文：<c>donations</c> 沒有記錄捐款人語系的欄位（規劃書與 docs/16 都沒有），不自己發明——
/// 英文版信件列為待裁決（README）。
///
/// <b>樣板代換</b>：本文與主旨中的 <c>{token}</c> 以 <paramref name="tokens"/> 代換，未知的 <c>{xxx}</c> 原樣保留。
/// 可用 token：<c>{donor_name}</c>、<c>{order_no}</c>、<c>{amount}</c>、<c>{project_name}</c>、<c>{fund_usage}</c>、
/// <c>{invoice_no}</c>、<c>{paid_at}</c>、<c>{refund_reason}</c>、<c>{order_details}</c>（單號／金額／項目／款項用途的整塊摘要）。
/// 樣板本文沒有 <c>{order_details}</c> 時，感謝信與憑證通知會自動在本文後附上這一塊——規劃書要求感謝信「含單號、金額、
/// 項目與款項用途」，不能因為後台編輯樣板時漏寫就少了。
/// </summary>
public sealed class CharityEmailService(CharityDbContext db, IEmailSender sender, ILogger<CharityEmailService> logger)
{
    public const string OrderDetailsToken = "{order_details}";

    public async Task<bool> SendAsync(
        string templateCode, string toAddress, IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken)
    {
        var template = await db.EmailTemplates.AsNoTracking()
            .Where(t => t.Code == templateCode && t.IsActive)
            .Select(t => new
            {
                t.Id,
                Zh = t.EmailTemplatesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => new { i.Subject, i.Body }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (template?.Zh is null)
        {
            // 樣板被停用或缺漏：這是設定問題，不是寄送失敗——不寫 email_logs（沒有嘗試寄送），留警告讓人處理。
            logger.LogWarning("系統信樣板 {TemplateCode} 停用或缺少繁中內容，信件未寄出", templateCode);
            return false;
        }

        var subject = Render(template.Zh.Subject, tokens);
        var body = Render(template.Zh.Body, tokens);
        var wantsDetails = templateCode is (EmailTemplateCodes.DonationThanks or EmailTemplateCodes.InvoiceIssued);
        if (wantsDetails
            && !template.Zh.Body.Contains(OrderDetailsToken, StringComparison.Ordinal)
            && tokens.TryGetValue("order_details", out var details))
        {
            body = body.TrimEnd() + "\n\n" + details;
        }

        var status = "sent";
        try
        {
            await sender.SendAsync(new EmailMessage(templateCode, toAddress, subject, body), cancellationToken);
        }
        catch (Exception ex) when (ex is EmailSendException or HttpRequestException or TimeoutException or OperationCanceledException)
        {
            status = "failed";
            logger.LogWarning(ex, "系統信寄送失敗：樣板 {TemplateCode}", templateCode); // 不含收件者與本文
        }

        db.EmailLogs.Add(new EmailLog
        {
            Id = Guid.NewGuid(),
            EmailTemplateId = template.Id,
            Type = templateCode,
            RecipientEmail = toAddress,
            SentAt = DateTime.UtcNow,
            Status = status,
        });
        await db.SaveChangesAsync(CancellationToken.None);
        return status == "sent";
    }

    /// <summary>感謝信／憑證通知附帶的訂單摘要（單號、金額、項目、款項用途）。純文字，每行一項。</summary>
    public static string BuildOrderDetails(string orderNo, int amount, string projectName, string? fundUsage, string? invoiceNo = null)
    {
        var lines = new List<string>
        {
            $"捐款單號：{orderNo}",
            $"捐款金額：NT$ {amount.ToString("N0", CultureInfo.InvariantCulture)}",
            $"捐款項目：{projectName}",
        };
        if (!string.IsNullOrWhiteSpace(fundUsage))
        {
            lines.Add($"款項用途：{fundUsage}");
        }

        if (!string.IsNullOrWhiteSpace(invoiceNo))
        {
            lines.Add($"憑證號碼：{invoiceNo}");
        }

        return string.Join("\n", lines);
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        var result = template;
        foreach (var (key, value) in tokens)
        {
            result = result.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
