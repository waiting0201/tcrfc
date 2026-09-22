using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipPlansI18n
{
    public Guid MembershipPlanId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? BenefitNote { get; set; }

    public virtual MembershipPlan MembershipPlan { get; set; } = null!;
}
