using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarTeamSetting
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid TeamId { get; set; }

    public string? Colour { get; set; }

    public int? SortOrder { get; set; }

    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CalendarTeamSettingsI18n> CalendarTeamSettingsI18ns { get; set; } = new List<CalendarTeamSettingsI18n>();

    public virtual Club Club { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}
