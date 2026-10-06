namespace Tcrfc.Api.Features.AdminShop;

// ───────────── S3 訂單 ─────────────

public sealed record AdminOrderListItemDto
{
    public required Guid Id { get; init; }
    public required string OrderNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public required int Subtotal { get; init; }
    public required int ShippingFee { get; init; }
    public required int Total { get; init; }
    public required string PaymentStatus { get; init; }
    public required string PaymentStatusLabel { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PaymentMethodLabel { get; init; }

    /// <summary>訂單狀態（中文，直接顯示）：待付款／已付款／備貨中／已出貨／已完成／已取消／退貨處理中／已退款。</summary>
    public required string OrderStatus { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }

    /// <summary>出貨狀態（依出貨資料換算）：未出貨／已出貨／已送達（自取為已領取）。</summary>
    public required string ShipmentStatusLabel { get; init; }
    public required bool IsMember { get; init; }
    public required bool IsManual { get; init; }
    public required Guid SellingClubId { get; init; }
    public required string SellingClubCode { get; init; }
    public string? SellingClubName { get; init; }
    public required string SettlementStatus { get; init; }
    public required string SettlementStatusLabel { get; init; }
    public string? RecipientName { get; init; }
    public required int ItemCount { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record AdminOrderItemDto
{
    public required Guid Id { get; init; }
    public required Guid VariantId { get; init; }
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required string Sku { get; init; }
    public required int UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required int LineTotal { get; init; }

    /// <summary>已申請退貨的數量（不含已駁回的案件）。</summary>
    public required int RefundedQuantity { get; init; }
}

public sealed record AdminOrderShipmentDto
{
    public required Guid Id { get; init; }
    public string? Carrier { get; init; }
    public string? TrackingNo { get; init; }
    public string? StoreBranchCode { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }

    /// <summary><c>waiting</c>／<c>picked_up</c>／<c>overdue</c>（待領取且已過領取期限）；宅配為 <c>null</c>。</summary>
    public string? PickupStatus { get; init; }
    public string? PickupStatusLabel { get; init; }
    public DateOnly? PickupDeadlineOn { get; init; }
    public DateTime? ArrivalNotifiedAt { get; init; }
}

public sealed record AdminOrderInvoiceDto
{
    public string? InvoiceNo { get; init; }
    public DateTime? IssuedAt { get; init; }
    public required string IssueStatus { get; init; }
    public string IssueStatusLabel { get; init; } = string.Empty;
    public required string VoidStatus { get; init; }
    public string VoidStatusLabel { get; init; } = string.Empty;

    /// <summary>開立方式代碼：<c>mobile_barcode</c>（手機條碼載具）／<c>citizen_cert</c>（自然人憑證載具）／<c>tax_id</c>（統編）／<c>donation</c>（捐贈）；都沒有時為 <c>null</c>。</summary>
    public string? Type { get; init; }
    public string? TypeLabel { get; init; }

    /// <summary>載具號碼。視同個資：沒有 <c>shop.order.reveal</c> 時只回遮罩值（見 <see cref="AdminOrderDetailDto.IsMasked"/>）。</summary>
    public string? CarrierId { get; init; }

    /// <summary>公司統一編號（公開資訊，不遮罩）。</summary>
    public string? TaxId { get; init; }

    /// <summary>捐贈碼（公開資訊，不遮罩）。</summary>
    public string? DonationCode { get; init; }
}

public sealed record AdminOrderRefundSummaryDto
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public int? RefundAmount { get; init; }
    public string? Reason { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminOrderDetailDto
{
    public required Guid Id { get; init; }
    public required string OrderNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public required int Subtotal { get; init; }
    public required int ShippingFee { get; init; }
    public required int Total { get; init; }

    /// <summary>LINE Pay 交易編號（現場收款的訂單沒有）。</summary>
    public string? LinepayTransactionId { get; init; }
    public required string PaymentStatus { get; init; }
    public required string PaymentStatusLabel { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PaymentMethodLabel { get; init; }
    public required string OrderStatus { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public required string ShipmentStatusLabel { get; init; }
    public required bool IsMember { get; init; }
    public Guid? MemberId { get; init; }
    public string? MemberNo { get; init; }
    public required bool IsManual { get; init; }
    public required Guid SellingClubId { get; init; }
    public required string SellingClubCode { get; init; }
    public string? SellingClubName { get; init; }
    public required Guid CollectingClubId { get; init; }
    public string? CollectingClubName { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }

    /// <summary>買家 Email（客服聯絡用，訪客單沒有其他聯絡方式）。視同個資：沒有 <c>shop.order.reveal</c> 時回遮罩值。</summary>
    public string? BuyerEmail { get; init; }
    public string? CustomerNote { get; init; }
    public string? InternalNote { get; init; }
    public required string SettlementStatus { get; init; }
    public required string SettlementStatusLabel { get; init; }
    public DateOnly? SettledOn { get; init; }
    public string? SettlementNote { get; init; }
    public required IReadOnlyList<AdminOrderItemDto> Items { get; init; }
    public AdminOrderShipmentDto? Shipment { get; init; }
    public AdminOrderInvoiceDto? Invoice { get; init; }
    public required IReadOnlyList<AdminOrderRefundSummaryDto> Refunds { get; init; }

    /// <summary>目前這張訂單能做的動作（畫面依此顯示按鈕）：<c>mark_paid</c>、<c>prepare</c>、<c>ship</c>、<c>complete</c>、<c>cancel</c>、<c>request_refund</c>。</summary>
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public required bool IsMasked { get; init; }
    public required bool CanReveal { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminOrderLineInput
{
    public required Guid VariantId { get; init; }
    public required int Quantity { get; init; }
}

public sealed record CreateAdminOrderRequest
{
    public required IReadOnlyList<AdminOrderLineInput> Items { get; init; }

    /// <summary><c>home_delivery</c> 宅配／<c>cvs_pickup</c> 超商取貨／<c>onsite_pickup</c> 現場自取。</summary>
    public required string DeliveryMethod { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }

    /// <summary>綁定會員（選填）；省略＝非會員。</summary>
    public Guid? MemberId { get; init; }
    public string? CustomerNote { get; init; }
    public string? InternalNote { get; init; }

    /// <summary>覆寫運費（元）；省略＝依商店設定計算（現場自取免運）。</summary>
    public int? ShippingFee { get; init; }

    /// <summary>現場自取且當場交貨時設為 <c>true</c>，訂單直接完成。</summary>
    public bool CompleteImmediately { get; init; }
}

public sealed record CancelAdminOrderRequest
{
    public required string Reason { get; init; }
}

public sealed record UpdateAdminOrderNotesRequest
{
    /// <summary>內部註記（空白＝清除）。顧客備註是顧客自己填的，後台不改。</summary>
    public string? InternalNote { get; init; }
}

public sealed record UpdateAdminOrderSettlementRequest
{
    /// <summary><c>pending</c> 待結算／<c>settled</c> 已結算。這只是人工標記的旗標，不是狀態機；系統不計算應付金額。</summary>
    public required string Status { get; init; }

    /// <summary>結算日期；標為已結算時省略＝今天，標回待結算時清除。</summary>
    public DateOnly? SettledOn { get; init; }
    public string? Note { get; init; }
}

public sealed record BatchAdminOrderSettlementRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public required string Status { get; init; }
    public DateOnly? SettledOn { get; init; }
    public string? Note { get; init; }
}

public sealed record ReleaseExpiredOrdersResultDto
{
    public required int ExpiredCount { get; init; }
    public required int TimeoutMinutes { get; init; }
}

// ───────────── S4 出貨與物流 ─────────────

public sealed record ShipAdminOrderRequest
{
    /// <summary>物流商（宅配、超商取貨）。</summary>
    public string? Carrier { get; init; }

    /// <summary>物流單號；可先出貨、之後再回填。</summary>
    public string? TrackingNo { get; init; }

    /// <summary>超商取貨的門市代碼。</summary>
    public string? StoreBranchCode { get; init; }

    /// <summary>現場自取／超商取貨的領取期限。</summary>
    public DateOnly? PickupDeadlineOn { get; init; }
}

public sealed record UpdateAdminShipmentRequest
{
    public string? Carrier { get; init; }
    public string? TrackingNo { get; init; }
    public string? StoreBranchCode { get; init; }
    public DateOnly? PickupDeadlineOn { get; init; }
}

public sealed record ArrivalNotifiedRequest
{
    /// <summary>領取期限；省略＝沿用既有期限。</summary>
    public DateOnly? PickupDeadlineOn { get; init; }
}

public sealed record BatchShipAdminOrdersRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public string? Carrier { get; init; }
}

public sealed record AdminShipmentListItemDto
{
    public required Guid OrderId { get; init; }
    public required string OrderNo { get; init; }
    public required string OrderStatus { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? RecipientName { get; init; }
    public required int ItemCount { get; init; }
    public AdminOrderShipmentDto? Shipment { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record AdminPickingLineDto
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required int Quantity { get; init; }
    public required int OrderCount { get; init; }
}

public sealed record AdminPickingListDto
{
    public required DateTime GeneratedAt { get; init; }
    public required int OrderCount { get; init; }
    public required int TotalQuantity { get; init; }
    public required IReadOnlyList<AdminPickingLineDto> Lines { get; init; }
}

public sealed record AdminDispatchSlipItemDto
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required int Quantity { get; init; }
}

public sealed record AdminDispatchSlipDto
{
    public required Guid OrderId { get; init; }
    public required string OrderNo { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }
    public string? StoreBranchCode { get; init; }
    public string? CustomerNote { get; init; }
    public required IReadOnlyList<AdminDispatchSlipItemDto> Items { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record AdminShipmentImportSkippedDto
{
    public required int Row { get; init; }
    public string? OrderNo { get; init; }
    public required string Reason { get; init; }
}

public sealed record AdminShipmentImportResultDto
{
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<AdminShipmentImportSkippedDto> Skipped { get; init; }
}
