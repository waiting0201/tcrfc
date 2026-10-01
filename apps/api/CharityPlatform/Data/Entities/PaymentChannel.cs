using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class PaymentChannel
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

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

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
