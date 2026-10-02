using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Reconciliation;

public static class ReconciliationRunStatus
{
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class DiscrepancyTypes
{
    /// <summary>本站有（已付款）、金流端沒有。</summary>
    public const string SiteOnly = "site_only";

    /// <summary>金流端有扣款、本站沒有已付款的紀錄。</summary>
    public const string GatewayOnly = "gateway_only";

    /// <summary>兩邊都有，但金額不同。</summary>
    public const string AmountMismatch = "amount_mismatch";
}

public static class DiscrepancyResolution
{
    public const string Pending = "pending";
    public const string Resolved = "resolved";
}

/// <param name="NewDiscrepancies">這次新增的差異筆數（重跑時既有的不重複計）。</param>
/// <param name="AutoResolved">重跑時，先前的待處理差異現在已經一致、自動標記為已處理的筆數。</param>
public sealed record ReconciliationRunSummary(
    Guid RunId, DateOnly RunOn, string Source, string Status, int ComparedCount, int MatchedCount, int DiscrepancyCount,
    int NewDiscrepancies, int AutoResolved);

/// <summary>
/// 每日對帳（規劃書 §4.5）：把某個台灣日期當天本站的 <c>paid</c> 捐款單與金流端的交易明細逐筆比對，
/// 三種差異——<b>本站有金流無</b>（<c>site_only</c>）、<b>金流有本站無</b>（<c>gateway_only</c>）、<b>金額不符</b>（<c>amount_mismatch</c>）——
/// 寫進 <c>reconciliation_discrepancies</c> 進異常佇列供人工處理，<b>批次與差異都保留供稽核、不可刪除</b>（只能更新處理狀態）。
///
/// <b>比對鍵是金流交易識別碼</b>（本站 <c>donation_payments.transaction_id</c> 對金流端 <c>TransactionId</c>）；本站側取「當天付款成立」
/// （<c>paid_at</c>）且狀態為 <c>paid</c> 或 <c>refunded</c> 的捐款——退款是付款之後的事，當天付款的單即使後來退了，金流端當天仍有那筆扣款，
/// 不能因此變成「本站有金流無」的誤報。
///
/// <b>冪等與重跑</b>：<c>(run_on, source)</c> 唯一，同一天重跑更新同一個批次：新的差異新增、既有的差異保留（含人工已處理的決定）、
/// 先前待處理但現在已一致的差異自動標記已處理（附註說明），人工看得到是系統標記的。同一天同一來源以交易層級 <c>sp_getapplock</c> 串行化，
/// 排程與手動對帳同時執行不會寫出兩份。
///
/// 🔴 <b>取不到金流明細 ≠ 沒有交易</b>：來源丟 <see cref="PaymentGatewayUnavailableException"/>／<see cref="PaymentGatewayNotConfiguredException"/> 時，
/// 這一天記成失敗的批次（若已有成功的批次則不覆蓋）並把例外往上丟，<b>絕不拿空清單去比對</b>——那會把當天所有捐款都判成「本站有金流無」。
/// </summary>
public sealed class CharityReconciliationRunner(
    CharityDbContext db, IPaymentReconciliationSource source, IConfiguration configuration, ILogger<CharityReconciliationRunner> logger)
{
    /// <summary>失敗的批次最短多久後排程才會再試一次（避免金流端中斷時每分鐘都打一次）。</summary>
    private static readonly TimeSpan FailedRetryInterval = TimeSpan.FromMinutes(30);

    private sealed record Finding(string Type, Guid? DonationId, string? GatewayTransactionId, int? SiteAmount, int? GatewayAmount)
    {
        public string Key => $"{Type}|{DonationId}|{GatewayTransactionId}";
    }

    public async Task<ReconciliationRunSummary> RunAsync(DateOnly date, Guid? adminUserId, CancellationToken cancellationToken)
    {
        if (date > TaiwanClock.Today)
        {
            throw new AdminValidationException("對帳日期不可以是未來的日期。");
        }

        var sourceName = source.SourceName;
        IReadOnlyList<GatewayTransaction> gateway;
        try
        {
            gateway = await source.ListTransactionsAsync(date, cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayUnavailableException or PaymentGatewayNotConfiguredException)
        {
            logger.LogWarning(ex, "每日對帳取不到金流交易明細，對帳日 {Date}", date);
            await RecordFailureAsync(date, sourceName, adminUserId, cancellationToken);
            throw;
        }

        var startUtc = TaiwanClock.StartOfDayUtc(date);
        var endUtc = TaiwanClock.StartOfDayUtc(date.AddDays(1));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await CharitySqlLocks.TryAcquireAsync(db, transaction, $"charity-reconcile:{date:yyyyMMdd}:{sourceName}", 15_000, cancellationToken))
        {
            throw new CharityConflictException("對帳處理中", "這一天的對帳正在執行，請稍候重新整理頁面確認結果。");
        }

        var siteRows = await db.Donations.AsNoTracking()
            .Where(d => (d.Status == DonationStatus.Paid || d.Status == DonationStatus.Refunded) && d.PaidAt >= startUtc && d.PaidAt < endUtc)
            .Select(d => new
            {
                d.Id,
                d.Amount,
                TransactionId = d.DonationPayments.Where(p => p.Status == PaymentStatus.Confirmed).OrderByDescending(p => p.Seq)
                    .Select(p => p.TransactionId).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var gatewayById = new Dictionary<string, GatewayTransaction>(StringComparer.Ordinal);
        foreach (var tx in gateway)
        {
            gatewayById.TryAdd(tx.TransactionId, tx);
        }

        var findings = new List<Finding>();
        var matched = 0;
        foreach (var row in siteRows)
        {
            if (string.IsNullOrEmpty(row.TransactionId) || !gatewayById.Remove(row.TransactionId, out var gw))
            {
                // 本站有金流無：DDL 約定 gateway_transaction_id 為空（金流端沒有這筆）；本站的交易識別碼可由 donation_id 查到。
                findings.Add(new Finding(DiscrepancyTypes.SiteOnly, row.Id, null, row.Amount, null));
            }
            else if (gw.Amount != row.Amount)
            {
                findings.Add(new Finding(DiscrepancyTypes.AmountMismatch, row.Id, row.TransactionId, row.Amount, gw.Amount));
            }
            else
            {
                matched++;
            }
        }

        // 金流端剩下的：本站沒有對應的已付款單。若本站有這個交易識別碼（例如「確認結果未知」的 pending 單），把捐款單連起來讓人直接點進去處理。
        var leftoverIds = gatewayById.Keys.ToList();
        var linked = leftoverIds.Count == 0
            ? new Dictionary<string, Guid>()
            : (await db.DonationPayments.AsNoTracking()
                .Where(p => p.TransactionId != null && leftoverIds.Contains(p.TransactionId))
                .Select(p => new { p.TransactionId, p.DonationId }).ToListAsync(cancellationToken))
                .GroupBy(p => p.TransactionId!).ToDictionary(g => g.Key, g => g.First().DonationId);
        foreach (var (txId, gw) in gatewayById)
        {
            findings.Add(new Finding(DiscrepancyTypes.GatewayOnly, linked.TryGetValue(txId, out var donationId) ? donationId : null, txId, null, gw.Amount));
        }

        var now = DateTime.UtcNow;
        var run = await db.ReconciliationRuns.SingleOrDefaultAsync(r => r.RunOn == date && r.Source == sourceName, cancellationToken);
        if (run is null)
        {
            run = new ReconciliationRun { Id = Guid.NewGuid(), RunOn = date, Source = sourceName, CreatedAt = now, CreatedBy = adminUserId };
            db.ReconciliationRuns.Add(run);
        }

        var existing = await db.ReconciliationDiscrepancies.Where(d => d.ReconciliationRunId == run.Id).ToListAsync(cancellationToken);
        var existingKeys = existing.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
        var findingKeys = findings.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var f in findings.Where(f => !existingKeys.Contains(f.Key)))
        {
            db.ReconciliationDiscrepancies.Add(new ReconciliationDiscrepancy
            {
                Id = Guid.NewGuid(),
                ReconciliationRunId = run.Id,
                DiscrepancyType = f.Type,
                DonationId = f.DonationId,
                GatewayTransactionId = f.GatewayTransactionId,
                SiteAmount = f.SiteAmount,
                GatewayAmount = f.GatewayAmount,
                ResolutionStatus = DiscrepancyResolution.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = adminUserId,
                UpdatedBy = adminUserId,
            });
            added++;
        }

        var autoResolved = 0;
        foreach (var old in existing.Where(d => d.ResolutionStatus == DiscrepancyResolution.Pending && !findingKeys.Contains(KeyOf(d))))
        {
            old.ResolutionStatus = DiscrepancyResolution.Resolved;
            old.ResolveNote = "重新對帳後兩邊已一致（系統自動標記）";
            old.ResolvedBy = null;
            old.UpdatedAt = now;
            old.UpdatedBy = adminUserId;
            autoResolved++;
        }

        run.Status = ReconciliationRunStatus.Completed;
        run.RanAt = now;
        run.MatchedCount = matched;
        run.DiscrepancyCount = findings.Count;
        run.ComparedCount = matched + findings.Count;
        run.UpdatedAt = now;
        run.UpdatedBy = adminUserId;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReconciliationRunSummary(run.Id, date, sourceName, run.Status, run.ComparedCount, matched, findings.Count, added, autoResolved);

        static string KeyOf(ReconciliationDiscrepancy d) => $"{d.DiscrepancyType}|{d.DonationId}|{d.GatewayTransactionId}";
    }

    /// <summary>
    /// 排程用：台灣時間過了設定的整點（預設 4 點）後，對「昨天」做一次對帳。已有成功批次就略過；失敗的批次 30 分鐘後才再試。
    /// 回傳 <c>null</c> 代表現在不需要跑。取不到明細的例外由呼叫端（背景服務）記錄，不會讓背景服務終止。
    /// </summary>
    public async Task<ReconciliationRunSummary?> RunIfDueAsync(CancellationToken cancellationToken)
    {
        var afterHour = CharityOptions.ResolveReconciliationAfterHour(configuration);
        if (DateTime.UtcNow.AddHours(8).Hour < afterHour)
        {
            return null;
        }

        var date = TaiwanClock.Today.AddDays(-1);
        var sourceName = source.SourceName;
        var latest = await db.ReconciliationRuns.AsNoTracking()
            .Where(r => r.RunOn == date && r.Source == sourceName)
            .Select(r => new { r.Status, r.RanAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (latest is not null
            && (latest.Status == ReconciliationRunStatus.Completed || latest.RanAt > DateTime.UtcNow - FailedRetryInterval))
        {
            return null;
        }

        return await RunAsync(date, null, cancellationToken);
    }

    private async Task RecordFailureAsync(DateOnly date, string sourceName, Guid? adminUserId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var run = await db.ReconciliationRuns.SingleOrDefaultAsync(r => r.RunOn == date && r.Source == sourceName, cancellationToken);
        if (run is { Status: ReconciliationRunStatus.Completed })
        {
            return; // 已有成功的批次：失敗的重跑不覆蓋它
        }

        if (run is null)
        {
            run = new ReconciliationRun { Id = Guid.NewGuid(), RunOn = date, Source = sourceName, CreatedAt = now, CreatedBy = adminUserId };
            db.ReconciliationRuns.Add(run);
        }

        run.Status = ReconciliationRunStatus.Failed;
        run.RanAt = now;
        run.UpdatedAt = now;
        run.UpdatedBy = adminUserId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // 並發下另一個請求剛好建立了同一個批次（唯一鍵）：記錄失敗只是輔助資訊，不能蓋掉原本要丟出去的金流例外。
            logger.LogWarning(ex, "記錄對帳失敗批次時發生衝突，對帳日 {Date}", date);
        }
    }
}
