using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>訂單清單與匯出共用的篩選條件。</summary>
public sealed record OrderFilter(
    string? PaymentStatus, string? OrderStatus, string? DeliveryMethod, bool? IsMember, DateOnly? From, DateOnly? To,
    string? Keyword, string? SettlementStatus, string? PaymentMethod);

/// <summary>
/// S3 訂單管理（規劃書 §4.13 S3）：列表與篩選、詳情、狀態動作、人工建立與補登（現場收款）、內部註記、代收代付分帳標記、CSV 匯出。
/// 🔴 <b>資料範圍</b>：一律只看 <c>selling_club_id</c> 為目前俱樂部的訂單（受範圍限制的帳號看不到別的俱樂部的訂單；跨俱樂部 id 一律 404）。
/// 🔴 <b>收件人資料（姓名／電話／地址）視同會員個資</b>：只有 <c>shop.order.reveal</c> 看得到完整值，其餘遮罩；沒有權限時關鍵字只比對訂單編號
/// （否則搜尋會變成探測個資的工具）。匯出另需 <c>shop.order.export</c>（受限），<b>先套資料範圍再套受限欄位授權，兩道關卡</b>，須填用途並寫入敏感操作日誌。
/// 🔴 訂單狀態、庫存屬「不得讀快取」五類，本類別不注入快取服務。<b>金流與發票不串接</b>（B-10）：付款狀態只由現場收款建單或日後的金流回呼寫入。
/// </summary>
public sealed class AdminShopOrdersRepository(
    ClubDbContext db, IPermissionChecker permissions, SensitiveActionLogger audit, InventoryService inventory,
    ShopOrderLifecycle lifecycle, ShopSettingsReader shopSettings)
{
    private const int ExportRowLimit = 20000;

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, ShopPermissions.OrderReveal, cancellationToken);

    private async Task<Dictionary<Guid, (string Code, string? Name)>> ClubNamesAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Clubs.AsNoTracking().Select(c => new
        {
            c.Id, c.Code, Name = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
        }).ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => (r.Code, r.Name));
    }

    // ═════════════ 清單 ═════════════

    private IQueryable<Order> Filter(AdminClubScope scope, OrderFilter f, bool canReveal)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.SellingClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(f.PaymentStatus))
        {
            AdminInput.OneOf(f.PaymentStatus, ShopLabels.PaymentStatus.Keys.ToHashSet(), "付款狀態", "待付款、已付款、付款失敗、已逾時或已退款");
            query = query.Where(o => o.PaymentStatus == f.PaymentStatus);
        }

        if (!string.IsNullOrWhiteSpace(f.OrderStatus))
        {
            AdminInput.OneOf(f.OrderStatus, ShopLabels.OrderStatuses.ToHashSet(), "訂單狀態", "待付款、已付款、備貨中、已出貨、已完成、已取消、退貨處理中或已退款");
            query = query.Where(o => o.OrderStatus == f.OrderStatus);
        }

        if (!string.IsNullOrWhiteSpace(f.DeliveryMethod))
        {
            AdminInput.OneOf(f.DeliveryMethod, ShopLabels.Delivery.Keys.ToHashSet(), "配送方式", "宅配、超商取貨或現場自取");
            query = query.Where(o => o.DeliveryMethod == f.DeliveryMethod);
        }

        if (!string.IsNullOrWhiteSpace(f.PaymentMethod))
        {
            AdminInput.OneOf(f.PaymentMethod, ShopLabels.PaymentMethod.Keys.ToHashSet(), "付款方式", "LINE Pay 或現場收款");
            query = query.Where(o => o.PaymentMethod == f.PaymentMethod);
        }

        if (!string.IsNullOrWhiteSpace(f.SettlementStatus))
        {
            AdminInput.OneOf(f.SettlementStatus, ShopLabels.Settlement.Keys.ToHashSet(), "分帳狀態", "待結算或已結算");
            query = query.Where(o => o.SettlementStatus == f.SettlementStatus);
        }

        if (f.IsMember is bool member)
        {
            query = member ? query.Where(o => o.MemberId != null) : query.Where(o => o.MemberId == null);
        }

        if (f.From is DateOnly from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            query = query.Where(o => o.CreatedAt >= fromUtc);
        }

        if (f.To is DateOnly to)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(o => o.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim();
            query = canReveal
                ? query.Where(o => o.OrderNo.Contains(k) || (o.RecipientName != null && o.RecipientName.Contains(k)) || (o.RecipientPhone != null && o.RecipientPhone.Contains(k)))
                : query.Where(o => o.OrderNo.Contains(k));
        }

        return query;
    }

    public async Task<PagedResult<AdminOrderListItemDto>> ListAsync(AdminClubScope scope, OrderFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = Filter(scope, filter, canReveal);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                Order = o, ItemCount = o.OrderItems.Sum(i => (int?)i.Quantity) ?? 0,
                ShippedAt = o.Shipment == null ? (DateTime?)null : o.Shipment.ShippedAt,
                DeliveredAt = o.Shipment == null ? (DateTime?)null : o.Shipment.DeliveredAt,
            }).ToListAsync(cancellationToken);
        var clubs = await ClubNamesAsync(cancellationToken);
        if (canReveal && rows.Count > 0)
        {
            audit.Record(scope, "檢視訂單收件人（完整）", "訂單清單", rows.Count);
        }

        return new PagedResult<AdminOrderListItemDto>
        {
            Items = rows.Select(r => ToListItem(r.Order, r.ItemCount, r.ShippedAt, r.DeliveredAt, clubs, canReveal)).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    // ═════════════ 詳情 ═════════════

    public async Task<AdminOrderDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var detail = await BuildDetailAsync(scope, id, canReveal, cancellationToken);
        if (detail is not null && canReveal)
        {
            audit.Record(scope, "檢視訂單收件人（完整）", detail.OrderNo, 1);
        }

        return detail;
    }

    private async Task<AdminOrderDetailDto?> BuildDetailAsync(AdminClubScope scope, Guid id, bool canReveal, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.OrderItems).Include(o => o.Shipment).Include(o => o.Member)
            .AsSplitQuery().FirstOrDefaultAsync(o => o.Id == id && o.SellingClubId == scope.ClubId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var refunds = await db.RefundRequests.AsNoTracking().Include(r => r.RefundRequestItems).Where(r => r.OrderId == id)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        var invoice = await db.StoreInvoices.AsNoTracking().Where(i => i.OrderId == id).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var clubs = await ClubNamesAsync(cancellationToken);
        var refundedByItem = refunds.Where(r => r.Status != "rejected").SelectMany(r => r.RefundRequestItems)
            .GroupBy(i => i.OrderItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var openRefund = refunds.Any(r => r.Status is "requested" or "approved" or "received" or "processing");
        var refundable = order.PaymentStatus == "paid" && !openRefund
            && order.OrderStatus is ShopLabels.Shipped or ShopLabels.Completed && order.Total - refunds.Where(r => r.Status == "refunded").Sum(r => r.RefundAmount ?? 0) > 0;
        var actions = ActionsFor(order, refundable);
        var shipment = order.Shipment;
        return new AdminOrderDetailDto
        {
            Id = order.Id, OrderNo = order.OrderNo, CreatedAt = order.CreatedAt, PaidAt = order.PaidAt, CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt, CancelReason = order.CancelReason, Subtotal = order.Subtotal, ShippingFee = order.ShippingFee, Total = order.Total,
            LinepayTransactionId = order.LinepayTransactionId, PaymentStatus = order.PaymentStatus,
            PaymentStatusLabel = ShopLabels.Of(ShopLabels.PaymentStatus, order.PaymentStatus), PaymentMethod = order.PaymentMethod,
            PaymentMethodLabel = ShopLabels.Of(ShopLabels.PaymentMethod, order.PaymentMethod), OrderStatus = order.OrderStatus,
            DeliveryMethod = order.DeliveryMethod, DeliveryMethodLabel = order.DeliveryMethod is null ? null : ShopLabels.Of(ShopLabels.Delivery, order.DeliveryMethod),
            ShipmentStatusLabel = ShipmentStatusLabel(order.DeliveryMethod, shipment?.ShippedAt, shipment?.DeliveredAt), IsMember = order.MemberId is not null,
            MemberId = order.MemberId, MemberNo = order.Member?.MemberNo, IsManual = order.IsManual,
            SellingClubId = order.SellingClubId, SellingClubCode = clubs[order.SellingClubId].Code, SellingClubName = clubs[order.SellingClubId].Name,
            CollectingClubId = order.CollectingClubId, CollectingClubName = clubs.TryGetValue(order.CollectingClubId, out var cc) ? cc.Name : null,
            RecipientName = canReveal ? order.RecipientName : PiiMasking.MaskName(order.RecipientName),
            RecipientPhone = canReveal ? order.RecipientPhone : PiiMasking.MaskPhone(order.RecipientPhone),
            RecipientAddress = canReveal ? order.RecipientAddress : PiiMasking.MaskAddress(order.RecipientAddress),
            CustomerNote = order.CustomerNote, InternalNote = order.InternalNote, SettlementStatus = order.SettlementStatus,
            SettlementStatusLabel = ShopLabels.Of(ShopLabels.Settlement, order.SettlementStatus), SettledOn = order.SettledOn, SettlementNote = order.SettlementNote,
            Items = order.OrderItems.OrderBy(i => i.RowSeq).Select(i => new AdminOrderItemDto
            {
                Id = i.Id, VariantId = i.ProductVariantId, ProductName = i.ProductNameSnapshot, VariantLabel = i.VariantLabelSnapshot, Sku = i.SkuSnapshot,
                UnitPrice = i.UnitPriceSnapshot, Quantity = i.Quantity, LineTotal = i.LineTotal, RefundedQuantity = refundedByItem.GetValueOrDefault(i.Id),
            }).ToList(),
            Shipment = shipment is null ? null : ToShipmentDto(shipment),
            Invoice = invoice is null ? null : new AdminOrderInvoiceDto { InvoiceNo = invoice.InvoiceNo, IssuedAt = invoice.IssuedAt, IssueStatus = invoice.IssueStatus, VoidStatus = invoice.VoidStatus },
            Refunds = refunds.Select(r => new AdminOrderRefundSummaryDto
            {
                Id = r.Id, Status = r.Status, StatusLabel = ShopLabels.Of(ShopLabels.Refund, r.Status), RefundAmount = r.RefundAmount, Reason = r.Reason, CreatedAt = r.CreatedAt,
            }).ToList(),
            AvailableActions = actions, IsMasked = !canReveal, CanReveal = canReveal, UpdatedAt = order.UpdatedAt,
        };
    }

    // ═════════════ 建立（人工建立與補登）═════════════

    public async Task<AdminOrderDetailDto> CreateManualAsync(AdminClubScope scope, CreateAdminOrderRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.DeliveryMethod, ShopLabels.Delivery.Keys.ToHashSet(), "配送方式", "宅配、超商取貨或現場自取");
        if (request.Items is null || request.Items.Count is 0 or > 50)
        {
            throw new AdminValidationException("訂單至少要有 1 個品項，最多 50 個。");
        }

        var name = AdminInput.OptionalText(request.RecipientName, "收件人姓名", 64);
        var phone = AdminInput.OptionalText(request.RecipientPhone, "收件人電話", 32);
        var address = AdminInput.OptionalText(request.RecipientAddress, "收件地址", 500);
        var customerNote = AdminInput.OptionalText(request.CustomerNote, "顧客備註", 500);
        var internalNote = AdminInput.OptionalText(request.InternalNote, "內部註記", 2000);
        if (request.DeliveryMethod == "home_delivery" && (name is null || phone is null || address is null))
        {
            throw new AdminValidationException("宅配訂單必須填寫收件人姓名、電話與地址。");
        }

        if (request.DeliveryMethod == "cvs_pickup" && (name is null || phone is null))
        {
            throw new AdminValidationException("超商取貨訂單必須填寫收件人姓名與電話。");
        }

        if (request.ShippingFee is < 0)
        {
            throw new AdminValidationException("運費不可為負數。");
        }

        if (request.CompleteImmediately && request.DeliveryMethod != "onsite_pickup")
        {
            throw new AdminValidationException("只有「現場自取」的訂單可以當場完成。");
        }

        var lines = request.Items.GroupBy(i => i.VariantId).Select(g => (VariantId: g.Key, Quantity: g.Sum(x => x.Quantity))).ToList();
        if (lines.Any(l => l.Quantity is < 1 or > 999))
        {
            throw new AdminValidationException("每個品項的數量必須是 1 到 999。");
        }

        Guid? memberId = null;
        if (request.MemberId is Guid mid)
        {
            if (!await db.Members.AsNoTracking().AnyAsync(m => m.Id == mid && m.Status != "deleted", cancellationToken))
            {
                throw new AdminValidationException("找不到指定的會員，或這個帳號已刪除。");
            }

            memberId = mid;
        }

        var variantIds = lines.Select(l => l.VariantId).ToList();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => variantIds.Contains(v.Id) && v.ClubId == scope.ClubId)
            .Select(v => new
            {
                Variant = v, ProductSlug = v.Product.Slug,
                ProductName = v.Product.ProductsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        if (variants.Count != lines.Count)
        {
            throw new AdminValidationException("品項含有不存在的商品規格，請確認規格屬於目前的俱樂部。");
        }

        var inactive = variants.FirstOrDefault(v => v.Variant.Status != "active");
        if (inactive is not null)
        {
            throw new AdminValidationException($"規格「{inactive.Variant.Sku}」已停售，不能建立訂單。");
        }

        var collecting = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        if (collecting == Guid.Empty)
        {
            throw new AdminConflictException("尚未設定收款主體", "系統裡沒有設定收款主體的俱樂部，請先請系統管理員設定。");
        }

        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var items = lines.Select(l =>
        {
            var v = variants.First(x => x.Variant.Id == l.VariantId);
            var price = v.Variant.SalePrice ?? v.Variant.Price;
            return new OrderItem
            {
                Id = Guid.NewGuid(), ClubId = scope.ClubId, OrderId = orderId, ProductVariantId = v.Variant.Id,
                ProductNameSnapshot = v.ProductName ?? v.ProductSlug, VariantLabelSnapshot = AdminShopProductsRepository.VariantLabel(v.Variant.Size, v.Variant.Colour) is { Length: > 0 } label ? label : null,
                SkuSnapshot = v.Variant.Sku, UnitPriceSnapshot = price, Quantity = l.Quantity, LineTotal = price * l.Quantity,
                CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            };
        }).ToList();
        var subtotal = items.Sum(i => i.LineTotal);
        var shippingFee = request.ShippingFee ?? ShopSettingsReader.ComputeShippingFee(await shopSettings.GetShippingAsync(scope.ClubId, cancellationToken), request.DeliveryMethod, subtotal);
        var completed = request.CompleteImmediately;
        var order = new Order
        {
            Id = orderId, OrderNo = await NewOrderNoAsync(scope.ClubCode, cancellationToken), ClubId = scope.ClubId, SellingClubId = scope.ClubId, CollectingClubId = collecting,
            MemberId = memberId, LookupToken = SecureToken.Generate(), RecipientName = name, RecipientPhone = phone, RecipientAddress = address,
            Subtotal = subtotal, ShippingFee = shippingFee, Total = subtotal + shippingFee, PaymentStatus = "paid", PaymentMethod = "onsite",
            OrderStatus = completed ? ShopLabels.Completed : ShopLabels.Paid, DeliveryMethod = request.DeliveryMethod, IsManual = true, PaidAt = now,
            CompletedAt = completed ? now : null, CustomerNote = customerNote, InternalNote = internalNote, SettlementStatus = "pending",
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Orders.Add(order);
        db.OrderItems.AddRange(items);
        if (completed)
        {
            db.Shipments.Add(new Shipment
            {
                Id = Guid.NewGuid(), ClubId = scope.ClubId, OrderId = orderId, ShippedAt = now, DeliveredAt = now, PickupStatus = "picked_up",
                CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var item in items)
        {
            // 現場收款的訂單沒有「保留」階段，直接售出扣減；可售量不足整張訂單一起回復。
            await inventory.ApplyAsync(new InventoryChange(scope.ClubId, item.ProductVariantId, "sale", -item.Quantity, 0, "現場收款訂單", orderId, scope.Identity.AdminUserId), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return (await BuildDetailAsync(scope, orderId, await CanRevealAsync(scope, cancellationToken), cancellationToken))!;
    }

    private async Task<string> NewOrderNoAsync(string clubCode, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RegistrationNumberGenerator.Generate(ShopLabels.OrderPrefix(clubCode), DateTime.UtcNow.AddHours(8));
            if (!await db.Orders.AsNoTracking().AnyAsync(o => o.OrderNo == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new AdminConflictException("無法產生訂單編號", "產生訂單編號時發生重複，請再試一次。");
    }

    // ═════════════ 動作 ═════════════

    /// <summary>確認訂單屬於目前俱樂部（selling_club_id）；不屬於一律 404。</summary>
    public async Task<bool> ExistsAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
        => await db.Orders.AsNoTracking().AnyAsync(o => o.Id == id && o.SellingClubId == scope.ClubId, cancellationToken);

    public async Task<AdminOrderDetailDto?> DetailAfterActionAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
        => await BuildDetailAsync(scope, id, await CanRevealAsync(scope, cancellationToken), cancellationToken);

    public async Task<AdminOrderDetailDto?> UpdateNotesAsync(AdminClubScope scope, Guid id, UpdateAdminOrderNotesRequest request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.SellingClubId == scope.ClubId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.InternalNote = AdminInput.OptionalText(request.InternalNote, "內部註記", 2000);
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAfterActionAsync(scope, id, cancellationToken);
    }

    public async Task<AdminOrderDetailDto?> UpdateSettlementAsync(AdminClubScope scope, Guid id, UpdateAdminOrderSettlementRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, ShopLabels.Settlement.Keys.ToHashSet(), "分帳狀態", "「待結算」或「已結算」");
        var note = AdminInput.OptionalText(request.Note, "結算備註", 500);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.SellingClubId == scope.ClubId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        ApplySettlement(order, request.Status, request.SettledOn, note, scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAfterActionAsync(scope, id, cancellationToken);
    }

    public async Task<BatchOperationResultDto> BatchSettlementAsync(AdminClubScope scope, BatchAdminOrderSettlementRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, ShopLabels.Settlement.Keys.ToHashSet(), "分帳狀態", "「待結算」或「已結算」");
        var note = AdminInput.OptionalText(request.Note, "結算備註", 500);
        if (request.Ids.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多處理 200 筆，至少選 1 筆。");
        }

        var ids = request.Ids.Distinct().ToList();
        var orders = await db.Orders.Where(o => o.SellingClubId == scope.ClubId && ids.Contains(o.Id)).ToListAsync(cancellationToken);
        var skipped = new List<BatchSkippedItemDto>();
        var updated = 0;
        foreach (var id in ids)
        {
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order is null)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = "找不到這張訂單。" });
                continue;
            }

            try
            {
                ApplySettlement(order, request.Status, request.SettledOn, note, scope.Identity.AdminUserId);
                updated++;
            }
            catch (AdminConflictException ex)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = ex.Message });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BatchOperationResultDto { UpdatedCount = updated, Skipped = skipped };
    }

    private static void ApplySettlement(Order order, string status, DateOnly? settledOn, string? note, Guid? operatorId)
    {
        if (order.PaymentStatus is "pending" or "failed" or "expired")
        {
            throw new AdminConflictException("尚未付款", $"訂單 {order.OrderNo} 還沒有付款，不需要結算。");
        }

        order.SettlementStatus = status;
        order.SettledOn = status == "settled" ? settledOn ?? TaiwanClock.Today : null;
        order.SettlementNote = note;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = operatorId;
    }

    public async Task<ReleaseExpiredOrdersResultDto> ReleaseExpiredAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var minutes = await shopSettings.GetPendingTimeoutMinutesAsync(scope.ClubId, cancellationToken);
        var count = await lifecycle.ExpireStalePendingAsync(scope.ClubId, TimeSpan.FromMinutes(minutes), scope.Identity.AdminUserId, cancellationToken);
        return new ReleaseExpiredOrdersResultDto { ExpiredCount = count, TimeoutMinutes = minutes };
    }

    // ═════════════ 匯出 ═════════════

    /// <summary>訂單 CSV（<c>shop.order.export</c>，受限）。先套資料範圍（selling_club_id）與篩選，含完整收件人資料；須填用途並寫日誌。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, OrderFilter filter, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var rows = await Filter(scope, filter, canReveal: true).OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.RowSeq).Take(ExportRowLimit + 1)
            .Select(o => new { Order = o, ItemCount = o.OrderItems.Sum(i => (int?)i.Quantity) ?? 0, MemberNo = o.Member == null ? null : o.Member.MemberNo }).ToListAsync(cancellationToken);
        if (rows.Count > ExportRowLimit)
        {
            throw new AdminValidationException($"符合條件的訂單超過 {ExportRowLimit} 筆，請縮小期間或條件後再匯出。");
        }

        var clubs = await ClubNamesAsync(cancellationToken);
        var lines = new List<IEnumerable<string?>>
        {
            new[]
            {
                "訂單編號", "賣方俱樂部", "成立時間", "付款方式", "付款狀態", "訂單狀態", "配送方式", "商品小計", "運費", "總額", "品項數量", "會員編號",
                "收件人", "電話", "地址", "顧客備註", "分帳狀態", "結算日期", "結算備註",
            },
        };
        lines.AddRange(rows.Select(r => new[]
        {
            r.Order.OrderNo, clubs[r.Order.SellingClubId].Name ?? clubs[r.Order.SellingClubId].Code, TaiwanText(r.Order.CreatedAt),
            ShopLabels.Of(ShopLabels.PaymentMethod, r.Order.PaymentMethod), ShopLabels.Of(ShopLabels.PaymentStatus, r.Order.PaymentStatus), r.Order.OrderStatus,
            r.Order.DeliveryMethod is null ? null : ShopLabels.Of(ShopLabels.Delivery, r.Order.DeliveryMethod), r.Order.Subtotal.ToString(), r.Order.ShippingFee.ToString(),
            r.Order.Total.ToString(), r.ItemCount.ToString(), r.MemberNo, r.Order.RecipientName, r.Order.RecipientPhone, r.Order.RecipientAddress, r.Order.CustomerNote,
            ShopLabels.Of(ShopLabels.Settlement, r.Order.SettlementStatus), r.Order.SettledOn?.ToString("yyyy-MM-dd"), r.Order.SettlementNote,
        }));
        audit.Record(scope, "匯出訂單", $"共 {rows.Count} 筆", rows.Count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }

    // ═════════════ 對應 ═════════════

    internal static string TaiwanText(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(8).ToString("yyyy-MM-dd HH:mm");

    internal static string ShipmentStatusLabel(string? deliveryMethod, DateTime? shippedAt, DateTime? deliveredAt)
        => deliveredAt is not null ? (deliveryMethod is "cvs_pickup" or "onsite_pickup" ? "已領取" : "已送達") : shippedAt is not null ? "已出貨" : "未出貨";

    internal static AdminOrderShipmentDto ToShipmentDto(Shipment s)
    {
        var status = s.PickupStatus == "waiting" && s.PickupDeadlineOn is DateOnly d && d < TaiwanClock.Today ? "overdue" : s.PickupStatus;
        return new AdminOrderShipmentDto
        {
            Id = s.Id, Carrier = s.Carrier, TrackingNo = s.TrackingNo, StoreBranchCode = s.StoreBranchCode, ShippedAt = s.ShippedAt, DeliveredAt = s.DeliveredAt,
            PickupStatus = status, PickupStatusLabel = status is null ? null : ShopLabels.Of(ShopLabels.Pickup, status), PickupDeadlineOn = s.PickupDeadlineOn,
            ArrivalNotifiedAt = s.ArrivalNotifiedAt,
        };
    }

    private static AdminOrderListItemDto ToListItem(
        Order o, int itemCount, DateTime? shippedAt, DateTime? deliveredAt, IReadOnlyDictionary<Guid, (string Code, string? Name)> clubs, bool reveal) => new()
    {
        Id = o.Id, OrderNo = o.OrderNo, CreatedAt = o.CreatedAt, PaidAt = o.PaidAt, Subtotal = o.Subtotal, ShippingFee = o.ShippingFee, Total = o.Total,
        PaymentStatus = o.PaymentStatus, PaymentStatusLabel = ShopLabels.Of(ShopLabels.PaymentStatus, o.PaymentStatus), PaymentMethod = o.PaymentMethod,
        PaymentMethodLabel = ShopLabels.Of(ShopLabels.PaymentMethod, o.PaymentMethod), OrderStatus = o.OrderStatus, DeliveryMethod = o.DeliveryMethod,
        DeliveryMethodLabel = o.DeliveryMethod is null ? null : ShopLabels.Of(ShopLabels.Delivery, o.DeliveryMethod),
        ShipmentStatusLabel = ShipmentStatusLabel(o.DeliveryMethod, shippedAt, deliveredAt), IsMember = o.MemberId is not null, IsManual = o.IsManual,
        SellingClubId = o.SellingClubId, SellingClubCode = clubs[o.SellingClubId].Code, SellingClubName = clubs[o.SellingClubId].Name,
        SettlementStatus = o.SettlementStatus, SettlementStatusLabel = ShopLabels.Of(ShopLabels.Settlement, o.SettlementStatus),
        RecipientName = reveal ? o.RecipientName : PiiMasking.MaskName(o.RecipientName), ItemCount = itemCount, IsMasked = !reveal,
    };

    private static IReadOnlyList<string> ActionsFor(Order order, bool refundable) => order.OrderStatus switch
    {
        ShopLabels.Pending => ["cancel"],
        ShopLabels.Paid => ["prepare", "ship", "cancel"],
        ShopLabels.Preparing => ["ship", "cancel"],
        ShopLabels.Shipped => refundable ? ["complete", "request_refund"] : ["complete"],
        ShopLabels.Completed => refundable ? ["request_refund"] : [],
        _ => [],
    };
}
