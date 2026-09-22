using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class RefundRequest
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid OrderId { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public int? RefundAmount { get; set; }

    public string? RefundMethod { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? RefundedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? ApprovedByNavigation { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<RefundRequestItem> RefundRequestItems { get; set; } = new List<RefundRequestItem>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
