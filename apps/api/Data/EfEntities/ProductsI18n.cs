using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ProductsI18n
{
    public Guid ProductId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Narrative { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? Tags { get; set; }

    public virtual Product Product { get; set; } = null!;
}
