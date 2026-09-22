using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ArticleCategoriesI18n
{
    public Guid ArticleCategoryId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual ArticleCategory ArticleCategory { get; set; } = null!;
}
