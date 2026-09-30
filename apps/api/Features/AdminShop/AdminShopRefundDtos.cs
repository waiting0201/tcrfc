namespace Tcrfc.Api.Features.AdminShop;

public sealed record AdminRefundItemInput
{
    public required Guid OrderItemId { get; init; }
    public required int Quantity { get; init; }
}

public sealed record CreateAdminRefundRequest
{
    public required Guid OrderId { get; init; }
    public required string Reason { get; init; }

    /// <summary>要退的品項與數量（支援部分退款）。</summary>
    public required IReadOnlyList<AdminRefundItemInput> Items { get; init; }

    /// <summary>退款金額（元）；省略＝所退品項的小計。要連運費一起退時自行填寫，不得超過訂單尚可退的金額。</summary>
    public int? RefundAmount { get; init; }

    /// <summary>是否需要顧客退回商品（預設 <c>true</c>）。不需退回的案件核准後即可執行退款。</summary>
    public bool? NeedsReturn { get; init; }
}

public sealed record ReviewAdminRefundRequest
{
    /// <summary>審核意見。駁回時必填。</summary>
    public string? Note { get; init; }
}

public sealed record ReceiveAdminRefundRequest
{
    /// <summary>驗收退回品後是否把商品回補庫存（預設 <c>true</c>；商品損毀無法再賣時請設為 <c>false</c>）。</summary>
    public bool? Restock { get; init; }
    public string? Note { get; init; }
}

public sealed record ExecuteAdminRefundRequest
{
    /// <summary>備註（現場收款訂單的人工退款：說明退款方式，例如「現場退現金」）。</summary>
    public string? Note { get; init; }
}

public sealed record AdminRefundItemDto
{
    public required Guid OrderItemId { get; init; }
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required int UnitPrice { get; init; }
}

public sealed record AdminRefundListItemDto
{
    public required Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public int? RefundAmount { get; init; }
    public string? Reason { get; init; }
    public required bool NeedsReturn { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PaymentMethodLabel { get; init; }
    public string? RefundMethod { get; init; }
    public string? RefundMethodLabel { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminRefundDetailDto
{
    public required Guid Id { get; init; }
    public required Guid OrderId { get; init; }
    public required string OrderNo { get; init; }
    public required string OrderStatus { get; init; }
    public required int OrderTotal { get; init; }

    /// <summary>這張訂單已退款的金額合計（含本案若已退）。</summary>
    public required int OrderRefundedTotal { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PaymentMethodLabel { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public int? RefundAmount { get; init; }
    public string? Reason { get; init; }
    public required bool NeedsReturn { get; init; }
    public string? ReviewNote { get; init; }
    public string? ApprovedByName { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public string? ReceivedByName { get; init; }
    public string? RefundMethod { get; init; }
    public string? RefundMethodLabel { get; init; }
    public string? RefundReference { get; init; }
    public DateTime? RefundedAt { get; init; }
    public string? RefundedByName { get; init; }
    public required IReadOnlyList<AdminRefundItemDto> Items { get; init; }

    /// <summary>目前能做的動作：<c>approve</c>、<c>reject</c>、<c>receive</c>、<c>execute</c>（執行退款只有系統管理員）。</summary>
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminRefundExecuteResultDto
{
    public required AdminRefundDetailDto Refund { get; init; }

    /// <summary>發票處理提示（有已開立的發票才有）：作廢或折讓已登記，實際作廢需在發票服務端完成時會註明。</summary>
    public string? InvoiceAction { get; init; }
}
