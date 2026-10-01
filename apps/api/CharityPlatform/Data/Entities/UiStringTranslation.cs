using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class UiStringTranslation
{
    public Guid UiStringId { get; set; }

    public string Locale { get; set; } = null!;

    public string Value { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;

    public virtual UiString UiString { get; set; } = null!;
}
