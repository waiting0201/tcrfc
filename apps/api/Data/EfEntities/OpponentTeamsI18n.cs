using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class OpponentTeamsI18n
{
    public Guid OpponentTeamId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public virtual OpponentTeam OpponentTeam { get; set; } = null!;
}
