using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L4 匯出（主站規劃書 §4.12 L4「匯出：指定期間的賽事匯出 CSV／.ics」）。內容是行事曆總覽的同一份資料（賽事、自建活動、
/// 同步的試訓），套用相同的隊別、場地、類型篩選；不公開的自建活動只有持有「檢視自建事件」權限的人才會出現在匯出裡
/// （行事曆資料本身是前台公開資訊，匯出不設受限碼、不寫日誌）。
/// </summary>
public sealed class AdminCalendarExportRepository(
    ClubDbContext db, AdminCalendarOverviewRepository overview, IPermissionChecker permissions)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan MatchDuration = TimeSpan.FromHours(2);

    private static readonly IReadOnlyDictionary<string, string> SourceLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["match"] = "賽事", ["custom"] = "俱樂部活動", ["trial"] = "試訓",
    };

    private static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["scheduled"] = "未開始", ["live"] = "進行中", ["played"] = "已結束", ["postponed"] = "延賽", ["cancelled"] = "取消",
    };

    private static readonly IReadOnlyDictionary<string, string> HomeAwayLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["HOME"] = "主場", ["AWAY"] = "客場", ["主場"] = "主場", ["客場"] = "客場",
    };

    private async Task<IReadOnlyList<AdminCalendarEventDto>> LoadAsync(
        AdminClubScope scope, DateOnly from, DateOnly toExclusive, string? team, string? sourceType, Guid? venueId, string? status, string? type,
        CancellationToken cancellationToken)
    {
        var events = await overview.ListAsync(scope, from, toExclusive, team, sourceType, cancellationToken, venueId, status, type);
        var canSeePrivate = await permissions.HasPermissionAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, "calendar.custom_event.view", cancellationToken);
        return canSeePrivate ? events : events.Where(e => e.SourceType != "custom" || e.IsPublic == true).ToList();
    }

    /// <summary>俱樂部活動（custom）的起訖是 UTC 時間戳，輸出台灣時間；賽事與試訓本來就是台灣當地日期，不轉換。</summary>
    private static DateTime Local(AdminCalendarEventDto e, DateTime value)
        => e.SourceType == "custom" ? DateTime.SpecifyKind(value, DateTimeKind.Utc).AddHours(8) : value;

    public async Task<string> ExportCsvAsync(
        AdminClubScope scope, DateOnly from, DateOnly toExclusive, string? team, string? sourceType, Guid? venueId, string? status, string? type,
        CancellationToken cancellationToken)
    {
        var events = await LoadAsync(scope, from, toExclusive, team, sourceType, venueId, status, type, cancellationToken);
        var lines = new List<IEnumerable<string?>>
        {
            new[] { "類別", "日期", "開始時間", "結束時間", "標題／對手", "隊別", "場地", "主客場", "狀態", "類型", "是否公開" },
        };
        lines.AddRange(events.Select(e => new[]
        {
            SourceLabels.GetValueOrDefault(e.SourceType, e.SourceType),
            Local(e, e.StartsAt).ToString("yyyy-MM-dd"),
            e.IsAllDay ? null : e.SourceType == "match" ? e.Kickoff : Local(e, e.StartsAt).ToString("HH:mm"),
            e.EndsAt is { } end && !e.IsAllDay ? Local(e, end).ToString("HH:mm") : null,
            e.Title,
            string.Join("、", e.TeamCodes),
            e.VenueName,
            e.HomeAway is null ? null : HomeAwayLabels.GetValueOrDefault(e.HomeAway, e.HomeAway),
            e.Status is null ? null : StatusLabels.GetValueOrDefault(e.Status, e.Status),
            e.SourceType == "match" ? e.CompetitionTag : e.EventTypeCode,
            e.IsPublic is bool p ? (p ? "是" : "否") : null,
        }));
        return CsvUtils.BuildCsv(lines);
    }

    public async Task<string> ExportIcsAsync(
        AdminClubScope scope, DateOnly from, DateOnly toExclusive, string? team, string? sourceType, Guid? venueId, string? status, string? type,
        CancellationToken cancellationToken)
    {
        var events = await LoadAsync(scope, from, toExclusive, team, sourceType, venueId, status, type, cancellationToken);
        var clubName = await db.ClubsI18ns.AsNoTracking().Where(i => i.ClubId == scope.ClubId && i.Locale == RequestLocale.DefaultDbLocale)
            .Select(i => i.Name).FirstOrDefaultAsync(cancellationToken) ?? scope.ClubCode;
        var now = DateTime.UtcNow;
        var ics = events.Select(e =>
        {
            if (e.SourceType == "match")
            {
                var hasTime = !string.IsNullOrEmpty(e.Kickoff) && TimeOnly.TryParse(e.Kickoff, out _);
                var start = hasTime
                    ? DateTime.SpecifyKind(e.StartsAt.Date + TimeOnly.Parse(e.Kickoff!).ToTimeSpan() - TaipeiOffset, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(e.StartsAt.Date, DateTimeKind.Utc);
                var away = e.HomeAway is "AWAY" or "客場";
                return new IcsEvent
                {
                    Uid = $"match-{e.SourceId}@tcrfc", StartsAtUtc = start, EndsAtUtc = hasTime ? start + MatchDuration : null, IsAllDay = !hasTime,
                    Summary = away ? $"{e.Title} vs {clubName}" : $"{clubName} vs {e.Title}", Location = e.VenueName,
                    Status = e.Status == "cancelled" ? "CANCELLED" : "CONFIRMED", CreatedAtUtc = now, UpdatedAtUtc = now,
                };
            }

            return new IcsEvent
            {
                Uid = $"{e.SourceType}-{e.SourceId}-{e.StartsAt:yyyyMMdd}@tcrfc",
                StartsAtUtc = DateTime.SpecifyKind(e.SourceType == "trial" || e.IsAllDay ? e.StartsAt.Date : e.StartsAt, DateTimeKind.Utc),
                EndsAtUtc = e.EndsAt is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
                IsAllDay = e.SourceType == "trial" || e.IsAllDay, Summary = e.Title, Location = e.VenueName, Status = "CONFIRMED",
                CreatedAtUtc = now, UpdatedAtUtc = now,
            };
        });
        return IcsBuilder.BuildCalendar(clubName, ics.OrderBy(e => e.StartsAtUtc));
    }
}
