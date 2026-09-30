using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AppDiagnosticReport
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string? DeviceInstallId { get; set; }

    public string Platform { get; set; } = null!;

    public string AppVersion { get; set; } = null!;

    public string? BuildNumber { get; set; }

    public string? OsVersion { get; set; }

    public DateTime OccurredAt { get; set; }

    public string ReportType { get; set; } = null!;

    public int? MetricValue { get; set; }

    public string? Summary { get; set; }

    public string? Detail { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
