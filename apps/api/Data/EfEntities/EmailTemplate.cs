using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EmailTemplate
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string TemplateCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailTemplatesI18n> EmailTemplatesI18ns { get; set; } = new List<EmailTemplatesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
