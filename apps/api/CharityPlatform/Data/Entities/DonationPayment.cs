using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationPayment
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid DonationId { get; set; }

    public string? TransactionId { get; set; }

    public DateTime? RequestedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public int Amount { get; set; }

    public string Status { get; set; } = null!;

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Donation Donation { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
