using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdSlot
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string SlotCode { get; set; } = null!;

    public string Surface { get; set; } = null!;

    public string? ScreenCode { get; set; }

    public int? BlockOrder { get; set; }

    public string? AspectRatio { get; set; }

    public int? MinWidth { get; set; }

    public int? MinHeight { get; set; }

    public int? MaxFileKb { get; set; }

    public string? AllowedFormats { get; set; }

    public bool AllowVideo { get; set; }

    public int? SessionImpressionCap { get; set; }

    public int RotationCap { get; set; }

    public string? FallbackImageKey { get; set; }

    public int? FallbackImageWidth { get; set; }

    public int? FallbackImageHeight { get; set; }

    public string? FallbackLink { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AdCampaign> AdCampaigns { get; set; } = new List<AdCampaign>();

    public virtual ICollection<AdSlotsI18n> AdSlotsI18ns { get; set; } = new List<AdSlotsI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
