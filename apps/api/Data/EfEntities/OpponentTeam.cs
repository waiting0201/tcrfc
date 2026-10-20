using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

/// <summary>對手球隊庫（主站規劃書 v3.31）：俱樂部範圍的對手隊伍主檔（隊名中文＋英文側表、隊徽圖片欄位組）。</summary>
public partial class OpponentTeam
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>隊名（中文預設，基礎表欄位；英文在 opponent_teams_i18n）。</summary>
    public string Name { get; set; } = null!;

    public string? CrestKey { get; set; }

    public int? CrestWidth { get; set; }

    public int? CrestHeight { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual ICollection<OpponentTeamsI18n> OpponentTeamsI18ns { get; set; } = new List<OpponentTeamsI18n>();

    public virtual ICollection<Standing> Standings { get; set; } = new List<Standing>();
}
