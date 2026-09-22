using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class InventoryMovement
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid ProductVariantId { get; set; }

    public Guid? OrderId { get; set; }

    public string? MovementType { get; set; }

    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public Guid? HandledBy { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
