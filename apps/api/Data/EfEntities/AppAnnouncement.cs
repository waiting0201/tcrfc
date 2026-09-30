using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppAnnouncement
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string? LinkUrl { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string AudienceTier { get; set; } = null!;

    public Guid? AudienceClubId { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AppAnnouncementsI18n> AppAnnouncementsI18ns { get; set; } = new List<AppAnnouncementsI18n>();

    public virtual Club? AudienceClub { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
