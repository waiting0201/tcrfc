using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tcrfc.Api.Features.News;

/// <summary>
/// 定時呼叫 <see cref="ScheduledPublishRunner.PublishDueArticlesAsync"/> 的殼——本身不含任何
/// SQL 或快取邏輯，那些都在 <see cref="ScheduledPublishRunner"/>（分開是為了讓測試能不等計時器、
/// 直接呼叫 runner）。
///
/// 啟動後**立刻執行一次**再進入計時迴圈，理由：容器重建／服務重啟期間任何原本該發布的文章都會
/// 被延後到服務恢復後的第一輪執行，立刻跑一次能把這段空窗盡量縮短，而不是先乾等一個完整輪詢
/// 間隔。
/// </summary>
public sealed class ScheduledPublishBackgroundService(
    ScheduledPublishRunner runner,
    IConfiguration configuration,
    ILogger<ScheduledPublishBackgroundService> logger) : BackgroundService
{
    /// <summary>預設輪詢間隔（秒）。docs/17-deployment.md「排程」一項只定案「用 hosted service」，
    /// 沒有給數字——這裡的取捨：60 秒讓「排定 10:00 發布」的使用者觀感上等同準時（比舊有
    /// 「完全靠 5 分鐘 TTL 兜底」的取捨精細很多，見 apps/api/README.md「排程發布與快取」），
    /// 同時遠低於觸發 DTU 或連線數壓力的門檻（單一 UPDATE ＋ 條件式 0 筆命中的查詢成本可忽略）。
    /// 可用 <c>SCHEDULED_PUBLISH_INTERVAL_SECONDS</c> 覆寫。</summary>
    public const int DefaultIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue<int?>("SCHEDULED_PUBLISH_INTERVAL_SECONDS") ?? DefaultIntervalSeconds;
        if (intervalSeconds < 1)
        {
            // 防呆：0 或負數會讓 PeriodicTimer 建構子直接丟例外，讓整支服務啟動失敗——
            // 一個環境變數打錯字不該有這麼大的爆炸半徑。
            logger.LogWarning(
                "SCHEDULED_PUBLISH_INTERVAL_SECONDS 設定值 {Value} 無效（必須 >= 1），改用預設值 {Default} 秒。",
                intervalSeconds, DefaultIntervalSeconds);
            intervalSeconds = DefaultIntervalSeconds;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        do
        {
            try
            {
                await runner.PublishDueArticlesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // 正常關機（app.Run() 收到停止訊號），不記成錯誤。
            }
            catch (Exception ex)
            {
                // 🔴 fail-open：這一輪失敗（例如 SQL Server 短暫連不上、重開機期間）不得讓整支
                // hosted service 停止運作——下一輪還有機會補上，跟 docs/17 §4 快取的 fail-open
                // 原則一致（單一失敗不放大成整個排程發布機制停擺）。
                logger.LogError(ex, "排程發布掃描失敗，將於下一輪（{IntervalSeconds} 秒後）重試。", intervalSeconds);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
