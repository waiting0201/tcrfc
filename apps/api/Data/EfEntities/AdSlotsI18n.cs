using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdSlotsI18n
{
    public Guid AdSlotId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? FallbackAlt { get; set; }

    public virtual AdSlot AdSlot { get; set; } = null!;
}
