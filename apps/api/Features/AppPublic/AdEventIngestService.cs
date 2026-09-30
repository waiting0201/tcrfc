using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminAds;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// 廣告事件批次上報（App 規劃書 §7.5、§7.6、§9.6、docs/19 §6.4）。伺服器端的三道關卡：
/// ① <b>時間</b>：以「發生時間」記錄，拒收超過 24 小時的事件（否則成效可被人為堆積）與明顯在未來的事件；
/// ② <b>去重</b>：曝光以 <c>presentationId</c>（沒有就以「素材＋裝置＋秒」）、點擊以「同裝置同素材 5 秒內只計 1 次」，
///    唯一鍵擋住重送與並行（批次可安全重送，同一個 <c>batchId</c> 再送只會全部算重複）；
/// ③ <b>來源</b>：檔期與版位一律由素材推導，不信任 App 傳來的；只接受「曾經投放過」的檔期（草稿與作廢不收）。
/// 🔴 <c>ad_events</c> 不存 <c>member_id</c>、完整 IP、定位座標與廣告識別碼——這裡連請求裡的 IP 都不讀。
/// </summary>
public sealed class AdEventIngestService(ClubDbContext dbContext)
{
    public const int MaxBatch = 200;
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClickWindow = TimeSpan.FromSeconds(5);

    public async Task<AppAdEventBatchResult> IngestAsync(AppAdEventBatchRequest request, CancellationToken cancellationToken)
    {
        AppInput.RequireDeviceId(request.DeviceInstallId);
        var platform = AppInput.RequirePlatform(request.Platform);
        var locale = request.Locale is null ? null : AdLabels.ToDbLocale(request.Locale);
        var appVersion = AppInput.OptionalVersion(request.AppVersion);
        var batchId = AppInput.OptionalToken(request.BatchId, "批次編號", 64);
        if (request.Events.Count == 0)
        {
            throw new Common.AdminValidationException("沒有任何事件。");
        }

        if (request.Events.Count > MaxBatch)
        {
            throw new Common.AdminValidationException($"單次最多上報 {MaxBatch} 筆事件。");
        }

        var now = DateTime.UtcNow;
        var creativeIds = request.Events.Select(e => e.CreativeId).Distinct().ToList();
        var creatives = await dbContext.AdCreatives.AsNoTracking().Where(c => creativeIds.Contains(c.Id))
            .Select(c => new { c.Id, c.CampaignId, c.Campaign.SlotId, c.Campaign.Status }).ToDictionaryAsync(c => c.Id, cancellationToken);

        var rejected = new List<AppAdEventRejectionDto>();
        var perCampaign = new Dictionary<Guid, int>();
        int accepted = 0, duplicates = 0;
        for (var i = 0; i < request.Events.Count; i++)
        {
            var e = request.Events[i];
            if (e.Type is not ("impression" or "click"))
            {
                rejected.Add(new AppAdEventRejectionDto { Index = i, Reason = "invalid" });
                continue;
            }

            var occurred = e.OccurredAt.UtcDateTime;
            if (now - occurred > MaxAge)
            {
                rejected.Add(new AppAdEventRejectionDto { Index = i, Reason = "too_old" });
                continue;
            }

            if (occurred - now > MaxFutureSkew)
            {
                rejected.Add(new AppAdEventRejectionDto { Index = i, Reason = "future" });
                continue;
            }

            if (!creatives.TryGetValue(e.CreativeId, out var creative))
            {
                rejected.Add(new AppAdEventRejectionDto { Index = i, Reason = "unknown_creative" });
                continue;
            }

            if (creative.Status is AdCampaignLifecycle.Draft or AdCampaignLifecycle.PendingReview or AdCampaignLifecycle.Voided)
            {
                rejected.Add(new AppAdEventRejectionDto { Index = i, Reason = "not_serving" });
                continue;
            }

            var presentation = AppInput.OptionalToken(e.PresentationId, "曝光識別碼", 64);
            string keySource;
            if (e.Type == "impression")
            {
                keySource = presentation is not null
                    ? $"i|{e.CreativeId:N}|{presentation}"
                    : $"i|{e.CreativeId:N}|{request.DeviceInstallId}|{occurred:yyyyMMddHHmmss}";
            }
            else
            {
                keySource = $"c|{e.CreativeId:N}|{request.DeviceInstallId}|{occurred.Ticks / ClickWindow.Ticks}";
                var from = occurred - ClickWindow;
                var to = occurred + ClickWindow;
                if (await dbContext.AdEvents.AnyAsync(x => x.DeviceInstallId == request.DeviceInstallId && x.CreativeId == e.CreativeId
                        && x.EventType == "click" && x.OccurredAt > from && x.OccurredAt < to, cancellationToken))
                {
                    duplicates++;
                    continue;
                }
            }

            var dedupe = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(keySource)))[..32];
            var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ad_events (event_type, creative_id, campaign_id, slot_id, occurred_at, received_at, device_install_id, platform, app_version, locale, presentation_id, batch_id, dedupe_key)
SELECT {e.Type}, {e.CreativeId}, {creative.CampaignId}, {creative.SlotId}, {occurred}, {now}, {request.DeviceInstallId}, {platform}, {appVersion}, {locale}, {presentation}, {batchId}, {dedupe}
WHERE NOT EXISTS (SELECT 1 FROM ad_events WHERE dedupe_key = {dedupe})", cancellationToken);
            if (inserted == 0)
            {
                duplicates++;
                continue;
            }

            accepted++;
            if (e.Type == "impression")
            {
                perCampaign[creative.CampaignId] = perCampaign.GetValueOrDefault(creative.CampaignId) + 1;
            }
        }

        var today = TaiwanClock.Today;
        foreach (var (campaignId, n) in perCampaign)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ad_campaigns SET delivered_total = delivered_total + {n},
  delivered_today = CASE WHEN delivered_on = {today} THEN delivered_today + {n} ELSE {n} END, delivered_on = {today}
WHERE id = {campaignId}", cancellationToken);
        }

        return new AppAdEventBatchResult { Accepted = accepted, Duplicates = duplicates, Rejected = rejected };
    }
}
