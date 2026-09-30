using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppDeepLinksI18n
{
    public Guid AppDeepLinkId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Label { get; set; }

    public virtual AppDeepLink AppDeepLink { get; set; } = null!;
}
