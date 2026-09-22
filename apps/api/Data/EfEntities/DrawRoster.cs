using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class DrawRoster
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid MemberDrawId { get; set; }

    public int SerialNo { get; set; }

    public string MemberNoSnapshot { get; set; } = null!;

    public string? NameSnapshot { get; set; }

    public string? TierSnapshot { get; set; }

    public DateOnly? MembershipEndOnSnapshot { get; set; }

    public bool IsWinner { get; set; }

    public string? PrizeName { get; set; }

    public string? ClaimMethod { get; set; }

    public string? FulfilmentStatus { get; set; }

    public string? WithholdingDataEncrypted { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual MemberDraw MemberDraw { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
