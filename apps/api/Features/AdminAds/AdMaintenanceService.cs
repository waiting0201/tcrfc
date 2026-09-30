using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// 廣告與 App 的定時維護作業（App 規劃書 §7.4／§7.6／§10.1）：① 檔期依起訖時間自動推進；② 每日聚合原始事件成日聚合；
/// ③ 清除超過 90 天的原始事件與診斷回報（90 天同時是個資保存期限政策）。三件事都是可重複執行的冪等作業。
/// 由 <see cref="AppMaintenanceBackgroundService"/> 定時呼叫，也可由系統管理員從後台手動觸發（<c>ad.maintenance.run</c>）。
/// 🔴 「聚合失敗須告警，不得靜默跳過」：超過 2 天仍未聚合的事件數回傳在 <see cref="AdMaintenanceResultDto.OverdueUnaggregated"/>，
/// 且清除只會刪「已聚合」的事件——聚合壞掉時原始資料不會被清掉。
/// </summary>
public sealed class AdMaintenanceService(ClubDbContext dbContext, ILogger<AdMaintenanceService> logger)
{
    public const int RawRetentionDays = 90;

    public async Task<AdMaintenanceResultDto> RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (started, ended) = await AdCampaignLifecycle.AdvanceAsync(dbContext, now, cancellationToken);
        var (aggregated, days) = await AggregateAsync(now, cancellationToken);
        var (purged, diagnostics) = await PurgeAsync(now, cancellationToken);
        var overdue = await dbContext.AdEvents.CountAsync(e => e.AggregatedAt == null && e.ReceivedAt < now.AddDays(-2), cancellationToken);
        if (overdue > 0)
        {
            logger.LogWarning("廣告事件聚合落後：有 {Count} 筆事件超過 2 天仍未聚合，請檢查聚合作業。", overdue);
        }

        return new AdMaintenanceResultDto
        {
            CampaignsStarted = started, CampaignsEnded = ended, EventsAggregated = aggregated, DaysRebuilt = days,
            EventsPurged = purged, DiagnosticsPurged = diagnostics, OverdueUnaggregated = overdue,
        };
    }

    /// <summary>把尚未聚合的事件依「台灣當地日期」聚合到 <c>ad_daily_stats</c>。有未聚合事件的日期整天重算（原始事件保留 90 天，可重算）。</summary>
    public async Task<(int Events, int Days)> AggregateAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        // 只重算保留期內的日期：更早的日期原始事件可能已被部分清除（清除只刪「已聚合」的），整天重算會少算。
        // 那些事件留在原地不聚合、不清除，並由 OverdueUnaggregated 告警，交給人工處理。
        var oldest = utcNow.AddDays(-(RawRetentionDays - 1));
        var pendingTimes = await dbContext.AdEvents.AsNoTracking().Where(e => e.AggregatedAt == null && e.OccurredAt >= oldest)
            .Select(e => e.OccurredAt).ToListAsync(cancellationToken);
        var dates = pendingTimes.Select(TaiwanClock.ToDate).Distinct().OrderBy(d => d).ToList();
        var total = 0;
        foreach (var date in dates)
        {
            var from = TaiwanClock.StartOfDayUtc(date);
            var to = TaiwanClock.StartOfDayUtc(date.AddDays(1));
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM ad_daily_stats WHERE stat_date = {date}", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ad_daily_stats (stat_date, campaign_id, creative_id, slot_id, platform, locale, impressions, clicks, unique_devices)
SELECT {date}, campaign_id, creative_id, slot_id, ISNULL(platform, 'unknown'), ISNULL(locale, 'unknown'),
       SUM(CASE WHEN event_type = 'impression' THEN 1 ELSE 0 END), SUM(CASE WHEN event_type = 'click' THEN 1 ELSE 0 END),
       COUNT(DISTINCT device_install_id)
FROM ad_events WHERE occurred_at >= {from} AND occurred_at < {to}
GROUP BY campaign_id, creative_id, slot_id, ISNULL(platform, 'unknown'), ISNULL(locale, 'unknown')", cancellationToken);
            total += await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ad_events SET aggregated_at = {utcNow} WHERE occurred_at >= {from} AND occurred_at < {to} AND aggregated_at IS NULL", cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        return (total, dates.Count);
    }

    /// <summary>清除超過 90 天且已聚合的原始事件，以及超過 90 天的診斷回報。分批刪除避免長交易。</summary>
    public async Task<(int Events, int Diagnostics)> PurgeAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var cut = utcNow.AddDays(-RawRetentionDays);
        var events = 0;
        int batch;
        do
        {
            batch = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE TOP (5000) FROM ad_events WHERE aggregated_at IS NOT NULL AND occurred_at < {cut}", cancellationToken);
            events += batch;
        }
        while (batch == 5000);

        var diagnostics = await dbContext.AppDiagnosticReports.Where(r => r.ReceivedAt < cut).ExecuteDeleteAsync(cancellationToken);
        return (events, diagnostics);
    }
}
