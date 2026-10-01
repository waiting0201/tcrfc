using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class SettingsI18n
{
    public Guid SettingId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Value { get; set; }

    public virtual Locale LocaleNavigation { get; set; } = null!;

    public virtual Setting Setting { get; set; } = null!;
}
