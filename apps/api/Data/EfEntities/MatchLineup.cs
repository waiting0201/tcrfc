using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MatchLineup
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MatchId { get; set; }

    public Guid PlayerId { get; set; }

    public bool IsStarter { get; set; }

    /// <summary>替補是否有上場（出賽判定＝先發或 came_on）。先發恆存 false；有進球／牌的替補由後台儲存時強制為 true。</summary>
    public bool CameOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Player Player { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
