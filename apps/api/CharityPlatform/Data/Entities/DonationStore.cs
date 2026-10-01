using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationStore
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string StoreSlug { get; set; } = null!;

    public string? Category { get; set; }

    public string? Address { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public decimal StoreSharePct { get; set; }

    public string? LogoKey { get; set; }

    public int? LogoWidth { get; set; }

    public int? LogoHeight { get; set; }

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DonationStoresI18n> DonationStoresI18ns { get; set; } = new List<DonationStoresI18n>();

    public virtual ICollection<Donation> Donations { get; set; } = new List<Donation>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
