using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarEventTeam
{
    public string SourceType { get; set; } = null!;

    public Guid SourceId { get; set; }

    public Guid TeamId { get; set; }

    public virtual Team Team { get; set; } = null!;
}
