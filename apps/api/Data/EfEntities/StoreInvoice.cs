using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class StoreInvoice
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? PaymentChannelId { get; set; }

    public string? InvoiceNo { get; set; }

    public DateTime? IssuedAt { get; set; }

    public string? CarrierType { get; set; }

    public string? CarrierIdEncrypted { get; set; }

    public string? TaxId { get; set; }

    public string? DonationCode { get; set; }

    public string IssueStatus { get; set; } = null!;

    public int RetryCount { get; set; }

    public string VoidStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual PaymentChannel? PaymentChannel { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
