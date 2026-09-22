using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorsI18n
{
    public Guid SponsorId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Content { get; set; }

    public virtual Sponsor Sponsor { get; set; } = null!;
}
