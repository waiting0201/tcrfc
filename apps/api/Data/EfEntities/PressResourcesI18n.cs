using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PressResourcesI18n
{
    public Guid PressResourceId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public virtual PressResource PressResource { get; set; } = null!;
}
