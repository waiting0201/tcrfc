using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdCreative
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid CampaignId { get; set; }

    public string Locale { get; set; } = null!;

    public string? ImageKey { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public string? VideoKey { get; set; }

    public string? AltText { get; set; }

    public string? Title { get; set; }

    public string? CtaText { get; set; }

    public string? ClickUrl { get; set; }

    public string Theme { get; set; } = null!;

    public string? VariantTag { get; set; }

    public string ReviewStatus { get; set; } = null!;

    public string? RejectReason { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public bool IsPaused { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AdDailyStat> AdDailyStats { get; set; } = new List<AdDailyStat>();

    public virtual ICollection<AdEvent> AdEvents { get; set; } = new List<AdEvent>();

    public virtual AdCampaign Campaign { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? ReviewedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
