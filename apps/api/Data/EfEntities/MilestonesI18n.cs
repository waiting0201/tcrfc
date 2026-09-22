using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MilestonesI18n
{
    public Guid MilestoneId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public virtual Milestone Milestone { get; set; } = null!;
}
