using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class Setting
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string SettingKey { get; set; } = null!;

    public string? Value { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<SettingsI18n> SettingsI18ns { get; set; } = new List<SettingsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
