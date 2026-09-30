using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FanEventArticle
{
    public Guid FanEventId { get; set; }

    public Guid ArticleId { get; set; }

    public int SortOrder { get; set; }

    public virtual Article Article { get; set; } = null!;

    public virtual FanEvent FanEvent { get; set; } = null!;
}
