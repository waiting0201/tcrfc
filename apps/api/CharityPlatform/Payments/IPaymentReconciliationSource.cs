using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Payments;

/// <summary>
/// 協會 LINE Pay 的「交易明細」來源，每日對帳用（慈善規劃書 §4.5：「每日將本站的 <c>paid</c> 捐款單與 LINE Pay 的交易明細比對」）。
/// 與 <see cref="IPaymentGateway"/> 同屬協會商店號、同一個命名空間、同樣的例外語意，但<b>刻意是另一個介面</b>：
/// 付款三動作是即時的、冪等的、被捐款人流程呼叫；明細是批次的、唯讀的、只被對帳排程與後台手動對帳呼叫。
/// 正式實作（卡 <c>B-7</c>：協會的 LINE Pay 商店號與其交易查詢／結算檔 API）通常與 <see cref="IPaymentGateway"/> 是同一個類別，
/// 只需在 <c>CharityPlatformRegistration</c> 換掉一行 DI 註冊，對帳流程不動。
/// 例外語意：技術性失敗（連線、逾時、5xx、無法解析）→ <see cref="PaymentGatewayUnavailableException"/>；尚未設定 →
/// <see cref="PaymentGatewayNotConfiguredException"/>。呼叫端會把「取不到明細」記成失敗的對帳批次，<b>絕不當成「沒有交易」而判定全部差異</b>。
/// </summary>
public interface IPaymentReconciliationSource
{
    /// <summary>來源代號，寫入 <c>reconciliation_runs.source</c>（同一天同一來源只有一份對帳批次）。</summary>
    string SourceName { get; }

    /// <summary>某個台灣日期當天，金流端「扣款成功」的交易明細。</summary>
    Task<IReadOnlyList<GatewayTransaction>> ListTransactionsAsync(DateOnly taiwanDate, CancellationToken cancellationToken);
}

/// <param name="TransactionId">金流端交易識別碼（對應本站 <c>donation_payments.transaction_id</c>）。</param>
/// <param name="Amount">金流端記錄的金額（元）。</param>
/// <param name="OccurredAtUtc">金流端的交易時間（UTC）。</param>
public sealed record GatewayTransaction(string TransactionId, int Amount, DateTime OccurredAtUtc);

/// <summary>
/// <see cref="IPaymentReconciliationSource"/> 的本機假實作：<b>以本站自己的金流交易紀錄當作「金流端明細」</b>，所以本機對帳永遠一致
/// （只能驗證流程跑得通，驗證不了差異偵測——差異偵測由測試用的可編排替身驗證）。
/// 🔴 只在 <see cref="CharityFakeGuard"/> 允許的環境運作，正式環境一律丟 <see cref="PaymentGatewayNotConfiguredException"/>：
/// 假明細若在正式環境運作，會讓對帳永遠顯示「完全一致」，把真正的差異蓋掉。
/// </summary>
public sealed class FakePaymentReconciliationSource(CharityDbContext db, IHostEnvironment environment, IConfiguration configuration)
    : IPaymentReconciliationSource
{
    public string SourceName => "linepay";

    public async Task<IReadOnlyList<GatewayTransaction>> ListTransactionsAsync(DateOnly taiwanDate, CancellationToken cancellationToken)
    {
        if (!CharityFakeGuard.IsAllowed(environment, configuration))
        {
            throw new PaymentGatewayNotConfiguredException(
                "協會的 LINE Pay 尚未設定，目前環境不允許使用模擬的交易明細（避免對帳永遠顯示一致而蓋掉真正的差異）。");
        }

        var startUtc = TaiwanClock.StartOfDayUtc(taiwanDate);
        var endUtc = TaiwanClock.StartOfDayUtc(taiwanDate.AddDays(1));
        var rows = await db.DonationPayments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Confirmed && p.ConfirmedAt >= startUtc && p.ConfirmedAt < endUtc && p.TransactionId != null)
            .Select(p => new { p.TransactionId, p.Amount, p.ConfirmedAt })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new GatewayTransaction(r.TransactionId!, r.Amount, r.ConfirmedAt!.Value)).ToList();
    }
}
