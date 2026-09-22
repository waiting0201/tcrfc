using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class UiStringTranslation
{
    public Guid UiStringId { get; set; }

    public string Locale { get; set; } = null!;

    public string Value { get; set; } = null!;

    public virtual UiString UiString { get; set; } = null!;
}
