using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Membership
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MemberId { get; set; }

    public Guid ClubId { get; set; }

    public Guid SeasonId { get; set; }

    public string Tier { get; set; } = null!;

    public DateOnly? MembershipStartOn { get; set; }

    public DateOnly? MembershipEndOn { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual ICollection<MemberCard> MemberCards { get; set; } = new List<MemberCard>();

    public virtual ICollection<MembershipPayment> MembershipPayments { get; set; } = new List<MembershipPayment>();

    public virtual Season Season { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
