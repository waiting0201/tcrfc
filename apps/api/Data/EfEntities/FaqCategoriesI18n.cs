using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FaqCategoriesI18n
{
    public Guid FaqCategoryId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual FaqCategory FaqCategory { get; set; } = null!;
}
