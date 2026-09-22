using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Venue
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public decimal? Lat { get; set; }

    public decimal? Lng { get; set; }

    public string? PhotoKey { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CalendarCustomEvent> CalendarCustomEvents { get; set; } = new List<CalendarCustomEvent>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual ICollection<Trial> Trials { get; set; } = new List<Trial>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<VenuesI18n> VenuesI18ns { get; set; } = new List<VenuesI18n>();
}
