using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FormsI18n
{
    public Guid FormId { get; set; }

    public string Locale { get; set; } = null!;

    public string? AutoReplyBody { get; set; }

    public virtual Form Form { get; set; } = null!;
}
