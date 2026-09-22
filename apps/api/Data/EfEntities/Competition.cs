using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Competition
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid SeasonId { get; set; }

    public string Code { get; set; } = null!;

    public string? CompType { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual ICollection<CompetitionsI18n> CompetitionsI18ns { get; set; } = new List<CompetitionsI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
