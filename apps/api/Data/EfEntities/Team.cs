using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Team
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Gender { get; set; } = null!;

    public string? AgeBand { get; set; }

    public string? HeroKey { get; set; }

    public string? TeamColor { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();

    public virtual ICollection<AdminUserTeam> AdminUserTeams { get; set; } = new List<AdminUserTeam>();

    public virtual ICollection<CalendarEventTeam> CalendarEventTeams { get; set; } = new List<CalendarEventTeam>();

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<Player> Players { get; set; } = new List<Player>();

    public virtual ICollection<StaffTeam> StaffTeams { get; set; } = new List<StaffTeam>();

    public virtual ICollection<TeamsI18n> TeamsI18ns { get; set; } = new List<TeamsI18n>();

    public virtual ICollection<Trial> Trials { get; set; } = new List<Trial>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();
}
