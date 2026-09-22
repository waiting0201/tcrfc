using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class StaffTeam
{
    public Guid StaffId { get; set; }

    public Guid TeamId { get; set; }

    public string? RoleCode { get; set; }

    public virtual Staff Staff { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}
