using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorArticle
{
    public Guid SponsorId { get; set; }

    public Guid ArticleId { get; set; }

    public int SortOrder { get; set; }

    public virtual Article Article { get; set; } = null!;

    public virtual Sponsor Sponsor { get; set; } = null!;
}
