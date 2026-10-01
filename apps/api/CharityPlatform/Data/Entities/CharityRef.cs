using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class CharityRef
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string RefCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime ImportedAt { get; set; }

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CharityProgramRef> CharityProgramRefs { get; set; } = new List<CharityProgramRef>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
