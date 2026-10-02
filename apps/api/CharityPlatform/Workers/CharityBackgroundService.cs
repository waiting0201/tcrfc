using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Reconciliation;

namespace Tcrfc.Api.CharityPlatform.Workers;

/// <summary>
/// 定時執行 <see cref="CharityMaintenanceRunner"/>（逾時轉換、憑證重試）。每輪開一個新的 DI scope（DbContext 是 scoped）。
/// 任何例外只記錄、不讓背景服務終止（.NET 預設 <c>BackgroundServiceExceptionBehavior.StopHost</c> 會讓整個 API 行程停機，
/// 慈善的背景工作失敗不該拖垮同一個行程裡的俱樂部 API）。可用 <c>CHARITY_WORKERS_ENABLED=false</c> 整個關掉；Development 預設關閉（見 <see cref="CharityOptions.WorkersEnabled"/>）。
/// </summary>
public sealed class CharityBackgroundService(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<CharityBackgroundService> logger)
    : BackgroundService
{
    /// <summary>
    /// 每日對帳（規劃書 §4.5）：台灣時間過了設定的整點後對前一天對帳一次。獨立一個 try 區塊，且<b>不放進</b>
    /// <see cref="CharityMaintenanceRunner.RunOnceAsync"/>——那個方法會被整合測試直接呼叫，對帳會把測試跑在共用本機庫時的副作用（寫對帳批次）帶進每個測試。
    /// 取不到金流明細只記警告（批次已記成失敗，排程 30 分鐘後再試），不讓背景服務終止。
    /// </summary>
    private async Task RunDailyReconciliationAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var summary = await scope.ServiceProvider.GetRequiredService<CharityReconciliationRunner>().RunIfDueAsync(stoppingToken);
            if (summary is not null)
            {
                logger.LogInformation("每日對帳完成：{Date} 比對 {Compared} 筆、差異 {Discrepancies} 筆", summary.RunOn, summary.ComparedCount, summary.DiscrepancyCount);
            }
        }
        catch (Exception ex) when (ex is PaymentGatewayUnavailableException or PaymentGatewayNotConfiguredException)
        {
            logger.LogWarning(ex, "每日對帳取不到金流交易明細，稍後再試");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "每日對帳發生未預期錯誤，下一輪再試");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!CharityOptions.WorkersEnabled(configuration, environment))
        {
            logger.LogInformation("慈善背景工作未啟用（{Key}；Development 預設關閉，需明確設 true）", CharityOptions.WorkersEnabledConfigKey);
            return;
        }

        var interval = CharityOptions.ResolveWorkerInterval(configuration);
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var runner = scope.ServiceProvider.GetRequiredService<CharityMaintenanceRunner>();
                    var result = await runner.RunOnceAsync(stoppingToken);
                    if (result.Expired + result.InvoicesIssued + result.InvoicesFailed > 0)
                    {
                        logger.LogInformation("慈善維護完成：逾時 {Expired}、憑證開立 {Issued}、憑證失敗 {Failed}、待重試 {Retry}",
                            result.Expired, result.InvoicesIssued, result.InvoicesFailed, result.InvoicesRetryLater);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "慈善背景維護發生未預期錯誤，下一輪再試");
                }

                await RunDailyReconciliationAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 行程關閉，正常結束。
        }
    }
}
