using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FanEventImage
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid FanEventId { get; set; }

    public string ImageKey { get; set; } = null!;

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual FanEvent FanEvent { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
