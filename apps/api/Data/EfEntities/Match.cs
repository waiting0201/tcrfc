using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Match
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid SeasonId { get; set; }

    public Guid? CompetitionId { get; set; }

    public Guid? VenueId { get; set; }

    public DateOnly MatchOn { get; set; }

    public string? Kickoff { get; set; }

    public string? HomeAway { get; set; }

    public string? Opponent { get; set; }

    public string? Competition { get; set; }

    public string? Status { get; set; }

    public int? ScoreHome { get; set; }

    public int? ScoreAway { get; set; }

    public int? RoundNo { get; set; }

    public int? MatchNo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual Competition? CompetitionNavigation { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MatchCard> MatchCards { get; set; } = new List<MatchCard>();

    public virtual ICollection<MatchGoal> MatchGoals { get; set; } = new List<MatchGoal>();

    public virtual ICollection<MatchLineup> MatchLineups { get; set; } = new List<MatchLineup>();

    public virtual ICollection<MatchesI18n> MatchesI18ns { get; set; } = new List<MatchesI18n>();

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
}
