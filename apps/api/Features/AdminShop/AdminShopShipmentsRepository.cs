using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S4 出貨與物流（規劃書 §4.13 S4）：待出貨與出貨中的訂單清單、揀貨單與出貨單資料（畫面直接列印）、批次標記已出貨、
/// 物流單號人工或 CSV 批次回填、超商取貨到店通知、自取領取狀態（待領取／已領取／逾期）。
/// <b>不串接物流商 API</b>（v2.6）。出貨單含收件人資料：完整值需 <c>shop.order.reveal</c>，否則遮罩（規劃書 §6）。
/// 訂單一律限定 <c>selling_club_id</c> 為目前俱樂部。刻意不注入快取服務。
/// </summary>
public sealed class AdminShopShipmentsRepository(ClubDbContext db, IPermissionChecker permissions, SensitiveActionLogger audit, ShopOrderLifecycle lifecycle)
{
    private static readonly string[] Fulfilling = [ShopLabels.Paid, ShopLabels.Preparing, ShopLabels.Shipped];
    private static readonly string[] ToPick = [ShopLabels.Paid, ShopLabels.Preparing];

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, ShopPermissions.OrderReveal, cancellationToken);

    public async Task<PagedResult<AdminShipmentListItemDto>> ListAsync(
        AdminClubScope scope, string? orderStatus, string? deliveryMethod, string? pickupStatus, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = db.Orders.AsNoTracking().Where(o => o.SellingClubId == scope.ClubId && o.PaymentStatus == "paid");
        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            AdminInput.OneOf(orderStatus, Fulfilling.ToHashSet(), "訂單狀態", "「已付款」「備貨中」或「已出貨」");
            query = query.Where(o => o.OrderStatus == orderStatus);
        }
        else
        {
            query = query.Where(o => Fulfilling.Contains(o.OrderStatus));
        }

        if (!string.IsNullOrWhiteSpace(deliveryMethod))
        {
            AdminInput.OneOf(deliveryMethod, ShopLabels.Delivery.Keys.ToHashSet(), "配送方式", "宅配、超商取貨或現場自取");
            query = query.Where(o => o.DeliveryMethod == deliveryMethod);
        }

        if (!string.IsNullOrWhiteSpace(pickupStatus))
        {
            AdminInput.OneOf(pickupStatus, ShopLabels.Pickup.Keys.ToHashSet(), "領取狀態", "待領取、已領取或逾期");
            var today = TaiwanClock.Today;
            query = pickupStatus switch
            {
                "overdue" => query.Where(o => o.Shipment != null && o.Shipment.PickupStatus == "waiting" && o.Shipment.PickupDeadlineOn != null && o.Shipment.PickupDeadlineOn < today),
                "waiting" => query.Where(o => o.Shipment != null && o.Shipment.PickupStatus == "waiting" && (o.Shipment.PickupDeadlineOn == null || o.Shipment.PickupDeadlineOn >= today)),
                _ => query.Where(o => o.Shipment != null && o.Shipment.PickupStatus == "picked_up"),
            };
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = canReveal
                ? query.Where(o => o.OrderNo.Contains(k) || (o.RecipientName != null && o.RecipientName.Contains(k)) || (o.Shipment != null && o.Shipment.TrackingNo != null && o.Shipment.TrackingNo.Contains(k)))
                : query.Where(o => o.OrderNo.Contains(k) || (o.Shipment != null && o.Shipment.TrackingNo != null && o.Shipment.TrackingNo.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(o => o.OrderStatus == ShopLabels.Shipped ? 1 : 0).ThenBy(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { Order = o, o.Shipment, ItemCount = o.OrderItems.Sum(i => (int?)i.Quantity) ?? 0 }).ToListAsync(cancellationToken);
        return new PagedResult<AdminShipmentListItemDto>
        {
            Items = rows.Select(r => new AdminShipmentListItemDto
            {
                OrderId = r.Order.Id, OrderNo = r.Order.OrderNo, OrderStatus = r.Order.OrderStatus, DeliveryMethod = r.Order.DeliveryMethod,
                DeliveryMethodLabel = r.Order.DeliveryMethod is null ? null : ShopLabels.Of(ShopLabels.Delivery, r.Order.DeliveryMethod), CreatedAt = r.Order.CreatedAt,
                RecipientName = canReveal ? r.Order.RecipientName : PiiMasking.MaskName(r.Order.RecipientName), ItemCount = r.ItemCount,
                Shipment = r.Shipment is null ? null : AdminShopOrdersRepository.ToShipmentDto(r.Shipment), IsMasked = !canReveal,
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    /// <summary>揀貨單：把待出貨（已付款、備貨中）訂單的品項依 SKU 加總，供倉儲一次撿齊。<paramref name="orderIds"/> 省略＝全部待出貨訂單。</summary>
    public async Task<AdminPickingListDto> PickingListAsync(AdminClubScope scope, IReadOnlyList<Guid>? orderIds, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.SellingClubId == scope.ClubId && ToPick.Contains(o.OrderStatus));
        if (orderIds is { Count: > 0 })
        {
            if (orderIds.Count > 200)
            {
                throw new AdminValidationException("一次最多列印 200 張訂單的揀貨單。");
            }

            orders = orders.Where(o => orderIds.Contains(o.Id));
        }

        var items = await db.OrderItems.AsNoTracking().Where(i => orders.Select(o => o.Id).Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.SkuSnapshot, i.ProductNameSnapshot, i.VariantLabelSnapshot, i.Quantity }).ToListAsync(cancellationToken);
        var lines = items.GroupBy(i => i.SkuSnapshot).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new AdminPickingLineDto
        {
            Sku = g.Key, ProductName = g.First().ProductNameSnapshot, VariantLabel = g.First().VariantLabelSnapshot,
            Quantity = g.Sum(x => x.Quantity), OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
        }).ToList();
        return new AdminPickingListDto
        {
            GeneratedAt = DateTime.UtcNow, OrderCount = items.Select(i => i.OrderId).Distinct().Count(), TotalQuantity = lines.Sum(l => l.Quantity), Lines = lines,
        };
    }

    /// <summary>出貨單資料（一張訂單一張）。收件人資料依 <c>shop.order.reveal</c> 遮罩；有權限者的每次取得寫日誌。</summary>
    public async Task<IReadOnlyList<AdminDispatchSlipDto>> DispatchSlipsAsync(AdminClubScope scope, IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count is 0 or > 100)
        {
            throw new AdminValidationException("出貨單一次最多列印 100 張，至少選 1 張。");
        }

        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var ids = orderIds.Distinct().ToList();
        var orders = await db.Orders.AsNoTracking().Include(o => o.OrderItems).Include(o => o.Shipment)
            .Where(o => o.SellingClubId == scope.ClubId && ids.Contains(o.Id)).AsSplitQuery().ToListAsync(cancellationToken);
        if (canReveal && orders.Count > 0)
        {
            audit.Record(scope, "列印出貨單（含完整收件人）", "出貨單", orders.Count);
        }

        return ids.Select(id => orders.FirstOrDefault(o => o.Id == id)).OfType<Data.EfEntities.Order>().Select(o => new AdminDispatchSlipDto
        {
            OrderId = o.Id, OrderNo = o.OrderNo, DeliveryMethod = o.DeliveryMethod,
            DeliveryMethodLabel = o.DeliveryMethod is null ? null : ShopLabels.Of(ShopLabels.Delivery, o.DeliveryMethod),
            RecipientName = canReveal ? o.RecipientName : PiiMasking.MaskName(o.RecipientName),
            RecipientPhone = canReveal ? o.RecipientPhone : PiiMasking.MaskPhone(o.RecipientPhone),
            RecipientAddress = canReveal ? o.RecipientAddress : PiiMasking.MaskAddress(o.RecipientAddress),
            StoreBranchCode = o.Shipment?.StoreBranchCode, CustomerNote = o.CustomerNote,
            Items = o.OrderItems.OrderBy(i => i.RowSeq).Select(i => new AdminDispatchSlipItemDto
            {
                Sku = i.SkuSnapshot, ProductName = i.ProductNameSnapshot, VariantLabel = i.VariantLabelSnapshot, Quantity = i.Quantity,
            }).ToList(),
            IsMasked = !canReveal,
        }).ToList();
    }

    /// <summary>批次標記已出貨（同一個物流商；物流單號之後再回填）。能處理的處理，不能處理的進 <c>skipped</c>。</summary>
    public async Task<BatchOperationResultDto> BatchShipAsync(AdminClubScope scope, BatchShipAdminOrdersRequest request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多處理 200 筆，至少選 1 筆。");
        }

        var carrier = AdminInput.OptionalText(request.Carrier, "物流商", 32);
        var ids = request.Ids.Distinct().ToList();
        var known = await db.Orders.AsNoTracking().Where(o => o.SellingClubId == scope.ClubId && ids.Contains(o.Id)).Select(o => o.Id).ToListAsync(cancellationToken);
        var skipped = new List<BatchSkippedItemDto>();
        var updated = 0;
        foreach (var id in ids)
        {
            if (!known.Contains(id))
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = "找不到這張訂單。" });
                continue;
            }

            try
            {
                await lifecycle.ShipAsync(id, new ShipAdminOrderRequest { Carrier = carrier }, scope.Identity.AdminUserId, cancellationToken);
                updated++;
            }
            catch (Exception ex) when (ex is AdminValidationException or AdminConflictException)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = ex.Message });
            }
        }

        return new BatchOperationResultDto { UpdatedCount = updated, Skipped = skipped };
    }

    /// <summary>
    /// 物流單號 CSV 批次回填。欄位（第一列表頭，順序不拘）：<c>訂單編號</c>（必填）、<c>物流商</c>、<c>物流單號</c>、<c>門市代碼</c>。
    /// 已付款／備貨中的訂單會一併標記已出貨；已出貨的訂單只更新物流資料；其餘狀態略過並註明原因。
    /// </summary>
    public async Task<AdminShipmentImportResultDto> ImportTrackingAsync(AdminClubScope scope, string csvText, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvText);
        if (rows.Count < 2)
        {
            throw new AdminValidationException("檔案沒有資料列，第一列必須是表頭（訂單編號、物流商、物流單號）。");
        }

        if (rows.Count > 2001)
        {
            throw new AdminValidationException("一次最多匯入 2000 列，請分批處理。");
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Contains(h, StringComparer.OrdinalIgnoreCase));
        var iOrder = Col("訂單編號", "order_no");
        var iCarrier = Col("物流商", "carrier");
        var iTracking = Col("物流單號", "tracking_no");
        var iBranch = Col("門市代碼", "store_branch_code");
        if (iOrder < 0 || iTracking < 0)
        {
            throw new AdminValidationException("表頭必須包含「訂單編號」與「物流單號」欄位。");
        }

        string? Cell(List<string> row, int i) => i >= 0 && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
        var skipped = new List<AdminShipmentImportSkippedDto>();
        var updated = 0;
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var no = Cell(row, iOrder);
            if (no is null)
            {
                skipped.Add(new AdminShipmentImportSkippedDto { Row = r + 1, Reason = "缺少訂單編號。" });
                continue;
            }

            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNo == no && o.SellingClubId == scope.ClubId, cancellationToken);
            if (order is null)
            {
                skipped.Add(new AdminShipmentImportSkippedDto { Row = r + 1, OrderNo = no, Reason = "找不到這張訂單（或不屬於目前的俱樂部）。" });
                continue;
            }

            try
            {
                var carrier = Cell(row, iCarrier);
                var tracking = Cell(row, iTracking);
                var branch = Cell(row, iBranch);
                var shippedNow = false;
                if (order.OrderStatus is ShopLabels.Paid or ShopLabels.Preparing)
                {
                    await lifecycle.ShipAsync(order.Id, new ShipAdminOrderRequest { Carrier = carrier, TrackingNo = tracking, StoreBranchCode = branch }, scope.Identity.AdminUserId, cancellationToken);
                    shippedNow = true;
                }
                else if (order.OrderStatus is ShopLabels.Shipped or ShopLabels.Completed)
                {
                    shippedNow = await lifecycle.UpdateShipmentAsync(order.Id, new UpdateAdminShipmentRequest { Carrier = carrier, TrackingNo = tracking, StoreBranchCode = branch }, scope.Identity.AdminUserId, cancellationToken);
                }

                if (!shippedNow)
                {
                    skipped.Add(new AdminShipmentImportSkippedDto { Row = r + 1, OrderNo = no, Reason = $"訂單目前是「{order.OrderStatus}」，不能回填物流資料。" });
                    continue;
                }

                updated++;
            }
            catch (Exception ex) when (ex is AdminValidationException or AdminConflictException)
            {
                skipped.Add(new AdminShipmentImportSkippedDto { Row = r + 1, OrderNo = no, Reason = ex.Message });
            }
        }

        return new AdminShipmentImportResultDto { UpdatedCount = updated, Skipped = skipped };
    }
}
