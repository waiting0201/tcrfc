using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// M4 推播裝置（App 規劃書 §8.4）。🔴 <b>推播權杖與裝置識別碼視同個資</b>：清單與詳情一律遮罩（識別碼只留前 4 碼與後 2 碼、權杖完全不顯示），
/// 完整值只有「檢視完整值」權限（僅系統管理員）能看，並寫敏感操作日誌。統計只給彙總數字（平台、版本、權限狀態的分佈），供決定最低支援版本。
/// </summary>
public sealed class AdminAppDevicesRepository(ClubDbContext dbContext, PushTokenProtector protector, SensitiveActionLogger audit)
{
    private static readonly IReadOnlyDictionary<string, string> PermissionLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["not_determined"] = "尚未詢問", ["granted"] = "已允許", ["denied"] = "已拒絕", ["provisional"] = "暫時允許",
    };

    private static readonly IReadOnlyDictionary<string, string> TokenStatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["none"] = "沒有權杖", ["valid"] = "有效", ["invalid"] = "已失效",
    };

    public async Task<PagedResult<AdminAppDeviceListItemDto>> ListAsync(
        string? platform, string? appVersion, string? permission, string? tokenStatus, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        // 🔴 驗證一律先做完再組查詢：把會丟例外的驗證寫在 Where 的運算式裡，例外會被 EF 包成 500。
        var q = dbContext.AppDevices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(platform))
        {
            var pf = AppInput.RequirePlatform(platform);
            q = q.Where(d => d.Platform == pf);
        }

        if (!string.IsNullOrEmpty(appVersion)) { q = q.Where(d => d.AppVersion == appVersion); }
        if (!string.IsNullOrEmpty(permission))
        {
            AdminInput.OneOf(permission, PermissionLabels.Keys.ToHashSet(StringComparer.Ordinal), "推播權限", "「尚未詢問」「已允許」「已拒絕」或「暫時允許」");
            q = q.Where(d => d.PushPermission == permission);
        }

        if (!string.IsNullOrEmpty(tokenStatus))
        {
            AdminInput.OneOf(tokenStatus, TokenStatusLabels.Keys.ToHashSet(StringComparer.Ordinal), "權杖狀態", "「沒有權杖」「有效」或「已失效」");
            q = q.Where(d => d.PushTokenStatus == tokenStatus);
        }

        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(d => d.LastActiveAt).ThenBy(d => d.RowSeq).Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<AdminAppDeviceListItemDto> { Items = rows.Select(ToItem).ToList(), Page = p, PageSize = size, TotalCount = total };
    }

    public async Task<AdminAppDeviceDetailDto?> GetAsync(Guid id, bool reveal, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var d = await dbContext.AppDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d is null)
        {
            return null;
        }

        var subs = await dbContext.PushTopicSubscriptions.CountAsync(s => s.DeviceId == id, cancellationToken);
        if (reveal)
        {
            audit.Record(scope, "檢視 App 裝置完整值", $"裝置 {d.Id}", 1, "M4 檢視推播權杖與裝置識別碼完整值");
        }

        return new AdminAppDeviceDetailDto
        {
            Device = ToItem(d), SubscriptionCount = subs, Revealed = reveal,
            DeviceInstallId = reveal ? d.DeviceInstallId : null, PushToken = reveal ? protector.TryDecrypt(d.PushTokenEncrypted) : null,
        };
    }

    public async Task<AdminAppDeviceStatsDto> StatsAsync(string? platform, string? belowVersion, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var all = dbContext.AppDevices.AsNoTracking();
        var versionRows = await all.GroupBy(d => new { d.Platform, d.AppVersion }).Select(g => new { g.Key.Platform, g.Key.AppVersion, Count = g.Count() }).ToListAsync(cancellationToken);
        var permission = await all.GroupBy(d => d.PushPermission).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        int? below = null;
        if (!string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(belowVersion))
        {
            var pf = AppInput.RequirePlatform(platform);
            if (!AppVersion.IsValid(belowVersion))
            {
                throw new AdminValidationException("版本號格式不正確（例如 1.2.0）。");
            }

            below = versionRows.Where(r => r.Platform == pf && r.AppVersion is not null && AppVersion.Compare(r.AppVersion, belowVersion) is < 0).Sum(r => r.Count);
        }

        return new AdminAppDeviceStatsDto
        {
            TotalDevices = versionRows.Sum(r => r.Count),
            ActiveLast7Days = await all.CountAsync(d => d.LastActiveAt >= now.AddDays(-7), cancellationToken),
            ActiveLast30Days = await all.CountAsync(d => d.LastActiveAt >= now.AddDays(-30), cancellationToken),
            InvalidTokenCount = await all.CountAsync(d => d.PushTokenStatus == "invalid", cancellationToken),
            ByVersion = versionRows.OrderBy(r => r.Platform).ThenByDescending(r => AppVersion.TryParse(r.AppVersion, out var v) ? v : default)
                .Select(r => new AdminAppDeviceVersionRowDto { Platform = r.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, r.Platform), AppVersion = r.AppVersion, Count = r.Count }).ToList(),
            ByPermission = permission.OrderByDescending(r => r.Count).Select(r => new AdminAppDeviceCountRowDto { Key = r.Key, Label = PermissionLabels.GetValueOrDefault(r.Key, r.Key), Count = r.Count }).ToList(),
            DevicesBelowVersion = below,
        };
    }

    /// <summary>失效權杖清理：把「已失效」的權杖資料清空（狀態改為沒有權杖）。裝置列本身保留（追蹤偏好與活躍紀錄仍有用）。</summary>
    public async Task<AdminAppDeviceCleanupResultDto> CleanupInvalidTokensAsync(AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var cleared = await dbContext.AppDevices.Where(d => d.PushTokenStatus == "invalid")
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.PushTokenStatus, "none").SetProperty(d => d.PushTokenEncrypted, (string?)null).SetProperty(d => d.PushTokenHash, (string?)null), cancellationToken);
        audit.Record(scope, "清理失效推播權杖", null, cleared, "M4 失效權杖清理");
        return new AdminAppDeviceCleanupResultDto { TokensCleared = cleared };
    }

    private static AdminAppDeviceListItemDto ToItem(AppDevice d) => new()
    {
        Id = d.Id, DeviceInstallIdMasked = Mask(d.DeviceInstallId), Platform = d.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, d.Platform),
        OsVersion = d.OsVersion, AppVersion = d.AppVersion, Locale = d.Locale is null ? null : AdLabels.ToExternalLocale(d.Locale),
        LocaleLabel = d.Locale is null ? null : AdLabels.Of(AdLabels.Locale, d.Locale), PushPermission = d.PushPermission,
        PushPermissionLabel = PermissionLabels.GetValueOrDefault(d.PushPermission, d.PushPermission), PushTokenStatus = d.PushTokenStatus,
        PushTokenStatusLabel = TokenStatusLabels.GetValueOrDefault(d.PushTokenStatus, d.PushTokenStatus), IsMemberBound = d.MemberId is not null,
        FirstSeenAt = d.FirstSeenAt, LastActiveAt = d.LastActiveAt,
    };

    internal static string Mask(string value) => value.Length <= 8 ? new string('*', value.Length) : $"{value[..4]}****{value[^2..]}";
}
