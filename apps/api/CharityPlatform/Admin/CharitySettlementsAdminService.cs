using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Admin;

public static class SettlementStatus
{
    public const string Pending = "pending";
    public const string Settled = "settled";
    public const string Paid = "paid";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Pending, Settled, Paid };
}

public static class SettlementPayeeTypes
{
    public const string Store = "store";
    public const string Project = "project";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Store, Project };
}

/// <summary>
/// N4 回饋金結算（規劃書 §6.4、§8）。每個方法第一個參數都是 <see cref="CharityAdminScope"/>（型別層強制授權）。
///
/// <b>規則（全部來自規劃書 §8，這裡只是落地）</b>
/// <list type="bullet">
/// <item>只計 <c>paid</c> 的捐款，使用捐款成立時的<b>分潤快照金額</b>（<c>store_amount</c>／<c>project_amount</c>）——改設定不追溯（§8.4）。</item>
/// <item>店家與項目<b>分開結算</b>：一份結算單只有一個對象；同一筆捐款對「店家」與「項目」各進一份，彼此獨立（§8.1）。</item>
/// <item>退款沖回（§8.5）：①尚未結算——直接排除（草稿重算／確認結算時剔除已退款的捐款）；②已結算未付款——從該期對帳單扣除、重出對帳單
/// （<see cref="RecalculateAsync"/>）；③已結算且已付款——<b>不追討</b>，以<b>負項</b>計入該對象的下一份結算單，明列沖回原因與原捐款單號。</item>
/// <item>「已付款」登記與「執行結算」是不同權限碼（§10：與執行匯款者分離，docs/16 §4.2 以權限碼分離不落資料表）。系統不經手任何出款（§8.6）。</item>
/// </list>
///
/// 🔴 <b>並發</b>：會改動「哪些捐款屬於哪份結算單」的操作（產生、重算、確認、刪除草稿）共用同一個交易層級 <c>sp_getapplock</c>
/// （<see cref="CharitySqlLocks"/>），所以「一筆捐款對同一類對象只進一份結算單」不會被兩個同時按下的請求破壞；
/// 資料層另有唯一索引 <c>(settlement_id, donation_id, is_clawback)</c> 當最後防線。每次狀態異動寫稽核（經辦人＋時間），
/// 與被稽核的變更在同一次 <c>SaveChanges</c> 提交。
/// </summary>
public sealed class CharitySettlementsAdminService(CharityDbContext db, CharityAuditLogger audit)
{
    private const string LockResource = "charity-settlement";
    private const int LockWaitMilliseconds = 15_000;
    private const int MaxPeriodDays = 400;

    // ═══════════════════════════════════════════════════════════════════════
    // 查詢
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<AdminSettlementListItemDto>> ListAsync(
        CharityAdminScope scope, string? status, string? payeeType, Guid? payeeId, DateOnly? from, DateOnly? to,
        int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        var query = db.Settlements.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(status) && SettlementStatus.All.Contains(status))
        {
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrEmpty(payeeType) && SettlementPayeeTypes.All.Contains(payeeType))
        {
            query = query.Where(s => s.PayeeType == payeeType);
        }

        if (payeeId is { } pid)
        {
            query = query.Where(s => s.PayeeId == pid);
        }

        if (from is { } f)
        {
            query = query.Where(s => s.PeriodEnd >= f);
        }

        if (to is { } t)
        {
            query = query.Where(s => s.PeriodStart <= t);
        }

        var total = await query.CountAsync(cancellationToken);
        var ids = await query.OrderByDescending(s => s.PeriodEnd).ThenByDescending(s => s.Seq)
            .Skip((p - 1) * size).Take(size).Select(s => s.Id).ToListAsync(cancellationToken);

        return new PagedResult<AdminSettlementListItemDto>
        {
            Items = await ToListItemsAsync(ids, cancellationToken),
            Page = p,
            PageSize = size,
            TotalCount = total,
        };
    }

    public async Task<AdminSettlementDetailDto?> GetAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
        => await BuildDetailAsync(id, cancellationToken);

    // ═══════════════════════════════════════════════════════════════════════
    // 產生結算單（草稿）
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdminSettlementRunResultDto> RunAsync(
        CharityAdminScope scope, RunSettlementRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        if (request.PeriodStart > request.PeriodEnd)
        {
            throw new AdminValidationException("結算期間的開始日不可晚於結束日。");
        }

        if (request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber >= MaxPeriodDays)
        {
            throw new AdminValidationException($"結算期間最長 {MaxPeriodDays} 天，請分段結算。");
        }

        if (request.PeriodEnd >= TaiwanClock.Today)
        {
            throw new CharityUnprocessableException("結算期間必須在今天以前結束：期間還沒結束就結算，當天稍後才付款的捐款會漏掉。");
        }

        string[] types;
        if (string.IsNullOrEmpty(request.PayeeType))
        {
            types = [SettlementPayeeTypes.Store, SettlementPayeeTypes.Project];
        }
        else if (SettlementPayeeTypes.All.Contains(request.PayeeType))
        {
            types = [request.PayeeType];
        }
        else
        {
            throw new AdminValidationException("結算對象只能是「店家」或「項目」。");
        }

        var startUtc = TaiwanClock.StartOfDayUtc(request.PeriodStart);
        var endUtc = TaiwanClock.StartOfDayUtc(request.PeriodEnd.AddDays(1));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(transaction, cancellationToken);

        var createdIds = new List<Guid>();
        var skipped = new List<AdminSettlementSkipDto>();
        var now = DateTime.UtcNow;

        foreach (var type in types)
        {
            var eligible = await EligibleRowsAsync(type, startUtc, endUtc, request.PayeeId, cancellationToken);
            var clawbacks = await ClawbackRowsAsync(type, request.PayeeId, cancellationToken);
            var payeeIds = eligible.Select(e => e.PayeeId).Concat(clawbacks.Select(c => c.PayeeId)).Distinct().ToList();
            if (payeeIds.Count == 0)
            {
                continue;
            }

            // 同一對象的期間不得與既有結算單重疊（草稿也算）：重疊的期間會讓人分不清哪一份才是這段時間的對帳單。
            var overlapping = await db.Settlements.AsNoTracking()
                .Where(s => s.PayeeType == type && payeeIds.Contains(s.PayeeId) && s.PeriodStart <= request.PeriodEnd && s.PeriodEnd >= request.PeriodStart)
                .Select(s => s.PayeeId).Distinct().ToListAsync(cancellationToken);
            var names = await PayeeNamesAsync(type, payeeIds, cancellationToken);

            foreach (var payeeId in payeeIds.OrderBy(i => names.GetValueOrDefault(i), StringComparer.Ordinal))
            {
                if (overlapping.Contains(payeeId))
                {
                    skipped.Add(new AdminSettlementSkipDto(type, payeeId, names.GetValueOrDefault(payeeId), "這個對象在同一段期間已經有結算單，請直接處理既有的結算單。"));
                    continue;
                }

                var settlement = new Settlement
                {
                    Id = Guid.NewGuid(),
                    PeriodStart = request.PeriodStart,
                    PeriodEnd = request.PeriodEnd,
                    PayeeType = type,
                    PayeeId = payeeId,
                    Status = SettlementStatus.Pending,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = scope.Identity.AdminUserId,
                    UpdatedBy = scope.Identity.AdminUserId,
                };
                db.Settlements.Add(settlement);

                var positives = eligible.Where(e => e.PayeeId == payeeId).ToList();
                foreach (var row in positives)
                {
                    db.SettlementLines.Add(NewLine(settlement.Id, row.DonationId, row.Share, isClawback: false, reason: null, scope, now));
                }

                var negatives = clawbacks.Where(c => c.PayeeId == payeeId).ToList();
                foreach (var row in negatives)
                {
                    db.SettlementLines.Add(NewLine(settlement.Id, row.DonationId, -row.Share, isClawback: true, ClawbackReasonText(row.OrderNo, row.RefundReason), scope, now));
                }

                settlement.DonationCount = positives.Count;
                settlement.DonationTotal = positives.Sum(r => r.Amount);
                settlement.PayableAmount = positives.Sum(r => r.Share) - negatives.Sum(r => r.Share);
                createdIds.Add(settlement.Id);
            }
        }

        if (createdIds.Count > 0)
        {
            audit.Stage(scope, CharityAuditActions.SettlementRun, CharityAuditTargets.Settlement, null,
                $"產生 {createdIds.Count} 份結算單（期間 {request.PeriodStart:yyyy-MM-dd} 至 {request.PeriodEnd:yyyy-MM-dd}，{string.Join("、", types.Select(TypeLabel))}）", null, sourceIp);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminSettlementRunResultDto { Created = await ToListItemsAsync(createdIds, cancellationToken), Skipped = skipped };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 重算、確認結算、登記付款、刪除草稿
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 重算：把已退款的捐款從對帳單剔除（規劃書 §8.5「已結算但未付款：從該期對帳單中扣除，重出對帳單」）。
    /// <b>草稿</b>另外會納入期間內新符合條件的捐款與新的沖回負項；<b>已結算</b>的對帳單只扣除、不新增（已經送出的對帳單不能多出新項目）。已付款的不可重算。
    /// </summary>
    public async Task<AdminSettlementRecalculationDto> RecalculateAsync(
        CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(transaction, cancellationToken);

        var settlement = await LoadAsync(id, cancellationToken);
        if (settlement.Status == SettlementStatus.Paid)
        {
            throw new CharityConflictException("已付款", "這份結算單已經登記付款，不可重算。");
        }

        var outcome = await RebuildAsync(settlement, allowAdd: settlement.Status == SettlementStatus.Pending, scope, cancellationToken);
        audit.Stage(scope, CharityAuditActions.SettlementRecalculate, CharityAuditTargets.Settlement, id,
            $"重算結算單：扣除已退款 {outcome.Removed.Count} 筆{(outcome.Removed.Count > 0 ? "（" + string.Join("、", outcome.Removed) + "）" : string.Empty)}，新增 {outcome.Added} 筆、沖回 {outcome.AddedClawbacks} 筆，應付 NT$ {settlement.PayableAmount.ToString("N0", CultureInfo.InvariantCulture)}",
            null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminSettlementRecalculationDto
        {
            Detail = (await BuildDetailAsync(id, cancellationToken))!,
            RemovedOrderNos = outcome.Removed,
            AddedCount = outcome.Added,
            AddedClawbackCount = outcome.AddedClawbacks,
        };
    }

    /// <summary>確認結算：待結算 → 已結算，鎖定金額。確認當下會先重算一次，確保已退款的捐款不會被帶進對帳單（§8.5 第一種情況）。</summary>
    public async Task<AdminSettlementDetailDto> SettleAsync(CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(transaction, cancellationToken);

        var settlement = await LoadAsync(id, cancellationToken);
        if (settlement.Status != SettlementStatus.Pending)
        {
            throw new CharityConflictException("狀態已變更", "這份結算單已經不是「待結算」，請重新整理頁面確認。");
        }

        var outcome = await RebuildAsync(settlement, allowAdd: true, scope, cancellationToken);
        var lineCount = await db.SettlementLines.CountAsync(l => l.SettlementId == id, cancellationToken);
        if (lineCount == 0)
        {
            throw new CharityConflictException("沒有可結算的項目", "這份結算單目前沒有任何明細（可能都已退款），請刪除這份草稿。");
        }

        settlement.Status = SettlementStatus.Settled;
        settlement.UpdatedAt = DateTime.UtcNow;
        settlement.UpdatedBy = scope.Identity.AdminUserId;
        audit.Stage(scope, CharityAuditActions.SettlementSettle, CharityAuditTargets.Settlement, id,
            $"確認結算：{settlement.DonationCount} 筆捐款、沖回 {lineCount - settlement.DonationCount} 筆，應付 NT$ {settlement.PayableAmount.ToString("N0", CultureInfo.InvariantCulture)}"
            + (outcome.Removed.Count > 0 ? $"；確認前扣除已退款 {outcome.Removed.Count} 筆" : string.Empty),
            null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildDetailAsync(id, cancellationToken))!;
    }

    /// <summary>登記付款：已結算 → 已付款。實際匯款在系統外執行，這裡只登記匯款日期、方式與備註（規劃書 §6.4、§8.6）。</summary>
    public async Task<AdminSettlementDetailDto> MarkPaidAsync(
        CharityAdminScope scope, Guid id, MarkSettlementPaidRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var method = AdminInput.RequireText(request.RemitMethod, "匯款方式", 32);
        var note = AdminInput.OptionalText(request.RemitNote, "匯款備註", 255);
        if (request.RemittedOn == default)
        {
            throw new AdminValidationException("請填寫匯款日期。");
        }

        if (request.RemittedOn > TaiwanClock.Today)
        {
            throw new AdminValidationException("匯款日期不可晚於今天。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(transaction, cancellationToken);

        var settlement = await LoadAsync(id, cancellationToken);
        if (settlement.Status != SettlementStatus.Settled)
        {
            throw new CharityConflictException("無法登記付款", settlement.Status == SettlementStatus.Paid
                ? "這份結算單已經登記過付款。"
                : "這份結算單還沒有確認結算，請先確認結算。");
        }

        settlement.Status = SettlementStatus.Paid;
        settlement.RemittedOn = request.RemittedOn;
        settlement.RemitMethod = method;
        settlement.RemitNote = note;
        settlement.UpdatedAt = DateTime.UtcNow;
        settlement.UpdatedBy = scope.Identity.AdminUserId;
        audit.Stage(scope, CharityAuditActions.SettlementMarkPaid, CharityAuditTargets.Settlement, id,
            $"登記已付款：匯款日 {request.RemittedOn:yyyy-MM-dd}，方式 {method}，應付 NT$ {settlement.PayableAmount.ToString("N0", CultureInfo.InvariantCulture)}", note, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await BuildDetailAsync(id, cancellationToken))!;
    }

    /// <summary>刪除草稿（只限待結算）。已結算與已付款的結算單是帳務紀錄，不可刪除。</summary>
    public async Task DeleteDraftAsync(CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLockAsync(transaction, cancellationToken);

        var settlement = await LoadAsync(id, cancellationToken);
        if (settlement.Status != SettlementStatus.Pending)
        {
            throw new CharityConflictException("無法刪除", "只有「待結算」的草稿可以刪除；已結算或已付款的結算單是帳務紀錄。");
        }

        var lines = await db.SettlementLines.Where(l => l.SettlementId == id).ToListAsync(cancellationToken);
        db.SettlementLines.RemoveRange(lines);
        db.Settlements.Remove(settlement);
        audit.Stage(scope, CharityAuditActions.SettlementDeleteDraft, CharityAuditTargets.Settlement, id,
            $"刪除結算草稿（{TypeLabel(settlement.PayeeType)}，期間 {settlement.PeriodStart:yyyy-MM-dd} 至 {settlement.PeriodEnd:yyyy-MM-dd}，{lines.Count} 筆明細）", null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 對帳單 CSV
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>對帳單 CSV（UTF-8 BOM）。只有單號、時間、金額與分潤，不含任何捐款人資料（對帳單會交給店家或撥付對象）。</summary>
    public async Task<(byte[] Bytes, string FileName)?> ExportCsvAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var detail = await BuildDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var s = detail.Settlement;
        var rows = new List<IEnumerable<string?>>
        {
            new string?[] { "對帳單" },
            new string?[] { "對象類型", TypeLabel(s.PayeeType) },
            new string?[] { "對象", CsvUtils.SafeCell(s.PayeeName) },
            new string?[] { "結算期間", $"{s.PeriodStart:yyyy-MM-dd} ~ {s.PeriodEnd:yyyy-MM-dd}" },
            new string?[] { "狀態", StatusLabel(s.Status) },
            new string?[] { "捐款筆數", s.DonationCount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "捐款總額", s.DonationTotal.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "沖回筆數", s.ClawbackCount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "沖回金額", s.ClawbackAmount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "應付金額", s.PayableAmount.ToString(CultureInfo.InvariantCulture) },
            new string?[] { "匯款日期", s.RemittedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            new string?[] { "匯款方式", CsvUtils.SafeCell(s.RemitMethod) },
            new string?[] { "匯款備註", CsvUtils.SafeCell(s.RemitNote) },
            Array.Empty<string?>(),
            new string?[] { "捐款單號", "付款時間", "捐款金額", "分潤率（%）", "應付金額", "性質", "沖回原因" },
        };
        foreach (var l in detail.Lines)
        {
            rows.Add(new string?[]
            {
                CsvUtils.SafeCell(l.OrderNo),
                l.PaidAt is { } paid ? paid.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null,
                l.DonationAmount.ToString(CultureInfo.InvariantCulture),
                l.SharePct.ToString("0.##", CultureInfo.InvariantCulture),
                l.ShareAmount.ToString(CultureInfo.InvariantCulture),
                l.IsClawback ? "退款沖回" : "一般",
                CsvUtils.SafeCell(l.ClawbackReason),
            });
        }

        var safeName = new string((s.PayeeName ?? "對象").Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != ' ').Take(30).ToArray());
        return (CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(rows)), $"settlement-{s.PeriodStart:yyyyMMdd}-{s.PeriodEnd:yyyyMMdd}-{safeName}.csv");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 內部：資格查詢、重建明細
    // ═══════════════════════════════════════════════════════════════════════

    private sealed record EligibleRow(Guid DonationId, Guid PayeeId, int Amount, int Share);

    private sealed record ClawbackRow(Guid DonationId, Guid PayeeId, int Share, string OrderNo, string? RefundReason);

    private sealed record RebuildOutcome(IReadOnlyList<string> Removed, int Added, int AddedClawbacks);

    /// <summary>
    /// 期間內、已付款、分潤大於 0、且對這一類對象<b>尚未進過任何結算單</b>（含草稿）的捐款。
    /// 排除條件是這個方法的核心：它讓「產生結算單」天然冪等，也讓一筆捐款對店家與項目各只結一次。
    /// </summary>
    private async Task<List<EligibleRow>> EligibleRowsAsync(string type, DateTime startUtc, DateTime endUtc, Guid? payeeId, CancellationToken cancellationToken)
    {
        var query = db.Donations.AsNoTracking()
            .Where(d => d.Status == DonationStatus.Paid && d.PaidAt >= startUtc && d.PaidAt < endUtc)
            .Where(d => !d.SettlementLines.Any(l => !l.IsClawback && l.Settlement.PayeeType == type));

        if (type == SettlementPayeeTypes.Store)
        {
            query = query.Where(d => d.DonationStoreId != null && d.StoreAmount > 0);
            if (payeeId is { } sid)
            {
                query = query.Where(d => d.DonationStoreId == sid);
            }

            return (await query.Select(d => new { d.Id, Payee = d.DonationStoreId!.Value, d.Amount, Share = d.StoreAmount }).ToListAsync(cancellationToken))
                .Select(r => new EligibleRow(r.Id, r.Payee, r.Amount, r.Share)).ToList();
        }

        query = query.Where(d => d.ProjectAmount > 0);
        if (payeeId is { } pid)
        {
            query = query.Where(d => d.DonationProjectId == pid);
        }

        return (await query.Select(d => new { d.Id, Payee = d.DonationProjectId, d.Amount, Share = d.ProjectAmount }).ToListAsync(cancellationToken))
            .Select(r => new EligibleRow(r.Id, r.Payee, r.Amount, r.Share)).ToList();
    }

    /// <summary>
    /// 需要沖回的負項：原本的正項<b>已在「已付款」的結算單裡</b>、捐款後來被退款、而且這個對象還沒有為這筆捐款開過沖回負項。
    /// 負項金額是原本那條正項分潤的反向（不追討、改計入下一期，規劃書 §8.5）。
    /// </summary>
    private async Task<List<ClawbackRow>> ClawbackRowsAsync(string type, Guid? payeeId, CancellationToken cancellationToken)
    {
        var query = db.SettlementLines.AsNoTracking()
            .Where(l => !l.IsClawback && l.Settlement.PayeeType == type && l.Settlement.Status == SettlementStatus.Paid
                        && l.Donation.Status == DonationStatus.Refunded)
            .Where(l => !db.SettlementLines.Any(c => c.IsClawback && c.DonationId == l.DonationId && c.Settlement.PayeeType == type));
        if (payeeId is { } pid)
        {
            query = query.Where(l => l.Settlement.PayeeId == pid);
        }

        return (await query.Select(l => new { l.DonationId, l.Settlement.PayeeId, l.ShareAmount, l.Donation.OrderNo, l.Donation.RefundReason })
                .ToListAsync(cancellationToken))
            .Select(r => new ClawbackRow(r.DonationId, r.PayeeId, r.ShareAmount, r.OrderNo, r.RefundReason)).ToList();
    }

    private async Task<RebuildOutcome> RebuildAsync(Settlement settlement, bool allowAdd, CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // ① 剔除已不是 paid 的正項（已退款；其他狀態理論上不會出現）。
        var stale = await db.SettlementLines
            .Where(l => l.SettlementId == settlement.Id && !l.IsClawback && l.Donation.Status != DonationStatus.Paid)
            .Select(l => new { Line = l, l.Donation.OrderNo })
            .ToListAsync(cancellationToken);
        db.SettlementLines.RemoveRange(stale.Select(s => s.Line));
        await db.SaveChangesAsync(cancellationToken);

        var added = 0;
        var addedClawbacks = 0;
        if (allowAdd)
        {
            // ② 草稿：納入期間內新符合條件的捐款與新的沖回負項。排除查詢會自動略過已經在這份（或任何一份）結算單裡的捐款。
            var startUtc = TaiwanClock.StartOfDayUtc(settlement.PeriodStart);
            var endUtc = TaiwanClock.StartOfDayUtc(settlement.PeriodEnd.AddDays(1));
            foreach (var row in await EligibleRowsAsync(settlement.PayeeType, startUtc, endUtc, settlement.PayeeId, cancellationToken))
            {
                db.SettlementLines.Add(NewLine(settlement.Id, row.DonationId, row.Share, false, null, scope, now));
                added++;
            }

            foreach (var row in await ClawbackRowsAsync(settlement.PayeeType, settlement.PayeeId, cancellationToken))
            {
                db.SettlementLines.Add(NewLine(settlement.Id, row.DonationId, -row.Share, true, ClawbackReasonText(row.OrderNo, row.RefundReason), scope, now));
                addedClawbacks++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        // ③ 重算合計（從資料庫現況算，不靠記憶體裡的增減）。
        var totals = await db.SettlementLines.AsNoTracking()
            .Where(l => l.SettlementId == settlement.Id)
            .Select(l => new { l.IsClawback, l.ShareAmount, l.Donation.Amount })
            .ToListAsync(cancellationToken);
        settlement.DonationCount = totals.Count(t => !t.IsClawback);
        settlement.DonationTotal = totals.Where(t => !t.IsClawback).Sum(t => t.Amount);
        settlement.PayableAmount = totals.Sum(t => t.ShareAmount);
        settlement.UpdatedAt = now;
        settlement.UpdatedBy = scope.Identity.AdminUserId;

        return new RebuildOutcome(stale.Select(s => s.OrderNo).OrderBy(n => n, StringComparer.Ordinal).ToList(), added, addedClawbacks);
    }

    private static SettlementLine NewLine(Guid settlementId, Guid donationId, int shareAmount, bool isClawback, string? reason, CharityAdminScope scope, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            DonationId = donationId,
            ShareAmount = shareAmount,
            IsClawback = isClawback,
            ClawbackReason = reason,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId,
            UpdatedBy = scope.Identity.AdminUserId,
        };

    private static string ClawbackReasonText(string orderNo, string? refundReason)
    {
        var text = string.IsNullOrWhiteSpace(refundReason) ? $"捐款單 {orderNo} 已退款" : $"捐款單 {orderNo} 已退款：{refundReason.Trim()}";
        return text.Length <= 255 ? text : text[..255];
    }

    private async Task AcquireLockAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        if (!await CharitySqlLocks.TryAcquireAsync(db, transaction, LockResource, LockWaitMilliseconds, cancellationToken))
        {
            throw new CharityConflictException("結算處理中", "另一位同仁正在處理結算，請稍候幾秒後重新整理頁面再試。");
        }
    }

    private async Task<Settlement> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await db.Settlements.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw new CharityNotFoundException("找不到這份結算單。");

    // ═══════════════════════════════════════════════════════════════════════
    // 內部：DTO 組裝
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<Guid, string>> PayeeNamesAsync(string type, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        if (type == SettlementPayeeTypes.Store)
        {
            return await db.DonationStoresI18ns.AsNoTracking()
                .Where(i => ids.Contains(i.DonationStoreId) && i.Locale == RequestLocale.DefaultDbLocale)
                .ToDictionaryAsync(i => i.DonationStoreId, i => i.Name, cancellationToken);
        }

        return await db.DonationProjectsI18ns.AsNoTracking()
            .Where(i => ids.Contains(i.DonationProjectId) && i.Locale == RequestLocale.DefaultDbLocale)
            .ToDictionaryAsync(i => i.DonationProjectId, i => i.Name, cancellationToken);
    }

    private async Task<IReadOnlyList<AdminSettlementListItemDto>> ToListItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Settlements.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new
            {
                s.Id, s.PeriodStart, s.PeriodEnd, s.PayeeType, s.PayeeId, s.Status, s.DonationCount, s.DonationTotal, s.PayableAmount,
                s.RemittedOn, s.RemitMethod, s.RemitNote, s.CreatedAt,
                ClawbackCount = s.SettlementLines.Count(l => l.IsClawback),
                ClawbackAmount = s.SettlementLines.Where(l => l.IsClawback).Sum(l => (int?)l.ShareAmount) ?? 0,
            })
            .ToListAsync(cancellationToken);

        var storeNames = await PayeeNamesAsync(SettlementPayeeTypes.Store, rows.Where(r => r.PayeeType == SettlementPayeeTypes.Store).Select(r => r.PayeeId).ToList(), cancellationToken);
        var projectNames = await PayeeNamesAsync(SettlementPayeeTypes.Project, rows.Where(r => r.PayeeType == SettlementPayeeTypes.Project).Select(r => r.PayeeId).ToList(), cancellationToken);

        var targetIds = ids.Select(i => (Guid?)i).ToList();
        var events = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TargetType == CharityAuditTargets.Settlement && targetIds.Contains(a.TargetId)
                        && (a.Action == CharityAuditActions.SettlementSettle || a.Action == CharityAuditActions.SettlementMarkPaid))
            .Select(a => new { a.TargetId, a.Action, a.OccurredAt, Name = a.AdminUser.DisplayName })
            .ToListAsync(cancellationToken);

        var byId = rows.ToDictionary(r => r.Id);
        return ids.Where(byId.ContainsKey).Select(id =>
        {
            var r = byId[id];
            var settled = events.Where(e => e.TargetId == id && e.Action == CharityAuditActions.SettlementSettle).OrderByDescending(e => e.OccurredAt).FirstOrDefault();
            var paid = events.Where(e => e.TargetId == id && e.Action == CharityAuditActions.SettlementMarkPaid).OrderByDescending(e => e.OccurredAt).FirstOrDefault();
            var names = r.PayeeType == SettlementPayeeTypes.Store ? storeNames : projectNames;
            return new AdminSettlementListItemDto
            {
                Id = r.Id,
                PeriodStart = r.PeriodStart,
                PeriodEnd = r.PeriodEnd,
                PayeeType = r.PayeeType,
                PayeeId = r.PayeeId,
                PayeeName = names.GetValueOrDefault(r.PayeeId),
                Status = r.Status,
                DonationCount = r.DonationCount,
                DonationTotal = r.DonationTotal,
                ClawbackCount = r.ClawbackCount,
                ClawbackAmount = r.ClawbackAmount,
                PayableAmount = r.PayableAmount,
                RemittedOn = r.RemittedOn,
                RemitMethod = r.RemitMethod,
                RemitNote = r.RemitNote,
                SettledAt = settled?.OccurredAt,
                SettledByName = settled?.Name,
                PaidRegisteredAt = paid?.OccurredAt,
                PaidRegisteredByName = paid?.Name,
                CreatedAt = r.CreatedAt,
            };
        }).ToList();
    }

    private async Task<AdminSettlementDetailDto?> BuildDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = (await ToListItemsAsync([id], cancellationToken)).FirstOrDefault();
        if (item is null)
        {
            return null;
        }

        var lines = await db.SettlementLines.AsNoTracking()
            .Where(l => l.SettlementId == id)
            .Select(l => new
            {
                l.Id, l.DonationId, l.Donation.OrderNo, l.Donation.PaidAt, DonationAmount = l.Donation.Amount, l.ShareAmount, l.IsClawback, l.ClawbackReason,
                l.Donation.StoreSharePctSnapshot, l.Donation.ProjectSharePctSnapshot,
            })
            .ToListAsync(cancellationToken);

        var isStore = item.PayeeType == SettlementPayeeTypes.Store;
        return new AdminSettlementDetailDto
        {
            Settlement = item,
            Lines = lines
                .OrderBy(l => l.IsClawback).ThenBy(l => l.PaidAt).ThenBy(l => l.OrderNo, StringComparer.Ordinal)
                .Select(l => new AdminSettlementLineDto(
                    l.Id, l.DonationId, l.OrderNo, l.PaidAt, l.DonationAmount,
                    isStore ? l.StoreSharePctSnapshot : l.ProjectSharePctSnapshot, l.ShareAmount, l.IsClawback, l.ClawbackReason))
                .ToList(),
        };
    }

    private static string TypeLabel(string type) => type == SettlementPayeeTypes.Store ? "店家回饋金" : "項目撥付金";

    private static string StatusLabel(string status) => status switch
    {
        SettlementStatus.Pending => "待結算",
        SettlementStatus.Settled => "已結算",
        SettlementStatus.Paid => "已付款",
        _ => status,
    };
}
