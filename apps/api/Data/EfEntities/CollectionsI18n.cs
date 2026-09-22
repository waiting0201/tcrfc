using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CollectionsI18n
{
    public Guid CollectionId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Narrative { get; set; }

    public virtual Collection Collection { get; set; } = null!;
}
