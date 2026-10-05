using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// App 端診斷與錯誤回報的收件（App 規劃書 §10.1 <c>AppDiagnosticReport</c>、docs/19 §8「零第三方」監控）。
/// 🔴 不得存個資：不收 <c>member_id</c>、不讀來源 IP、沒有定位欄位；自由文字裡的 Email 與長串數字（電話、證號）在入庫前遮成 <c>[已遮蔽]</c>；
/// 保存 90 天後由維護作業清除。
/// </summary>
public sealed partial class AppDiagnosticsIntake(ClubDbContext dbContext)
{
    public const int MaxBatch = 50;
    private static readonly IReadOnlySet<string> Types = new HashSet<string>(["crash", "abnormal_exit", "api_error", "startup_time", "user_report"], StringComparer.Ordinal);

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}|\d{8,}")]
    private static partial Regex PersonalData();

    public async Task<int> IngestAsync(AppDiagnosticBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Reports.Count is 0 or > MaxBatch)
        {
            throw new AdminValidationException($"單次上報 1 到 {MaxBatch} 筆回報。");
        }

        var now = DateTime.UtcNow;
        var rows = new List<AppDiagnosticReport>();
        foreach (var r in request.Reports)
        {
            var platform = AppInput.RequirePlatform(r.Platform);
            var version = AppInput.OptionalVersion(r.AppVersion) ?? throw new AdminValidationException("缺少 App 版本號。");
            AdminInput.OneOf(r.Type, Types, "回報類型", "「崩潰」「異常退出」「連線錯誤」「啟動耗時」或「使用者回報」");
            if (r.DeviceInstallId is not null)
            {
                AppInput.RequireDeviceId(r.DeviceInstallId);
            }

            var occurred = r.OccurredAt.UtcDateTime;
            if (now - occurred > TimeSpan.FromDays(7) || occurred - now > AdEventIngestService.MaxFutureSkew)
            {
                throw new AdminValidationException("回報的發生時間不合理（超過 7 天前或在未來）。");
            }

            rows.Add(new AppDiagnosticReport
            {
                Id = Guid.NewGuid(), DeviceInstallId = r.DeviceInstallId, Platform = platform, AppVersion = version,
                BuildNumber = AdminInput.OptionalText(r.BuildNumber, "建置號", 32), OsVersion = AdminInput.OptionalText(r.OsVersion, "作業系統版本", 32),
                OccurredAt = occurred, ReportType = r.Type, MetricValue = r.MetricValue is < 0 ? null : r.MetricValue,
                Summary = Scrub(AdminInput.OptionalText(r.Summary, "摘要", 500)), Detail = Scrub(Truncate(r.Detail, 8000)),
                Status = "new", ReceivedAt = now, UpdatedAt = now,
            });
        }

        dbContext.AppDiagnosticReports.AddRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static string? Truncate(string? value, int max) => value is null ? null : value.Length > max ? value[..max] : value;

    internal static string? Scrub(string? text) => text is null ? null : PersonalData().Replace(text, "[已遮蔽]");
}
