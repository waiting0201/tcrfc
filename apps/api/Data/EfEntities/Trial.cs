using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Trial
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? TeamId { get; set; }

    public Guid? VenueId { get; set; }

    public DateOnly TrialOn { get; set; }

    public int? Capacity { get; set; }

    public DateOnly? DeadlineOn { get; set; }

    public bool SyncToCalendar { get; set; }

    public int EnrolledCount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual Team? Team { get; set; }

    public virtual ICollection<TrialsI18n> TrialsI18ns { get; set; } = new List<TrialsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }
}
