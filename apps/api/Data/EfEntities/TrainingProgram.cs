using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class TrainingProgram
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string? ProgramType { get; set; }

    public string? Audience { get; set; }

    public int? AgeMin { get; set; }

    public int? AgeMax { get; set; }

    public string? Status { get; set; }

    public string? CoverKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<ProgramsI18n> ProgramsI18ns { get; set; } = new List<ProgramsI18n>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Partner> Partners { get; set; } = new List<Partner>();

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();
}
