using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCharity;

/// <summary>
/// B5「影響力數據」（規劃書 §4.2 B5、前台 11.4）：可自訂統計項目（名稱、單位、數值、是否公開）。
/// 🔴 **金額類項目預設不公開**：<c>is_public</c> 資料庫預設 <c>0</c>，API 請求省略時也是 <c>false</c>，
/// 要公開必須明確送 <c>true</c>——「是否為金額類」沒有獨立欄位可判斷（規劃書沒有要求），
/// 因此採最保守做法：所有項目一律預設不公開。<c>club_id</c> 可為空：共同列只讀。
/// </summary>
public sealed class AdminImpactMetricsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    public async Task<IReadOnlyList<AdminImpactMetricDto>> ListAsync(AdminClubScope scope, Guid? programId, CancellationToken cancellationToken)
    {
        var query = dbContext.ImpactMetrics.AsNoTracking().Include(m => m.ImpactMetricsI18ns)
            .Include(m => m.CharityProgram).ThenInclude(p => p!.CharityProgramsI18ns)
            .Where(m => m.ClubId == scope.ClubId || m.ClubId == null);
        if (programId is Guid p)
        {
            query = query.Where(m => m.CharityProgramId == p);
        }

        var rows = await query.OrderBy(m => m.SortOrder).ThenBy(m => m.RowSeq).AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminImpactMetricDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var metric = await dbContext.ImpactMetrics.AsNoTracking().Include(m => m.ImpactMetricsI18ns)
            .Include(m => m.CharityProgram).ThenInclude(p => p!.CharityProgramsI18ns).AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id && (m.ClubId == scope.ClubId || m.ClubId == null), cancellationToken);
        return metric is null ? null : ToDto(metric);
    }

    public async Task<AdminImpactMetricDto> CreateAsync(
        AdminClubScope scope, UpsertAdminImpactMetricRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        await EnsureProgramAsync(scope, request.CharityProgramId, cancellationToken);
        var now = DateTime.UtcNow;
        var metric = new ImpactMetric
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, MetricKey = $"metric-{Guid.NewGuid():N}"[..15],
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(metric, request);
        dbContext.ImpactMetrics.Add(metric);
        SetI18n(metric, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, metric.Id, cancellationToken))!;
    }

    public async Task<AdminImpactMetricDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminImpactMetricRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var metric = await dbContext.ImpactMetrics.Include(m => m.ImpactMetricsI18ns)
            .FirstOrDefaultAsync(m => m.Id == id && (m.ClubId == scope.ClubId || m.ClubId == null), cancellationToken);
        if (metric is null)
        {
            return null;
        }

        if (metric.ClubId is null)
        {
            throw new SharedContentReadOnlyException("影響力數據");
        }

        await EnsureProgramAsync(scope, request.CharityProgramId, cancellationToken);
        Apply(metric, request);
        metric.UpdatedAt = DateTime.UtcNow;
        metric.UpdatedBy = operatorId;
        SetI18n(metric, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var metric = await dbContext.ImpactMetrics.FirstOrDefaultAsync(m => m.Id == id && (m.ClubId == scope.ClubId || m.ClubId == null), cancellationToken);
        if (metric is null)
        {
            return false;
        }

        if (metric.ClubId is null)
        {
            throw new SharedContentReadOnlyException("影響力數據");
        }

        dbContext.ImpactMetrics.Remove(metric);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    private static void Validate(UpsertAdminImpactMetricRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.Name, "中文項目名稱", 64);
        AdminInput.OptionalText(request.Content.Zh.Unit, "單位", 16);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文項目名稱", 64);
            AdminInput.OptionalText(request.Content.En.Unit, "單位（英文）", 16);
        }
    }

    private async Task EnsureProgramAsync(AdminClubScope scope, Guid? programId, CancellationToken cancellationToken)
    {
        if (programId is Guid p
            && !await dbContext.CharityPrograms.AsNoTracking().AnyAsync(x => x.Id == p && (x.ClubId == scope.ClubId || x.ClubId == null), cancellationToken))
        {
            throw new AdminValidationException("找不到指定的慈善計畫，請重新選擇。");
        }
    }

    private static void Apply(ImpactMetric metric, UpsertAdminImpactMetricRequest request)
    {
        metric.CharityProgramId = request.CharityProgramId;
        metric.MetricValue = request.Value;
        metric.IsPublic = request.IsPublic;
        metric.SortOrder = request.SortOrder;
    }

    private void SetI18n(ImpactMetric metric, AdminImpactMetricContentInput content)
    {
        Upsert(metric, RequestLocale.DefaultDbLocale, content.Zh);
        var en = metric.ImpactMetricsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(metric, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(ImpactMetric metric, string locale, AdminImpactMetricLocaleContent content)
    {
        var row = metric.ImpactMetricsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new ImpactMetricsI18n { ImpactMetricId = metric.Id, Locale = locale };
            metric.ImpactMetricsI18ns.Add(row);
            dbContext.ImpactMetricsI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Unit = string.IsNullOrWhiteSpace(content.Unit) ? null : content.Unit.Trim();
    }

    private static AdminImpactMetricDto ToDto(ImpactMetric metric)
    {
        var zh = metric.ImpactMetricsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = metric.ImpactMetricsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminImpactMetricDto
        {
            Id = metric.Id, IsShared = metric.ClubId is null, CharityProgramId = metric.CharityProgramId,
            ProgramNameZh = metric.CharityProgram?.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            Value = metric.MetricValue, IsPublic = metric.IsPublic, SortOrder = metric.SortOrder,
            Zh = new AdminImpactMetricLocaleContent { Name = zh?.Name ?? "", Unit = zh?.Unit },
            En = en is null ? null : new AdminImpactMetricLocaleContent { Name = en.Name ?? "", Unit = en.Unit },
            UpdatedAt = metric.UpdatedAt,
        };
    }
}
