using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// 裝置註冊與追蹤／推播訂閱（App 規劃書 §9.2「裝置 註冊／更新」「推播訂閱 更新」、§10.1）。匿名呼叫：裝置可獨立存在（未登入亦註冊），
/// <c>member_id</c> 不在這裡設定（會員登入與綁定屬 AP-3）。推播權杖加密儲存；同一個權杖出現在別的裝置列（重裝、換機）時，舊列的權杖標記失效，
/// 避免同一支手機收到兩次推播。
/// </summary>
public sealed class AppDevicesService(ClubDbContext dbContext, PushTokenProtector protector)
{
    public const int MaxSubscriptionItems = 100;
    private static readonly IReadOnlySet<string> Permissions = new HashSet<string>(["not_determined", "granted", "denied", "provisional"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> TopicTypes = new HashSet<string>(["team", "news_category", "club"], StringComparer.Ordinal);

    public async Task<AppDeviceRegisteredDto> RegisterAsync(string deviceInstallId, RegisterAppDeviceRequest request, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(deviceInstallId);
        var platform = AppInput.RequirePlatform(request.Platform);
        var appVersion = AppInput.OptionalVersion(request.AppVersion);
        var osVersion = AdminInput.OptionalText(request.OsVersion, "作業系統版本", 32);
        var locale = request.Locale is null ? null : AdLabels.ToDbLocale(request.Locale);
        if (request.PushPermission is not null)
        {
            AdminInput.OneOf(request.PushPermission, Permissions, "推播權限", "「尚未詢問」「已允許」「已拒絕」或「暫時允許」");
        }

        if (request.PushToken is { Length: > 1024 })
        {
            throw new AdminValidationException("推播權杖過長。");
        }

        var now = DateTime.UtcNow;
        var device = await dbContext.AppDevices.FirstOrDefaultAsync(d => d.DeviceInstallId == deviceInstallId, cancellationToken);
        var isNew = device is null;
        device ??= new AppDevice { Id = Guid.NewGuid(), DeviceInstallId = deviceInstallId, FirstSeenAt = now, PushTokenStatus = "none", PushPermission = "not_determined" };
        device.Platform = platform;
        device.OsVersion = osVersion ?? device.OsVersion;
        device.AppVersion = appVersion ?? device.AppVersion;
        device.Locale = locale ?? device.Locale;
        device.LastActiveAt = now;
        if (request.PushPermission is not null)
        {
            device.PushPermission = request.PushPermission;
        }

        if (!string.IsNullOrWhiteSpace(request.PushToken))
        {
            var token = request.PushToken.Trim();
            var hash = PushTokenProtector.Hash(token);
            if (device.PushTokenHash != hash || device.PushTokenStatus != "valid")
            {
                device.PushTokenEncrypted = protector.Encrypt(token);
                device.PushTokenHash = hash;
                device.PushTokenStatus = "valid";
            }

            // 同一權杖在其他裝置列：舊列失效（重裝或換機後不會重複收到）。
            await dbContext.AppDevices.Where(d => d.PushTokenHash == hash && d.Id != device.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.PushTokenStatus, "invalid").SetProperty(d => d.PushTokenEncrypted, (string?)null).SetProperty(d => d.PushTokenHash, (string?)null), cancellationToken);
        }

        if (isNew)
        {
            dbContext.AppDevices.Add(device);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new AppDeviceRegisteredDto { DeviceInstallId = deviceInstallId, IsNew = isNew, PushTokenStatus = device.PushTokenStatus };
    }

    public async Task<IReadOnlyList<AppSubscriptionDto>?> ListSubscriptionsAsync(string deviceInstallId, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(deviceInstallId);
        var device = await dbContext.AppDevices.AsNoTracking().FirstOrDefaultAsync(d => d.DeviceInstallId == deviceInstallId, cancellationToken);
        return device is null ? null : await LoadAsync(device.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<AppSubscriptionDto>?> UpdateSubscriptionsAsync(
        string deviceInstallId, UpdateAppSubscriptionsRequest request, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(deviceInstallId);
        if (request.Items.Count > MaxSubscriptionItems)
        {
            throw new AdminValidationException($"一次最多更新 {MaxSubscriptionItems} 筆訂閱。");
        }

        var device = await dbContext.AppDevices.FirstOrDefaultAsync(d => d.DeviceInstallId == deviceInstallId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        foreach (var item in request.Items)
        {
            AdminInput.OneOf(item.TopicType, TopicTypes, "訂閱類型", "「球隊」「新聞分類」或「俱樂部」");
            AdminInput.RequireText(item.TopicValue, "訂閱對象", 64);
        }

        var duplicates = request.Items.GroupBy(i => (i.TopicType, i.TopicValue)).Any(g => g.Count() > 1);
        if (duplicates)
        {
            throw new AdminValidationException("訂閱清單裡有重複的項目。");
        }

        foreach (var g in request.Items.GroupBy(i => i.TopicType))
        {
            var values = g.Select(i => i.TopicValue).ToList();
            var known = g.Key switch
            {
                "team" => await dbContext.Teams.AsNoTracking().Where(t => values.Contains(t.Code)).Select(t => t.Code).ToListAsync(cancellationToken),
                "club" => await dbContext.Clubs.AsNoTracking().Where(c => values.Contains(c.Code)).Select(c => c.Code).ToListAsync(cancellationToken),
                _ => await dbContext.ArticleCategories.AsNoTracking().Where(c => values.Contains(c.Code)).Select(c => c.Code).ToListAsync(cancellationToken),
            };
            var unknown = values.Except(known).FirstOrDefault();
            if (unknown is not null)
            {
                throw new AdminValidationException($"找不到訂閱對象「{unknown}」。");
            }
        }

        var existing = await dbContext.PushTopicSubscriptions.Where(s => s.DeviceId == device.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in request.Items)
        {
            var row = existing.FirstOrDefault(s => s.TopicType == item.TopicType && s.TopicValue == item.TopicValue);
            if (row is null)
            {
                dbContext.PushTopicSubscriptions.Add(new PushTopicSubscription
                {
                    Id = Guid.NewGuid(), DeviceId = device.Id, MemberId = device.MemberId, TopicType = item.TopicType, TopicValue = item.TopicValue,
                    IsFollowing = item.IsFollowing, IsPushEnabled = item.IsPushEnabled, CreatedAt = now, UpdatedAt = now,
                });
            }
            else
            {
                row.IsFollowing = item.IsFollowing;
                row.IsPushEnabled = item.IsPushEnabled;
                row.UpdatedAt = now;
            }
        }

        if (request.ReplaceAll)
        {
            var keep = request.Items.Select(i => (i.TopicType, i.TopicValue)).ToHashSet();
            dbContext.PushTopicSubscriptions.RemoveRange(existing.Where(s => !keep.Contains((s.TopicType, s.TopicValue))));
        }

        device.LastActiveAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadAsync(device.Id, cancellationToken);
    }

    private async Task<IReadOnlyList<AppSubscriptionDto>> LoadAsync(Guid deviceId, CancellationToken cancellationToken)
        => await dbContext.PushTopicSubscriptions.AsNoTracking().Where(s => s.DeviceId == deviceId).OrderBy(s => s.TopicType).ThenBy(s => s.TopicValue)
            .Select(s => new AppSubscriptionDto { TopicType = s.TopicType, TopicValue = s.TopicValue, IsFollowing = s.IsFollowing, IsPushEnabled = s.IsPushEnabled })
            .ToListAsync(cancellationToken);
}
