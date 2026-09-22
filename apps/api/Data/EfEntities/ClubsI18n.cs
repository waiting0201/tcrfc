using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ClubsI18n
{
    public Guid ClubId { get; set; }

    public string Locale { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
