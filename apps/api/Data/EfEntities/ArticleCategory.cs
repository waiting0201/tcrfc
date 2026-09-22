using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ArticleCategory
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<ArticleCategoriesI18n> ArticleCategoriesI18ns { get; set; } = new List<ArticleCategoriesI18n>();

    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
