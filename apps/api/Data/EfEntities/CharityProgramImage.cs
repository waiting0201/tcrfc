using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CharityProgramImage
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid CharityProgramId { get; set; }

    public string ImageKey { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual CharityProgram CharityProgram { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
