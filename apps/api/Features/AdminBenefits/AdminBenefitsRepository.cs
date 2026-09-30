using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminBenefits;

/// <summary>
/// K4 權益對照表（規劃書 §4.11 K4「權益對照表條目管理」，資料由前台 3.14 加入頁、8.2 球迷會頁、會員中心升級頁與 App 升級頁共用，
/// 此處是唯一維護點）。<c>membership_benefits</c> 不帶 <c>club_id</c>（§5.4：由父表推導），條目掛在方案（<c>membership_plans</c>）底下，
/// 所以本模組的範圍靠「方案屬於目前操作的俱樂部」強制：所有查詢都 join 方案並限定 <c>club_id</c>，跨俱樂部的 id 一律 404。
/// 分組的顯示文案（<c>group_label</c>）由分組代碼自動帶入雙語，不需要人工維護。
/// </summary>
public sealed class AdminBenefitsRepository(ClubDbContext db)
{
    public static readonly IReadOnlyList<AdminBenefitGroupDto> Groups =
    [
        new() { Code = "member_card", Label = "會員卡" },
        new() { Code = "store_discount", Label = "店家折扣" },
        new() { Code = "jersey", Label = "球衣" },
        new() { Code = "event", Label = "活動" },
    ];

    private static readonly Dictionary<string, (string Zh, string En)> GroupLabels = new(StringComparer.Ordinal)
    {
        ["member_card"] = ("會員卡", "Member card"),
        ["store_discount"] = ("店家折扣", "Store discounts"),
        ["jersey"] = ("球衣", "Jersey"),
        ["event"] = ("活動", "Events"),
    };

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };

    private IQueryable<MembershipBenefit> Scoped(AdminClubScope scope)
        => db.MembershipBenefits.Where(b => b.MembershipPlan.ClubId == scope.ClubId);

    public async Task<IReadOnlyList<AdminBenefitListItemDto>> ListAsync(
        AdminClubScope scope, Guid? planId, string? group, string? status, CancellationToken cancellationToken)
    {
        var query = Scoped(scope).AsNoTracking();
        if (planId is Guid p)
        {
            query = query.Where(b => b.MembershipPlanId == p);
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            AdminInput.OneOf(group, GroupLabels.Keys.ToHashSet(), "分組", "「會員卡」「店家折扣」「球衣」或「活動」");
            query = query.Where(b => b.BenefitGroup == group);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「上架」或「下架」");
            query = query.Where(b => b.Status == status);
        }

        var rows = await query.OrderBy(b => b.MembershipPlan.Season.StartOn).ThenBy(b => b.MembershipPlan.SortOrder).ThenBy(b => b.SortOrder).ThenBy(b => b.RowSeq)
            .Select(b => new
            {
                b.Id, b.MembershipPlanId, PlanCode = b.MembershipPlan.Code, SeasonCode = b.MembershipPlan.Season.Code, b.BenefitGroup, b.SortOrder, b.Status, b.UpdatedAt,
                PlanName = b.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameZh = b.MembershipBenefitsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = b.MembershipBenefitsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                FreeZh = b.MembershipBenefitsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.FreeValue).FirstOrDefault(),
                PaidZh = b.MembershipBenefitsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.PaidValue).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminBenefitListItemDto
        {
            Id = r.Id, PlanId = r.MembershipPlanId, PlanCode = r.PlanCode, PlanName = r.PlanName, SeasonCode = r.SeasonCode, Group = r.BenefitGroup,
            GroupLabel = GroupLabels.TryGetValue(r.BenefitGroup, out var gl) ? gl.Zh : r.BenefitGroup, SortOrder = r.SortOrder, Status = r.Status,
            StatusLabel = StatusLabel(r.Status), NameZh = r.NameZh, NameEn = r.NameEn, FreeValueZh = r.FreeZh, PaidValueZh = r.PaidZh, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminBenefitDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var benefit = await Scoped(scope).AsNoTracking().Include(b => b.MembershipBenefitsI18ns)
            .Include(b => b.MembershipPlan).ThenInclude(p => p.Season)
            .Include(b => b.MembershipPlan).ThenInclude(p => p.MembershipPlansI18ns)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return benefit is null ? null : ToDetail(benefit);
    }

    public async Task<AdminBenefitDetailDto> CreateAsync(
        AdminClubScope scope, UpsertAdminBenefitRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var plan = await ResolvePlanAsync(scope, request.PlanId, cancellationToken);
        var sortOrder = request.SortOrder
            ?? ((await db.MembershipBenefits.AsNoTracking().Where(b => b.MembershipPlanId == plan.Id).MaxAsync(b => (int?)b.SortOrder, cancellationToken)) ?? -1) + 1;

        var now = DateTime.UtcNow;
        var benefit = new MembershipBenefit
        {
            Id = Guid.NewGuid(), MembershipPlanId = plan.Id, BenefitGroup = request.Group, SortOrder = sortOrder, Status = request.Status,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.MembershipBenefits.Add(benefit);
        SetI18n(benefit, request);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, benefit.Id, cancellationToken))!;
    }

    public async Task<AdminBenefitDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminBenefitRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var benefit = await Scoped(scope).Include(b => b.MembershipBenefitsI18ns).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (benefit is null)
        {
            return null;
        }

        if (request.PlanId != benefit.MembershipPlanId)
        {
            throw new AdminValidationException("權益條目不能搬到其他方案；請在新方案底下新增，再刪除這一條。");
        }

        benefit.BenefitGroup = request.Group;
        if (request.SortOrder is int order)
        {
            benefit.SortOrder = order;
        }

        benefit.Status = request.Status;
        benefit.UpdatedAt = DateTime.UtcNow;
        benefit.UpdatedBy = operatorId;
        SetI18n(benefit, request);
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var benefit = await Scoped(scope).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (benefit is null)
        {
            return false;
        }

        db.MembershipBenefits.Remove(benefit); // 側表由資料庫串聯刪除
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>同一方案內依 <c>Ids</c> 順序重排（sortOrder 0,1,2…），未列入的條目排在其後、相對順序不變。</summary>
    public async Task ReorderAsync(AdminClubScope scope, ReorderBenefitsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0 || request.Ids.Distinct().Count() != request.Ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var plan = await ResolvePlanAsync(scope, request.PlanId, cancellationToken);
        var benefits = await db.MembershipBenefits.Where(b => b.MembershipPlanId == plan.Id).OrderBy(b => b.SortOrder).ThenBy(b => b.RowSeq).ToListAsync(cancellationToken);
        var byId = benefits.ToDictionary(b => b.Id);
        if (request.Ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不屬於這個方案的條目，請重新整理後再試。");
        }

        var order = request.Ids.Concat(benefits.Select(b => b.Id).Where(i => !request.Ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var b = byId[order[i]];
            if (b.SortOrder != i)
            {
                b.SortOrder = i;
                b.UpdatedAt = now;
                b.UpdatedBy = operatorId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // ── 內部 ───────────────────────────────────────────

    private static void Validate(UpsertAdminBenefitRequest request)
    {
        AdminInput.OneOf(request.Group, GroupLabels.Keys.ToHashSet(), "分組", "「會員卡」「店家折扣」「球衣」或「活動」");
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「上架」或「下架」");
        AdminInput.OptionalNonNegative(request.SortOrder, "排序");
        ValidateLocale(request.Content.Zh, "中文");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            ValidateLocale(request.Content.En, "英文");
        }
    }

    private static void ValidateLocale(AdminBenefitLocaleContent content, string prefix)
    {
        AdminInput.RequireText(content.Name, $"{prefix}條目名稱", 128);
        AdminInput.OptionalText(content.FreeValue, $"{prefix}免費層對應值", 255);
        AdminInput.OptionalText(content.PaidValue, $"{prefix}付費層對應值", 255);
    }

    private async Task<MembershipPlan> ResolvePlanAsync(AdminClubScope scope, Guid planId, CancellationToken cancellationToken)
        => await db.MembershipPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId && p.ClubId == scope.ClubId, cancellationToken)
           ?? throw new AdminValidationException("找不到指定的方案，請確認方案屬於目前的俱樂部。");

    private void SetI18n(MembershipBenefit benefit, UpsertAdminBenefitRequest request)
    {
        var (labelZh, labelEn) = GroupLabels[request.Group];
        Upsert(benefit, RequestLocale.DefaultDbLocale, request.Content.Zh, labelZh);
        var en = benefit.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            Upsert(benefit, "en", request.Content.En, labelEn);
        }
        else if (en is not null)
        {
            db.Remove(en);
        }
    }

    private void Upsert(MembershipBenefit benefit, string locale, AdminBenefitLocaleContent content, string groupLabel)
    {
        var row = benefit.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new MembershipBenefitsI18n { MembershipBenefitId = benefit.Id, Locale = locale };
            benefit.MembershipBenefitsI18ns.Add(row);
            db.MembershipBenefitsI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description;
        row.FreeValue = string.IsNullOrWhiteSpace(content.FreeValue) ? null : content.FreeValue.Trim();
        row.PaidValue = string.IsNullOrWhiteSpace(content.PaidValue) ? null : content.PaidValue.Trim();
        row.GroupLabel = groupLabel;
    }

    public static string StatusLabel(string status) => status == "published" ? "上架" : "下架";

    private static AdminBenefitDetailDto ToDetail(MembershipBenefit b)
    {
        var zh = b.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = b.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == "en");
        var planName = b.MembershipPlan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name;
        return new AdminBenefitDetailDto
        {
            Id = b.Id, PlanId = b.MembershipPlanId, PlanCode = b.MembershipPlan.Code, PlanName = planName, SeasonCode = b.MembershipPlan.Season.Code,
            Group = b.BenefitGroup, GroupLabel = GroupLabels.TryGetValue(b.BenefitGroup, out var gl) ? gl.Zh : b.BenefitGroup, SortOrder = b.SortOrder,
            Status = b.Status, StatusLabel = StatusLabel(b.Status),
            Zh = new AdminBenefitLocaleContent { Name = zh?.Name ?? "", Description = zh?.Description, FreeValue = zh?.FreeValue, PaidValue = zh?.PaidValue },
            En = en is null ? null : new AdminBenefitLocaleContent { Name = en.Name ?? "", Description = en.Description, FreeValue = en.FreeValue, PaidValue = en.PaidValue },
            CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt,
        };
    }
}
