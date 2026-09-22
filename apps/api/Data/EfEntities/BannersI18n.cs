using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class BannersI18n
{
    public Guid BannerId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public string? Cta1Label { get; set; }

    public string? Cta1Url { get; set; }

    public string? Cta2Label { get; set; }

    public string? Cta2Url { get; set; }

    public virtual Banner Banner { get; set; } = null!;
}
