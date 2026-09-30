using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S6 報表（規劃書 §4.13 S6）：營收、訂單數、客單價、熱銷 SKU、庫存、退貨率；<b>v3.0 依 <c>selling_club_id</c> 的加總與匯出</b>，供線下分帳作業使用。
/// A 儀表板只顯示摘要，明細一律回這裡。匯出須 <c>shop.report.export</c>（受限）並寫入敏感操作日誌（誰、何時、哪個期間、用途）。
/// 口徑（現金基礎，避免與會計認列混淆）：
/// <list type="bullet">
/// <item><b>營收</b>＝成立付款時間落在期間內的訂單「總額（含運費）」合計，<b>不論之後是否取消</b>；</item>
/// <item><b>退款</b>＝退款執行時間落在期間內的案件金額合計；<b>淨營收</b>＝營收－退款；</item>
/// <item><b>客單價</b>＝營收 ÷ 訂單數；<b>退貨率</b>＝期間內有退款的訂單數 ÷ 訂單數；</item>
/// <item><b>熱銷 SKU</b>＝期間內已付款訂單的品項依貨號加總（依數量取前 10）；<b>庫存</b>是即時值，不受期間影響。</item>
/// </list>
/// 🔴 <b>兩隊分潤不做</b>：只做「依賣方俱樂部加總與匯出」，<b>不計算應付金額、不產生結算單</b>；分帳標記是 S3 訂單上的人工旗標。
/// 資料範圍：單一俱樂部報表只算 <c>selling_club_id</c> 為目前俱樂部的訂單；分帳彙總只列帳號有授權的俱樂部（系統管理員為全部）。刻意不注入快取服務。
/// </summary>
public sealed class AdminShopReportsRepository(ClubDbContext db, ShopSettingsReader shopSettings, SensitiveActionLogger audit)
{
    private static readonly string[] PaidStatuses = ["paid", "refunded"];

    private static (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to)
    {
        var end = to ?? TaiwanClock.Today;
        var start = from ?? end.AddDays(-29);
        if (start > end)
        {
            throw new AdminValidationException("報表的起始日期不可晚於結束日期。");
        }

        if (end.DayNumber - start.DayNumber > 800)
        {
            throw new AdminValidationException("報表期間最長 800 天。");
        }

        return (start, end);
    }

    public async Task<AdminShopReportSummaryDto> SummaryAsync(AdminClubScope scope, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var (start, end) = Period(from, to);
        var fromUtc = TaiwanClock.StartOfDayUtc(start);
        var toUtc = TaiwanClock.StartOfDayUtc(end.AddDays(1));
        var paid = db.Orders.AsNoTracking().Where(o => o.SellingClubId == scope.ClubId && PaidStatuses.Contains(o.PaymentStatus) && o.PaidAt >= fromUtc && o.PaidAt < toUtc);
        var orderCount = await paid.CountAsync(cancellationToken);
        var gross = await paid.SumAsync(o => (int?)o.Total, cancellationToken) ?? 0;
        var refunds = db.RefundRequests.AsNoTracking().Where(r => r.Order.SellingClubId == scope.ClubId && r.Status == "refunded" && r.RefundedAt >= fromUtc && r.RefundedAt < toUtc);
        var refunded = await refunds.SumAsync(r => (int?)r.RefundAmount, cancellationToken) ?? 0;
        var returnOrders = await refunds.Select(r => r.OrderId).Distinct().CountAsync(cancellationToken);
        var top = await db.OrderItems.AsNoTracking().Where(i => paid.Select(o => o.Id).Contains(i.OrderId))
            .GroupBy(i => i.SkuSnapshot).Select(g => new
            {
                Sku = g.Key, Name = g.Max(x => x.ProductNameSnapshot), Label = g.Max(x => x.VariantLabelSnapshot),
                Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal),
            }).OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Revenue).Take(10).ToListAsync(cancellationToken);
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var stock = await db.ProductVariants.AsNoTracking().Where(v => v.ClubId == scope.ClubId && v.Status == "active")
            .Select(v => new { Available = v.StockQty - v.ReservedQty, Threshold = v.LowStockThreshold ?? threshold }).ToListAsync(cancellationToken);
        return new AdminShopReportSummaryDto
        {
            From = start, To = end, GrossRevenue = gross, RefundedAmount = refunded, NetRevenue = gross - refunded, OrderCount = orderCount,
            AverageOrderValue = orderCount == 0 ? 0 : (int)Math.Round(gross / (decimal)orderCount, MidpointRounding.AwayFromZero), ReturnOrderCount = returnOrders,
            ReturnRatePercent = orderCount == 0 ? 0 : Math.Round(returnOrders * 100m / orderCount, 1, MidpointRounding.AwayFromZero),
            TopSkus = top.Select(t => new AdminShopTopSkuDto { Sku = t.Sku, ProductName = t.Name, VariantLabel = t.Label, Quantity = t.Quantity, Revenue = t.Revenue }).ToList(),
            LowStockCount = stock.Count(s => s.Available <= s.Threshold), OutOfStockCount = stock.Count(s => s.Available <= 0), TotalAvailableQuantity = stock.Sum(s => Math.Max(s.Available, 0)),
        };
    }

    /// <summary>依賣方俱樂部加總（線下分帳用）：只列帳號有授權的俱樂部。</summary>
    public async Task<IReadOnlyList<AdminSellingClubTotalDto>> BySellingClubAsync(AdminClubScope scope, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var (start, end) = Period(from, to);
        var fromUtc = TaiwanClock.StartOfDayUtc(start);
        var toUtc = TaiwanClock.StartOfDayUtc(end.AddDays(1));
        var reach = await AdminReach.GetAuthorizedClubIdsAsync(db, scope, cancellationToken);
        var orders = db.Orders.AsNoTracking().Where(o => PaidStatuses.Contains(o.PaymentStatus) && o.PaidAt >= fromUtc && o.PaidAt < toUtc);
        var refunds = db.RefundRequests.AsNoTracking().Where(r => r.Status == "refunded" && r.RefundedAt >= fromUtc && r.RefundedAt < toUtc);
        if (reach is not null)
        {
            orders = orders.Where(o => reach.Contains(o.SellingClubId));
            refunds = refunds.Where(r => reach.Contains(r.Order.SellingClubId));
        }

        var orderRows = await orders.GroupBy(o => o.SellingClubId).Select(g => new
        {
            ClubId = g.Key, Count = g.Count(), Gross = g.Sum(o => o.Total),
            Settled = g.Where(o => o.SettlementStatus == "settled").Sum(o => o.Total), Pending = g.Where(o => o.SettlementStatus == "pending").Sum(o => o.Total),
        }).ToListAsync(cancellationToken);
        var refundRows = await refunds.GroupBy(r => r.Order.SellingClubId).Select(g => new { ClubId = g.Key, Amount = g.Sum(r => r.RefundAmount ?? 0) }).ToListAsync(cancellationToken);
        var clubs = await db.Clubs.AsNoTracking().Where(c => reach == null || reach.Contains(c.Id)).OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Code, Name = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() }).ToListAsync(cancellationToken);
        return clubs.Select(c =>
        {
            var o = orderRows.FirstOrDefault(x => x.ClubId == c.Id);
            var refunded = refundRows.FirstOrDefault(x => x.ClubId == c.Id)?.Amount ?? 0;
            return new AdminSellingClubTotalDto
            {
                SellingClubId = c.Id, SellingClubCode = c.Code, SellingClubName = c.Name, OrderCount = o?.Count ?? 0, GrossRevenue = o?.Gross ?? 0, RefundedAmount = refunded,
                NetRevenue = (o?.Gross ?? 0) - refunded, SettledAmount = o?.Settled ?? 0, PendingSettlementAmount = o?.Pending ?? 0,
            };
        }).ToList();
    }

    /// <summary>報表 CSV（<c>shop.report.export</c>，受限）。<paramref name="kind"/>：<c>summary</c>（含熱銷 SKU）或 <c>by-selling-club</c>（分帳彙總）。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, string? kind, DateOnly? from, DateOnly? to, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var effectiveKind = string.IsNullOrWhiteSpace(kind) ? "summary" : kind;
        AdminInput.OneOf(effectiveKind, new HashSet<string>(["summary", "by-selling-club"]), "報表種類", "「摘要」或「依賣方俱樂部加總」");
        var (start, end) = Period(from, to);
        var lines = new List<IEnumerable<string?>>();
        int count;
        if (effectiveKind == "by-selling-club")
        {
            var rows = await BySellingClubAsync(scope, start, end, cancellationToken);
            lines.Add(["賣方俱樂部", "訂單數", "營收", "退款", "淨營收", "已結算金額", "待結算金額", "期間起", "期間迄"]);
            lines.AddRange(rows.Select(r => new[]
            {
                r.SellingClubName ?? r.SellingClubCode, r.OrderCount.ToString(), r.GrossRevenue.ToString(), r.RefundedAmount.ToString(), r.NetRevenue.ToString(),
                r.SettledAmount.ToString(), r.PendingSettlementAmount.ToString(), start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"),
            }));
            count = rows.Count;
        }
        else
        {
            var s = await SummaryAsync(scope, start, end, cancellationToken);
            lines.Add(["項目", "數值"]);
            lines.Add(["期間", $"{s.From:yyyy-MM-dd} 至 {s.To:yyyy-MM-dd}"]);
            lines.Add(["訂單數", s.OrderCount.ToString()]);
            lines.Add(["營收", s.GrossRevenue.ToString()]);
            lines.Add(["退款", s.RefundedAmount.ToString()]);
            lines.Add(["淨營收", s.NetRevenue.ToString()]);
            lines.Add(["客單價", s.AverageOrderValue.ToString()]);
            lines.Add(["退貨率（%）", s.ReturnRatePercent.ToString("0.0")]);
            lines.Add(["低庫存規格數", s.LowStockCount.ToString()]);
            lines.Add(["缺貨規格數", s.OutOfStockCount.ToString()]);
            lines.Add([]);
            lines.Add(["熱銷貨號", "商品名稱", "規格", "數量", "營收"]);
            lines.AddRange(s.TopSkus.Select(t => new[] { t.Sku, t.ProductName, t.VariantLabel, t.Quantity.ToString(), t.Revenue.ToString() }));
            count = s.TopSkus.Count + 8;
        }

        audit.Record(scope, "匯出商店報表", $"{effectiveKind}／{start:yyyy-MM-dd}～{end:yyyy-MM-dd}", count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }
}
