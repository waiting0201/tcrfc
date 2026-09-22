using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ArticleRelation
{
    public Guid ArticleId { get; set; }

    public string TargetType { get; set; } = null!;

    public Guid TargetId { get; set; }

    public virtual Article Article { get; set; } = null!;
}
