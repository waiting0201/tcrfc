using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Tag
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Slug { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<TagsI18n> TagsI18ns { get; set; } = new List<TagsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();
}
