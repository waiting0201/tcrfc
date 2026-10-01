using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationAmountOption
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid DonationProjectId { get; set; }

    public int Amount { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual DonationProject DonationProject { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
