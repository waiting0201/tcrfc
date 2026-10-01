using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationInvoice
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid DonationId { get; set; }

    public string InvoiceType { get; set; } = null!;

    public string? InvoiceNo { get; set; }

    public DateTime? IssuedAt { get; set; }

    public string? CarrierType { get; set; }

    public string? CarrierIdEncrypted { get; set; }

    public string? TaxId { get; set; }

    public string? NationalIdEncrypted { get; set; }

    public string? ReceiptAddress { get; set; }

    public string? InvoiceTitle { get; set; }

    public string? ReceiptTitle { get; set; }

    public bool IsAnnualSummary { get; set; }

    public string IssueStatus { get; set; } = null!;

    public string VoidStatus { get; set; } = null!;

    public string? VoidReason { get; set; }

    public Guid? VoidedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Donation Donation { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual AdminUser? VoidedByNavigation { get; set; }
}
