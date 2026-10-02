using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Admin;

// N5 發票與收據管理（規劃書 §6.5）：列表與篩選、重新開立（失敗補開，沿用 N3 的端點）、手動填入外部號碼、作廢、折讓、供會計申報的明細 CSV。
// 每個操作記錄經辦人與原因（稽核；作廢與折讓另存 void_reason／voided_by）。

public sealed record InvoiceFilter(
    DateOnly? From, DateOnly? To, string? IssueStatus, string? VoidStatus, string? InvoiceType, Guid? ProjectId, string? Keyword);

public sealed record AdminInvoiceListItemDto
{
    public required Guid InvoiceId { get; init; }
    public required Guid DonationId { get; init; }
    public required string OrderNo { get; init; }

    /// <summary><c>b2c_invoice</c>（電子發票）／<c>donation_receipt</c>（捐贈收據）。</summary>
    public required string InvoiceType { get; init; }

    public required string? InvoiceNo { get; init; }
    public required DateTime? IssuedAt { get; init; }

    /// <summary><c>pending</c>（待開立）／<c>issued</c>（已開立）／<c>failed</c>（開立失敗）。</summary>
    public required string IssueStatus { get; init; }

    /// <summary><c>none</c>／<c>voided</c>（已作廢）／<c>allowance</c>（已折讓）。</summary>
    public required string VoidStatus { get; init; }

    public required string? VoidReason { get; init; }
    public required int Amount { get; init; }
    public required DateTime? PaidAt { get; init; }
    public required string DonationStatus { get; init; }
    public required string? ProjectName { get; init; }

    /// <summary>公司統編與抬頭（統編發票才有；商業登記公開資訊，不是個資）。捐款人姓名、載具、身分證字號、地址<b>不在列表回應裡</b>。</summary>
    public required string? TaxId { get; init; }

    public required string? InvoiceTitle { get; init; }
    public required bool IsAnnualSummary { get; init; }
}

public sealed record ManualInvoiceNumberRequest
{
    /// <summary>憑證號碼（大寫英數字與連字號，4–32 字）。</summary>
    public string? InvoiceNo { get; init; }

    /// <summary>開立日期（台灣日期）；省略＝今天。不可晚於今天。</summary>
    public DateOnly? IssuedOn { get; init; }

    /// <summary>原因（例如「加值中心後台補開」），必填，進稽核。</summary>
    public string? Reason { get; init; }
}

public sealed record InvoiceReasonRequest
{
    public string? Reason { get; init; }
}

/// <summary>N5 憑證管理。🔴 列表與 CSV 都<b>不含捐款人個資</b>（姓名、Email、載具、身分證字號、地址）；要看個資走 N3（遮罩＋reveal 稽核）。</summary>
public sealed partial class CharityInvoicesAdminService(CharityDbContext db, CharityInvoiceService invoices, CharityAuditLogger audit)
{
    private const int MaxExportRows = 50_000;

    [GeneratedRegex(@"^[A-Z0-9\-]{4,32}$")]
    private static partial Regex InvoiceNoFormat();

    public async Task<PagedResult<AdminInvoiceListItemDto>> ListAsync(
        CharityAdminScope scope, InvoiceFilter filter, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        var query = ApplyFilter(db.DonationInvoices.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderByDescending(i => i.Donation.PaidAt).ThenByDescending(i => i.Seq).Skip((p - 1) * size).Take(size), cancellationToken);
        return new PagedResult<AdminInvoiceListItemDto> { Items = items, Page = p, PageSize = size, TotalCount = total };
    }

    /// <summary>手動填入外部號碼（失敗補開）。號碼格式與唯一性在這裡與服務層雙重把關。</summary>
    public async Task<AdminInvoiceListItemDto> ManualNumberAsync(
        CharityAdminScope scope, Guid invoiceId, ManualInvoiceNumberRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var no = (request.InvoiceNo ?? string.Empty).Trim().ToUpperInvariant();
        if (!InvoiceNoFormat().IsMatch(no))
        {
            throw new AdminValidationException("憑證號碼只能包含大寫英文字母、數字與連字號，長度 4 到 32 個字。");
        }

        var reason = RequireReason(request.Reason);
        var issuedOn = request.IssuedOn ?? TaiwanClock.Today;
        if (issuedOn > TaiwanClock.Today)
        {
            throw new AdminValidationException("開立日期不可晚於今天。");
        }

        // 開立當天的台灣時間中午（避免換算 UTC 後跨日；當期判斷只看年月，日內時間不影響）。
        var issuedAtUtc = TaiwanClock.StartOfDayUtc(issuedOn).AddHours(4);
        // 🔴 稽核先 Stage、由服務層自己的 SaveChanges 一起提交——稽核與被稽核的變更同一次提交（驗證失敗丟例外時整個請求中止，暫存的稽核不會落地）。
        audit.Stage(scope, CharityAuditActions.InvoiceManualNumber, CharityAuditTargets.Invoice, invoiceId, $"手動填入憑證號碼 {no}，原因：{reason}", null, sourceIp);
        var invoice = await invoices.ManualIssueAsync(invoiceId, no, issuedAtUtc, scope.Identity.AdminUserId, cancellationToken);
        return await GetOneAsync(invoice.Id, cancellationToken);
    }

    /// <summary>作廢（限開立當期；跨期請折讓）。</summary>
    public async Task<AdminInvoiceListItemDto> VoidAsync(
        CharityAdminScope scope, Guid invoiceId, string? reasonText, string sourceIp, CancellationToken cancellationToken)
    {
        var reason = RequireReason(reasonText);
        audit.Stage(scope, CharityAuditActions.InvoiceVoid, CharityAuditTargets.Invoice, invoiceId, $"作廢憑證，原因：{reason}", null, sourceIp);
        try
        {
            await invoices.VoidManuallyAsync(invoiceId, reason, scope.Identity.AdminUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is InvoiceIssuerUnavailableException or InvoiceIssuerNotConfiguredException)
        {
            throw new CharityServiceUnavailableException("電子發票加值中心暫時無法使用，沒有作廢，請稍後再試。");
        }
        catch (InvoiceRejectedException)
        {
            throw new CharityConflictException("加值中心拒絕作廢", "加值中心沒有接受這次作廢（可能已經申報或已作廢），請到加值中心後台確認。");
        }

        return await GetOneAsync(invoiceId, cancellationToken);
    }

    /// <summary>折讓（已開立、跨期）。</summary>
    public async Task<AdminInvoiceListItemDto> AllowanceAsync(
        CharityAdminScope scope, Guid invoiceId, string? reasonText, string sourceIp, CancellationToken cancellationToken)
    {
        var reason = RequireReason(reasonText);
        audit.Stage(scope, CharityAuditActions.InvoiceAllowance, CharityAuditTargets.Invoice, invoiceId, $"折讓憑證，原因：{reason}", null, sourceIp);
        try
        {
            await invoices.AllowanceManuallyAsync(invoiceId, reason, scope.Identity.AdminUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is InvoiceIssuerUnavailableException or InvoiceIssuerNotConfiguredException)
        {
            throw new CharityServiceUnavailableException("電子發票加值中心暫時無法使用，沒有折讓，請稍後再試。");
        }
        catch (InvoiceRejectedException)
        {
            throw new CharityConflictException("加值中心拒絕折讓", "加值中心沒有接受這次折讓，請到加值中心後台確認。");
        }

        return await GetOneAsync(invoiceId, cancellationToken);
    }

    /// <summary>供會計申報用的明細 CSV（UTF-8 BOM）。不含捐款人個資；寫一筆稽核（誰、何時、幾筆、條件）。</summary>
    public async Task<byte[]> ExportAsync(CharityAdminScope scope, InvoiceFilter filter, string sourceIp, CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.DonationInvoices.AsNoTracking(), filter);
        var count = await query.CountAsync(cancellationToken);
        if (count > MaxExportRows)
        {
            throw new CharityUnprocessableException($"符合條件的憑證有 {count:N0} 筆，超過單次匯出上限 {MaxExportRows:N0} 筆，請縮小期間或篩選條件後再匯出。");
        }

        var items = await Project(query.OrderBy(i => i.Donation.PaidAt).ThenBy(i => i.Seq), cancellationToken);
        var rows = new List<IEnumerable<string?>>
        {
            new string?[] { "捐款單號", "憑證類型", "憑證號碼", "開立時間", "開立狀態", "作廢／折讓", "作廢或折讓原因", "金額", "付款時間", "捐款項目", "統一編號", "發票抬頭", "年度彙總" },
        };
        foreach (var i in items)
        {
            rows.Add(new string?[]
            {
                CsvUtils.SafeCell(i.OrderNo),
                i.InvoiceType == InvoiceModes.DonationReceipt ? "捐贈收據" : "電子發票",
                CsvUtils.SafeCell(i.InvoiceNo),
                i.IssuedAt is { } issued ? issued.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null,
                i.IssueStatus switch { "issued" => "已開立", "failed" => "開立失敗", _ => "待開立" },
                i.VoidStatus switch { "voided" => "已作廢", "allowance" => "已折讓", _ => "正常" },
                CsvUtils.SafeCell(i.VoidReason),
                i.Amount.ToString(CultureInfo.InvariantCulture),
                i.PaidAt is { } paid ? paid.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null,
                CsvUtils.SafeCell(i.ProjectName),
                CsvUtils.SafeCell(i.TaxId),
                CsvUtils.SafeCell(i.InvoiceTitle),
                i.IsAnnualSummary ? "是" : "否",
            });
        }

        audit.Stage(scope, CharityAuditActions.InvoiceExport, CharityAuditTargets.Invoice, null, $"匯出憑證明細 {items.Count} 筆（不含個資）；條件：{Describe(filter)}", null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);
        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows));
    }

    // ───────────────────────────────────────────────────────────────────────

    private static string RequireReason(string? reason)
    {
        var clean = AdminInput.RequireText(reason, "原因", 255);
        if (clean.Length < 2)
        {
            throw new AdminValidationException("原因至少要 2 個字，稽核與會計追蹤會用到。");
        }

        return clean;
    }

    private static string Describe(InvoiceFilter f)
    {
        var parts = new List<string>();
        if (f.From is { } from) parts.Add($"自 {from:yyyy-MM-dd}");
        if (f.To is { } to) parts.Add($"至 {to:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(f.IssueStatus)) parts.Add($"開立 {f.IssueStatus}");
        if (!string.IsNullOrEmpty(f.VoidStatus)) parts.Add($"作廢 {f.VoidStatus}");
        if (!string.IsNullOrEmpty(f.InvoiceType)) parts.Add($"類型 {f.InvoiceType}");
        if (f.ProjectId is { } pid) parts.Add($"項目 {pid}");
        return parts.Count == 0 ? "（無，全部）" : string.Join("、", parts);
    }

    private static IQueryable<Data.Entities.DonationInvoice> ApplyFilter(IQueryable<Data.Entities.DonationInvoice> query, InvoiceFilter f)
    {
        if (f.From is { } from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            query = query.Where(i => i.Donation.PaidAt >= fromUtc);
        }

        if (f.To is { } to)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(i => i.Donation.PaidAt < toUtc);
        }

        if (!string.IsNullOrEmpty(f.IssueStatus))
        {
            query = query.Where(i => i.IssueStatus == f.IssueStatus);
        }

        if (!string.IsNullOrEmpty(f.VoidStatus))
        {
            query = query.Where(i => i.VoidStatus == f.VoidStatus);
        }

        if (!string.IsNullOrEmpty(f.InvoiceType))
        {
            query = query.Where(i => i.InvoiceType == f.InvoiceType);
        }

        if (f.ProjectId is { } projectId)
        {
            query = query.Where(i => i.Donation.DonationProjectId == projectId);
        }

        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim();
            query = query.Where(i => i.Donation.OrderNo.StartsWith(k) || (i.InvoiceNo != null && i.InvoiceNo.StartsWith(k)));
        }

        return query;
    }

    private async Task<List<AdminInvoiceListItemDto>> Project(IQueryable<Data.Entities.DonationInvoice> query, CancellationToken cancellationToken)
    {
        var rows = await query.Select(i => new
        {
            i.Id, i.DonationId, i.Donation.OrderNo, i.InvoiceType, i.InvoiceNo, i.IssuedAt, i.IssueStatus, i.VoidStatus, i.VoidReason,
            i.Donation.Amount, i.Donation.PaidAt, DonationStatus = i.Donation.Status,
            ProjectName = i.Donation.DonationProject.DonationProjectsI18ns.Where(x => x.Locale == RequestLocale.DefaultDbLocale).Select(x => x.Name).FirstOrDefault(),
            i.TaxId, i.InvoiceTitle, i.IsAnnualSummary,
        }).ToListAsync(cancellationToken);

        return rows.Select(r => new AdminInvoiceListItemDto
        {
            InvoiceId = r.Id, DonationId = r.DonationId, OrderNo = r.OrderNo, InvoiceType = r.InvoiceType, InvoiceNo = r.InvoiceNo,
            IssuedAt = r.IssuedAt, IssueStatus = r.IssueStatus, VoidStatus = r.VoidStatus, VoidReason = r.VoidReason, Amount = r.Amount,
            PaidAt = r.PaidAt, DonationStatus = r.DonationStatus, ProjectName = r.ProjectName, TaxId = r.TaxId, InvoiceTitle = r.InvoiceTitle,
            IsAnnualSummary = r.IsAnnualSummary,
        }).ToList();
    }

    private async Task<AdminInvoiceListItemDto> GetOneAsync(Guid invoiceId, CancellationToken cancellationToken)
        => (await Project(db.DonationInvoices.AsNoTracking().Where(i => i.Id == invoiceId), cancellationToken)).Single();
}
