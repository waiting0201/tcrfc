using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ImpactMetricsI18n
{
    public Guid ImpactMetricId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual ImpactMetric ImpactMetric { get; set; } = null!;
}
