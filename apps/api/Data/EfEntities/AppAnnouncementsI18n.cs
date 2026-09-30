using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppAnnouncementsI18n
{
    public Guid AppAnnouncementId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Message { get; set; }

    public virtual AppAnnouncement AppAnnouncement { get; set; } = null!;
}
