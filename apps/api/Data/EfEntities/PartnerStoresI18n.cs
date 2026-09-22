using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PartnerStoresI18n
{
    public Guid PartnerStoreId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? OfferContent { get; set; }

    public virtual PartnerStore PartnerStore { get; set; } = null!;
}
