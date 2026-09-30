using Tcrfc.Api.Features.AdminAds;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// App 定時維護（docs/17「排程」既有定案：hosted service，同 <c>ScheduledPublishBackgroundService</c> 的形狀）：
/// ① 推播到點發送（已核可且 <c>scheduled_at</c> 已到的批次）；② 廣告檔期依起訖時間推進；③ 廣告事件每日聚合與 90 天清除。
/// 預設每 60 秒一輪；<c>APP_JOBS_INTERVAL_SECONDS</c> 可覆寫，<b>設為 0 停用</b>（整合測試主機停用，避免背景作業與測試資料互相干擾）。
/// fail-open：任何一輪失敗只記警告，下一輪再試；聚合與清除是冪等作業，可以每輪都跑（沒有待處理資料時是幾個空查詢）。
/// 單一 VM 單一 API 行程的部署前提下不需要分散式鎖；若日後改為多副本，推播發送本身有原子領取（<c>UPDATE … WHERE status='scheduled'</c>），其餘作業冪等。
/// </summary>
public sealed class AppMaintenanceBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<AppMaintenanceBackgroundService> logger) : BackgroundService
{
    public const int DefaultIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue<int?>("APP_JOBS_INTERVAL_SECONDS") ?? DefaultIntervalSeconds;
        if (seconds <= 0)
        {
            logger.LogInformation("APP_JOBS_INTERVAL_SECONDS={Seconds}，App 定時維護已停用。", seconds);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PushDispatcher>().DispatchDueAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AdMaintenanceService>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "App 定時維護這一輪失敗，下一輪會再試。");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
