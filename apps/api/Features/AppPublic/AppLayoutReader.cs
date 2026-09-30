using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// App 內容編排的公開讀取（App 規劃書 §8.2 M2）：首頁區塊的開關與排序、快捷入口、「更多」分頁項目、公告條、深連結對照表。
/// 只管「呈現順序與開關」，內容本身（新聞、賽事…）仍走各自的端點。公告條的「目標對象」在帶了裝置識別時才依會籍層級與俱樂部篩選；
/// 沒帶裝置識別時只給「全部對象」的公告，因此可以放心在邊緣快取。
/// </summary>
public sealed class AppLayoutReader(ClubDbContext dbContext, IImagePublicUrlResolver imageUrls)
{
    public async Task<AppLayoutResponse> ReadAsync(string? lang, string? deviceInstallId, CancellationToken cancellationToken)
    {
        var db = AdLabels.ToDbLocale(lang);
        var links = await dbContext.AppDeepLinks.AsNoTracking().Include(l => l.AppDeepLinksI18ns).Where(l => l.IsActive)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.RowSeq).ToListAsync(cancellationToken);
        var linkById = links.ToDictionary(l => l.Id);
        var items = await dbContext.AppLayoutItems.AsNoTracking().Include(i => i.AppLayoutItemsI18ns).Where(i => i.IsEnabled)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).ToListAsync(cancellationToken);

        AppLayoutItemDto Map(AppLayoutItem i)
        {
            var link = i.DeepLinkId is { } id ? linkById.GetValueOrDefault(id) : null;
            return new AppLayoutItemDto
            {
                Code = i.ItemKey, Icon = i.IconKey, DeepLink = link?.AppLink, WebUrl = link?.WebUrl,
                Label = RequestLocale.Pick(i.AppLayoutItemsI18ns.FirstOrDefault(x => x.Locale == db)?.Label,
                    i.AppLayoutItemsI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Label),
            };
        }

        var now = DateTime.UtcNow;
        var announcements = await dbContext.AppAnnouncements.AsNoTracking().Include(a => a.AppAnnouncementsI18ns)
            .Where(a => a.IsEnabled && (a.StartsAt == null || a.StartsAt <= now) && (a.EndsAt == null || a.EndsAt > now))
            .OrderBy(a => a.RowSeq).ToListAsync(cancellationToken);
        var visible = new List<AppAnnouncement>();
        Guid? deviceId = null;
        if (!string.IsNullOrEmpty(deviceInstallId) && announcements.Any(a => a.AudienceTier != "all" || a.AudienceClubId != null))
        {
            deviceId = await dbContext.AppDevices.AsNoTracking().Where(d => d.DeviceInstallId == deviceInstallId).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(cancellationToken);
        }

        foreach (var a in announcements)
        {
            if (a.AudienceTier == "all" && a.AudienceClubId is null)
            {
                visible.Add(a);
            }
            else if (deviceId is { } did
                     && await PushAudience.Filter(dbContext, dbContext.AppDevices.AsNoTracking().Where(d => d.Id == did),
                         new PushAudienceSpec(a.AudienceTier, a.AudienceClubId, [])).AnyAsync(cancellationToken))
            {
                visible.Add(a);
            }
        }

        return new AppLayoutResponse
        {
            GeneratedAt = now,
            HomeSections = items.Where(i => i.Kind == "home_section").Select(Map).ToList(),
            QuickEntries = items.Where(i => i.Kind == "quick_entry").Select(Map).ToList(),
            MoreItems = items.Where(i => i.Kind == "more_item").Select(Map).ToList(),
            Announcements = visible.Select(a => new AppAnnouncementDto
            {
                Id = a.Id, LinkUrl = a.LinkUrl, EndsAt = a.EndsAt,
                Message = RequestLocale.Pick(a.AppAnnouncementsI18ns.FirstOrDefault(x => x.Locale == db)?.Message,
                    a.AppAnnouncementsI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Message),
            }).ToList(),
            DeepLinks = links.Select(l => new AppDeepLinkDto
            {
                Code = l.Code, AppLink = l.AppLink, WebUrl = l.WebUrl, RequiresLogin = l.RequiresLogin,
                Label = RequestLocale.Pick(l.AppDeepLinksI18ns.FirstOrDefault(x => x.Locale == db)?.Label,
                    l.AppDeepLinksI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Label),
            }).ToList(),
        };
    }

    /// <summary>App 通知中心（App 規劃書 §3.13、§9.2「通知中心 列表」）：已送出的推播，保留 90 天。帶裝置識別時只列「這台裝置是對象」的訊息；
    /// 沒帶就只列對所有人發送的訊息。標題與內文依請求語系，缺漏回退繁中。</summary>
    public async Task<IReadOnlyList<AppNotificationDto>> ListNotificationsAsync(string? lang, string? deviceInstallId, CancellationToken cancellationToken)
    {
        var db = AdLabels.ToDbLocale(lang);
        var cut = DateTime.UtcNow.AddDays(-90);
        var messages = await dbContext.PushMessages.AsNoTracking().Include(m => m.PushMessagesI18ns)
            .Where(m => (m.Status == "sent" || m.Status == "partial") && m.SentAt != null && m.SentAt >= cut)
            .OrderByDescending(m => m.SentAt).Take(200).ToListAsync(cancellationToken);
        Guid? deviceId = string.IsNullOrEmpty(deviceInstallId)
            ? null
            : await dbContext.AppDevices.AsNoTracking().Where(d => d.DeviceInstallId == deviceInstallId).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(cancellationToken);

        var result = new List<AppNotificationDto>();
        foreach (var m in messages)
        {
            var spec = PushAudienceSpec.From(m);
            var forEveryone = spec.Tier == "all" && spec.ClubId is null && spec.TeamCodes.Count == 0;
            if (!forEveryone && (deviceId is not { } did
                || !await PushAudience.Filter(dbContext, dbContext.AppDevices.AsNoTracking().Where(d => d.Id == did), spec).AnyAsync(cancellationToken)))
            {
                continue;
            }

            result.Add(new AppNotificationDto
            {
                Id = m.Id, SentAt = m.SentAt!.Value, DeepLink = m.DeepLink, ImageUrl = imageUrls.Resolve(m.ImageKey),
                Title = RequestLocale.Pick(m.PushMessagesI18ns.FirstOrDefault(x => x.Locale == db)?.Title, m.PushMessagesI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Title),
                Body = RequestLocale.Pick(m.PushMessagesI18ns.FirstOrDefault(x => x.Locale == db)?.Body, m.PushMessagesI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Body),
            });
            if (result.Count >= 50)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>App 回報「通知被開啟」：只累加彙總數字（批次 × 平台 × 語系），<b>不記錄是哪一台裝置或哪位會員開的</b>（規劃書 §6.6）。
    /// 因為不記個人層級，同一台裝置重複回報無法去重——已知限制，靠限流擋濫用。</summary>
    public async Task<bool> RecordOpenedAsync(Guid messageId, string deviceInstallId, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(deviceInstallId);
        var device = await dbContext.AppDevices.AsNoTracking().Where(d => d.DeviceInstallId == deviceInstallId)
            .Select(d => new { d.Platform, d.Locale }).FirstOrDefaultAsync(cancellationToken);
        if (device is null || !await dbContext.PushMessages.AnyAsync(m => m.Id == messageId && (m.Status == "sent" || m.Status == "partial"), cancellationToken))
        {
            return false;
        }

        var locale = device.Locale ?? RequestLocale.DefaultDbLocale;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE push_messages SET opened_count = opened_count + 1 WHERE id = {messageId}", cancellationToken);
        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE push_message_stats SET opened = opened + 1 WHERE push_message_id = {messageId} AND platform = {device.Platform} AND locale = {locale}", cancellationToken);
        if (updated == 0)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO push_message_stats (push_message_id, platform, locale, sent, delivered, opened) VALUES ({messageId}, {device.Platform}, {locale}, 0, 0, 1)", cancellationToken);
        }

        return true;
    }
}
