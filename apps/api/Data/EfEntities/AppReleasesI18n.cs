using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppReleasesI18n
{
    public Guid AppReleaseId { get; set; }

    public string Locale { get; set; } = null!;

    public string? WhatsNew { get; set; }

    public string? ForceMessage { get; set; }

    public string? RecommendMessage { get; set; }

    public virtual AppRelease AppRelease { get; set; } = null!;
}
