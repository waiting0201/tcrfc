using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Permission
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public string ModuleCode { get; set; } = null!;

    public string? SubmoduleCode { get; set; }

    public string? Domain { get; set; }

    public string? Action { get; set; }

    public bool IsClubScoped { get; set; }

    public bool IsRestricted { get; set; }

    public bool SysadminOnly { get; set; }

    public string NameZh { get; set; } = null!;

    public string? NameEn { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
