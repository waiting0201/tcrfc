using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FaqCategory
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Slug { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<FaqCategoriesI18n> FaqCategoriesI18ns { get; set; } = new List<FaqCategoriesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();
}
