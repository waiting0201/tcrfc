using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppLayoutItem
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Kind { get; set; } = null!;

    public string ItemKey { get; set; } = null!;

    public Guid? DeepLinkId { get; set; }

    public string? IconKey { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AppLayoutItemsI18n> AppLayoutItemsI18ns { get; set; } = new List<AppLayoutItemsI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AppDeepLink? DeepLink { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
