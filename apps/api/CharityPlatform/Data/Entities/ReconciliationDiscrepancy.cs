using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class ReconciliationDiscrepancy
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ReconciliationRunId { get; set; }

    public string DiscrepancyType { get; set; } = null!;

    public Guid? DonationId { get; set; }

    public string? GatewayTransactionId { get; set; }

    public int? SiteAmount { get; set; }

    public int? GatewayAmount { get; set; }

    public string ResolutionStatus { get; set; } = null!;

    public Guid? ResolvedBy { get; set; }

    public string? ResolveNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Donation? Donation { get; set; }

    public virtual ReconciliationRun ReconciliationRun { get; set; } = null!;
}
