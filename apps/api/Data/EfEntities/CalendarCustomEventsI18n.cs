using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarCustomEventsI18n
{
    public Guid CalendarCustomEventId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public virtual CalendarCustomEvent CalendarCustomEvent { get; set; } = null!;
}
