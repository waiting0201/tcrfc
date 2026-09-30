using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppDeepLink
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public string AppLink { get; set; } = null!;

    public string? WebUrl { get; set; }

    public bool RequiresLogin { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AppDeepLinksI18n> AppDeepLinksI18ns { get; set; } = new List<AppDeepLinksI18n>();

    public virtual ICollection<AppLayoutItem> AppLayoutItems { get; set; } = new List<AppLayoutItem>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
