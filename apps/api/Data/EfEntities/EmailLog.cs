using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EmailLog
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? EmailTemplateId { get; set; }

    public string Type { get; set; } = null!;

    public string ToEmail { get; set; } = null!;

    public Guid? MemberId { get; set; }

    public string SendStatus { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual EmailTemplate? EmailTemplate { get; set; }

    public virtual Member? Member { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
