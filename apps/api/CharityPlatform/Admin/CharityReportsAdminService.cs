using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Admin;

// N6 捐款報表（規劃書 §7）。五張固定報表：捐款總覽／依店家／依項目／發票開立狀況／逐筆明細。
// 共通維度（§7.1）：期間、店家、項目、付款狀態、發票類型、金額級距。預設只計 paid，可切換含已退款。
// 🔴 全部報表<b>不含捐款人個資</b>（姓名、Email、身分證字號）——§7.3「不含個資的彙總報表不受限」；逐筆明細也不含，要個資走 N3 的含個資匯出（額外授權＋用途備註＋稽核）。

/// <param name="PaymentStatus"><c>paid</c>（預設）／<c>refunded</c>／<c>all</c>（已付款＋已退款，檢視毛額）。</param>
public sealed record ReportFilter(
    DateOnly? From, DateOnly? To, Guid? StoreId, bool NoStore, Guid? ProjectId, string PaymentStatus, string? InvoiceType, int? AmountMin, int? AmountMax);

public sealed record AdminReportTrendPointDto(string Period, int Count, long Amount);

public sealed record AdminReportOverviewDto
{
    public required int DonationCount { get; init; }
    public required long TotalAmount { get; init; }

    /// <summary>平均單筆金額（元，四捨五入到整數）；沒有捐款時為 0。</summary>
    public required int AverageAmount { get; init; }

    /// <summary>期間內建單數、其中成功付款（<c>paid</c> 或後來退款）的筆數與比率（0–1，四位小數）。轉換率以<b>建單時間</b>落在期間內的捐款單為母體。</summary>
    public required int CreatedCount { get; init; }

    public required int ConvertedCount { get; init; }
    public required decimal ConversionRate { get; init; }

    /// <summary><c>day</c>／<c>month</c>。</summary>
    public required string Granularity { get; init; }

    public required IReadOnlyList<AdminReportTrendPointDto> Trend { get; init; }
}

public sealed record AdminReportStoreRowDto
{
    /// <summary>沒有店家歸屬的捐款合併成一列，<c>storeId</c> 與 <c>storeName</c> 為 <c>null</c>。</summary>
    public required Guid? StoreId { get; init; }

    public required string? StoreName { get; init; }
    public required int DonationCount { get; init; }
    public required long TotalAmount { get; init; }

    /// <summary>佔全部捐款金額的比率（0–1，四位小數）。</summary>
    public required decimal AmountRatio { get; init; }

    /// <summary>店家<b>目前</b>的分潤率（%）；歷史捐款以當時快照金額計，不受這個數字影響。無店家歸屬為 0。</summary>
    public required decimal CurrentSharePct { get; init; }

    /// <summary>只計已付款的捐款：應付回饋金、其中已結算（已確認或已付款的結算單）與未結算。</summary>
    public required long PayableAmount { get; init; }

    public required long SettledAmount { get; init; }
    public required long UnsettledAmount { get; init; }
}

public sealed record AdminReportProjectRowDto
{
    public required Guid ProjectId { get; init; }
    public required string? ProjectName { get; init; }
    public required int DonationCount { get; init; }
    public required long TotalAmount { get; init; }
    public required decimal AmountRatio { get; init; }
    public required int AverageAmount { get; init; }
    public required decimal CurrentSharePct { get; init; }

    /// <summary>只計已付款的捐款：應撥付金額、其中已結算與未結算。</summary>
    public required long PayableAmount { get; init; }

    public required long SettledAmount { get; init; }
    public required long UnsettledAmount { get; init; }
}

public sealed record AdminReportInvoiceStatusDto
{
    /// <summary>已開立（含正常、未作廢）。</summary>
    public required int Issued { get; init; }

    public required int Pending { get; init; }

    /// <summary>開立失敗——前台畫面可直接連到 N5 處理。</summary>
    public required int Failed { get; init; }

    public required int Voided { get; init; }
    public required int Allowance { get; init; }

    /// <summary>沒有憑證資料的捐款（理論上不應出現，出現代表資料異常）。</summary>
    public required int NoInvoice { get; init; }
}

public sealed record AdminReportDetailRowDto
{
    public required Guid DonationId { get; init; }
    public required string OrderNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? PaidAt { get; init; }
    public required string Status { get; init; }
    public required int Amount { get; init; }
    public required string? ProjectName { get; init; }
    public required string? StoreName { get; init; }
    public required string? InvoiceStatus { get; init; }
    public required string? InvoiceVoidStatus { get; init; }
    public required bool IsAnonymous { get; init; }
    public required int StoreAmount { get; init; }
    public required int ProjectAmount { get; init; }
    public required int AssociationAmount { get; init; }
}

public sealed class CharityReportsAdminService(CharityDbContext db)
{
    private const int MaxRows = 200_000;

    public static ReportFilter NormalizeFilter(
        DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType, int? amountMin, int? amountMax)
    {
        var status = string.IsNullOrWhiteSpace(paymentStatus) ? "paid" : paymentStatus.Trim();
        if (status is not ("paid" or "refunded" or "all"))
        {
            throw new AdminValidationException("付款狀態只能是「已付款」、「已退款」或「含已退款」。");
        }

        if (!string.IsNullOrEmpty(invoiceType) && !InvoiceModes.All.Contains(invoiceType))
        {
            throw new AdminValidationException("憑證類型只能是「電子發票」或「捐贈收據」。");
        }

        if (from is { } f && to is { } t && f > t)
        {
            throw new AdminValidationException("期間的開始日不可晚於結束日。");
        }

        if (amountMin is { } min && amountMax is { } max && min > max)
        {
            throw new AdminValidationException("金額級距的下限不可大於上限。");
        }

        return new ReportFilter(from, to, storeId, noStore == true, projectId, status, string.IsNullOrEmpty(invoiceType) ? null : invoiceType, amountMin, amountMax);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 捐款總覽
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdminReportOverviewDto> OverviewAsync(CharityAdminScope scope, ReportFilter filter, string? granularity, CancellationToken cancellationToken)
    {
        var grain = string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase) ? "month" : "day";
        var rows = await LoadRowsAsync(filter, cancellationToken);

        var trend = rows.Where(r => r.PaidAt != null)
            .GroupBy(r => grain == "month"
                ? TaiwanClock.ToDate(r.PaidAt!.Value).ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : TaiwanClock.ToDate(r.PaidAt!.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new AdminReportTrendPointDto(g.Key, g.Count(), g.Sum(r => (long)r.Amount)))
            .ToList();

        // 轉換率：以「建單時間落在期間內」為母體（不套付款狀態與付款時間篩選），分子是其中曾經成功付款的。
        var cohort = db.Donations.AsNoTracking().AsQueryable();
        if (filter.From is { } from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            cohort = cohort.Where(d => d.CreatedAt >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            cohort = cohort.Where(d => d.CreatedAt < toUtc);
        }

        cohort = ApplyDimensions(cohort, filter);
        var created = await cohort.CountAsync(cancellationToken);
        var converted = await cohort.CountAsync(d => d.Status == DonationStatus.Paid || d.Status == DonationStatus.Refunded, cancellationToken);

        var total = rows.Sum(r => (long)r.Amount);
        return new AdminReportOverviewDto
        {
            DonationCount = rows.Count,
            TotalAmount = total,
            AverageAmount = rows.Count == 0 ? 0 : (int)Math.Round(total / (decimal)rows.Count, MidpointRounding.AwayFromZero),
            CreatedCount = created,
            ConvertedCount = converted,
            ConversionRate = created == 0 ? 0m : Math.Round(converted / (decimal)created, 4),
            Granularity = grain,
            Trend = trend,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 依店家／依項目
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<AdminReportStoreRowDto>> ByStoreAsync(CharityAdminScope scope, ReportFilter filter, CancellationToken cancellationToken)
    {
        var rows = await LoadRowsAsync(filter, cancellationToken);
        var total = rows.Sum(r => (long)r.Amount);
        var storeIds = rows.Where(r => r.StoreId != null).Select(r => r.StoreId!.Value).Distinct().ToList();
        var names = await db.DonationStoresI18ns.AsNoTracking()
            .Where(i => storeIds.Contains(i.DonationStoreId) && i.Locale == RequestLocale.DefaultDbLocale)
            .ToDictionaryAsync(i => i.DonationStoreId, i => i.Name, cancellationToken);
        var pcts = await db.DonationStores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.StoreSharePct, cancellationToken);

        return rows.GroupBy(r => r.StoreId)
            .Select(g =>
            {
                var paid = g.Where(r => r.Status == DonationStatus.Paid).ToList();
                var payable = paid.Sum(r => (long)r.StoreAmount);
                var settled = paid.Where(r => r.StoreSettled).Sum(r => (long)r.StoreAmount);
                var amount = g.Sum(r => (long)r.Amount);
                return new AdminReportStoreRowDto
                {
                    StoreId = g.Key,
                    StoreName = g.Key is { } id ? names.GetValueOrDefault(id) : null,
                    DonationCount = g.Count(),
                    TotalAmount = amount,
                    AmountRatio = total == 0 ? 0m : Math.Round(amount / (decimal)total, 4),
                    CurrentSharePct = g.Key is { } sid ? pcts.GetValueOrDefault(sid) : 0m,
                    PayableAmount = payable,
                    SettledAmount = settled,
                    UnsettledAmount = payable - settled,
                };
            })
            .OrderByDescending(r => r.TotalAmount).ThenBy(r => r.StoreName, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<AdminReportProjectRowDto>> ByProjectAsync(CharityAdminScope scope, ReportFilter filter, CancellationToken cancellationToken)
    {
        var rows = await LoadRowsAsync(filter, cancellationToken);
        var total = rows.Sum(r => (long)r.Amount);
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var names = await db.DonationProjectsI18ns.AsNoTracking()
            .Where(i => projectIds.Contains(i.DonationProjectId) && i.Locale == RequestLocale.DefaultDbLocale)
            .ToDictionaryAsync(i => i.DonationProjectId, i => i.Name, cancellationToken);
        var pcts = await db.DonationProjects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProjectSharePct, cancellationToken);

        // 規劃書 §7.2 的「目標達成率」不輸出：§1.2／§6.2 明文不設目標金額，沒有可除的分母（規劃書自相矛盾的殘留文字，見 docs/16a）。
        return rows.GroupBy(r => r.ProjectId)
            .Select(g =>
            {
                var paid = g.Where(r => r.Status == DonationStatus.Paid).ToList();
                var payable = paid.Sum(r => (long)r.ProjectAmount);
                var settled = paid.Where(r => r.ProjectSettled).Sum(r => (long)r.ProjectAmount);
                var amount = g.Sum(r => (long)r.Amount);
                return new AdminReportProjectRowDto
                {
                    ProjectId = g.Key,
                    ProjectName = names.GetValueOrDefault(g.Key),
                    DonationCount = g.Count(),
                    TotalAmount = amount,
                    AmountRatio = total == 0 ? 0m : Math.Round(amount / (decimal)total, 4),
                    AverageAmount = (int)Math.Round(amount / (decimal)g.Count(), MidpointRounding.AwayFromZero),
                    CurrentSharePct = pcts.GetValueOrDefault(g.Key),
                    PayableAmount = payable,
                    SettledAmount = settled,
                    UnsettledAmount = payable - settled,
                };
            })
            .OrderByDescending(r => r.TotalAmount).ThenBy(r => r.ProjectName, StringComparer.Ordinal)
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 發票開立狀況
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdminReportInvoiceStatusDto> InvoiceStatusAsync(CharityAdminScope scope, ReportFilter filter, CancellationToken cancellationToken)
    {
        var rows = await LoadRowsAsync(filter, cancellationToken);
        int Count(Func<ReportRow, bool> predicate) => rows.Count(predicate);
        return new AdminReportInvoiceStatusDto
        {
            NoInvoice = Count(r => r.InvoiceIssue is null),
            Voided = Count(r => r.InvoiceVoid == "voided"),
            Allowance = Count(r => r.InvoiceVoid == "allowance"),
            Issued = Count(r => r.InvoiceIssue == "issued" && r.InvoiceVoid is null or "none"),
            Pending = Count(r => r.InvoiceIssue == "pending" && r.InvoiceVoid is null or "none"),
            Failed = Count(r => r.InvoiceIssue == "failed" && r.InvoiceVoid is null or "none"),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 逐筆明細
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<AdminReportDetailRowDto>> DetailsAsync(
        CharityAdminScope scope, ReportFilter filter, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);
        var query = BaseQuery(filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectDetails(query.OrderByDescending(d => d.PaidAt).ThenByDescending(d => d.Seq).Skip((p - 1) * size).Take(size), cancellationToken);
        return new PagedResult<AdminReportDetailRowDto> { Items = items, Page = p, PageSize = size, TotalCount = total };
    }

    public async Task<IReadOnlyList<AdminReportDetailRowDto>> AllDetailsAsync(CharityAdminScope scope, ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = BaseQuery(filter);
        if (await query.CountAsync(cancellationToken) > MaxRows)
        {
            throw new CharityUnprocessableException($"符合條件的捐款超過 {MaxRows:N0} 筆，請縮小期間或篩選條件後再匯出。");
        }

        return await ProjectDetails(query.OrderBy(d => d.PaidAt).ThenBy(d => d.Seq), cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CSV（UTF-8 BOM）
    // ═══════════════════════════════════════════════════════════════════════

    public static byte[] OverviewCsv(AdminReportOverviewDto o)
    {
        var rows = new List<IEnumerable<string?>>
        {
            new string?[] { "項目", "數值" },
            new string?[] { "捐款筆數", o.DonationCount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "捐款總金額", o.TotalAmount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "平均單筆金額", o.AverageAmount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "建單數", o.CreatedCount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "成功付款數", o.ConvertedCount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "轉換率", o.ConversionRate.ToString("0.####", CultureInfo.InvariantCulture) },
            Array.Empty<string?>(),
            new string?[] { o.Granularity == "month" ? "月份" : "日期", "筆數", "金額" },
        };
        rows.AddRange(o.Trend.Select(t => (IEnumerable<string?>)new string?[] { t.Period, t.Count.ToString(CultureInfo.InvariantCulture), t.Amount.ToString(CultureInfo.InvariantCulture) }));
        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows));
    }

    public static byte[] ByStoreCsv(IEnumerable<AdminReportStoreRowDto> data)
    {
        var rows = new List<IEnumerable<string?>> { new string?[] { "店家", "筆數", "捐款金額", "佔比", "目前分潤率（%）", "應付回饋金", "已結算", "未結算" } };
        rows.AddRange(data.Select(r => (IEnumerable<string?>)new string?[]
        {
            CsvUtils.SafeCell(r.StoreName ?? "（無店家歸屬）"), r.DonationCount.ToString(CultureInfo.InvariantCulture), r.TotalAmount.ToString(CultureInfo.InvariantCulture),
            r.AmountRatio.ToString("0.####", CultureInfo.InvariantCulture), r.CurrentSharePct.ToString("0.##", CultureInfo.InvariantCulture),
            r.PayableAmount.ToString(CultureInfo.InvariantCulture), r.SettledAmount.ToString(CultureInfo.InvariantCulture), r.UnsettledAmount.ToString(CultureInfo.InvariantCulture),
        }));
        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows));
    }

    public static byte[] ByProjectCsv(IEnumerable<AdminReportProjectRowDto> data)
    {
        var rows = new List<IEnumerable<string?>> { new string?[] { "項目", "筆數", "捐款金額", "佔比", "平均單筆", "目前分潤率（%）", "應撥付金額", "已結算", "未結算" } };
        rows.AddRange(data.Select(r => (IEnumerable<string?>)new string?[]
        {
            CsvUtils.SafeCell(r.ProjectName), r.DonationCount.ToString(CultureInfo.InvariantCulture), r.TotalAmount.ToString(CultureInfo.InvariantCulture),
            r.AmountRatio.ToString("0.####", CultureInfo.InvariantCulture), r.AverageAmount.ToString(CultureInfo.InvariantCulture),
            r.CurrentSharePct.ToString("0.##", CultureInfo.InvariantCulture), r.PayableAmount.ToString(CultureInfo.InvariantCulture),
            r.SettledAmount.ToString(CultureInfo.InvariantCulture), r.UnsettledAmount.ToString(CultureInfo.InvariantCulture),
        }));
        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows));
    }

    public static byte[] InvoiceStatusCsv(AdminReportInvoiceStatusDto s)
        => CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(new List<IEnumerable<string?>>
        {
            new string?[] { "狀態", "筆數" },
            new string?[] { "已開立", s.Issued.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "待開立", s.Pending.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "開立失敗", s.Failed.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "已作廢", s.Voided.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "已折讓", s.Allowance.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "無憑證資料", s.NoInvoice.ToString(CultureInfo.InvariantCulture) },
        }));

    public static byte[] DetailsCsv(IEnumerable<AdminReportDetailRowDto> data)
    {
        var rows = new List<IEnumerable<string?>>
        {
            new string?[] { "單號", "建立時間", "付款時間", "狀態", "金額", "捐款項目", "來源店家", "憑證狀態", "作廢／折讓", "具名／匿名", "店家回饋金", "項目撥付金", "協會留存" },
        };
        rows.AddRange(data.Select(r => (IEnumerable<string?>)new string?[]
        {
            CsvUtils.SafeCell(r.OrderNo), Taiwan(r.CreatedAt), r.PaidAt is { } p ? Taiwan(p) : null, CharityLabels.DonationStatus(r.Status), r.Amount.ToString(CultureInfo.InvariantCulture),
            CsvUtils.SafeCell(r.ProjectName), CsvUtils.SafeCell(r.StoreName), CharityLabels.InvoiceIssueStatus(r.InvoiceStatus), CharityLabels.InvoiceVoidStatus(r.InvoiceVoidStatus), r.IsAnonymous ? "匿名" : "具名",
            r.StoreAmount.ToString(CultureInfo.InvariantCulture), r.ProjectAmount.ToString(CultureInfo.InvariantCulture), r.AssociationAmount.ToString(CultureInfo.InvariantCulture),
        }));
        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows));
    }

    // ───────────────────────────────────────────────────────────────────────

    private static string Taiwan(DateTime utc) => utc.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed record ReportRow(
        Guid? StoreId, Guid ProjectId, int Amount, int StoreAmount, int ProjectAmount, DateTime? PaidAt, string Status,
        bool StoreSettled, bool ProjectSettled, string? InvoiceIssue, string? InvoiceVoid);

    private static IQueryable<Donation> ApplyDimensions(IQueryable<Donation> query, ReportFilter f)
    {
        if (f.NoStore)
        {
            query = query.Where(d => d.DonationStoreId == null);
        }
        else if (f.StoreId is { } storeId)
        {
            query = query.Where(d => d.DonationStoreId == storeId);
        }

        if (f.ProjectId is { } projectId)
        {
            query = query.Where(d => d.DonationProjectId == projectId);
        }

        if (f.InvoiceType is { } type)
        {
            query = query.Where(d => d.InvoiceMode == type);
        }

        if (f.AmountMin is { } min)
        {
            query = query.Where(d => d.Amount >= min);
        }

        if (f.AmountMax is { } max)
        {
            query = query.Where(d => d.Amount <= max);
        }

        return query;
    }

    /// <summary>貨幣類報表的母體：付款時間落在期間內、狀態符合「付款狀態」篩選，再套其餘維度。</summary>
    private IQueryable<Donation> BaseQuery(ReportFilter f)
    {
        var query = db.Donations.AsNoTracking().AsQueryable();
        query = f.PaymentStatus switch
        {
            "refunded" => query.Where(d => d.Status == DonationStatus.Refunded),
            "all" => query.Where(d => d.Status == DonationStatus.Paid || d.Status == DonationStatus.Refunded),
            _ => query.Where(d => d.Status == DonationStatus.Paid),
        };

        if (f.From is { } from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            query = query.Where(d => d.PaidAt >= fromUtc);
        }

        if (f.To is { } to)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(d => d.PaidAt < toUtc);
        }

        return ApplyDimensions(query, f);
    }

    /// <summary>所有彙總報表共用同一個資料來源與投影——不同報表的數字永遠對得上。</summary>
    private async Task<List<ReportRow>> LoadRowsAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = BaseQuery(filter);
        if (await query.CountAsync(cancellationToken) > MaxRows)
        {
            throw new CharityUnprocessableException($"符合條件的捐款超過 {MaxRows:N0} 筆，請縮小期間或篩選條件。");
        }

        return await query.Select(d => new ReportRow(
            d.DonationStoreId, d.DonationProjectId, d.Amount, d.StoreAmount, d.ProjectAmount, d.PaidAt, d.Status,
            d.SettlementLines.Any(l => !l.IsClawback && l.Settlement.PayeeType == "store" && l.Settlement.Status != "pending"),
            d.SettlementLines.Any(l => !l.IsClawback && l.Settlement.PayeeType == "project" && l.Settlement.Status != "pending"),
            d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => i.IssueStatus).FirstOrDefault(),
            d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => i.VoidStatus).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<AdminReportDetailRowDto>> ProjectDetails(IQueryable<Donation> query, CancellationToken cancellationToken)
    {
        var rows = await query.Select(d => new
        {
            d.Id, d.OrderNo, d.CreatedAt, d.PaidAt, d.Status, d.Amount, d.IsAnonymous, d.StoreAmount, d.ProjectAmount, d.AssociationAmount,
            ProjectName = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            StoreName = d.DonationStore == null ? null : d.DonationStore.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            Invoice = d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => new { i.IssueStatus, i.VoidStatus }).FirstOrDefault(),
        }).ToListAsync(cancellationToken);

        return rows.Select(r => new AdminReportDetailRowDto
        {
            DonationId = r.Id, OrderNo = r.OrderNo, CreatedAt = r.CreatedAt, PaidAt = r.PaidAt, Status = r.Status, Amount = r.Amount,
            ProjectName = r.ProjectName, StoreName = r.StoreName, InvoiceStatus = r.Invoice?.IssueStatus, InvoiceVoidStatus = r.Invoice?.VoidStatus,
            IsAnonymous = r.IsAnonymous, StoreAmount = r.StoreAmount, ProjectAmount = r.ProjectAmount, AssociationAmount = r.AssociationAmount,
        }).ToList();
    }
}
