using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipPlan
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid SeasonId { get; set; }

    public string Code { get; set; } = null!;

    public int Fee { get; set; }

    public int CardQuota { get; set; }

    public int JerseyQuota { get; set; }

    public string? MidSeasonRule { get; set; }

    public int SortOrder { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MembershipBenefit> MembershipBenefits { get; set; } = new List<MembershipBenefit>();

    public virtual ICollection<MembershipPayment> MembershipPayments { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPlansI18n> MembershipPlansI18ns { get; set; } = new List<MembershipPlansI18n>();

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
