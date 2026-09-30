using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdCampaign
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid AdvertiserId { get; set; }

    public Guid SlotId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public int Weight { get; set; }

    public int? DailyImpressionCap { get; set; }

    public int? PerDeviceDailyCap { get; set; }

    public string GoalType { get; set; } = null!;

    public int? GoalImpressions { get; set; }

    public int DeliveredToday { get; set; }

    public DateOnly? DeliveredOn { get; set; }

    public int DeliveredTotal { get; set; }

    public int? ContractAmount { get; set; }

    public bool IsAmountHidden { get; set; }

    public string Status { get; set; } = null!;

    public string? PausedFrom { get; set; }

    public string? PauseReason { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AdCreative> AdCreatives { get; set; } = new List<AdCreative>();

    public virtual Advertiser Advertiser { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? ReviewedByNavigation { get; set; }

    public virtual AdSlot Slot { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
