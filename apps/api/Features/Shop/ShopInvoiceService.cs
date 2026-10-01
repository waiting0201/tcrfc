using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminShop;

namespace Tcrfc.Api.Features.Shop;

/// <summary>
/// 站內商店電子發票的開立與重試（主站規劃書 §4.13 S6「開立與作廢的重試設定」）。
/// 結帳時只<b>記下顧客選的發票方式</b>（<c>store_invoices</c> 一列、<c>issue_status = pending</c>，載具號碼以 Data Protection 加密）；<b>付款確認後</b>才呼叫 <see cref="IInvoiceIssuer"/> 開立。
/// 🔴 字軌一律取收款主體（俱樂部）名下的 <c>einvoice</c> 通道，<b>絕不與慈善平台的協會字軌共用</b>；沒有設定字軌就開不出來，記為 <c>failed</c> 並由重試作業再試。
/// 開立失敗<b>不影響付款與訂單</b>：訂單照常進入備貨，發票由重試補開（上限與間隔用 S6「發票重試」設定，預設 3 次／30 分鐘）。
/// </summary>
public sealed class ShopInvoiceService(
    ClubDbContext db, IInvoiceIssuer issuer, IDataProtectionProvider protection, ClubSettingsStore settingsStore, ILogger<ShopInvoiceService> logger)
{
    private const string Purpose = "Tcrfc.Shop.InvoiceCarrier.v1";

    public string ProtectCarrier(string carrierId) => protection.CreateProtector(Purpose).Protect(carrierId);

    private string? UnprotectCarrier(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
        {
            return null;
        }

        try
        {
            return protection.CreateProtector(Purpose).Unprotect(encrypted);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            logger.LogError("載具號碼解密失敗（Data Protection 金鑰環可能遺失）。");
            return null;
        }
    }

    /// <summary>開立這張（已付款）訂單的發票。冪等：已開立直接回傳號碼；沒有發票資料列回 null。</summary>
    public async Task<string?> TryIssueAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var invoice = await db.StoreInvoices.AsNoTracking().Where(i => i.OrderId == orderId).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        if (invoice.IssueStatus == "issued")
        {
            return invoice.InvoiceNo;
        }

        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).FirstAsync(o => o.Id == orderId, cancellationToken);
        if (order.PaymentStatus != "paid")
        {
            return null; // 沒付款的訂單不開發票
        }

        var channel = await FindChannelAsync(order.CollectingClubId, cancellationToken);
        InvoiceIssueResult result;
        try
        {
            result = await issuer.IssueAsync(new InvoiceIssueRequest(
                order.OrderNo, channel?.InvoicePrefix ?? string.Empty, order.Total, invoice.CarrierType, UnprotectCarrier(invoice.CarrierIdEncrypted), invoice.TaxId, invoice.DonationCode,
                order.OrderItems.Select(i => new InvoiceIssueItem(i.ProductNameSnapshot, i.Quantity, i.UnitPriceSnapshot)).ToList()), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "訂單 {OrderNo} 開立發票時發生例外，改由重試作業再試。", order.OrderNo);
            result = new InvoiceIssueResult(false, null, "發票服務呼叫失敗");
        }

        var now = DateTime.UtcNow;
        if (result.Success && !string.IsNullOrWhiteSpace(result.InvoiceNo))
        {
            await db.StoreInvoices.Where(i => i.Id == invoice.Id && i.IssueStatus != "issued")
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.InvoiceNo, result.InvoiceNo).SetProperty(i => i.IssuedAt, now).SetProperty(i => i.IssueStatus, "issued")
                    .SetProperty(i => i.PaymentChannelId, channel == null ? (Guid?)null : channel.Id).SetProperty(i => i.UpdatedAt, now), cancellationToken);
            return result.InvoiceNo;
        }

        logger.LogWarning("訂單 {OrderNo} 發票開立失敗：{Reason}", order.OrderNo, result.FailureReason);
        await db.StoreInvoices.Where(i => i.Id == invoice.Id && i.IssueStatus != "issued")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IssueStatus, "failed").SetProperty(i => i.RetryCount, i => i.RetryCount + 1).SetProperty(i => i.UpdatedAt, now), cancellationToken);
        return null;
    }

    /// <summary>重試作業：已付款但發票尚未開立（<c>pending</c>／<c>failed</c>）、重試次數未達上限、距上次嘗試已超過間隔的訂單。回傳本輪成功開立的張數。</summary>
    public async Task<int> RetryDueAsync(CancellationToken cancellationToken)
    {
        var due = await db.StoreInvoices.AsNoTracking()
            .Where(i => i.IssueStatus != "issued" && i.InvoiceNo == null && i.Order.PaymentStatus == "paid")
            .Select(i => new { i.OrderId, i.RetryCount, i.UpdatedAt, CollectingClubId = i.Order.CollectingClubId }).Take(200).ToListAsync(cancellationToken);
        var issued = 0;
        foreach (var d in due)
        {
            var map = await settingsStore.GetManyAsync(d.CollectingClubId, [ShopSettingKeys.InvoiceRetryMax, ShopSettingKeys.InvoiceRetryIntervalMinutes], cancellationToken);
            var max = int.TryParse(map.GetValueOrDefault(ShopSettingKeys.InvoiceRetryMax), out var m) ? m : 3;
            var interval = int.TryParse(map.GetValueOrDefault(ShopSettingKeys.InvoiceRetryIntervalMinutes), out var iv) ? iv : 30;
            // 第一次嘗試（RetryCount=0）在付款確認時已做過；pending 且從未嘗試過的（付款確認時服務剛好不可用）也在這裡補。
            if (d.RetryCount >= max || d.UpdatedAt > DateTime.UtcNow.AddMinutes(-interval))
            {
                continue;
            }

            if (await TryIssueAsync(d.OrderId, cancellationToken) is not null)
            {
                issued++;
            }
        }

        return issued;
    }

    private async Task<Data.EfEntities.PaymentChannel?> FindChannelAsync(Guid collectingClubId, CancellationToken cancellationToken)
    {
        var map = await settingsStore.GetManyAsync(collectingClubId, [ShopSettingKeys.PaymentEnvironment], cancellationToken);
        var environment = map.GetValueOrDefault(ShopSettingKeys.PaymentEnvironment) is "production" ? "production" : "sandbox";
        return await db.PaymentChannels.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OwnerClubId == collectingClubId && c.ChannelType == "einvoice" && c.Environment == environment, cancellationToken);
    }
}
