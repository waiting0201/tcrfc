using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MemberDraw
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string DrawCode { get; set; } = null!;

    public DateTime? SnapshotAt { get; set; }

    public DateTime? DrawnAt { get; set; }

    public string? DrawOccasion { get; set; }

    public DateOnly? ClaimDeadlineOn { get; set; }

    public string Status { get; set; } = null!;

    public int RosterVersion { get; set; }

    public int? TotalCount { get; set; }

    public string? RosterHash { get; set; }

    public Guid? AnnouncementArticleId { get; set; }

    public Guid? LockedBy { get; set; }

    public DateTime? LockedAt { get; set; }

    public string? CoverKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Article? AnnouncementArticle { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DrawRoster> DrawRosters { get; set; } = new List<DrawRoster>();

    public virtual AdminUser? LockedByNavigation { get; set; }

    public virtual ICollection<MemberDrawsI18n> MemberDrawsI18ns { get; set; } = new List<MemberDrawsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
