using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MemberDrawsI18n
{
    public Guid MemberDrawId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? PrizeDescription { get; set; }

    public string? Rules { get; set; }

    public string? Notes { get; set; }

    public virtual MemberDraw MemberDraw { get; set; } = null!;
}
