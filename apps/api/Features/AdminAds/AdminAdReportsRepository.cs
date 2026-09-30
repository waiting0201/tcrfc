using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E6 廣告成效報表（App 規劃書 §7.7、§8.9）。維度：檔期／版位／素材／平台／語系／日期；指標：曝光、點擊、CTR、不重複裝置數；
/// 曝光保證型附「目標 vs 已達成」與 pacing。🔴 <b>不提供任何個人層級資料</b>（裝置清單、會員身分關聯）——報表只讀日聚合。
/// 匯出（CSV）須有 <c>ad.report.export</c>、填用途，並寫敏感操作日誌（誰、何時、哪個檔期、用途）。贊助商 Logo 牆不計曝光、不在此報表。
/// </summary>
public sealed class AdminAdReportsRepository(ClubDbContext dbContext, SensitiveActionLogger audit)
{
    public const int MaxRangeDays = 366;
    private static readonly IReadOnlySet<string> Groups = new HashSet<string>(["campaign", "slot", "creative", "platform", "locale", "date"], StringComparer.Ordinal);

    public async Task<AdminAdReportDto> BuildAsync(AdReportQuery q, CancellationToken cancellationToken)
    {
        var (from, to, groupBy) = Normalize(q);
        var stats = dbContext.AdDailyStats.AsNoTracking().Where(s => s.StatDate >= from && s.StatDate <= to);
        if (q.CampaignId is { } cid) { stats = stats.Where(s => s.CampaignId == cid); }
        if (q.SlotId is { } sid) { stats = stats.Where(s => s.SlotId == sid); }
        if (q.CreativeId is { } crid) { stats = stats.Where(s => s.CreativeId == crid); }
        if (!string.IsNullOrWhiteSpace(q.Platform))
        {
            if (!AdLabels.Platform.ContainsKey(q.Platform)) { throw new AdminValidationException("平台只能是 iOS 或 Android（代碼 ios、android）。"); }
            stats = stats.Where(s => s.Platform == q.Platform);
        }

        if (!string.IsNullOrWhiteSpace(q.Locale))
        {
            var dbLocale = AdLabels.ToDbLocale(q.Locale);
            stats = stats.Where(s => s.Locale == dbLocale);
        }

        var rows = await stats.ToListAsync(cancellationToken);
        var campaignNames = await dbContext.AdCampaigns.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c, cancellationToken);
        var slotNames = await dbContext.AdSlots.AsNoTracking().Include(s => s.AdSlotsI18ns).ToDictionaryAsync(
            s => s.Id, s => $"{s.SlotCode}（{s.AdSlotsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name}）", cancellationToken);
        var creativeInfo = await dbContext.AdCreatives.AsNoTracking().ToDictionaryAsync(c => c.Id, c => (c.CampaignId, c.Locale, c.VariantTag, c.SortOrder), cancellationToken);

        (Guid? Id, string Label) Key(Data.EfEntities.AdDailyStat s) => groupBy switch
        {
            "campaign" => (s.CampaignId, campaignNames.TryGetValue(s.CampaignId, out var c) ? c.Name : "（已刪除的檔期）"),
            "slot" => (s.SlotId, slotNames.GetValueOrDefault(s.SlotId, "（已刪除的版位）")),
            "creative" => (s.CreativeId, creativeInfo.TryGetValue(s.CreativeId, out var ci)
                ? $"{(campaignNames.TryGetValue(ci.CampaignId, out var cc) ? cc.Name : "（檔期）")}／{AdLabels.Of(AdLabels.Locale, ci.Locale)}素材{(ci.VariantTag is null ? "" : "（" + ci.VariantTag + "）")}"
                : "（已刪除的素材）"),
            "platform" => (null, AdLabels.Of(AdLabels.Platform, s.Platform)),
            "locale" => (null, AdLabels.Of(AdLabels.Locale, s.Locale)),
            _ => (null, s.StatDate.ToString("yyyy-MM-dd")),
        };

        var grouped = rows.GroupBy(Key).Select(g => Row(g.Key.Label, g.Key.Id, g.Sum(x => (long)x.Impressions), g.Sum(x => (long)x.Clicks), g.Sum(x => (long)x.UniqueDevices)))
            .OrderBy(r => groupBy == "date" ? 0 : 1).ThenBy(r => groupBy == "date" ? r.Label : string.Empty)
            .ThenByDescending(r => r.Impressions).ToList();
        var total = Row("合計", null, rows.Sum(x => (long)x.Impressions), rows.Sum(x => (long)x.Clicks), rows.Sum(x => (long)x.UniqueDevices));

        var fromUtc = TaiwanClock.StartOfDayUtc(from);
        var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
        var pending = await dbContext.AdEvents.CountAsync(e => e.AggregatedAt == null && e.OccurredAt >= fromUtc && e.OccurredAt < toUtc, cancellationToken);

        var pacingQuery = dbContext.AdCampaigns.AsNoTracking().Where(c => c.GoalType == "guaranteed" && (c.Status == "running" || c.Status == "scheduled" || c.Status == "paused"));
        if (q.CampaignId is { } pc) { pacingQuery = pacingQuery.Where(c => c.Id == pc); }
        if (q.SlotId is { } ps) { pacingQuery = pacingQuery.Where(c => c.SlotId == ps); }
        var pacing = (await pacingQuery.ToListAsync(cancellationToken)).Select(c => (c, p: AdminAdCampaignsRepository.ComputePacing(c, DateTime.UtcNow)))
            .Where(x => x.p is not null).Select(x => new AdminAdPacingRowDto { CampaignId = x.c.Id, CampaignName = x.c.Name, Pacing = x.p! }).ToList();

        return new AdminAdReportDto { From = from, To = to, GroupBy = groupBy, Rows = grouped, Total = total, PendingEvents = pending, Pacing = pacing };
    }

    public async Task<string> ExportCsvAsync(AdReportQuery q, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var purpose = AdminInput.RequireText(q.Purpose, "匯出用途", 200);
        var report = await BuildAsync(q, cancellationToken);
        var lines = new List<IEnumerable<string?>> { new[] { "項目", "曝光數", "點擊數", "點擊率（%）", "不重複裝置數" } };
        lines.AddRange(report.Rows.Append(report.Total).Select(r => (IEnumerable<string?>)new[]
        {
            CsvUtils.SafeCell(r.Label), r.Impressions.ToString(), r.Clicks.ToString(), r.Ctr.ToString("0.00"), r.UniqueDevices.ToString(),
        }));

        string subject;
        if (q.CampaignId is { } id)
        {
            var name = await dbContext.AdCampaigns.AsNoTracking().Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
            subject = $"檔期 {id}（{name}）{report.From:yyyy-MM-dd}～{report.To:yyyy-MM-dd}";
        }
        else
        {
            subject = $"全部檔期（依{q.GroupBy ?? "campaign"}）{report.From:yyyy-MM-dd}～{report.To:yyyy-MM-dd}";
        }

        audit.Record(scope, "匯出廣告成效報表", subject, report.Rows.Count, purpose);
        return CsvUtils.BuildCsv(lines);
    }

    private static (DateOnly From, DateOnly To, string GroupBy) Normalize(AdReportQuery q)
    {
        var today = TaiwanClock.Today;
        var to = q.To ?? today;
        var from = q.From ?? to.AddDays(-29);
        if (to < from)
        {
            throw new AdminValidationException("結束日期不可早於開始日期。");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
        {
            throw new AdminValidationException($"報表期間最長 {MaxRangeDays} 天。");
        }

        var groupBy = q.GroupBy ?? "campaign";
        AdminInput.OneOf(groupBy, Groups, "彙總維度", "「檔期」「版位」「素材」「平台」「語系」或「日期」");
        return (from, to, groupBy);
    }

    private static AdminAdReportRowDto Row(string label, Guid? id, long impressions, long clicks, long uniqueDevices) => new()
    {
        Label = label, Id = id, Impressions = impressions, Clicks = clicks, UniqueDevices = uniqueDevices,
        Ctr = impressions == 0 ? 0m : Math.Round(100m * clicks / impressions, 2),
    };
}
