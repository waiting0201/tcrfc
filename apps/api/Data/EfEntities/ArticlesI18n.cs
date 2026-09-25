using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ArticlesI18n
{
    public Guid ArticleId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Body { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? SeoKeywords { get; set; }

    public string? OgImageAlt { get; set; }

    public virtual Article Article { get; set; } = null!;
}
