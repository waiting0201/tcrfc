using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AdminShop;

public enum PaymentConfirmResult
{
    Confirmed,
    AlreadyPaid,
}

/// <summary>取消訂單的結果：已付款的訂單取消後會自動建立一張「已核准、不需退回商品」的全額退款案件，等系統管理員執行退款。</summary>
public sealed record CancelOrderResult(bool RefundRequired, Guid? RefundRequestId);

/// <summary>
/// 訂單狀態機與庫存連動（規劃書 §4.13 S3／S2）：<c>待付款 → 已付款 → 備貨中 → 已出貨 → 已完成</c>；分支 <c>已取消</c>／<c>退貨處理中</c>／<c>已退款</c>。
/// 🔴 <b>每一次狀態轉換都是「帶前置狀態條件的單句更新」</b>（<c>UPDATE … WHERE order_status = 前一個狀態</c>，看影響筆數）——
/// 兩個並行請求（例如金流回呼與後台取消、兩次回呼重送）只有一個會成功，另一個看到 0 筆就知道狀態已變，<b>不會重複扣庫存或重複退款</b>。
/// 這也是「金流冪等」的落點：<see cref="ConfirmPaymentAsync"/> 對已付款的訂單直接回 <see cref="PaymentConfirmResult.AlreadyPaid"/>。
/// 庫存連動：下單保留 → 付款成立扣減（<c>sale</c>）；付款失敗／逾時釋回（<c>release</c>）；已付款取消回補（<c>cancel_restock</c>）。
/// 本類別刻意不注入快取服務（訂單狀態屬「不得讀快取」五類）。<b>金流不串接</b>（LINE Pay 商店號未到位）：付款成立由日後的金流回呼呼叫
/// <see cref="ConfirmPaymentAsync"/>，目前只有整合測試與現場收款（建單即已付款）會走到扣減。
/// </summary>
public sealed class ShopOrderLifecycle(ClubDbContext db, InventoryService inventory)
{
    // ═════════════ 付款成立／逾時 ═════════════

    /// <summary>付款成立（金流回呼呼叫）。冪等：已付款的訂單回 <see cref="PaymentConfirmResult.AlreadyPaid"/>，不重複扣庫存。</summary>
    public async Task<PaymentConfirmResult> ConfirmPaymentAsync(Guid orderId, string? linepayTransactionId, Guid? operatorId, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單。");
        if (order.PaymentStatus == "paid")
        {
            return PaymentConfirmResult.AlreadyPaid;
        }

        if (order.PaymentStatus != "pending" || order.OrderStatus != ShopLabels.Pending)
        {
            throw new AdminConflictException("訂單無法標記為已付款", $"這張訂單目前是「{order.OrderStatus}」，不能再標記為已付款。");
        }

        var now = DateTime.UtcNow;
        var affected = await db.Orders.Where(o => o.Id == orderId && o.PaymentStatus == "pending" && o.OrderStatus == ShopLabels.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, "paid").SetProperty(o => o.OrderStatus, ShopLabels.Paid).SetProperty(o => o.PaidAt, now)
                .SetProperty(o => o.LinepayTransactionId, linepayTransactionId ?? order.LinepayTransactionId)
                .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
        if (affected == 0)
        {
            var state = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.PaymentStatus).FirstAsync(cancellationToken);
            return state == "paid" ? PaymentConfirmResult.AlreadyPaid : throw new AdminConflictException("訂單無法標記為已付款", "這張訂單的狀態剛剛已經改變，請重新整理。");
        }

        foreach (var item in order.OrderItems)
        {
            await inventory.ApplyAsync(new InventoryChange(order.ClubId, item.ProductVariantId, "sale", -item.Quantity, -item.Quantity, "付款成立，扣減庫存", orderId, operatorId), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return PaymentConfirmResult.Confirmed;
    }

    /// <summary>付款失敗或逾時：待付款訂單改為已取消並釋回保留的庫存。已不是待付款（例如剛好付款成立）回 <c>false</c>。</summary>
    public async Task<bool> ExpireAsync(Guid orderId, string reason, string paymentStatus, Guid? operatorId, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var affected = await db.Orders.Where(o => o.Id == orderId && o.PaymentStatus == "pending" && o.OrderStatus == ShopLabels.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, paymentStatus).SetProperty(o => o.OrderStatus, ShopLabels.Cancelled).SetProperty(o => o.CancelledAt, now)
                .SetProperty(o => o.CancelReason, reason).SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
        if (affected == 0)
        {
            return false;
        }

        foreach (var item in order.OrderItems)
        {
            await inventory.ApplyAsync(new InventoryChange(order.ClubId, item.ProductVariantId, "release", 0, -item.Quantity, reason, orderId, operatorId), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>把逾時未付款的訂單全部釋回（規劃書「付款失敗或逾時自動釋回」）。目前沒有背景排程，由後台按鈕或日後的排程呼叫。</summary>
    public async Task<int> ExpireStalePendingAsync(Guid clubId, TimeSpan timeout, Guid? operatorId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var ids = await db.Orders.AsNoTracking()
            .Where(o => o.ClubId == clubId && o.PaymentStatus == "pending" && o.OrderStatus == ShopLabels.Pending && o.CreatedAt < cutoff)
            .Select(o => o.Id).ToListAsync(cancellationToken);
        var count = 0;
        foreach (var id in ids)
        {
            if (await ExpireAsync(id, "付款逾時，自動釋回庫存", "expired", operatorId, cancellationToken))
            {
                count++;
            }
        }

        return count;
    }

    // ═════════════ 後台動作 ═════════════

    /// <summary>已付款 → 備貨中。</summary>
    public async Task PrepareAsync(Guid orderId, Guid? operatorId, CancellationToken cancellationToken)
    {
        var affected = await Transition(orderId, [ShopLabels.Paid], ShopLabels.Preparing, operatorId, cancellationToken);
        if (affected == 0)
        {
            throw await StateConflictAsync(orderId, "開始備貨", "只有「已付款」的訂單可以開始備貨", cancellationToken);
        }
    }

    /// <summary>取消訂單。待付款：釋回保留；已付款／備貨中：回補庫存並自動建立全額退款案件；其餘狀態不可取消。</summary>
    public async Task<CancelOrderResult> CancelAsync(Guid orderId, string reason, Guid? operatorId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單。");
        if (order.OrderStatus == ShopLabels.Pending)
        {
            if (await ExpireAsync(orderId, reason, "expired", operatorId, cancellationToken))
            {
                return new CancelOrderResult(false, null);
            }

            throw await StateConflictAsync(orderId, "取消", "訂單的狀態剛剛已經改變", cancellationToken);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await db.Orders.Where(o => o.Id == orderId && (o.OrderStatus == ShopLabels.Paid || o.OrderStatus == ShopLabels.Preparing))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.OrderStatus, ShopLabels.Cancelled).SetProperty(o => o.CancelledAt, now).SetProperty(o => o.CancelReason, reason)
                .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
        if (affected == 0)
        {
            throw await StateConflictAsync(orderId, "取消", "只有尚未出貨的訂單可以取消；已出貨的訂單請改走退貨退款", cancellationToken);
        }

        foreach (var item in order.OrderItems)
        {
            await inventory.ApplyAsync(new InventoryChange(order.ClubId, item.ProductVariantId, "cancel_restock", item.Quantity, 0, reason, orderId, operatorId), cancellationToken);
        }

        var refund = new RefundRequest
        {
            Id = Guid.NewGuid(), ClubId = order.ClubId, OrderId = orderId, Reason = $"取消訂單：{reason}", Status = "approved", RefundAmount = order.Total,
            NeedsReturn = false, ApprovedBy = operatorId, ReviewNote = "取消未出貨訂單，系統自動建立全額退款案件",
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.RefundRequests.Add(refund);
        foreach (var item in order.OrderItems)
        {
            db.RefundRequestItems.Add(new RefundRequestItem { RefundRequestId = refund.Id, OrderItemId = item.Id, Quantity = item.Quantity });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new CancelOrderResult(true, refund.Id);
    }

    /// <summary>出貨：已付款／備貨中 → 已出貨，並建立（或更新）出貨資料。</summary>
    public async Task ShipAsync(Guid orderId, ShipAdminOrderRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單。");
        var carrier = AdminInput.OptionalText(request.Carrier, "物流商", 32, "carrier");
        var tracking = AdminInput.OptionalText(request.TrackingNo, "物流單號", 64, "trackingNo");
        var branch = AdminInput.OptionalText(request.StoreBranchCode, "門市代碼", 32, "storeBranchCode");
        if (order.DeliveryMethod == "cvs_pickup" && branch is null)
        {
            throw new AdminValidationException("超商取貨請填寫門市代碼。", "storeBranchCode");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var affected = await Transition(orderId, [ShopLabels.Paid, ShopLabels.Preparing], ShopLabels.Shipped, operatorId, cancellationToken);
        if (affected == 0)
        {
            throw await StateConflictAsync(orderId, "出貨", "只有「已付款」或「備貨中」的訂單可以出貨", cancellationToken);
        }

        var now = DateTime.UtcNow;
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        if (shipment is null)
        {
            shipment = new Shipment { Id = Guid.NewGuid(), ClubId = order.ClubId, OrderId = orderId, CreatedAt = now, CreatedBy = operatorId };
            db.Shipments.Add(shipment);
        }

        shipment.Carrier = carrier;
        shipment.TrackingNo = tracking;
        shipment.StoreBranchCode = branch;
        shipment.ShippedAt = now;
        shipment.UpdatedAt = now;
        shipment.UpdatedBy = operatorId;
        if (order.DeliveryMethod == "onsite_pickup")
        {
            // 現場自取：出貨＝備妥待領。
            shipment.PickupStatus = "waiting";
            shipment.PickupDeadlineOn = request.PickupDeadlineOn;
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>出貨後補填或更正物流資料。</summary>
    public async Task<bool> UpdateShipmentAsync(Guid orderId, UpdateAdminShipmentRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        if (shipment is null)
        {
            return false;
        }

        shipment.Carrier = AdminInput.OptionalText(request.Carrier, "物流商", 32, "carrier");
        shipment.TrackingNo = AdminInput.OptionalText(request.TrackingNo, "物流單號", 64, "trackingNo");
        shipment.StoreBranchCode = AdminInput.OptionalText(request.StoreBranchCode, "門市代碼", 32, "storeBranchCode");
        if (request.PickupDeadlineOn is not null)
        {
            shipment.PickupDeadlineOn = request.PickupDeadlineOn;
        }

        shipment.UpdatedAt = DateTime.UtcNow;
        shipment.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>超商取貨：到店通知（只記錄「已通知」時間，本系統不寄任何通知），進入待領取。</summary>
    public async Task ArrivalNotifiedAsync(Guid orderId, DateOnly? deadline, Guid? operatorId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單。");
        if (order.DeliveryMethod != "cvs_pickup" || order.OrderStatus != ShopLabels.Shipped)
        {
            throw new AdminConflictException("無法登記到店通知", "只有已出貨的「超商取貨」訂單可以登記到店通知。");
        }

        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken)
            ?? throw new AdminConflictException("無法登記到店通知", "這張訂單還沒有出貨資料。");
        var now = DateTime.UtcNow;
        shipment.ArrivalNotifiedAt = now;
        shipment.PickupStatus = "waiting";
        shipment.PickupDeadlineOn = deadline ?? shipment.PickupDeadlineOn;
        shipment.UpdatedAt = now;
        shipment.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>送達／領取：已出貨 → 已完成。超商取貨與現場自取同時標記為已領取。</summary>
    public async Task CompleteAsync(Guid orderId, Guid? operatorId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單。");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await db.Orders.Where(o => o.Id == orderId && o.OrderStatus == ShopLabels.Shipped)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, ShopLabels.Completed).SetProperty(o => o.CompletedAt, now)
                .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
        if (affected == 0)
        {
            throw await StateConflictAsync(orderId, "標記完成", "只有「已出貨」的訂單可以標記為完成", cancellationToken);
        }

        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        if (shipment is not null)
        {
            shipment.DeliveredAt = now;
            if (order.DeliveryMethod is "cvs_pickup" or "onsite_pickup")
            {
                shipment.PickupStatus = "picked_up";
            }

            shipment.UpdatedAt = now;
            shipment.UpdatedBy = operatorId;
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    // ═════════════ 內部 ═════════════

    private Task<int> Transition(Guid orderId, string[] from, string to, Guid? operatorId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return db.Orders.Where(o => o.Id == orderId && from.Contains(o.OrderStatus))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, to).SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
    }

    private async Task<AdminConflictException> StateConflictAsync(Guid orderId, string action, string rule, CancellationToken cancellationToken)
    {
        var status = await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.OrderStatus).FirstOrDefaultAsync(cancellationToken);
        return new AdminConflictException("訂單狀態不允許", $"{rule}（這張訂單目前是「{status}」，無法{action}）。");
    }
}
