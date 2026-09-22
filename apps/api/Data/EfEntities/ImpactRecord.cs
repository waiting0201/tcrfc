using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ImpactRecord
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public Guid? CharityProgramId { get; set; }

    public Guid CharityId { get; set; }

    public string? ImageKey { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public DateOnly? HappenedOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Charity Charity { get; set; } = null!;

    public virtual CharityProgram? CharityProgram { get; set; }

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<ImpactRecordImage> ImpactRecordImages { get; set; } = new List<ImpactRecordImage>();

    public virtual ICollection<ImpactRecordsI18n> ImpactRecordsI18ns { get; set; } = new List<ImpactRecordsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
