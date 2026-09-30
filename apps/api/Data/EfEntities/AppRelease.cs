using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppRelease
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Platform { get; set; } = null!;

    public string Version { get; set; } = null!;

    public string? BuildNumber { get; set; }

    public DateOnly? ReleasedOn { get; set; }

    public string Status { get; set; } = null!;

    public bool IsMinSupported { get; set; }

    public bool IsRecommended { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AppReleasesI18n> AppReleasesI18ns { get; set; } = new List<AppReleasesI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
