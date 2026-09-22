using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarEvent
{
    public string SourceType { get; set; } = null!;

    public Guid SourceId { get; set; }

    public Guid? EventTypeId { get; set; }

    public DateTime? StartsAt { get; set; }

    public bool? IsAllDay { get; set; }

    public Guid? VenueId { get; set; }

    public Guid ClubId { get; set; }
}
