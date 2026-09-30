using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppFeatureFlag
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string FlagKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? StringValue { get; set; }

    public string Platform { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
