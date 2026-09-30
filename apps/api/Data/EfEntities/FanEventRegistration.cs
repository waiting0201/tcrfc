using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FanEventRegistration
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid FanEventId { get; set; }

    public Guid? MemberId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? ApplicantName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Note { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual FanEvent FanEvent { get; set; } = null!;

    public virtual Member? Member { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
