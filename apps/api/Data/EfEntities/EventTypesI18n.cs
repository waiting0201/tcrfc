using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EventTypesI18n
{
    public Guid EventTypeId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual EventType EventType { get; set; } = null!;
}
