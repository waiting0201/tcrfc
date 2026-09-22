using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PageBlock
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid PageId { get; set; }

    public string BlockType { get; set; } = null!;

    public string? Content { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Page Page { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
