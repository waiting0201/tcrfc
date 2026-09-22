using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FanEventsI18n
{
    public Guid FanEventId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public virtual FanEvent FanEvent { get; set; } = null!;
}
