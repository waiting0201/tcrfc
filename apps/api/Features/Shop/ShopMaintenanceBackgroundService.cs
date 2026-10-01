using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminShop;

namespace Tcrfc.Api.Features.Shop;

/// <summary>
/// 商店定時維護（同 <c>AppMaintenanceBackgroundService</c> 的形狀：hosted service，單一 VM 單一 API 行程的部署前提，docs/17「排程」）：
/// ① <b>逾時未付款的訂單自動取消並釋回庫存</b>（規劃書 §3.8／§4.13：付款未完成的訂單逾時自動取消並釋回庫存）；
/// ② <b>發票開立失敗的重試</b>（§4.13 S6 的重試次數與間隔）；
/// ③ 清掉未付款取消的訂單遺留的「待開立」發票資料列，以及超過 30 天沒動過的訪客購物車。
/// 全部冪等、fail-open（任何一輪失敗只記警告，下一輪再試）。
/// 間隔：<c>SHOP_JOBS_INTERVAL_SECONDS</c>；<b>未設定時 Production 預設 60 秒、其餘環境預設停用（0）</b>——整合測試與本機開發不會有背景作業與測試資料互相干擾；
/// 本機要驗證時明確設成 &gt; 0。<b>設為 0 停用</b>。「讀到時換算」仍然有效（讀取訂單與結帳庫存不足時會先掃一輪），所以停用背景作業不會讓逾時訂單永遠占著庫存被看到。
/// </summary>
public sealed class ShopMaintenanceBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<ShopMaintenanceBackgroundService> logger)
    : BackgroundService
{
    public const int DefaultIntervalSeconds = 60;
    public static readonly TimeSpan GuestCartRetention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue<int?>("SHOP_JOBS_INTERVAL_SECONDS") ?? (environment.IsProduction() ? DefaultIntervalSeconds : 0);
        if (seconds <= 0)
        {
            logger.LogInformation("商店定時維護已停用（SHOP_JOBS_INTERVAL_SECONDS={Seconds}）。", seconds);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ShopMaintenanceService>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "商店定時維護這一輪失敗，下一輪會再試。");
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

/// <summary>一輪商店維護的實際內容（獨立成類別方便測試直接呼叫，不必等計時器）。</summary>
public sealed class ShopMaintenanceService(ClubDbContext db, ShopOrderLifecycle lifecycle, ShopSettingsReader settings, ShopInvoiceService invoices)
{
    public sealed record Result(int ExpiredOrders, int IssuedInvoices, int RemovedInvoiceRows, int RemovedCarts);

    public async Task<Result> RunAsync(CancellationToken cancellationToken)
    {
        var expired = 0;
        var clubIds = await db.Clubs.AsNoTracking().Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var clubId in clubIds)
        {
            var timeout = TimeSpan.FromMinutes(await settings.GetPendingTimeoutMinutesAsync(clubId, cancellationToken));
            expired += await lifecycle.ExpireStalePendingAsync(clubId, timeout, null, cancellationToken);
        }

        var issued = await invoices.RetryDueAsync(cancellationToken);

        // 付款失敗／逾時／取消而沒有付款的訂單，不需要發票：移除尚未開立的資料列。
        var removedInvoices = await db.StoreInvoices
            .Where(i => i.InvoiceNo == null && i.IssueStatus == "pending" && i.Order.PaymentStatus != "pending" && i.Order.PaymentStatus != "paid")
            .ExecuteDeleteAsync(cancellationToken);

        var guestCutoff = DateTime.UtcNow - ShopMaintenanceBackgroundService.GuestCartRetention;
        var staleCartIds = await db.Carts.AsNoTracking().Where(c => c.MemberId == null && c.UpdatedAt < guestCutoff).Select(c => c.Id).Take(500).ToListAsync(cancellationToken);
        var removedCarts = 0;
        if (staleCartIds.Count > 0)
        {
            await db.CartItems.Where(i => staleCartIds.Contains(i.CartId)).ExecuteDeleteAsync(cancellationToken);
            removedCarts = await db.Carts.Where(c => staleCartIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        return new Result(expired, issued, removedInvoices, removedCarts);
    }
}
