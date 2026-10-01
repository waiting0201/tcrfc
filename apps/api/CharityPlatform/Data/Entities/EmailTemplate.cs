using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class EmailTemplate
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailTemplatesI18n> EmailTemplatesI18ns { get; set; } = new List<EmailTemplatesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
