using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class NewsletterSubscriber
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Email { get; set; } = null!;

    public string? Source { get; set; }

    public string? Status { get; set; }

    public DateTime? SubscribedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
