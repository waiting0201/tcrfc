using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Session
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid? VenueId { get; set; }

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public string? WeeklySchedule { get; set; }

    public int? Capacity { get; set; }

    public int EnrolledCount { get; set; }

    public int? Price { get; set; }

    public int? EarlyBirdPrice { get; set; }

    public DateOnly? EarlyBirdUntil { get; set; }

    public DateTime? SignupOpensAt { get; set; }

    public DateTime? SignupClosesAt { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual TrainingProgram TrainingProgram { get; set; } = null!;

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }
}
