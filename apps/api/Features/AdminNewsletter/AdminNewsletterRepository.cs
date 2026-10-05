using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminNewsletter;

/// <summary>
/// G3 電子報訂閱名單（主站規劃書 §4.7 G3）。名單以 <c>club_id</c> 分開、唯一鍵 <c>(club_id, email)</c>——
/// 法遵：同一人可以只退訂其中一站，所以另一個俱樂部的名單永遠不受影響。名單視同個資：匯出須額外授權、填用途並寫敏感操作日誌；
/// 退訂是法遵事實，<b>不得由後台「無理由」改回訂閱</b>（重新訂閱須註明是訂閱者本人要求）。
/// </summary>
public sealed class AdminNewsletterRepository(ClubDbContext dbContext, SensitiveActionLogger audit)
{
    public const string StatusSubscribed = "subscribed";
    public const string StatusUnsubscribed = "unsubscribed";

    private static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [StatusSubscribed] = "已訂閱",
        [StatusUnsubscribed] = "已退訂",
    };

    private static string SourceLabel(string? source) => string.IsNullOrWhiteSpace(source) ? "未註明" : source;

    public async Task<PagedResult<AdminNewsletterSubscriberDto>> ListAsync(
        AdminClubScope scope, AdminNewsletterListQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = PagingQuery.Normalize(query.Page, query.PageSize, defaultPageSize: 20, maxPageSize: 100);
        var source = Filter(scope, query);
        var total = await source.CountAsync(cancellationToken);
        var rows = await source.OrderByDescending(s => s.SubscribedAt ?? s.CreatedAt).ThenBy(s => s.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AdminNewsletterSubscriberDto>
        {
            Items = rows.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    private IQueryable<NewsletterSubscriber> Filter(AdminClubScope scope, AdminNewsletterListQuery query)
    {
        var q = dbContext.NewsletterSubscribers.AsNoTracking().Where(s => s.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!StatusLabels.ContainsKey(query.Status))
            {
                throw new AdminValidationException("狀態篩選只能是「已訂閱」或「已退訂」。");
            }

            q = q.Where(s => s.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            q = q.Where(s => s.Source == query.Source);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var k = query.Keyword.Trim().ToLowerInvariant();
            q = q.Where(s => s.Email.Contains(k));
        }

        return q;
    }

    public async Task<AdminNewsletterSummaryDto> SummaryAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.NewsletterSubscribers.AsNoTracking().Where(s => s.ClubId == scope.ClubId)
            .GroupBy(s => new { s.Status, s.Source }).Select(g => new { g.Key.Status, g.Key.Source, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var sources = rows.Where(r => r.Status == StatusSubscribed).GroupBy(r => r.Source)
            .Select(g => new AdminNewsletterSourceCountDto { Source = g.Key, SourceLabel = SourceLabel(g.Key), Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).ToList();
        return new AdminNewsletterSummaryDto
        {
            SubscribedCount = rows.Where(r => r.Status == StatusSubscribed).Sum(r => r.Count),
            UnsubscribedCount = rows.Where(r => r.Status == StatusUnsubscribed).Sum(r => r.Count),
            Sources = sources,
        };
    }

    public async Task<AdminNewsletterSubscriberDto> CreateAsync(
        AdminClubScope scope, CreateAdminNewsletterSubscriberRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var source = AdminInput.OptionalText(request.Source, "來源說明", 64) ?? "後台新增";
        var existing = await dbContext.NewsletterSubscribers
            .FirstOrDefaultAsync(s => s.ClubId == scope.ClubId && s.Email == email, cancellationToken);
        if (existing is not null)
        {
            throw existing.Status == StatusUnsubscribed
                ? new AdminConflictException("此信箱曾經退訂", "這個信箱已經退訂過電子報，不能由後台直接加回；若是本人要求重新訂閱，請在名單中改狀態並註明原因。")
                : new AdminConflictException("信箱已在名單中", "這個信箱已經在電子報名單裡了。");
        }

        var now = DateTime.UtcNow;
        var row = new NewsletterSubscriber
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Email = email, Source = source, Status = StatusSubscribed,
            SubscribedAt = now, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        dbContext.NewsletterSubscribers.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<AdminNewsletterSubscriberDto?> UpdateStatusAsync(
        AdminClubScope scope, Guid id, UpdateAdminNewsletterStatusRequest request, CancellationToken cancellationToken)
    {
        var status = AdminInput.OneOf(request.Status, StatusLabels.Keys.ToHashSet(StringComparer.Ordinal), "狀態", "「已訂閱」或「已退訂」");
        var row = await dbContext.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (row.Status == status)
        {
            return ToDto(row);
        }

        var now = DateTime.UtcNow;
        if (status == StatusUnsubscribed)
        {
            row.UnsubscribedAt = now;
        }
        else
        {
            // 重新訂閱：退訂是法遵事實，只有「訂閱者本人要求」才能改回，原因必填並留敏感操作日誌。
            var reason = AdminInput.RequireText(request.Reason, "重新訂閱的原因", 200);
            row.SubscribedAt = now;
            row.UnsubscribedAt = null;
            audit.Record(scope, "電子報重新訂閱", $"名單 {row.Id}", 1, reason);
        }

        row.Status = status;
        row.UpdatedAt = now;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    /// <summary>刪除一位訂閱者（個資刪除請求）。刪除後不留退訂紀錄——若對方是「退訂」而不是「要求刪除個資」，請改用退訂，
    /// 否則日後同一信箱再被加入時無從得知他曾經退訂。</summary>
    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await dbContext.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        dbContext.NewsletterSubscribers.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "刪除電子報訂閱者", $"名單 {id}", 1, "個資刪除");
        return true;
    }

    public async Task<string> ExportCsvAsync(
        AdminClubScope scope, AdminNewsletterListQuery query, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var rows = await Filter(scope, query).OrderBy(s => s.Email).ToListAsync(cancellationToken);
        var lines = new List<IEnumerable<string?>> { new[] { "Email", "來源", "狀態", "訂閱時間（台灣時間）", "退訂時間（台灣時間）" } };
        lines.AddRange(rows.Select(r => (IEnumerable<string?>)new[]
        {
            CsvUtils.SafeCell(r.Email), CsvUtils.SafeCell(SourceLabel(r.Source)), StatusLabels[r.Status],
            r.SubscribedAt is { } sub ? TaiwanClock.ToText(sub) : null, r.UnsubscribedAt is { } unsub ? TaiwanClock.ToText(unsub) : null,
        }));
        audit.Record(scope, "匯出電子報名單", $"共 {rows.Count} 筆", rows.Count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }

    /// <summary>把名單交給 EDM 平台（接縫，尚未串接時回「尚未串接」）。訂閱與退訂兩份名單一併送出。</summary>
    public async Task<AdminNewsletterEdmSyncResultDto> SyncToEdmAsync(
        AdminClubScope scope, INewsletterEdmSync edm, CancellationToken cancellationToken)
    {
        var all = await dbContext.NewsletterSubscribers.AsNoTracking().Where(s => s.ClubId == scope.ClubId)
            .Select(s => new { s.Email, s.Status }).ToListAsync(cancellationToken);
        var subscribed = all.Where(s => s.Status == StatusSubscribed).Select(s => s.Email).ToList();
        var unsubscribed = all.Where(s => s.Status == StatusUnsubscribed).Select(s => s.Email).ToList();
        var result = await edm.SyncAsync(new EdmSyncRequest(scope.ClubCode, subscribed, unsubscribed), cancellationToken);
        if (result.Configured)
        {
            audit.Record(scope, "同步電子報名單至 EDM 平台", $"訂閱 {subscribed.Count}／退訂 {unsubscribed.Count}", result.SyncedCount, "EDM 同步");
        }

        return new AdminNewsletterEdmSyncResultDto
        {
            Configured = result.Configured, SubscribedCount = subscribed.Count, UnsubscribedCount = unsubscribed.Count,
            SyncedCount = result.SyncedCount, Message = result.Message,
        };
    }

    internal static string NormalizeEmail(string? raw)
    {
        var email = AdminInput.OptionalEmail(raw, "Email") ?? throw new AdminValidationException("Email 為必填欄位。");
        return email.ToLowerInvariant();
    }

    private static AdminNewsletterSubscriberDto ToDto(NewsletterSubscriber s) => new()
    {
        Id = s.Id, Email = s.Email, Source = s.Source, SourceLabel = SourceLabel(s.Source), Status = s.Status,
        StatusLabel = StatusLabels.GetValueOrDefault(s.Status, s.Status), SubscribedAt = s.SubscribedAt,
        UnsubscribedAt = s.UnsubscribedAt, UpdatedAt = s.UpdatedAt,
    };
}
