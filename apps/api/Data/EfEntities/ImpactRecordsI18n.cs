using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ImpactRecordsI18n
{
    public Guid ImpactRecordId { get; set; }

    public string Locale { get; set; } = null!;

    public string? DonationContent { get; set; }

    public string? Location { get; set; }

    public string? BriefDescription { get; set; }

    public virtual ImpactRecord ImpactRecord { get; set; } = null!;
}
