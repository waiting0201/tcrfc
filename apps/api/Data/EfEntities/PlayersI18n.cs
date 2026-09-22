using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PlayersI18n
{
    public Guid PlayerId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Bio { get; set; }

    public virtual Player Player { get; set; } = null!;
}
