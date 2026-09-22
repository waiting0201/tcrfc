using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FormField
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid FormId { get; set; }

    public string FieldKey { get; set; } = null!;

    public string FieldType { get; set; } = null!;

    public bool IsRequired { get; set; }

    public string? ValidationRule { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EnquiryAnswer> EnquiryAnswers { get; set; } = new List<EnquiryAnswer>();

    public virtual Form Form { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
