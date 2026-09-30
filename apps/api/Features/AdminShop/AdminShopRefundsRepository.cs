using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S5 退貨與退款（規劃書 §4.13 S5）：承接前台以表單或客服信箱進來的退換貨申請（不做專屬的線上退貨精靈）——
/// 案件列表、審核、退回驗收（可回補庫存）、退款執行；支援全額與部分退款。
/// 狀態：<c>requested 申請中 → approved 已核准 → received 已驗收退回品 → processing 退款處理中 → refunded 已退款</c>，另有 <c>rejected 已駁回</c>；
/// 不需退回商品的案件（例如取消未出貨訂單自動建立的）核准後直接可執行退款。
/// 🔴 <b>退款執行只有系統管理員</b>（<c>shop.refund.execute</c>，sysadmin_only）：退款是不可逆的金錢動作。
/// 🔴 <b>防重複退款</b>：執行時先以「帶前置狀態條件的單句更新」把案件搶到 <c>processing</c>，只有一個請求會成功；金流失敗或尚未串接時退回原狀態。
/// 🔴 <b>LINE Pay 不串接</b>（B-10）：LINE Pay 訂單目前無法由系統執行原路退款（回 409 說明原因）；現場收款的訂單以人工退款並登錄經辦人。
/// 發票：已開立的發票同步登記作廢（全額）或折讓（部分）；電子發票服務未串接時註明需人工處理。刻意不注入快取服務。
/// </summary>
public sealed class AdminShopRefundsRepository(
    ClubDbContext db, InventoryService inventory, ILinePayGateway linePay, IEInvoiceService invoices, SensitiveActionLogger audit)
{
    private static readonly string[] OpenStatuses = ["requested", "approved", "received", "processing"];

    // ═════════════ 讀取 ═════════════

    public async Task<PagedResult<AdminRefundListItemDto>> ListAsync(AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.RefundRequests.AsNoTracking().Where(r => r.Order.SellingClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, ShopLabels.Refund.Keys.ToHashSet(), "案件狀態", "申請中、已核准、已驗收退回品、退款處理中、已退款或已駁回");
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(r => r.Order.OrderNo.Contains(k));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(r => r.Status == "requested" ? 0 : r.Status == "approved" || r.Status == "received" ? 1 : 2).ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new { Refund = r, r.Order.OrderNo, r.Order.PaymentMethod }).ToListAsync(cancellationToken);
        return new PagedResult<AdminRefundListItemDto>
        {
            Items = rows.Select(r => new AdminRefundListItemDto
            {
                Id = r.Refund.Id, OrderId = r.Refund.OrderId, OrderNo = r.OrderNo, Status = r.Refund.Status, StatusLabel = ShopLabels.Of(ShopLabels.Refund, r.Refund.Status),
                RefundAmount = r.Refund.RefundAmount, Reason = r.Refund.Reason, NeedsReturn = r.Refund.NeedsReturn, PaymentMethod = r.PaymentMethod,
                PaymentMethodLabel = ShopLabels.Of(ShopLabels.PaymentMethod, r.PaymentMethod), RefundMethod = r.Refund.RefundMethod,
                RefundMethodLabel = r.Refund.RefundMethod is null ? null : ShopLabels.Of(ShopLabels.RefundMethod, r.Refund.RefundMethod),
                CreatedAt = r.Refund.CreatedAt, UpdatedAt = r.Refund.UpdatedAt,
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<AdminRefundDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var refund = await db.RefundRequests.AsNoTracking().Include(r => r.RefundRequestItems).ThenInclude(i => i.OrderItem).Include(r => r.Order)
            .AsSplitQuery().FirstOrDefaultAsync(r => r.Id == id && r.Order.SellingClubId == scope.ClubId, cancellationToken);
        return refund is null ? null : await ToDetailAsync(refund, cancellationToken);
    }

    // ═════════════ 建立 ═════════════

    public async Task<AdminRefundDetailDto> CreateAsync(AdminClubScope scope, CreateAdminRefundRequest request, CancellationToken cancellationToken)
    {
        var reason = AdminInput.RequireText(request.Reason, "退貨原因", 255);
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new AdminValidationException("請選擇要退的品項。");
        }

        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == request.OrderId && o.SellingClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的訂單，請確認訂單屬於目前的俱樂部。");
        if (order.PaymentStatus != "paid" || order.OrderStatus is not (ShopLabels.Shipped or ShopLabels.Completed))
        {
            throw new AdminConflictException("訂單不能申請退貨", order.OrderStatus is ShopLabels.Paid or ShopLabels.Preparing
                ? "這張訂單還沒有出貨，請直接取消訂單（系統會自動建立全額退款案件）。"
                : $"這張訂單目前是「{order.OrderStatus}」，不能申請退貨退款。");
        }

        if (await db.RefundRequests.AsNoTracking().AnyAsync(r => r.OrderId == order.Id && OpenStatuses.Contains(r.Status), cancellationToken))
        {
            throw new AdminConflictException("已有處理中的退款案件", "這張訂單已經有一件處理中的退貨退款案件，請先處理完成或駁回。");
        }

        var previous = await db.RefundRequests.AsNoTracking().Include(r => r.RefundRequestItems).Where(r => r.OrderId == order.Id && r.Status == "refunded").ToListAsync(cancellationToken);
        var refundedQty = previous.SelectMany(r => r.RefundRequestItems).GroupBy(i => i.OrderItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var refundedAmount = previous.Sum(r => r.RefundAmount ?? 0);
        var items = new List<(OrderItem Item, int Quantity)>();
        foreach (var line in request.Items.GroupBy(i => i.OrderItemId).Select(g => (Id: g.Key, Quantity: g.Sum(x => x.Quantity))))
        {
            var item = order.OrderItems.FirstOrDefault(i => i.Id == line.Id)
                ?? throw new AdminValidationException("退貨品項含有不屬於這張訂單的品項。");
            var left = item.Quantity - refundedQty.GetValueOrDefault(item.Id);
            if (line.Quantity < 1 || line.Quantity > left)
            {
                throw new AdminValidationException($"「{item.ProductNameSnapshot}」最多還能退 {left} 件。");
            }

            items.Add((item, line.Quantity));
        }

        var amount = request.RefundAmount ?? items.Sum(x => x.Item.UnitPriceSnapshot * x.Quantity);
        var remaining = order.Total - refundedAmount;
        if (amount < 1 || amount > remaining)
        {
            throw new AdminValidationException($"退款金額必須介於 1 與這張訂單尚可退的 {remaining} 元之間。");
        }

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var affected = await db.Orders.Where(o => o.Id == order.Id && (o.OrderStatus == ShopLabels.Shipped || o.OrderStatus == ShopLabels.Completed))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, ShopLabels.Returning).SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        if (affected == 0)
        {
            throw new AdminConflictException("訂單狀態不允許", "這張訂單的狀態剛剛已經改變，請重新整理後再試。");
        }

        var refund = new RefundRequest
        {
            Id = Guid.NewGuid(), ClubId = order.ClubId, OrderId = order.Id, Reason = reason, Status = "requested", RefundAmount = amount,
            NeedsReturn = request.NeedsReturn ?? true, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.RefundRequests.Add(refund);
        foreach (var (item, quantity) in items)
        {
            db.RefundRequestItems.Add(new RefundRequestItem { RefundRequestId = refund.Id, OrderItemId = item.Id, Quantity = quantity });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await GetAsync(scope, refund.Id, cancellationToken))!;
    }

    // ═════════════ 審核與驗收 ═════════════

    public async Task<AdminRefundDetailDto?> ApproveAsync(AdminClubScope scope, Guid id, ReviewAdminRefundRequest request, CancellationToken cancellationToken)
    {
        var refund = await FindAsync(scope, id, cancellationToken);
        if (refund is null)
        {
            return null;
        }

        var note = AdminInput.OptionalText(request.Note, "審核意見", 500);
        var affected = await db.RefundRequests.Where(r => r.Id == id && r.Status == "requested")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "approved").SetProperty(r => r.ApprovedBy, scope.Identity.AdminUserId)
                .SetProperty(r => r.ReviewNote, note).SetProperty(r => r.UpdatedAt, DateTime.UtcNow).SetProperty(r => r.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        if (affected == 0)
        {
            throw await StatusConflictAsync(id, "核准", "只有「申請中」的案件可以核准", cancellationToken);
        }

        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<AdminRefundDetailDto?> RejectAsync(AdminClubScope scope, Guid id, ReviewAdminRefundRequest request, CancellationToken cancellationToken)
    {
        var refund = await FindAsync(scope, id, cancellationToken);
        if (refund is null)
        {
            return null;
        }

        var note = AdminInput.RequireText(request.Note, "駁回原因", 500);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await db.RefundRequests.Where(r => r.Id == id && (r.Status == "requested" || r.Status == "approved"))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "rejected").SetProperty(r => r.ReviewNote, note).SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        if (affected == 0)
        {
            throw await StatusConflictAsync(id, "駁回", "只有「申請中」或「已核准」的案件可以駁回", cancellationToken);
        }

        await RestoreOrderStatusAsync(refund.OrderId, scope.Identity.AdminUserId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<AdminRefundDetailDto?> ReceiveAsync(AdminClubScope scope, Guid id, ReceiveAdminRefundRequest request, CancellationToken cancellationToken)
    {
        var refund = await db.RefundRequests.AsNoTracking().Include(r => r.RefundRequestItems).ThenInclude(i => i.OrderItem)
            .FirstOrDefaultAsync(r => r.Id == id && r.Order.SellingClubId == scope.ClubId, cancellationToken);
        if (refund is null)
        {
            return null;
        }

        if (!refund.NeedsReturn)
        {
            throw new AdminConflictException("不需要驗收", "這個案件不需要顧客退回商品，核准後可直接執行退款。");
        }

        var note = AdminInput.OptionalText(request.Note, "驗收備註", 500);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await db.RefundRequests.Where(r => r.Id == id && r.Status == "approved")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "received").SetProperty(r => r.ReceivedAt, now).SetProperty(r => r.ReceivedBy, scope.Identity.AdminUserId)
                .SetProperty(r => r.ReviewNote, note ?? refund.ReviewNote).SetProperty(r => r.UpdatedAt, now).SetProperty(r => r.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        if (affected == 0)
        {
            throw await StatusConflictAsync(id, "驗收", "只有「已核准」的案件可以驗收退回品", cancellationToken);
        }

        if (request.Restock ?? true)
        {
            foreach (var line in refund.RefundRequestItems)
            {
                await inventory.ApplyAsync(new InventoryChange(
                    refund.ClubId, line.OrderItem.ProductVariantId, "return_restock", line.Quantity, 0, "退貨驗收，回補庫存", refund.OrderId, scope.Identity.AdminUserId), cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    // ═════════════ 退款執行 ═════════════

    public async Task<AdminRefundExecuteResultDto?> ExecuteAsync(AdminClubScope scope, Guid id, ExecuteAdminRefundRequest request, CancellationToken cancellationToken)
    {
        var refund = await db.RefundRequests.AsNoTracking().Include(r => r.Order).FirstOrDefaultAsync(r => r.Id == id && r.Order.SellingClubId == scope.ClubId, cancellationToken);
        if (refund is null)
        {
            return null;
        }

        var note = AdminInput.OptionalText(request.Note, "備註", 500);
        var from = refund.NeedsReturn ? "received" : "approved";
        var amount = refund.RefundAmount ?? throw new AdminConflictException("案件沒有退款金額", "這個案件沒有退款金額，無法執行退款。");
        // 搶佔：只有一個請求能把案件從「可退款」搶到「退款處理中」，避免重複退款。
        var claimed = await db.RefundRequests.Where(r => r.Id == id && r.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "processing").SetProperty(r => r.UpdatedAt, DateTime.UtcNow).SetProperty(r => r.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        if (claimed == 0)
        {
            throw await StatusConflictAsync(id, "執行退款", refund.NeedsReturn ? "只有「已驗收退回品」的案件可以執行退款" : "只有「已核准」的案件可以執行退款", cancellationToken);
        }

        string method;
        string? reference = null;
        if (refund.Order.PaymentMethod == "linepay")
        {
            GatewayRefundResult result;
            try
            {
                result = await linePay.RefundAsync(new GatewayRefundRequest(refund.Order.OrderNo, refund.Order.LinepayTransactionId, amount), cancellationToken);
            }
            catch
            {
                await RevertAsync(id, from, scope.Identity.AdminUserId);
                throw;
            }

            if (result.Outcome != GatewayOutcome.Succeeded)
            {
                await RevertAsync(id, from, scope.Identity.AdminUserId);
                throw new AdminConflictException(
                    result.Outcome == GatewayOutcome.NotConfigured ? "LINE Pay 尚未串接" : "退款失敗",
                    result.Message ?? "LINE Pay 退款沒有成功，案件已退回原本的狀態，請稍後再試。");
            }

            method = "linepay";
            reference = result.Reference;
        }
        else
        {
            method = "manual";
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await db.RefundRequests.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, "refunded").SetProperty(r => r.RefundedAt, now).SetProperty(r => r.RefundedBy, scope.Identity.AdminUserId)
            .SetProperty(r => r.RefundMethod, method).SetProperty(r => r.RefundReference, reference)
            .SetProperty(r => r.ReviewNote, note ?? refund.ReviewNote).SetProperty(r => r.UpdatedAt, now).SetProperty(r => r.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        var refundedTotal = await db.RefundRequests.AsNoTracking().Where(r => r.OrderId == refund.OrderId && r.Status == "refunded").SumAsync(r => r.RefundAmount ?? 0, cancellationToken);
        var full = refundedTotal >= refund.Order.Total;
        if (full)
        {
            await db.Orders.Where(o => o.Id == refund.OrderId).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, "refunded")
                .SetProperty(o => o.OrderStatus, o => o.OrderStatus == ShopLabels.Cancelled ? ShopLabels.Cancelled : ShopLabels.Refunded)
                .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);
        }
        else
        {
            await RestoreOrderStatusAsync(refund.OrderId, scope.Identity.AdminUserId, cancellationToken);
        }

        string? invoiceAction = null;
        var invoice = await db.StoreInvoices.Where(i => i.OrderId == refund.OrderId && i.InvoiceNo != null && i.VoidStatus == "none").OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (invoice is not null)
        {
            var voidResult = await invoices.VoidOrAllowanceAsync(invoice.InvoiceNo!, full ? InvoiceVoidKind.Void : InvoiceVoidKind.Allowance, amount, cancellationToken);
            invoice.VoidStatus = full ? "voided" : "allowance";
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = scope.Identity.AdminUserId;
            invoiceAction = voidResult.Message ?? (full ? "發票已作廢。" : "發票已折讓。");
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        audit.Record(scope, "執行退款", $"訂單 {refund.Order.OrderNo}／案件 {id}", 1, method == "linepay" ? "原路退回 LINE Pay" : "人工退款");
        return new AdminRefundExecuteResultDto { Refund = (await GetAsync(scope, id, cancellationToken))!, InvoiceAction = invoiceAction };
    }

    // ═════════════ 內部 ═════════════

    private async Task RevertAsync(Guid id, string status, Guid? operatorId)
        => await db.RefundRequests.Where(r => r.Id == id && r.Status == "processing")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status).SetProperty(r => r.UpdatedAt, DateTime.UtcNow).SetProperty(r => r.UpdatedBy, operatorId), CancellationToken.None);

    /// <summary>沒有其他處理中的退貨案件時，訂單由「退貨處理中」回到「已完成」（已送達過）或「已出貨」。</summary>
    private async Task RestoreOrderStatusAsync(Guid orderId, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (await db.RefundRequests.AsNoTracking().AnyAsync(r => r.OrderId == orderId && OpenStatuses.Contains(r.Status), cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await db.Orders.Where(o => o.Id == orderId && o.OrderStatus == ShopLabels.Returning).ExecuteUpdateAsync(s => s
            .SetProperty(o => o.OrderStatus, o => o.CompletedAt != null ? ShopLabels.Completed : ShopLabels.Shipped)
            .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.UpdatedBy, operatorId), cancellationToken);
    }

    private Task<RefundRequest?> FindAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
        => db.RefundRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.Order.SellingClubId == scope.ClubId, cancellationToken);

    private async Task<AdminConflictException> StatusConflictAsync(Guid id, string action, string rule, CancellationToken cancellationToken)
    {
        var status = await db.RefundRequests.AsNoTracking().Where(r => r.Id == id).Select(r => r.Status).FirstOrDefaultAsync(cancellationToken);
        return new AdminConflictException("案件狀態不允許", $"{rule}（這個案件目前是「{ShopLabels.Of(ShopLabels.Refund, status)}」，無法{action}）。");
    }

    private async Task<AdminRefundDetailDto> ToDetailAsync(RefundRequest r, CancellationToken cancellationToken)
    {
        var adminIds = new[] { r.ApprovedBy, r.ReceivedBy, r.RefundedBy }.OfType<Guid>().Distinct().ToList();
        var names = await db.AdminUsers.AsNoTracking().Where(a => adminIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.DisplayName, cancellationToken);
        string? Name(Guid? id) => id is Guid g && names.TryGetValue(g, out var n) ? n : null;
        var refundedTotal = await db.RefundRequests.AsNoTracking().Where(x => x.OrderId == r.OrderId && x.Status == "refunded").SumAsync(x => x.RefundAmount ?? 0, cancellationToken);
        var actions = r.Status switch
        {
            "requested" => new[] { "approve", "reject" },
            "approved" => r.NeedsReturn ? ["receive", "reject"] : ["execute", "reject"],
            "received" => ["execute"],
            _ => Array.Empty<string>(),
        };
        return new AdminRefundDetailDto
        {
            Id = r.Id, OrderId = r.OrderId, OrderNo = r.Order.OrderNo, OrderStatus = r.Order.OrderStatus, OrderTotal = r.Order.Total, OrderRefundedTotal = refundedTotal,
            PaymentMethod = r.Order.PaymentMethod, PaymentMethodLabel = ShopLabels.Of(ShopLabels.PaymentMethod, r.Order.PaymentMethod), Status = r.Status,
            StatusLabel = ShopLabels.Of(ShopLabels.Refund, r.Status), RefundAmount = r.RefundAmount, Reason = r.Reason, NeedsReturn = r.NeedsReturn, ReviewNote = r.ReviewNote,
            ApprovedByName = Name(r.ApprovedBy), ReceivedAt = r.ReceivedAt, ReceivedByName = Name(r.ReceivedBy), RefundMethod = r.RefundMethod,
            RefundMethodLabel = r.RefundMethod is null ? null : ShopLabels.Of(ShopLabels.RefundMethod, r.RefundMethod), RefundReference = r.RefundReference,
            RefundedAt = r.RefundedAt, RefundedByName = Name(r.RefundedBy),
            Items = r.RefundRequestItems.Select(i => new AdminRefundItemDto
            {
                OrderItemId = i.OrderItemId, ProductName = i.OrderItem.ProductNameSnapshot, VariantLabel = i.OrderItem.VariantLabelSnapshot, Sku = i.OrderItem.SkuSnapshot,
                Quantity = i.Quantity, UnitPrice = i.OrderItem.UnitPriceSnapshot,
            }).ToList(),
            AvailableActions = actions, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt,
        };
    }
}
