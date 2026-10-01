using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class EmailLog
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid? EmailTemplateId { get; set; }

    public string Type { get; set; } = null!;

    public string RecipientEmail { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual EmailTemplate? EmailTemplate { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
