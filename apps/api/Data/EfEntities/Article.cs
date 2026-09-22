using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Article
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public Guid ArticleCategoryId { get; set; }

    public string? CoverKey { get; set; }

    public bool IsFeatured { get; set; }

    public int ViewCount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ArticleCategory ArticleCategory { get; set; } = null!;

    public virtual ICollection<ArticleRelation> ArticleRelations { get; set; } = new List<ArticleRelation>();

    public virtual ICollection<ArticlesI18n> ArticlesI18ns { get; set; } = new List<ArticlesI18n>();

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MemberDraw> MemberDraws { get; set; } = new List<MemberDraw>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
