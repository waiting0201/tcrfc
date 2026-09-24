using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Player
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid TeamId { get; set; }

    public int? ShirtNo { get; set; }

    public string? Position { get; set; }

    public DateOnly? BirthOn { get; set; }

    public int? HeightCm { get; set; }

    public int? WeightKg { get; set; }

    public string? Nationality { get; set; }

    public string? PreferredFoot { get; set; }

    public DateOnly? JoinedOn { get; set; }

    public string? Status { get; set; }

    public string? PhotoKey { get; set; }

    /// <summary>肖像同意狀態（S1-8／S1-7a，三態：not_consented／consented／consented_by_guardian，
    /// fail-closed 預設 not_consented）。公開端點在此值為 not_consented 時不得輸出 <see cref="PhotoKey"/>，
    /// 見 Features/Players/PlayersRepository.cs。</summary>
    public string PortraitConsentStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual ICollection<ComicCharacter> ComicCharacters { get; set; } = new List<ComicCharacter>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MatchCard> MatchCards { get; set; } = new List<MatchCard>();

    public virtual ICollection<MatchGoal> MatchGoals { get; set; } = new List<MatchGoal>();

    public virtual ICollection<MatchLineup> MatchLineups { get; set; } = new List<MatchLineup>();

    public virtual ICollection<PlayerSeasonStat> PlayerSeasonStats { get; set; } = new List<PlayerSeasonStat>();

    public virtual ICollection<PlayersI18n> PlayersI18ns { get; set; } = new List<PlayersI18n>();

    public virtual Team Team { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
