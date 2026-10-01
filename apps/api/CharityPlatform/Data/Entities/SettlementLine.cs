using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class SettlementLine
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid SettlementId { get; set; }

    public Guid DonationId { get; set; }

    public int ShareAmount { get; set; }

    public bool IsClawback { get; set; }

    public string? ClawbackReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Donation Donation { get; set; } = null!;

    public virtual Settlement Settlement { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
