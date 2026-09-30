using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class TrialsI18n
{
    public Guid TrialId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Audience { get; set; }

    public virtual Trial Trial { get; set; } = null!;
}
