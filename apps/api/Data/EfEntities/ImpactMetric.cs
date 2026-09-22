using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ImpactMetric
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public Guid CharityProgramId { get; set; }

    public string MetricKey { get; set; } = null!;

    public int? MetricValue { get; set; }

    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual CharityProgram CharityProgram { get; set; } = null!;

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<ImpactMetricsI18n> ImpactMetricsI18ns { get; set; } = new List<ImpactMetricsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
