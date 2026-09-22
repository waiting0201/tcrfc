using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Locale
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsDefault { get; set; }

    public string? FallbackCode { get; set; }

    public bool IsEnabled { get; set; }

    public int SortOrder { get; set; }

    public virtual ICollection<ClubsI18n> ClubsI18ns { get; set; } = new List<ClubsI18n>();

    public virtual ICollection<CompetitionsI18n> CompetitionsI18ns { get; set; } = new List<CompetitionsI18n>();
}
