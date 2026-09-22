using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MemberCard
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MembershipId { get; set; }

    public Guid ClubId { get; set; }

    public string HolderName { get; set; } = null!;

    public string Token { get; set; } = null!;

    public string? Status { get; set; }

    public int ReissueCount { get; set; }

    public DateTime? IssuedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Membership Membership { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
