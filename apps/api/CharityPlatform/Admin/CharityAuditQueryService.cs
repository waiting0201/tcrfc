using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Admin;

public sealed record AuditLogFilter(DateOnly? From, DateOnly? To, string? Action, string? TargetType, Guid? TargetId, Guid? AdminUserId, string? Keyword);

public sealed record AdminAuditLogDto
{
    public required Guid Id { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required Guid AdminUserId { get; init; }
    public required string? AdminName { get; init; }

    /// <summary>動作代碼（程式識別用；畫面請顯示 <c>actionLabel</c>）。</summary>
    public required string Action { get; init; }

    public required string ActionLabel { get; init; }
    public required string TargetType { get; init; }
    public required string TargetTypeLabel { get; init; }
    public required Guid? TargetId { get; init; }

    /// <summary>對象的好讀名稱（捐款單號、店家或項目名稱）；對象已不存在或沒有好讀名稱時為 <c>null</c>。</summary>
    public required string? TargetLabel { get; init; }

    public required string? ChangeSummary { get; init; }
    public required string? PurposeNote { get; init; }
    public required string? SourceIp { get; init; }
}

public sealed record AdminAuditActionOptionDto(string Action, string Label);

/// <summary>
/// 稽核紀錄查詢（規劃書 §11.2：退款、分潤設定、含個資匯出三類操作全數留下稽核軌跡；本平台再加上個資明文檢視、QR 重產、憑證與結算操作等）。
/// 🔴 唯讀：<c>audit_logs</c> 是 append-only（<see cref="CharityAuditLogger"/> 只有 Stage），這裡只有查詢。🔴 僅 <see cref="CharityPermissions.AuditLogView"/>
/// （系統管理員）：稽核本身含操作者與來源 IP。查詢稽核本身不再寫稽核。
/// </summary>
public sealed class CharityAuditQueryService(CharityDbContext db)
{
    public async Task<PagedResult<AdminAuditLogDto>> QueryAsync(
        CharityAdminScope scope, AuditLogFilter filter, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (filter.From is { } from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            query = query.Where(a => a.OccurredAt >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(a => a.OccurredAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetType))
        {
            var type = filter.TargetType.Trim();
            query = query.Where(a => a.TargetType == type);
        }

        if (filter.TargetId is { } targetId)
        {
            query = query.Where(a => a.TargetId == targetId);
        }

        if (filter.AdminUserId is { } adminId)
        {
            query = query.Where(a => a.AdminUserId == adminId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var k = filter.Keyword.Trim();
            query = query.Where(a => a.ChangeSummary != null && a.ChangeSummary.Contains(k));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Seq)
            .Skip((p - 1) * size).Take(size)
            .Select(a => new
            {
                a.Id, a.OccurredAt, a.AdminUserId, AdminName = a.AdminUser.DisplayName, a.Action, a.TargetType, a.TargetId,
                a.ChangeSummary, a.PurposeNote, a.SourceIp,
            })
            .ToListAsync(cancellationToken);

        // 對象好讀名稱：每一類對象各一次批次查詢（不逐列查）。
        var donationIds = IdsOf(rows, CharityAuditTargets.Donation, r => r.TargetType, r => r.TargetId);
        var storeIds = IdsOf(rows, CharityAuditTargets.Store, r => r.TargetType, r => r.TargetId);
        var projectIds = IdsOf(rows, CharityAuditTargets.Project, r => r.TargetType, r => r.TargetId);

        var orderNos = donationIds.Count == 0 ? new Dictionary<Guid, string>()
            : await db.Donations.AsNoTracking().Where(d => donationIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.OrderNo, cancellationToken);
        var storeNames = storeIds.Count == 0 ? new Dictionary<Guid, string>()
            : await db.DonationStoresI18ns.AsNoTracking().Where(i => storeIds.Contains(i.DonationStoreId) && i.Locale == RequestLocale.DefaultDbLocale)
                .ToDictionaryAsync(i => i.DonationStoreId, i => i.Name, cancellationToken);
        var projectNames = projectIds.Count == 0 ? new Dictionary<Guid, string>()
            : await db.DonationProjectsI18ns.AsNoTracking().Where(i => projectIds.Contains(i.DonationProjectId) && i.Locale == RequestLocale.DefaultDbLocale)
                .ToDictionaryAsync(i => i.DonationProjectId, i => i.Name, cancellationToken);

        string? Label(string type, Guid? id)
        {
            if (id is not { } g)
            {
                return null;
            }

            return type switch
            {
                CharityAuditTargets.Donation => orderNos.GetValueOrDefault(g),
                CharityAuditTargets.Store => storeNames.GetValueOrDefault(g),
                CharityAuditTargets.Project => projectNames.GetValueOrDefault(g),
                _ => null,
            };
        }

        return new PagedResult<AdminAuditLogDto>
        {
            Items = rows.Select(r => new AdminAuditLogDto
            {
                Id = r.Id,
                OccurredAt = r.OccurredAt,
                AdminUserId = r.AdminUserId,
                AdminName = r.AdminName,
                Action = r.Action,
                ActionLabel = CharityAuditActions.Labels.GetValueOrDefault(r.Action, "其他操作"),
                TargetType = r.TargetType,
                TargetTypeLabel = CharityAuditTargets.Labels.GetValueOrDefault(r.TargetType, "其他"),
                TargetId = r.TargetId,
                TargetLabel = Label(r.TargetType, r.TargetId),
                ChangeSummary = r.ChangeSummary,
                PurposeNote = r.PurposeNote,
                SourceIp = r.SourceIp,
            }).ToList(),
            Page = p,
            PageSize = size,
            TotalCount = total,
        };
    }

    /// <summary>篩選下拉用：所有可能出現的動作與對象類型（日常中文）。</summary>
    public Task<IReadOnlyList<AdminAuditActionOptionDto>> ListActionsAsync(CharityAdminScope scope, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdminAuditActionOptionDto>>(
            CharityAuditActions.Labels.Select(kv => new AdminAuditActionOptionDto(kv.Key, kv.Value)).OrderBy(o => o.Label, StringComparer.Ordinal).ToList());

    private static List<Guid> IdsOf<T>(IEnumerable<T> rows, string targetType, Func<T, string> type, Func<T, Guid?> id)
        => rows.Where(r => type(r) == targetType && id(r) is not null).Select(r => id(r)!.Value).Distinct().ToList();
}
