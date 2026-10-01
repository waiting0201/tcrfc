using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class AuditLog
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string Action { get; set; } = null!;

    public string TargetType { get; set; } = null!;

    public Guid? TargetId { get; set; }

    public string? ChangeSummary { get; set; }

    public string? PurposeNote { get; set; }

    public string? SourceIp { get; set; }

    public virtual AdminUser AdminUser { get; set; } = null!;
}
