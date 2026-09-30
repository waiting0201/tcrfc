using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdEvent
{
    public long Id { get; set; }

    public string EventType { get; set; } = null!;

    public Guid CreativeId { get; set; }

    public Guid CampaignId { get; set; }

    public Guid SlotId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string DeviceInstallId { get; set; } = null!;

    public string? Platform { get; set; }

    public string? AppVersion { get; set; }

    public string? Locale { get; set; }

    public string? PresentationId { get; set; }

    public string? BatchId { get; set; }

    public string DedupeKey { get; set; } = null!;

    public DateTime? AggregatedAt { get; set; }

    public virtual AdCreative Creative { get; set; } = null!;
}
