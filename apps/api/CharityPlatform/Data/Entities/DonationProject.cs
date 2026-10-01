using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationProject
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string ProjectSlug { get; set; } = null!;

    public int? MinAmount { get; set; }

    public int? MaxAmount { get; set; }

    public decimal ProjectSharePct { get; set; }

    public string InvoiceMode { get; set; } = null!;

    public string? CharityRefCode { get; set; }

    public string? CharityNameSnapshot { get; set; }

    public string? CharityProgramRefCode { get; set; }

    public string? CharityProgramNameSnapshot { get; set; }

    public string? CoverKey { get; set; }

    public int? CoverWidth { get; set; }

    public int? CoverHeight { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DonationAmountOption> DonationAmountOptions { get; set; } = new List<DonationAmountOption>();

    public virtual ICollection<DonationProjectsI18n> DonationProjectsI18ns { get; set; } = new List<DonationProjectsI18n>();

    public virtual ICollection<Donation> Donations { get; set; } = new List<Donation>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
