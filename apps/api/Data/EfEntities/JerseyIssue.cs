using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class JerseyIssue
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid MemberId { get; set; }

    public string RecipientName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Size { get; set; }

    public string? DeliveryMethod { get; set; }

    public string? Address { get; set; }

    public string? Status { get; set; }

    public DateOnly? ShippedOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
