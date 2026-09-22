using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ComicEpisodesI18n
{
    public Guid ComicEpisodeId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public virtual ComicEpisode ComicEpisode { get; set; } = null!;
}
