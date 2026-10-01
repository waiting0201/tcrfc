using Tcrfc.Api.CharityPlatform.Common;

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
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // 行程關閉，正常結束。
        }
    }
}
