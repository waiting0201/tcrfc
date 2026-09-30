using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdDailyStat
{
    public DateOnly StatDate { get; set; }

    public Guid CampaignId { get; set; }

    public Guid CreativeId { get; set; }

    public Guid SlotId { get; set; }

    public string Platform { get; set; } = null!;

    public string Locale { get; set; } = null!;

    public int Impressions { get; set; }

    public int Clicks { get; set; }

    public int UniqueDevices { get; set; }

    public virtual AdCreative Creative { get; set; } = null!;
}
