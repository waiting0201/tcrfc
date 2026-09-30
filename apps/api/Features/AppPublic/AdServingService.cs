using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Videos;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// 廣告投放（App 規劃書 §7.4、§9.2「廣告投放 讀取 匿名」）：依版位回傳「當下應顯示」的素材。
/// 規則：只投「投放中」且在起訖時間內的檔期；只投「已通過審核且未暫停」的素材（素材沒審過不會上線）；
/// 每日曝光上限、每人（每裝置）每日頻次上限、曝光保證型的 pacing（目標平均分配到檔期天數，不在前幾天燒完）；
/// 同版位同時段依權重加權隨機（不重複抽取，最多到版位的輪播張數上限）；沒有可投放的就回備援素材（版位永不空白）。
/// 🔴 不做行為定向：分眾只到「版位 × 語系 × 平台」，不讀任何個人資料。
/// </summary>
public sealed class AdServingService(ClubDbContext dbContext, IImagePublicUrlResolver imageUrls, IVideoPublicUrlResolver videoUrls)
{
    public async Task<AppAdResponse?> ServeAsync(
        string slotCode, string? locale, string? deviceInstallId, string? theme, CancellationToken cancellationToken)
    {
        await AdCampaignLifecycle.AdvanceIfDueAsync(dbContext, cancellationToken);
        var slot = await dbContext.AdSlots.AsNoTracking().Include(s => s.AdSlotsI18ns)
            .FirstOrDefaultAsync(s => s.SlotCode == slotCode && s.IsActive, cancellationToken);
        if (slot is null)
        {
            return null;
        }

        var dbLocale = AdLabels.ToDbLocale(locale);
        var now = DateTime.UtcNow;
        var today = TaiwanClock.Today;
        var campaigns = await dbContext.AdCampaigns.AsNoTracking()
            .Where(c => c.SlotId == slot.Id && c.Status == AdCampaignLifecycle.Running && c.StartsAt <= now && c.EndsAt > now)
            .Include(c => c.AdCreatives).ToListAsync(cancellationToken);

        Dictionary<Guid, int> deviceCounts = [];
        if (!string.IsNullOrEmpty(deviceInstallId) && campaigns.Any(c => c.PerDeviceDailyCap is not null))
        {
            var from = TaiwanClock.StartOfDayUtc(today);
            var ids = campaigns.Where(c => c.PerDeviceDailyCap is not null).Select(c => c.Id).ToList();
            deviceCounts = await dbContext.AdEvents.AsNoTracking()
                .Where(e => e.DeviceInstallId == deviceInstallId && e.EventType == "impression" && e.OccurredAt >= from && ids.Contains(e.CampaignId))
                .GroupBy(e => e.CampaignId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        }

        var eligible = new List<(AdCampaign Campaign, AdCreative Creative)>();
        foreach (var c in campaigns)
        {
            var deliveredToday = c.DeliveredOn == today ? c.DeliveredToday : 0;
            if (c.DailyImpressionCap is { } cap && deliveredToday >= cap) { continue; }
            if (c.PerDeviceDailyCap is { } dcap && deviceCounts.GetValueOrDefault(c.Id) >= dcap) { continue; }
            if (c.GoalType == "guaranteed" && c.GoalImpressions is { } goal)
            {
                if (c.DeliveredTotal >= goal) { continue; }
                // pacing：把「還沒交付的量」平均分配到剩餘天數（含今天），今天已交付量超過今天的份額就先停。
                var remaining = goal - (c.DeliveredTotal - deliveredToday);
                var remainingDays = Math.Max(1, (int)Math.Ceiling((c.EndsAt - now).TotalDays));
                if (deliveredToday >= (int)Math.Ceiling((double)remaining / remainingDays)) { continue; }
            }

            var pool = c.AdCreatives.Where(x => x.ReviewStatus == "approved" && !x.IsPaused).ToList();
            var byLocale = pool.Where(x => x.Locale == dbLocale).ToList();
            if (byLocale.Count == 0)
            {
                byLocale = pool.Where(x => x.Locale == RequestLocale.DefaultDbLocale).ToList(); // 沒有該語系素材就回退繁中
            }

            if (byLocale.Count == 0) { continue; }
            var themed = theme is "light" or "dark" ? byLocale.Where(x => x.Theme == theme || x.Theme == "both").ToList() : byLocale;
            var pick = (themed.Count > 0 ? themed : byLocale)[Random.Shared.Next(themed.Count > 0 ? themed.Count : byLocale.Count)];
            eligible.Add((c, pick));
        }

        var chosen = WeightedSample(eligible, slot.RotationCap);
        var label = dbLocale == "en" ? "Ad" : "廣告";
        if (chosen.Count == 0)
        {
            return new AppAdResponse
            {
                SlotCode = slot.SlotCode, IsFallback = true, DisclosureLabel = label, SessionImpressionCap = slot.SessionImpressionCap,
                Items = slot.FallbackImageKey is null ? [] : [FallbackItem(slot, dbLocale)],
            };
        }

        return new AppAdResponse
        {
            SlotCode = slot.SlotCode, IsFallback = false, DisclosureLabel = label, SessionImpressionCap = slot.SessionImpressionCap,
            Items = chosen.Select(x => new AppAdItemDto
            {
                CreativeId = x.Creative.Id, CampaignId = x.Campaign.Id, IsFallback = false, ImageUrl = imageUrls.Resolve(x.Creative.ImageKey),
                ImageWidth = x.Creative.ImageWidth, ImageHeight = x.Creative.ImageHeight, VideoUrl = videoUrls.Resolve(x.Creative.VideoKey),
                AltText = x.Creative.AltText, Title = x.Creative.Title, CtaText = x.Creative.CtaText, ClickUrl = x.Creative.ClickUrl, Theme = x.Creative.Theme,
            }).ToList(),
        };
    }

    private AppAdItemDto FallbackItem(AdSlot slot, string dbLocale) => new()
    {
        IsFallback = true, ImageUrl = imageUrls.Resolve(slot.FallbackImageKey), ImageWidth = slot.FallbackImageWidth, ImageHeight = slot.FallbackImageHeight,
        AltText = RequestLocale.Pick(slot.AdSlotsI18ns.FirstOrDefault(i => i.Locale == dbLocale)?.FallbackAlt,
            slot.AdSlotsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.FallbackAlt),
        ClickUrl = slot.FallbackLink,
    };

    /// <summary>依權重的不重複加權隨機抽取，最多 <paramref name="take"/> 個。</summary>
    internal static List<(AdCampaign Campaign, AdCreative Creative)> WeightedSample(List<(AdCampaign Campaign, AdCreative Creative)> pool, int take)
    {
        var remaining = pool.ToList();
        var result = new List<(AdCampaign, AdCreative)>();
        while (result.Count < take && remaining.Count > 0)
        {
            var total = remaining.Sum(x => x.Campaign.Weight);
            var roll = Random.Shared.Next(total);
            var acc = 0;
            for (var i = 0; i < remaining.Count; i++)
            {
                acc += remaining[i].Campaign.Weight;
                if (roll < acc)
                {
                    result.Add(remaining[i]);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return result;
    }
}
