using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Reconciliation;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Admin;

// 每日對帳的後台介面（規劃書 §4.5、§6.3 異常佇列的第三類）。對帳「結果保留供稽核」：批次與差異都不可刪除，
// 差異只能更新處理狀態（pending → resolved），所以這裡沒有任何刪除操作。

public sealed record AdminReconciliationRunDto
{
    public required Guid Id { get; init; }
    public required DateOnly RunOn { get; init; }
    public required string Source { get; init; }

    /// <summary><c>completed</c>（已完成）／<c>failed</c>（取不到金流明細，沒有比對）。</summary>
    public required string Status { get; init; }

    public required int ComparedCount { get; init; }
    public required int MatchedCount { get; init; }
    public required int DiscrepancyCount { get; init; }

    /// <summary>其中還沒處理的差異筆數。</summary>
    public required int PendingCount { get; init; }

    public required DateTime RanAt { get; init; }
}

public sealed record AdminReconciliationDiscrepancyDto
{
    public required Guid Id { get; init; }

    /// <summary><c>site_only</c>（本站有金流無）／<c>gateway_only</c>（金流有本站無）／<c>amount_mismatch</c>（金額不符）。</summary>
    public required string Type { get; init; }

    public required Guid? DonationId { get; init; }
    public required string? OrderNo { get; init; }
    public required string? GatewayTransactionId { get; init; }
    public required int? SiteAmount { get; init; }
    public required int? GatewayAmount { get; init; }

    /// <summary><c>pending</c>（待處理）／<c>resolved</c>（已處理）。</summary>
    public required string ResolutionStatus { get; init; }

    public required string? ResolvedByName { get; init; }
    public required string? ResolveNote { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminReconciliationRunDetailDto
{
    public required AdminReconciliationRunDto Run { get; init; }
    public required IReadOnlyList<AdminReconciliationDiscrepancyDto> Discrepancies { get; init; }
}

public sealed record RunReconciliationRequest
{
    /// <summary>對帳日（台灣日期）。省略＝昨天。</summary>
    public DateOnly? Date { get; init; }
}

public sealed record ResolveDiscrepancyRequest
{
    /// <summary>處理說明（至少 2 個字，255 字內）。差異留在紀錄裡不刪除，這段說明就是之後稽核看到的處理結果。</summary>
    public string? Note { get; init; }
}

public sealed record AdminReconciliationRunResultDto(
    Guid RunId, DateOnly RunOn, string Source, string Status, int ComparedCount, int MatchedCount, int DiscrepancyCount,
    int NewDiscrepancies, int AutoResolved);

/// <summary>
/// 對帳批次與差異的查詢、手動重跑、處理差異。手動對帳與「重新確認付款結果」同一組人（客服／行政、系統管理員），
/// 共用權限碼 <see cref="CharityPermissions.DonationRecheckPayment"/>——處理差異常常就是去重新確認一筆付款。
/// </summary>
public sealed class CharityReconciliationAdminService(CharityDbContext db, CharityReconciliationRunner runner, CharityAuditLogger audit)
{
    public async Task<PagedResult<AdminReconciliationRunDto>> ListRunsAsync(
        CharityAdminScope scope, DateOnly? from, DateOnly? to, bool? onlyPending, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        var query = db.ReconciliationRuns.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            query = query.Where(r => r.RunOn >= f);
        }

        if (to is { } t)
        {
            query = query.Where(r => r.RunOn <= t);
        }

        if (onlyPending == true)
        {
            query = query.Where(r => r.ReconciliationDiscrepancies.Any(d => d.ResolutionStatus == DiscrepancyResolution.Pending));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.RunOn).ThenByDescending(r => r.RowSeq)
            .Skip((p - 1) * size).Take(size)
            .Select(r => new
            {
                r.Id, r.RunOn, r.Source, r.Status, r.ComparedCount, r.MatchedCount, r.DiscrepancyCount, r.RanAt,
                Pending = r.ReconciliationDiscrepancies.Count(d => d.ResolutionStatus == DiscrepancyResolution.Pending),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReconciliationRunDto>
        {
            Items = rows.Select(r => new AdminReconciliationRunDto
            {
                Id = r.Id, RunOn = r.RunOn, Source = r.Source, Status = r.Status, ComparedCount = r.ComparedCount,
                MatchedCount = r.MatchedCount, DiscrepancyCount = r.DiscrepancyCount, PendingCount = r.Pending, RanAt = r.RanAt,
            }).ToList(),
            Page = p,
            PageSize = size,
            TotalCount = total,
        };
    }

    public async Task<AdminReconciliationRunDetailDto?> GetRunAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var run = await db.ReconciliationRuns.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id, r.RunOn, r.Source, r.Status, r.ComparedCount, r.MatchedCount, r.DiscrepancyCount, r.RanAt,
                Pending = r.ReconciliationDiscrepancies.Count(d => d.ResolutionStatus == DiscrepancyResolution.Pending),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return null;
        }

        var discrepancies = await db.ReconciliationDiscrepancies.AsNoTracking()
            .Where(d => d.ReconciliationRunId == id)
            .OrderBy(d => d.ResolutionStatus == DiscrepancyResolution.Pending ? 0 : 1).ThenBy(d => d.RowSeq)
            .Select(d => new
            {
                d.Id, d.DiscrepancyType, d.DonationId, OrderNo = d.Donation == null ? null : d.Donation.OrderNo, d.GatewayTransactionId,
                d.SiteAmount, d.GatewayAmount, d.ResolutionStatus, d.ResolveNote, d.CreatedAt, d.UpdatedAt, d.ResolvedBy,
            })
            .ToListAsync(cancellationToken);

        var resolverIds = discrepancies.Where(d => d.ResolvedBy != null).Select(d => d.ResolvedBy!.Value).Distinct().ToList();
        var names = resolverIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.AdminUsers.AsNoTracking().Where(u => resolverIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return new AdminReconciliationRunDetailDto
        {
            Run = new AdminReconciliationRunDto
            {
                Id = run.Id, RunOn = run.RunOn, Source = run.Source, Status = run.Status, ComparedCount = run.ComparedCount,
                MatchedCount = run.MatchedCount, DiscrepancyCount = run.DiscrepancyCount, PendingCount = run.Pending, RanAt = run.RanAt,
            },
            Discrepancies = discrepancies.Select(d => new AdminReconciliationDiscrepancyDto
            {
                Id = d.Id, Type = d.DiscrepancyType, DonationId = d.DonationId, OrderNo = d.OrderNo, GatewayTransactionId = d.GatewayTransactionId,
                SiteAmount = d.SiteAmount, GatewayAmount = d.GatewayAmount, ResolutionStatus = d.ResolutionStatus,
                ResolvedByName = d.ResolvedBy is { } rb ? names.GetValueOrDefault(rb) : null, ResolveNote = d.ResolveNote,
                CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
            }).ToList(),
        };
    }

    /// <summary>手動對帳（任一天，可重跑）。取不到金流明細回 503，本站不會因此判定任何差異。</summary>
    public async Task<AdminReconciliationRunResultDto> RunAsync(
        CharityAdminScope scope, DateOnly? date, string sourceIp, CancellationToken cancellationToken)
    {
        var target = date ?? TaiwanClock.Today.AddDays(-1);
        ReconciliationRunSummary summary;
        try
        {
            summary = await runner.RunAsync(target, scope.Identity.AdminUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayUnavailableException or PaymentGatewayNotConfiguredException)
        {
            throw new CharityServiceUnavailableException("目前取不到金流端的交易明細，這一天沒有比對（已記錄為失敗的批次）。請稍後再試。");
        }

        audit.Stage(scope, CharityAuditActions.ReconciliationRun, CharityAuditTargets.Reconciliation, summary.RunId,
            $"手動對帳 {target:yyyy-MM-dd}：比對 {summary.ComparedCount} 筆、相符 {summary.MatchedCount} 筆、差異 {summary.DiscrepancyCount} 筆（新增 {summary.NewDiscrepancies}、自動標記已處理 {summary.AutoResolved}）",
            null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);

        return new AdminReconciliationRunResultDto(
            summary.RunId, summary.RunOn, summary.Source, summary.Status, summary.ComparedCount, summary.MatchedCount,
            summary.DiscrepancyCount, summary.NewDiscrepancies, summary.AutoResolved);
    }

    /// <summary>處理差異：待處理 → 已處理，必須寫處理說明。差異本身留在紀錄裡（供稽核）。已處理的不可再改。</summary>
    public async Task<AdminReconciliationDiscrepancyDto> ResolveAsync(
        CharityAdminScope scope, Guid discrepancyId, string? note, string sourceIp, CancellationToken cancellationToken)
    {
        var cleanNote = AdminInput.RequireText(note, "處理說明", 255);
        if (cleanNote.Length < 2)
        {
            throw new AdminValidationException("處理說明至少要 2 個字。");
        }

        var now = DateTime.UtcNow;
        // 條件式更新：只有「還是待處理」的那一個請求會贏，並發的兩次處理不會互相覆蓋說明。更新與稽核在同一個交易提交。
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var moved = await db.ReconciliationDiscrepancies
            .Where(d => d.Id == discrepancyId && d.ResolutionStatus == DiscrepancyResolution.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ResolutionStatus, DiscrepancyResolution.Resolved)
                .SetProperty(d => d.ResolvedBy, scope.Identity.AdminUserId)
                .SetProperty(d => d.ResolveNote, cleanNote)
                .SetProperty(d => d.UpdatedAt, now)
                .SetProperty(d => d.UpdatedBy, scope.Identity.AdminUserId), cancellationToken);

        if (moved == 0)
        {
            if (!await db.ReconciliationDiscrepancies.AsNoTracking().AnyAsync(d => d.Id == discrepancyId, cancellationToken))
            {
                throw new CharityNotFoundException("找不到這筆對帳差異。");
            }

            throw new CharityConflictException("已經處理過", "這筆差異已經有人處理了，請重新整理頁面。");
        }

        audit.Stage(scope, CharityAuditActions.ReconciliationResolve, CharityAuditTargets.ReconciliationDiscrepancy, discrepancyId, $"處理對帳差異：{cleanNote}", null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        var d = await db.ReconciliationDiscrepancies.AsNoTracking().Where(x => x.Id == discrepancyId)
            .Select(x => new
            {
                x.Id, x.DiscrepancyType, x.DonationId, OrderNo = x.Donation == null ? null : x.Donation.OrderNo, x.GatewayTransactionId,
                x.SiteAmount, x.GatewayAmount, x.ResolutionStatus, x.ResolveNote, x.CreatedAt, x.UpdatedAt,
            })
            .SingleAsync(cancellationToken);
        return new AdminReconciliationDiscrepancyDto
        {
            Id = d.Id, Type = d.DiscrepancyType, DonationId = d.DonationId, OrderNo = d.OrderNo, GatewayTransactionId = d.GatewayTransactionId,
            SiteAmount = d.SiteAmount, GatewayAmount = d.GatewayAmount, ResolutionStatus = d.ResolutionStatus,
            ResolvedByName = (await db.AdminUsers.AsNoTracking().Where(u => u.Id == scope.Identity.AdminUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken)),
            ResolveNote = d.ResolveNote, CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
        };
    }
}
