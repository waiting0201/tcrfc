using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MembershipPayment
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MembershipId { get; set; }

    public Guid ClubId { get; set; }

    public Guid CollectingClubId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public string? Method { get; set; }

    public int Amount { get; set; }

    public DateOnly? PaidOn { get; set; }

    public string? Note { get; set; }

    public Guid? HandledBy { get; set; }

    public DateOnly? ActivatedStartOn { get; set; }

    public DateOnly? ActivatedEndOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual Club CollectingClub { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? HandledByNavigation { get; set; }

    public virtual Membership Membership { get; set; } = null!;

    public virtual MembershipPlan MembershipPlan { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
