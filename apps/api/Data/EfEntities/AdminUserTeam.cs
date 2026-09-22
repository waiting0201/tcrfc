using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdminUserTeam
{
    public Guid AdminUserId { get; set; }

    public Guid TeamId { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public bool IsActive { get; set; }

    public virtual AdminUser AdminUser { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}
