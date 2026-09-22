using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarCustomEvent
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? EventTypeId { get; set; }

    public Guid? VenueId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public bool IsAllDay { get; set; }

    public string? RepeatRule { get; set; }

    public bool IsPublic { get; set; }

    public string? CoverKey { get; set; }

    public string? CtaUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CalendarCustomEventsI18n> CalendarCustomEventsI18ns { get; set; } = new List<CalendarCustomEventsI18n>();

    public virtual ICollection<CalendarEventException> CalendarEventExceptions { get; set; } = new List<CalendarEventException>();

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual EventType? EventType { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }
}
