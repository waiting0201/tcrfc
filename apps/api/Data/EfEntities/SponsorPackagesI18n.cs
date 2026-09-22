using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorPackagesI18n
{
    public Guid SponsorPackageId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Content { get; set; }

    public string? BenefitList { get; set; }

    public string? Audience { get; set; }

    public virtual SponsorPackage SponsorPackage { get; set; } = null!;
}
