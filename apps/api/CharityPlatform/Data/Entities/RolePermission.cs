using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class RolePermission
{
    public Guid AdminRoleId { get; set; }

    public Guid PermissionId { get; set; }

    public string? ScopeType { get; set; }

    public virtual AdminRole AdminRole { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}
