using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class RefundRequestItem
{
    public Guid RefundRequestId { get; set; }

    public Guid OrderItemId { get; set; }

    public int Quantity { get; set; }

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual RefundRequest RefundRequest { get; set; } = null!;
}
