using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class EmailTemplatesI18n
{
    public Guid EmailTemplateId { get; set; }

    public string Locale { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public virtual EmailTemplate EmailTemplate { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
