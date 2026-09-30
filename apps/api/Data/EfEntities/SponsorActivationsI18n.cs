using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorActivationsI18n
{
    public Guid SponsorActivationId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? ResultSummary { get; set; }

    public virtual SponsorActivation SponsorActivation { get; set; } = null!;
}
