using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PressResource
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string ResourceType { get; set; } = null!;

    public string FileKey { get; set; } = null!;

    public int? FileBytes { get; set; }

    public string? CoverKey { get; set; }

    public int? CoverWidth { get; set; }

    public int? CoverHeight { get; set; }

    public DateOnly? PublishedOn { get; set; }

    public int DownloadCount { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<PressResourcesI18n> PressResourcesI18ns { get; set; } = new List<PressResourcesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
