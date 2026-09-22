using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EventType
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public string? Colour { get; set; }

    public string? Icon { get; set; }

    public bool IsPublic { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CalendarCustomEvent> CalendarCustomEvents { get; set; } = new List<CalendarCustomEvent>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EventTypesI18n> EventTypesI18ns { get; set; } = new List<EventTypesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
