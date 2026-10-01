using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Invoices;

namespace Tcrfc.Api.CharityPlatform.Workers;

public sealed record CharityMaintenanceResult(int Expired, int InvoicesIssued, int InvoicesFailed, int InvoicesRetryLater);

/// <summary>
/// 慈善平台的定期維護（<see cref="CharityBackgroundService"/> 定時呼叫；測試直接呼叫 <see cref="RunOnceAsync"/>，不用等計時器）：
/// <list type="number">
/// <item><b>逾時</b>：<c>created</c> 超過逾時時間沒付款、<c>pending</c> 最近一次付款發起超過逾時時間沒完成 → <c>expired</c>（規劃書 §4.3，預設 30 分鐘）。
/// 🔴 <b>「待人工處理」的單（<c>pending</c> ＋最近一次付款 <c>failed</c>＝Confirm 結果未知）不會被轉成逾時</b>——那是可能已經扣款的單，
/// 必須等人員確認，不能讓它悄悄變成「逾時」而消失在異常佇列之外。</item>
/// <item><b>憑證重試</b>：已付款但憑證仍 <c>pending</c> 的（付款確認當下的第一次嘗試失敗的）逐筆重試；
/// 超過 <see cref="CharityInvoiceService.RetryMinutesConfigKey"/>（預設 10 分鐘，自付款成功起算）仍失敗就標記 <c>failed</c> 並通知協會（§5.3）。
/// 固定間隔（等於掃描間隔）重試，不是指數退避：資料庫沒有「嘗試次數」欄位，不為此發明欄位（列為待裁決）。</item>
/// </list>
/// 全部是冪等的：條件式更新與 <see cref="CharityInvoiceService.TryIssueAsync"/> 對已完成的項目不會重做；多跑幾次或多個行程同時跑不會出錯
/// （憑證開立靠加值中心以單號為關聯號碼的冪等性，見 <see cref="IInvoiceIssuer"/>）。
/// </summary>
public sealed class CharityMaintenanceRunner(
    CharityDbContext db, CharityInvoiceService invoices, IConfiguration configuration, ILogger<CharityMaintenanceRunner> logger)
{
    private const int InvoiceBatchSize = 50;

    /// <param name="onlyDonations">測試接縫：只處理符合條件的捐款單（整合測試跑在有種子捐款的共用本機庫上，
    /// 不得讓測試改動種子資料）。正式執行永遠傳 <c>null</c>＝處理全部。</param>
    public async Task<CharityMaintenanceResult> RunOnceAsync(CancellationToken cancellationToken, Expression<Func<Donation, bool>>? onlyDonations = null)
    {
        var expired = await ExpireStaleDonationsAsync(onlyDonations, cancellationToken);
        var (issued, failed, retryLater) = await RetryPendingInvoicesAsync(onlyDonations, cancellationToken);
        return new CharityMaintenanceResult(expired, issued, failed, retryLater);
    }

    private async Task<int> ExpireStaleDonationsAsync(Expression<Func<Donation, bool>>? onlyDonations, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-CharityOptions.ResolvePaymentTimeoutMinutes(configuration));
        var donations = onlyDonations is null ? db.Donations : db.Donations.Where(onlyDonations);

        var createdExpired = await donations
            .Where(d => d.Status == DonationStatus.Created && d.CreatedAt < cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DonationStatus.Expired).SetProperty(d => d.UpdatedAt, now), cancellationToken);

        var pendingExpired = await donations
            .Where(d => d.Status == DonationStatus.Pending
                        && d.DonationPayments.OrderByDescending(p => p.Seq).Take(1)
                            .Any(p => p.Status == PaymentStatus.Requested && p.RequestedAt < cutoff))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DonationStatus.Expired).SetProperty(d => d.UpdatedAt, now), cancellationToken);

        return createdExpired + pendingExpired;
    }

    private async Task<(int Issued, int Failed, int RetryLater)> RetryPendingInvoicesAsync(
        Expression<Func<Donation, bool>>? onlyDonations, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(-CharityInvoiceService.ResolveRetryMinutes(configuration));
        var donations = onlyDonations is null ? db.Donations.AsNoTracking() : db.Donations.AsNoTracking().Where(onlyDonations);

        var candidates = await donations
            .Where(d => d.Status == DonationStatus.Paid)
            .SelectMany(d => d.DonationInvoices)
            .Where(i => i.IssueStatus == "pending" && !i.IsAnnualSummary && i.VoidStatus == "none")
            .OrderBy(i => i.Donation.PaidAt)
            .Select(i => new { i.DonationId, i.Donation.PaidAt })
            .Take(InvoiceBatchSize)
            .ToListAsync(cancellationToken);

        int issued = 0, failed = 0, retryLater = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                var pastDeadline = candidate.PaidAt is { } paidAt && paidAt < deadline;
                var outcome = await invoices.TryIssueAsync(candidate.DonationId, forceFinalFailure: pastDeadline, cancellationToken);
                switch (outcome)
                {
                    case InvoiceIssueOutcome.Issued: issued++; break;
                    case InvoiceIssueOutcome.Failed: failed++; break;
                    case InvoiceIssueOutcome.RetryLater: retryLater++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 單筆失敗不能擋住其他筆。
                logger.LogError(ex, "憑證背景重試發生未預期錯誤，捐款 {DonationId}", candidate.DonationId);
            }
        }

        return (issued, failed, retryLater);
    }
}
