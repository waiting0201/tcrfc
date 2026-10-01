using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class ReconciliationRun
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public DateOnly RunOn { get; set; }

    public string Source { get; set; } = null!;

    public int ComparedCount { get; set; }

    public int MatchedCount { get; set; }

    public int DiscrepancyCount { get; set; }

    public DateTime RanAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<ReconciliationDiscrepancy> ReconciliationDiscrepancies { get; set; } = new List<ReconciliationDiscrepancy>();
}
