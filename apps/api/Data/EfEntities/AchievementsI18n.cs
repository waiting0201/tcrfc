using System;
using System.Collections.Generic;
namespace Tcrfc.Api.Data.EfEntities;

public partial class AchievementsI18n
{
    public Guid AchievementId { get; set; }
    public string Locale { get; set; } = null!;
    public string? CompetitionName { get; set; }
    public string? Placing { get; set; }
    public virtual Achievement Achievement { get; set; } = null!;
}
