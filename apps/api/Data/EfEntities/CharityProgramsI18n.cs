using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CharityProgramsI18n
{
    public Guid CharityProgramId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? TargetAudience { get; set; }

    public string? Content { get; set; }

    public string? DonationContent { get; set; }

    public virtual CharityProgram CharityProgram { get; set; } = null!;
}
