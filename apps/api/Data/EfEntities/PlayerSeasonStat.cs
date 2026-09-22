using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PlayerSeasonStat
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid PlayerId { get; set; }

    public Guid SeasonId { get; set; }

    public int Appearances { get; set; }

    public int Goals { get; set; }

    public int Assists { get; set; }

    public int YellowCards { get; set; }

    public int RedCards { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Player Player { get; set; } = null!;

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
