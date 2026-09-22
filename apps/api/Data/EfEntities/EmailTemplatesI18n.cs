using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EmailTemplatesI18n
{
    public Guid EmailTemplateId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public virtual EmailTemplate EmailTemplate { get; set; } = null!;
}
