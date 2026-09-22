using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PageVersion
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid PageId { get; set; }

    public int VersionNo { get; set; }

    public string? Snapshot { get; set; }

    public string? PreviewToken { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Page Page { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
