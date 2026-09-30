using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PushMessage
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Kind { get; set; } = null!;

    public string? ImageKey { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public string? DeepLink { get; set; }

    public string AudienceTier { get; set; } = null!;

    public Guid? AudienceClubId { get; set; }

    public string? AudienceTeamCodes { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public string Status { get; set; } = null!;

    public string? RejectNote { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public int? AudienceEstimate { get; set; }

    public int SentCount { get; set; }

    public int DeliveredCount { get; set; }

    public int FailedCount { get; set; }

    public int OpenedCount { get; set; }

    public long SendCursor { get; set; }

    public string? FailureMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club? AudienceClub { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<PushMessageStat> PushMessageStats { get; set; } = new List<PushMessageStat>();

    public virtual ICollection<PushMessagesI18n> PushMessagesI18ns { get; set; } = new List<PushMessagesI18n>();

    public virtual AdminUser? ReviewedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
