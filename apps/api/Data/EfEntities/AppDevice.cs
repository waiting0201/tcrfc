using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppDevice
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string DeviceInstallId { get; set; } = null!;

    public string Platform { get; set; } = null!;

    public string? OsVersion { get; set; }

    public string? AppVersion { get; set; }

    public string? Locale { get; set; }

    public string? PushTokenEncrypted { get; set; }

    public string? PushTokenHash { get; set; }

    public string PushTokenStatus { get; set; } = null!;

    public string PushPermission { get; set; } = null!;

    public Guid? MemberId { get; set; }

    public DateTime FirstSeenAt { get; set; }

    public DateTime LastActiveAt { get; set; }

    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    public DateTime? RefreshTokenRotatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual Member? Member { get; set; }

    public virtual ICollection<PushTopicSubscription> PushTopicSubscriptions { get; set; } = new List<PushTopicSubscription>();
}
