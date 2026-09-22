using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class TagsI18n
{
    public Guid TagId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual Tag Tag { get; set; } = null!;
}
