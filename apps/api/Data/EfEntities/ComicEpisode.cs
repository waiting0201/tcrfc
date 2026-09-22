using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ComicEpisode
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public int EpisodeNo { get; set; }

    public string? CoverKey { get; set; }

    public DateOnly? PublishedOn { get; set; }

    public string? Status { get; set; }

    public bool IsLatest { get; set; }

    public int ViewCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual ICollection<ComicEpisodesI18n> ComicEpisodesI18ns { get; set; } = new List<ComicEpisodesI18n>();

    public virtual ICollection<ComicPage> ComicPages { get; set; } = new List<ComicPage>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
