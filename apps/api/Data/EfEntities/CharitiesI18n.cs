using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CharitiesI18n
{
    public Guid CharityId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Intro { get; set; }

    public virtual Charity Charity { get; set; } = null!;
}
