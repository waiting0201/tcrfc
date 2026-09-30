using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarTeamSettingsI18n
{
    public Guid CalendarTeamSettingId { get; set; }

    public string Locale { get; set; } = null!;

    public string? DisplayName { get; set; }

    public virtual CalendarTeamSetting CalendarTeamSetting { get; set; } = null!;
}
