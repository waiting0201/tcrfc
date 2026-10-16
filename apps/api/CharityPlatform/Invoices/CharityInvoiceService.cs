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
        // 🔴 同一筆捐款的開立以應用程式鎖串行化（E-338）：付款確認的贏家、背景重試工作、後台手動重開三條路徑都會呼叫這裡，
        // 「讀到 pending → 呼叫加值中心 → 寫回 issued」若不串行化，會對加值中心重複開立同一張憑證。RCSI 下讀取不會被另一方的寫入擋住，
        // 不能靠讀取阻塞。拿不到鎖＝別的路徑正在開這一張，回 RetryLater（背景工作稍後會看到結果）。
        return await WithInvoiceLockAsync(donationId, InvoiceIssueOutcome.RetryLater,
            () => TryIssueCoreAsync(donationId, forceFinalFailure, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 憑證相關的所有寫入路徑共用的鎖（資源名稱 <c>charity-invoice:{捐款}</c>）：背景開立、後台重開、退款連動作廢、人工登記號碼、人工作廢、人工折讓。
    /// 🔴 為什麼全部要取同一把：開立在「呼叫加值中心」期間持鎖，其他路徑若不取鎖，會在這段窗口內改寫同一張憑證的狀態——
    /// 退款把 pending 標成作廢（沒呼叫加值中心），開立完成後又寫回 issued，加值中心端留下一張有效憑證；人工登記的號碼被背景開立蓋掉，
    /// 同一筆捐款在加值中心有兩個號碼（E-338 後續）。
    /// </summary>
    private static string LockResource(Guid donationId) => $"charity-invoice:{donationId:N}";

    /// <summary>人工操作（後台登記號碼／作廢／折讓／重開）拿不到鎖時等待的毫秒數：0＝不排隊，直接回 409 請操作者稍後重試。</summary>
    private const int ManualLockWaitMilliseconds = 0;

    /// <summary>
    /// 退款連動作廢拿不到鎖時最多等待的毫秒數。🔴 必須大於加值中心呼叫逾時（<c>InvoiceIssuerHttp.Timeout</c>，15 秒）：
    /// 退款當下金流端已經退錢，不能因為背景開立正在跑就放棄作廢；等它跑完（鎖釋放）就會看到 <c>issued</c> 而走真正的作廢／折讓。
    /// </summary>
    internal const int RefundLockWaitMilliseconds = 20_000;

    private async Task<T> WithInvoiceLockAsync<T>(Guid donationId, T lockBusyResult, Func<Task<T>> action, CancellationToken cancellationToken)
        => await WithInvoiceLockAsync(donationId, 0, () => lockBusyResult, action, cancellationToken);

    private async Task<T> WithInvoiceLockAsync<T>(
        Guid donationId, int waitMilliseconds, Func<T> onBusy, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await action(); // 呼叫端已在自己的交易內：沿用，不巢狀開交易
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await CharitySqlLocks.TryAcquireAsync(db, transaction, LockResource(donationId), waitMilliseconds, cancellationToken))
        {
            return onBusy();
        }

        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static CharityConflictException InvoiceBusy()
        => new("憑證處理中", "這筆捐款的憑證正在處理中（開立、作廢或登記號碼），請稍候重新整理頁面確認結果後再操作。");

    private async Task<Guid> DonationIdOfInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
        => await db.DonationInvoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => (Guid?)i.DonationId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這張憑證。");

    private async Task<InvoiceIssueOutcome> TryIssueCoreAsync(Guid donationId, bool forceFinalFailure, CancellationToken cancellationToken)
    {
        var donation = await db.Donations
            .Include(d => d.DonationProject).ThenInclude(p => p.DonationProjectsI18ns)
            .Include(d => d.DonationInvoices)
            .SingleOrDefaultAsync(d => d.Id == donationId, cancellationToken);

        var invoice = donation?.DonationInvoices.OrderByDescending(i => i.Seq).FirstOrDefault();
        if (donation is not null && invoice is not null)
        {
            // 追蹤中的實體不會被查詢覆寫：同一個 DbContext 先前讀過的舊值要在鎖內重新整理，才是「鎖內的真實狀態」。
            await db.Entry(donation).ReloadAsync(cancellationToken);
            await db.Entry(invoice).ReloadAsync(cancellationToken);
        }

        if (donation is null || invoice is null || donation.Status != DonationStatus.Paid)
        {
            return InvoiceIssueOutcome.Skipped;
        }

        if (invoice.IssueStatus == "issued")
        {
            return InvoiceIssueOutcome.Issued;
        }

        if (invoice.VoidStatus != "none")
        {
            return InvoiceIssueOutcome.Skipped; // 已作廢（例如人工作廢了尚未開立的憑證）：不得再對加值中心開立
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
        // 與 TryIssueAsync 同一把鎖：「檢查沒開立 → 改回 pending」若被背景工作的開立插在中間，會把已開立的憑證改回 pending 而重複開立。
        return await WithInvoiceLockAsync(donationId, ManualLockWaitMilliseconds, () => throw InvoiceBusy(),
            () => ReissueCoreAsync(donationId, cancellationToken), cancellationToken);
    }

    private async Task<InvoiceIssueOutcome> ReissueCoreAsync(Guid donationId, CancellationToken cancellationToken)
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
        // 已在 ReissueAsync 的鎖內（呼叫端有交易），所以 TryIssueAsync 會沿用而不再取鎖。
        return await TryIssueAsync(donationId, forceFinalFailure: true, cancellationToken);
    }

    /// <summary>
    /// 退款連動：已開立的憑證當期內作廢、跨期則折讓（規劃書 §5.4）；尚未開立的（<c>pending</c>／<c>failed</c>）不需要對加值中心動作，
    /// 只標記作廢使其不再被背景工作開立。已作廢的不得重複作廢（回傳 <c>false</c>）。
    /// </summary>
    public async Task<bool> VoidOrAllowForRefundAsync(
        Donation donation, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        // 取同一把憑證鎖並<b>等待</b>（不是 409）：退款當下金流端已退錢，不能放棄作廢。若背景開立正在呼叫加值中心，等它提交後就會讀到 issued，
        // 走真正的作廢／折讓；等不到（超過 RefundLockWaitMilliseconds）才丟 409，由呼叫端記錄並進異常佇列，由人員到憑證管理手動作廢。
        return await WithInvoiceLockAsync(donation.Id, RefundLockWaitMilliseconds, () => throw InvoiceBusy(),
            () => VoidOrAllowForRefundCoreAsync(donation, reason, adminUserId, cancellationToken), cancellationToken);
    }

    private async Task<bool> VoidOrAllowForRefundCoreAsync(
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

    /// <summary>
    /// N5「手動填入外部號碼」（規劃書 §6.5、§5.3「失敗重試或手動填入外部號碼」）：加值中心端已另行開立（或人員在加值中心後台補開）時，
    /// 把號碼登記回本站，憑證轉為 <c>issued</c> 並寄出憑證通知。只允許尚未開立（<c>pending</c>／<c>failed</c>）、未作廢、捐款已付款的憑證。
    /// 號碼重複（篩選唯一索引）回 409。
    /// </summary>
    public async Task<DonationInvoice> ManualIssueAsync(
        Guid invoiceId, string invoiceNo, DateTime issuedAtUtc, Guid adminUserId, CancellationToken cancellationToken)
    {
        var donationId = await DonationIdOfInvoiceAsync(invoiceId, cancellationToken);
        return await WithInvoiceLockAsync(donationId, ManualLockWaitMilliseconds, () => throw InvoiceBusy(),
            () => ManualIssueCoreAsync(invoiceId, invoiceNo, issuedAtUtc, adminUserId, cancellationToken), cancellationToken);
    }

    private async Task<DonationInvoice> ManualIssueCoreAsync(
        Guid invoiceId, string invoiceNo, DateTime issuedAtUtc, Guid adminUserId, CancellationToken cancellationToken)
    {
        var invoice = await db.DonationInvoices
            .Include(i => i.Donation).ThenInclude(d => d.DonationProject).ThenInclude(p => p.DonationProjectsI18ns)
            .SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new CharityNotFoundException("找不到這張憑證。");

        if (invoice.IssueStatus == "issued")
        {
            throw new CharityConflictException("憑證已開立", "這張憑證已經有號碼，不需要再填入。");
        }

        if (invoice.VoidStatus != "none")
        {
            throw new CharityConflictException("憑證已作廢", "這張憑證已作廢或折讓，無法再填入號碼。");
        }

        if (invoice.Donation.Status != DonationStatus.Paid)
        {
            throw new CharityConflictException("無法填入", "只有已付款的捐款才能登記憑證號碼。");
        }

        if (await db.DonationInvoices.AsNoTracking().AnyAsync(i => i.InvoiceNo == invoiceNo && i.Id != invoiceId, cancellationToken))
        {
            throw new CharityConflictException("號碼重複", "這個憑證號碼已經登記在另一筆捐款上，請確認號碼是否正確。");
        }

        invoice.InvoiceNo = invoiceNo;
        invoice.IssuedAt = issuedAtUtc;
        invoice.IssueStatus = "issued";
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = adminUserId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new CharityConflictException("號碼重複", "這個憑證號碼已經登記在另一筆捐款上，請確認號碼是否正確。");
        }

        await SendIssuedNoticeAsync(invoice.Donation, invoice, cancellationToken);
        return invoice;
    }

    /// <summary>
    /// N5「作廢」：只限<b>開立當期內</b>（雙月期，規劃書 §5.4）；跨期請改用折讓。尚未開立的憑證（<c>pending</c>／<c>failed</c>）
    /// 不需要對加值中心動作，直接標記作廢使其不再被背景工作開立。加值中心連不上丟 <see cref="InvoiceIssuerUnavailableException"/>（呼叫端轉 503）。
    /// </summary>
    public async Task VoidManuallyAsync(Guid invoiceId, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        var donationId = await DonationIdOfInvoiceAsync(invoiceId, cancellationToken);
        await WithInvoiceLockAsync(donationId, ManualLockWaitMilliseconds, () => throw InvoiceBusy(), async () =>
        {
            await VoidManuallyCoreAsync(invoiceId, reason, adminUserId, cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task VoidManuallyCoreAsync(Guid invoiceId, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        var invoice = await LoadOpenInvoiceAsync(invoiceId, cancellationToken);
        var now = DateTime.UtcNow;
        if (invoice.IssueStatus == "issued" && invoice.InvoiceNo is not null)
        {
            if (invoice.IssuedAt is not { } issuedAt || !CharityDonationRules.IsSameInvoicePeriod(issuedAt, now))
            {
                throw new CharityConflictException("已跨期", "這張憑證已經不在開立當期，無法作廢，請改用折讓。");
            }

            await issuer.VoidAsync(invoice.InvoiceNo, reason, cancellationToken);
        }

        ApplyVoid(invoice, "voided", reason, adminUserId, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>N5「折讓」：已開立的憑證才能折讓（全額，與退款同一套語意，規劃書 §5.4；部分折讓不在範圍）。</summary>
    public async Task AllowanceManuallyAsync(Guid invoiceId, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        var donationId = await DonationIdOfInvoiceAsync(invoiceId, cancellationToken);
        await WithInvoiceLockAsync(donationId, ManualLockWaitMilliseconds, () => throw InvoiceBusy(), async () =>
        {
            await AllowanceManuallyCoreAsync(invoiceId, reason, adminUserId, cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task AllowanceManuallyCoreAsync(Guid invoiceId, string reason, Guid adminUserId, CancellationToken cancellationToken)
    {
        var invoice = await LoadOpenInvoiceAsync(invoiceId, cancellationToken);
        if (invoice.IssueStatus != "issued" || invoice.InvoiceNo is null)
        {
            throw new CharityConflictException("尚未開立", "只有已開立的憑證可以折讓；尚未開立的請直接作廢。");
        }

        await issuer.AllowanceAsync(invoice.InvoiceNo, invoice.Donation.Amount, reason, cancellationToken);
        ApplyVoid(invoice, "allowance", reason, adminUserId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DonationInvoice> LoadOpenInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.DonationInvoices.Include(i => i.Donation).SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new CharityNotFoundException("找不到這張憑證。");
        if (invoice.VoidStatus != "none")
        {
            throw new CharityConflictException("憑證已作廢", "這張憑證已經作廢或折讓過了。");
        }

        return invoice;
    }

    private static void ApplyVoid(DonationInvoice invoice, string voidStatus, string reason, Guid adminUserId, DateTime now)
    {
        invoice.VoidStatus = voidStatus;
        invoice.VoidReason = reason;
        invoice.VoidedBy = adminUserId;
        invoice.UpdatedAt = now;
        invoice.UpdatedBy = adminUserId;
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
        var channelEnvironment = await CharityPaymentEnvironment.ResolveAsync(db, environment, PaymentChannelTypes.EInvoice, cancellationToken);
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
