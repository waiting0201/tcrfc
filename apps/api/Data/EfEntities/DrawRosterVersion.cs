using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class DrawRosterVersion
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MemberDrawId { get; set; }

    public int RosterVersion { get; set; }

    public DateTime SnapshotAt { get; set; }

    public int TotalCount { get; set; }

    public string RosterHash { get; set; } = null!;

    public Guid? GeneratedBy { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? GeneratedByNavigation { get; set; }

    public virtual MemberDraw MemberDraw { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual AdminUser? VoidedByNavigation { get; set; }
}
