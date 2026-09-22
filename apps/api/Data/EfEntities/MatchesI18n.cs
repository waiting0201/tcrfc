using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MatchesI18n
{
    public Guid MatchId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Opponent { get; set; }

    public string? Venue { get; set; }

    public virtual Match Match { get; set; } = null!;
}
