using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ValueTagLink
{
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    public string ValueTag { get; set; } = null!;
}
