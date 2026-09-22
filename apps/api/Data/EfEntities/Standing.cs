using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Standing
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid SeasonId { get; set; }

    public string TeamName { get; set; } = null!;

    public int? Rank { get; set; }

    public int? Played { get; set; }

    public int? Points { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
