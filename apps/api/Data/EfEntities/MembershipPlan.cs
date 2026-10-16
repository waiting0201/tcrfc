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

    public int SortOrder { get; set; }

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>層級：<c>standard</c>（磐石會員）／<c>premium</c>（尊尚會員）；會籍的層級隨方案。</summary>
    public string Tier { get; set; } = null!;

    /// <summary>商店會員折扣比例（0–100 的整數百分比；0＝無折扣）。</summary>
    public int ShopDiscountPercent { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MembershipBenefit> MembershipBenefits { get; set; } = new List<MembershipBenefit>();

    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public virtual ICollection<MembershipPayment> MembershipPayments { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPlansI18n> MembershipPlansI18ns { get; set; } = new List<MembershipPlansI18n>();

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
