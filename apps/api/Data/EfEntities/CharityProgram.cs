using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CharityProgram
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public Guid CharityId { get; set; }

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public string Status { get; set; } = null!;

    public string? CoverKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public int SortOrder { get; set; }

    public bool IsPinned { get; set; }

    public virtual Charity Charity { get; set; } = null!;

    public virtual ICollection<CharityProgramArticle> CharityProgramArticles { get; set; } = new List<CharityProgramArticle>();

    public virtual ICollection<CharityProgramImage> CharityProgramImages { get; set; } = new List<CharityProgramImage>();

    public virtual ICollection<CharityProgramsI18n> CharityProgramsI18ns { get; set; } = new List<CharityProgramsI18n>();

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<ImpactMetric> ImpactMetrics { get; set; } = new List<ImpactMetric>();

    public virtual ICollection<ImpactRecord> ImpactRecords { get; set; } = new List<ImpactRecord>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Partner> Partners { get; set; } = new List<Partner>();

    public virtual ICollection<Sponsor> Sponsors { get; set; } = new List<Sponsor>();
}
