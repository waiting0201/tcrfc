using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipBenefit
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MembershipPlanId { get; set; }

    public string BenefitGroup { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MembershipBenefitsI18n> MembershipBenefitsI18ns { get; set; } = new List<MembershipBenefitsI18n>();

    public virtual MembershipPlan MembershipPlan { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
