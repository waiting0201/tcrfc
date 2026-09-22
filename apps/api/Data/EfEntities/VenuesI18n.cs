using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class VenuesI18n
{
    public Guid VenueId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? Directions { get; set; }

    public virtual Venue Venue { get; set; } = null!;
}
