using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class Settlement
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public string PayeeType { get; set; } = null!;

    public Guid PayeeId { get; set; }

    public int DonationCount { get; set; }

    public int DonationTotal { get; set; }

    public int PayableAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? RemittedOn { get; set; }

    public string? RemitMethod { get; set; }

    public string? RemitNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<SettlementLine> SettlementLines { get; set; } = new List<SettlementLine>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
