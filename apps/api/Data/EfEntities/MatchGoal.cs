using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MatchGoal
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MatchId { get; set; }

    public Guid PlayerId { get; set; }

    public int? Minute { get; set; }

    public string? GoalType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Player Player { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
