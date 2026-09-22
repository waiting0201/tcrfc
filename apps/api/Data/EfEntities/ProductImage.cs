using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ProductImage
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ProductId { get; set; }

    public string ImageKey { get; set; } = null!;

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
