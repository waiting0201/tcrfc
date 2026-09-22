using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PaymentChannel
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid OwnerClubId { get; set; }

    public string ChannelType { get; set; } = null!;

    public string Environment { get; set; } = null!;

    public string CredentialEncrypted { get; set; } = null!;

    public string? InvoicePrefix { get; set; }

    public DateTime? RotatedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Club OwnerClub { get; set; } = null!;

    public virtual ICollection<StoreInvoice> StoreInvoices { get; set; } = new List<StoreInvoice>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
