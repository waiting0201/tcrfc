using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarFeedFetch
{
    public Guid ClubId { get; set; }

    public string FeedKey { get; set; } = null!;

    public DateOnly FetchedOn { get; set; }

    public string ClientHash { get; set; } = null!;

    public virtual Club Club { get; set; } = null!;
}
