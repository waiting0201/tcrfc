using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FaqsI18n
{
    public Guid FaqId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public virtual Faq Faq { get; set; } = null!;
}
