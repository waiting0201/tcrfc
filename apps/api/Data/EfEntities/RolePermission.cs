using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class RolePermission
{
    public Guid AdminRoleId { get; set; }

    public Guid PermissionId { get; set; }

    public string ScopeType { get; set; } = null!;

    public virtual AdminRole AdminRole { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}
