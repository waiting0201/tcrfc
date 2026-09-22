using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Staff
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string? StaffGroup { get; set; }

    public string? Licence { get; set; }

    public string? PhotoKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<StaffI18n> StaffI18ns { get; set; } = new List<StaffI18n>();

    public virtual ICollection<StaffTeam> StaffTeams { get; set; } = new List<StaffTeam>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<TrainingProgram> Programs { get; set; } = new List<TrainingProgram>();
}
