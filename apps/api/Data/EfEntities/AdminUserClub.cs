using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdminUserClub
{
    public Guid AdminUserId { get; set; }

    public Guid ClubId { get; set; }

    public DateOnly GrantedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public Guid? GrantedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual AdminUser AdminUser { get; set; } = null!;

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? GrantedByNavigation { get; set; }
}
