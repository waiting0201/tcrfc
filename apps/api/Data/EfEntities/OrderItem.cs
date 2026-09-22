using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class OrderItem
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductVariantId { get; set; }

    public string ProductNameSnapshot { get; set; } = null!;

    public string? VariantLabelSnapshot { get; set; }

    public string SkuSnapshot { get; set; } = null!;

    public int UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public int LineTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ProductVariant ProductVariant { get; set; } = null!;

    public virtual ICollection<RefundRequestItem> RefundRequestItems { get; set; } = new List<RefundRequestItem>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
