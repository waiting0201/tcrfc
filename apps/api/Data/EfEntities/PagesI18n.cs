using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PagesI18n
{
    public Guid PageId { get; set; }

    public string Locale { get; set; } = null!;

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? SeoKeywords { get; set; }

    public string? OgImageAlt { get; set; }

    public virtual Page Page { get; set; } = null!;
}
