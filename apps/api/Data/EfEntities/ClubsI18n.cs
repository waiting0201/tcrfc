using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ClubsI18n
{
    public Guid ClubId { get; set; }

    public string Locale { get; set; } = null!;

    public string? OgImageAlt { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>簡稱（2026-10-05）：磐石「台中磐石」／「Taichung Rock FC」、藍鯨「台中藍鯨」；藍鯨英文一律 null（B-5）。</summary>
    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
