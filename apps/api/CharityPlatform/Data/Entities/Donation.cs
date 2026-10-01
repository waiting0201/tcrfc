using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class Donation
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string OrderNo { get; set; } = null!;

    public Guid DonationProjectId { get; set; }

    public Guid? DonationStoreId { get; set; }

    public int Amount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string DonorName { get; set; } = null!;

    public string DonorEmail { get; set; } = null!;

    public bool IsAnonymous { get; set; }

    public decimal StoreSharePctSnapshot { get; set; }

    public decimal ProjectSharePctSnapshot { get; set; }

    public int StoreAmount { get; set; }

    public int ProjectAmount { get; set; }

    public int AssociationAmount { get; set; }

    public string InvoiceMode { get; set; } = null!;

    public string? RefundReason { get; set; }

    public Guid? RefundedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DonationInvoice> DonationInvoices { get; set; } = new List<DonationInvoice>();

    public virtual ICollection<DonationPayment> DonationPayments { get; set; } = new List<DonationPayment>();

    public virtual DonationProject DonationProject { get; set; } = null!;

    public virtual DonationStore? DonationStore { get; set; }

    public virtual ICollection<ReconciliationDiscrepancy> ReconciliationDiscrepancies { get; set; } = new List<ReconciliationDiscrepancy>();

    public virtual AdminUser? RefundedByNavigation { get; set; }

    public virtual ICollection<SettlementLine> SettlementLines { get; set; } = new List<SettlementLine>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
