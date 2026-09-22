using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipBenefitsI18n
{
    public Guid MembershipBenefitId { get; set; }

    public string Locale { get; set; } = null!;

    public string? GroupLabel { get; set; }

    public string? FreeValue { get; set; }

    public string? PaidValue { get; set; }

    public virtual MembershipBenefit MembershipBenefit { get; set; } = null!;
}
