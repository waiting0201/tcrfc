using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class TeamsI18n
{
    public Guid TeamId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Intro { get; set; }

    public virtual Team Team { get; set; } = null!;
}
