using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarEventException
{
    public Guid CalendarCustomEventId { get; set; }

    public DateOnly ExcludedOn { get; set; }

    public virtual CalendarCustomEvent CalendarCustomEvent { get; set; } = null!;
}
