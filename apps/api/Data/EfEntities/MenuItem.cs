using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class MenuItem
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? ParentId { get; set; }

    public string? MenuLocation { get; set; }

    public string? Url { get; set; }

    public bool IsExternal { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<MenuItem> InverseParent { get; set; } = new List<MenuItem>();

    public virtual ICollection<MenuItemsI18n> MenuItemsI18ns { get; set; } = new List<MenuItemsI18n>();

    public virtual MenuItem? Parent { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
