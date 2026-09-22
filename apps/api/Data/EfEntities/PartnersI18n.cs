using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PartnersI18n
{
    public Guid PartnerId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}
