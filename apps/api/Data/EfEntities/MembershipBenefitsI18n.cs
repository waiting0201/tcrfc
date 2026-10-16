using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipBenefitsI18n
{
    public Guid MembershipBenefitId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? GroupLabel { get; set; }

    /// <summary>該方案的對應值（勾「✓」、叉「✗」或文字，如「10%」「一件」）。</summary>
    public string? Value { get; set; }

    public virtual MembershipBenefit MembershipBenefit { get; set; } = null!;
}
