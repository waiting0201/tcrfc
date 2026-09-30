using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppLayoutItemsI18n
{
    public Guid AppLayoutItemId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Label { get; set; }

    public virtual AppLayoutItem AppLayoutItem { get; set; } = null!;
}
