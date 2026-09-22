using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class StaffI18n
{
    public Guid StaffId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Title { get; set; }

    public string? Bio { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
