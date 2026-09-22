using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CompetitionsI18n
{
    public Guid CompetitionId { get; set; }

    public string Locale { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Organizer { get; set; }

    public virtual Competition Competition { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
