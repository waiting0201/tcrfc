using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MenuItemsI18n
{
    public Guid MenuItemId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Label { get; set; }

    public virtual MenuItem MenuItem { get; set; } = null!;
}
