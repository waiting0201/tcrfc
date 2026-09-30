using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppCredential
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Kind { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string? ExternalRef { get; set; }

    public DateOnly CreatedOn { get; set; }

    public DateOnly? LastRotatedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public int? RotationPeriodDays { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
