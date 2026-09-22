using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Page
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<PageBlock> PageBlocks { get; set; } = new List<PageBlock>();

    public virtual ICollection<PageVersion> PageVersions { get; set; } = new List<PageVersion>();

    public virtual ICollection<PagesI18n> PagesI18ns { get; set; } = new List<PagesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
