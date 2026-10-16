using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Order
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string OrderNo { get; set; } = null!;

    public Guid ClubId { get; set; }

    public Guid SellingClubId { get; set; }

    public Guid CollectingClubId { get; set; }

    public Guid? MemberId { get; set; }

    public string LookupToken { get; set; } = null!;

    public string? RecipientName { get; set; }

    public string? RecipientPhone { get; set; }

    public string? RecipientAddress { get; set; }

    public int Subtotal { get; set; }

    public int ShippingFee { get; set; }

    public int Total { get; set; }

    public string? LinepayTransactionId { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public string OrderStatus { get; set; } = null!;

    public string? DeliveryMethod { get; set; }

    public bool IsManual { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>因商店會員折扣少收的總額（已含在 <see cref="Subtotal"/> 內，即小計已是折後金額）。</summary>
    public int MemberDiscountAmount { get; set; }

    /// <summary>套用會員折扣當時的會籍方案（值複製）；沒有套用折扣為空。</summary>
    public Guid? MemberDiscountPlanId { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? CustomerNote { get; set; }

    public string? InternalNote { get; set; }

    public string SettlementStatus { get; set; } = null!;

    public DateOnly? SettledOn { get; set; }

    public string? SettlementNote { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual Club CollectingClub { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual Member? Member { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();

    public virtual Club SellingClub { get; set; } = null!;

    public virtual Shipment? Shipment { get; set; }

    public virtual ICollection<StoreInvoice> StoreInvoices { get; set; } = new List<StoreInvoice>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
