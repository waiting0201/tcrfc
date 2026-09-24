using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Faq
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public int ViewCount { get; set; }

    public int HelpfulCount { get; set; }

    public int UnhelpfulCount { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<FaqsI18n> FaqsI18ns { get; set; } = new List<FaqsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<FaqCategory> FaqCategories { get; set; } = new List<FaqCategory>();

    public virtual ICollection<FaqEmbedSlotLink> FaqEmbedSlotLinks { get; set; } = new List<FaqEmbedSlotLink>();
}
