using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Invoices;

public enum InvoiceIssueOutcome
{
    /// <summary>已開立（或原本就已開立，冪等）。</summary>
    Issued,

    /// <summary>暫時失敗（加值中心不可用／尚未設定），維持待開立，由背景工作稍後重試。</summary>
    RetryLater,

    /// <summary>加值中心明確拒絕，已標記開立失敗並通知協會，進人工補開佇列。</summary>
    Failed,

    /// <summary>不需要現在處理（不是已付款、不是待開立、年度彙總開立）。</summary>
    Skipped,
}

/// <summary>
/// 憑證（電子發票／捐贈收據）的開立、失敗處理與作廢折讓（規劃書 §5）。所有對加值中心的呼叫都經過 <see cref="IInvoiceIssuer"/>。
/// <b>開立時機</b>：捐款單轉 <c>paid</c> 後自動觸發（§5.3）——付款確認那一刻先試一次，失敗的由背景工作
/// （<c>CharityBackgroundRunner</c>）固定間隔重試，超過期限仍失敗才標記 <c>failed</c> 並通知協會。
/// </summary>
public sealed class CharityInvoiceService(
    CharityDbContext db, IInvoiceIssuer issuer, CharityDataProtector protector, CharityEmailService mail,
    IHostEnvironment environment, IConfiguration configuration, ILogger<CharityInvoiceService> logger)
{
    /// <summary>協會接收「開立失敗通知」的信箱（規劃書 §3.5 第 3 封，寄給協會後台人員）。未設定時不寄信，仍會標記失敗進佇列。</summary>
    public const string AssociationNotifyEmailConfigKey = "CHARITY_ASSOCIATION_NOTIFY_EMAIL";

    /// <summary>自動重試的期限（分鐘，自付款成功起算）。超過仍未成功就標記失敗。預設 10 分鐘。</summary>
    public const string RetryMinutesConfigKey = "CHARITY_INVOICE_RETRY_MINUTES";

    public static int ResolveRetryMinutes(IConfiguration configuration)
        => int.TryParse(configuration[RetryMinutesConfigKey], out var v) && v > 0 ? v : 10;

    /// <summary>
    /// 嘗試開立一張憑證。<paramref name="forceFinalFailure"/> 為 <c>true</c>（已超過重試期限）時，暫時性失敗也直接標記失敗。
    /// 冪等：已 <c>issued</c> 的不會再呼叫加值中心。
    /// </summary>
    public async Task<InvoiceIssueOutcome> TryIssueAsync(Guid donationId, bool forceFinalFailure, CancellationToken cancellationToken)
    {
        var donation = await db.Donations
            .Include(d => d.DonationProject).ThenInclude(p => p.DonationProjectsI18ns)
            .Include(d => d.DonationInvoices)
            .SingleOrDefaultAsync(d => d.Id == donationId, cancellationToken);

        var invoice = donation?.DonationInvoices.OrderByDescending(i => i.Seq).FirstOrDefault();
        if (donation is null || invoice is null || donation.Status != DonationStatus.Paid)
        {
            return InvoiceIssueOutcome.Skipped;
        }

        if (invoice.IssueStatus == "issued")
        {
            return InvoiceIssueOutcome.Issued;
        }

        if (invoice.IsAnnualSummary || invoice.IssueStatus is not ("pending" or "failed"))
        {
            return InvoiceIssueOutcome.Skipped; // 年度彙總開立由年底作業統一處理（規劃書 §5.2）
        }

        InvoiceIssueResult result;
        try
        {
            var request = new InvoiceIssueRequest(
                invoice.InvoiceType == InvoiceModes.DonationReceipt ? InvoiceKind.DonationReceipt : InvoiceKind.B2cInvoice,
                donation.OrderNo,
                donation.Amount,
                await ResolveTrackPrefixAsync(cancellationToken),
                donation.DonorName,
                CarrierTypes.Normalize(invoice.CarrierType),
                protector.TryDecryptCarrierId(invoice.CarrierIdEncrypted),
                invoice.TaxId,
                invoice.InvoiceType == InvoiceModes.DonationReceipt ? invoice.ReceiptTitle : invoice.InvoiceTitle,
                protector.TryDecryptNationalId(invoice.NationalIdEncrypted),
                invoice.ReceiptAddress,
                invoice.IsAnnualSummary);

            result = await issuer.IssueAsync(request, cancellationToken);
        }
        catch (InvoiceRejectedException ex)
        {
            logger.LogWarning(ex, "加值中心拒絕開立憑證，單號 {OrderNo}", donation.OrderNo);
            await MarkFailedAsync(donation, invoice, cancellationToken);
            return InvoiceIssueOutcome.Failed;
        }
        catch (Exception ex) when (ex is InvoiceIssuerUnavailableException or InvoiceIssuerNotConfiguredException)
        {
            logger.LogWarning(ex, "憑證開立暫時失敗，單號 {OrderNo}", donation.OrderNo);
            if (!forceFinalFailure)
            {
                return InvoiceIssueOutcome.RetryLater;
            }

            await MarkFailedAsync(donation, invoice, cancellationToken);
            return InvoiceIssueOutcome.Failed;
        }

        invoice.InvoiceNo = result.InvoiceNo;
        invoice.IssuedAt = result.IssuedAtUtc;
        invoice.IssueStatus = "issued";
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await SendIssuedNoticeAsync(donation, invoice, cancellationToken);
        return InvoiceIssueOutcome.Issued;
    }

    /// <summary>後台手動重新開立（規劃書 §5.3「失敗重試或手動」）：把失敗的重置回待開立再試一次，仍失敗就維持失敗。</summary>
    public async Task<InvoiceIssueOutcome> ReissueAsync(Guid donationId, CancellationToken cancellationToken)
    {
        var invoice = await db.DonationInvoices
            .Where(i => i.DonationId == donationId)
            .OrderByDescending(i => i.Seq)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款的憑證資料。");

        if (invoice.IssueStatus == "issued")
        {
            throw new CharityConflictException("憑證已開立", "這筆捐款的憑證已經開立，不需要重新開立。");
        }

        if (invoice.VoidStatus != "none")
        {
            throw new CharityConflictException("憑證已作廢", "這張憑證已作廢或折讓，無法重新開立。");
        }

        invoice.IssueStatus = "pending";
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        // forceFinalFailure=true：手動重開只試一次，失敗就回到失敗狀態讓人看到結果，不要又掛回背景重試。
        return await TryIssueAsync(donationId, forceFinalFailure: true, cancellationToken);
    }

    /// <summary>
    /// 退款連動：已開立的憑證當期內作廢、跨期則折讓（規劃書 §5.4）；尚未開立的（<c>pending</c>／<c>failed</c>）不需要對加值中心動作，
    /// 只標記作廢使其不再被背景工作開立。已作廢的不得重複作廢（回傳 <c>false</c>）。
    /// </summary>
    public async Task<bool> VoidOrAllowForRefundAsync(
        Donation donation, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        var invoice = await db.DonationInvoices
            .Where(i => i.DonationId == donation.Id)
            .OrderByDescending(i => i.Seq)
            .FirstOrDefaultAsync(cancellationToken);
        if (invoice is null || invoice.VoidStatus != "none")
        {
            return false;
        }

        var now = DateTime.UtcNow;
        string newVoidStatus;
        if (invoice.IssueStatus == "issued" && invoice.InvoiceNo is not null)
        {
            var samePeriod = invoice.IssuedAt is { } issuedAt && CharityDonationRules.IsSameInvoicePeriod(issuedAt, now);
            if (samePeriod)
            {
                await issuer.VoidAsync(invoice.InvoiceNo, reason, cancellationToken);
                newVoidStatus = "voided";
            }
            else
            {
                await issuer.AllowanceAsync(invoice.InvoiceNo, donation.Amount, reason, cancellationToken);
                newVoidStatus = "allowance";
            }
        }
        else
        {
            newVoidStatus = "voided";
        }

        invoice.VoidStatus = newVoidStatus;
        invoice.VoidReason = reason;
        invoice.VoidedBy = adminUserId;
        invoice.UpdatedAt = now;
        invoice.UpdatedBy = adminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task MarkFailedAsync(Donation donation, DonationInvoice invoice, CancellationToken cancellationToken)
    {
        invoice.IssueStatus = "failed";
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var notify = configuration[AssociationNotifyEmailConfigKey];
        if (string.IsNullOrWhiteSpace(notify))
        {
            logger.LogWarning("憑證開立失敗但未設定 {Key}，無法寄出通知，請至後台異常佇列處理（單號 {OrderNo}）", AssociationNotifyEmailConfigKey, donation.OrderNo);
            return;
        }

        await mail.SendAsync(
            EmailTemplateCodes.InvoiceFailed, notify,
            new Dictionary<string, string>
            {
                ["order_no"] = donation.OrderNo,
                ["amount"] = donation.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // 失敗通知寄給協會人員，不帶捐款人姓名與 Email（個資最小化；人員到後台以單號查詢）
            },
            cancellationToken);
    }

    private async Task SendIssuedNoticeAsync(Donation donation, DonationInvoice invoice, CancellationToken cancellationToken)
    {
        var project = donation.DonationProject.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var projectName = project?.Name ?? string.Empty;
        await mail.SendAsync(
            EmailTemplateCodes.InvoiceIssued, donation.DonorEmail,
            new Dictionary<string, string>
            {
                ["donor_name"] = donation.DonorName,
                ["order_no"] = donation.OrderNo,
                ["amount"] = donation.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["project_name"] = projectName,
                ["invoice_no"] = invoice.InvoiceNo ?? string.Empty,
                ["order_details"] = CharityEmailService.BuildOrderDetails(donation.OrderNo, donation.Amount, projectName, project?.FundUsage, invoice.InvoiceNo),
            },
            cancellationToken);
    }

    /// <summary>協會自己的字軌（慈善庫 <c>payment_channels.invoice_prefix</c>）。環境依執行環境：正式環境讀 <c>production</c>，其餘讀 <c>sandbox</c>。</summary>
    private async Task<string> ResolveTrackPrefixAsync(CancellationToken cancellationToken)
    {
        var channelEnvironment = environment.IsProduction() ? "production" : "sandbox";
        var prefix = await db.PaymentChannels.AsNoTracking()
            .Where(c => c.ChannelType == "einvoice" && c.Environment == channelEnvironment)
            .Select(c => c.InvoicePrefix)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            return prefix;
        }

        if (!environment.IsProduction())
        {
            return "TEST"; // 本機沒有種子時的保底，只在非正式環境
        }

        throw new InvoiceIssuerNotConfiguredException("協會的發票字軌尚未設定。");
    }
}
