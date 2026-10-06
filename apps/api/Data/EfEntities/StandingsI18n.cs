using System;
using System.Collections.Generic;
namespace Tcrfc.Api.Data.EfEntities;

public partial class StandingsI18n
{
    public Guid StandingId { get; set; }
    public string Locale { get; set; } = null!;
    public string? TeamName { get; set; }
    public virtual Standing Standing { get; set; } = null!;
}
