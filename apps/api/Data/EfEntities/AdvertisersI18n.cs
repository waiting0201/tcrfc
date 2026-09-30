using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdvertisersI18n
{
    public Guid AdvertiserId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual Advertiser Advertiser { get; set; } = null!;
}
