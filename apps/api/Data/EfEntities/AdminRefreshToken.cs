using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdminRefreshToken
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid AdminUserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public virtual AdminUser AdminUser { get; set; } = null!;

    public virtual ICollection<AdminRefreshToken> InverseReplacedBy { get; set; } = new List<AdminRefreshToken>();

    public virtual AdminRefreshToken? ReplacedBy { get; set; }
}
