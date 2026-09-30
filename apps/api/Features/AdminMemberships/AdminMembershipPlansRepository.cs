using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMemberships;

/// <summary>
/// K2 會籍方案（規劃書 §4.11 K2「方案設定」）。<c>membership_plans.club_id</c> 必填：兩隊的費用、發卡數與球季規則各自獨立，
/// 所有查詢一律 <c>club_id = scope.ClubId</c>，跨俱樂部的 id 一律 404。方案一旦有會籍使用就不能刪除或更換球季
/// （歷史付款紀錄仍指向它）。
/// </summary>
public sealed class AdminMembershipPlansRepository(ClubDbContext db)
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };
    private static readonly System.Text.RegularExpressions.Regex CodeFormat = new("^[a-z0-9]+(-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string StatusLabel(string status) => status == "published" ? "上架" : "下架";

    public async Task<IReadOnlyList<AdminPlanListItemDto>> ListAsync(
        AdminClubScope scope, Guid? seasonId, string? status, CancellationToken cancellationToken)
    {
        var query = db.MembershipPlans.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (seasonId is Guid s)
        {
            query = query.Where(p => p.SeasonId == s);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「上架」或「下架」");
            query = query.Where(p => p.Status == status);
        }

        var rows = await query.OrderByDescending(p => p.Season.StartOn).ThenBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .Select(p => new
            {
                p.Id, p.SeasonId, SeasonCode = p.Season.Code, p.Code, p.Fee, p.CardQuota, p.JerseyQuota, p.StartsOn, p.EndsOn,
                p.SortOrder, p.Status, p.UpdatedAt,
                NameZh = p.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.MembershipPlansI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                MembershipCount = p.Memberships.Count,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminPlanListItemDto
        {
            Id = r.Id, SeasonId = r.SeasonId, SeasonCode = r.SeasonCode, Code = r.Code, Fee = r.Fee, CardQuota = r.CardQuota,
            JerseyQuota = r.JerseyQuota, StartsOn = r.StartsOn, EndsOn = r.EndsOn, SortOrder = r.SortOrder, Status = r.Status,
            StatusLabel = StatusLabel(r.Status), NameZh = r.NameZh, NameEn = r.NameEn, MembershipCount = r.MembershipCount, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminPlanDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var plan = await db.MembershipPlans.AsNoTracking()
            .Include(p => p.MembershipPlansI18ns).Include(p => p.Season)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var count = await db.Memberships.AsNoTracking().CountAsync(m => m.MembershipPlanId == id, cancellationToken);
        return ToDetail(plan, count);
    }

    public async Task<AdminPlanDetailDto> CreateAsync(
        AdminClubScope scope, UpsertAdminPlanRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var v = Validate(request);
        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);
        await EnsureCodeFreeAsync(scope, season.Id, v.Code, null, cancellationToken);

        var now = DateTime.UtcNow;
        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, SeasonId = season.Id, Code = v.Code, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(plan, request);
        db.MembershipPlans.Add(plan);
        SetI18n(plan, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, plan.Id, cancellationToken))!;
    }

    public async Task<AdminPlanDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPlanRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var v = Validate(request);
        var plan = await db.MembershipPlans.Include(p => p.MembershipPlansI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);
        var inUse = await db.Memberships.AsNoTracking().AnyAsync(m => m.MembershipPlanId == id, cancellationToken)
            || await db.MembershipPayments.AsNoTracking().AnyAsync(p => p.MembershipPlanId == id, cancellationToken);
        if (inUse && season.Id != plan.SeasonId)
        {
            throw new AdminValidationException("已經有會員使用這個方案，不能更換球季。請新增一個新球季的方案。");
        }

        if (v.Code != plan.Code || season.Id != plan.SeasonId)
        {
            await EnsureCodeFreeAsync(scope, season.Id, v.Code, id, cancellationToken);
        }

        plan.SeasonId = season.Id;
        plan.Code = v.Code;
        Apply(plan, request);
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = operatorId;
        SetI18n(plan, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (plan is null)
        {
            return false;
        }

        if (await db.Memberships.AsNoTracking().AnyAsync(m => m.MembershipPlanId == id, cancellationToken)
            || await db.MembershipPayments.AsNoTracking().AnyAsync(p => p.MembershipPlanId == id, cancellationToken))
        {
            throw new AdminConflictException("方案仍被使用", "已經有會員使用這個方案（含歷史付款紀錄），不能刪除。可以改為「下架」讓它不再對外顯示。");
        }

        // 權益對照條目掛在方案底下（側表由資料庫串聯刪除）。
        var benefits = await db.MembershipBenefits.Where(b => b.MembershipPlanId == id).ToListAsync(cancellationToken);
        db.MembershipBenefits.RemoveRange(benefits);
        db.MembershipPlans.Remove(plan);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>依 <paramref name="ids"/> 順序重排（sortOrder 0,1,2…），未列入的方案排在其後、相對順序不變。</summary>
    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var plans = await db.MembershipPlans.Where(p => p.ClubId == scope.ClubId).OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .ToListAsync(cancellationToken);
        var byId = plans.ToDictionary(p => p.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的方案，請重新整理後再試。");
        }

        var order = ids.Concat(plans.Select(p => p.Id).Where(i => !ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var p = byId[order[i]];
            if (p.SortOrder != i)
            {
                p.SortOrder = i;
                p.UpdatedAt = now;
                p.UpdatedBy = operatorId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record Validated(string Code);

    private static Validated Validate(UpsertAdminPlanRequest request)
    {
        var code = AdminInput.RequireText(request.Code, "方案代碼", 32);
        if (!CodeFormat.IsMatch(code))
        {
            throw new AdminValidationException("方案代碼只能使用小寫英文字母、數字與連字號（-），開頭與結尾不能是連字號。");
        }

        if (request.Fee < 0)
        {
            throw new AdminValidationException("費用不可為負數。");
        }

        if (request.CardQuota is < 1 or > 10)
        {
            throw new AdminValidationException("發卡數請填 1 到 10。");
        }

        if (request.JerseyQuota is < 0 or > 10)
        {
            throw new AdminValidationException("含球衣件數請填 0 到 10。");
        }

        AdminInput.OptionalText(request.MidSeasonRule, "季中入會計價規則", 255);
        AdminInput.DateRange(request.StartsOn, request.EndsOn, "方案期間");
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「上架」或「下架」");
        AdminInput.RequireText(request.Content.Zh.Name, "中文方案名稱", 64);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文方案名稱", 64);
        }

        return new Validated(code);
    }

    private static void Apply(MembershipPlan plan, UpsertAdminPlanRequest request)
    {
        plan.Fee = request.Fee;
        plan.CardQuota = request.CardQuota;
        plan.JerseyQuota = request.JerseyQuota;
        plan.MidSeasonRule = AdminInput.OptionalText(request.MidSeasonRule, "季中入會計價規則", 255);
        plan.StartsOn = request.StartsOn;
        plan.EndsOn = request.EndsOn;
        plan.SortOrder = request.SortOrder;
        plan.Status = request.Status;
    }

    private void SetI18n(MembershipPlan plan, AdminPlanContentInput content)
    {
        Upsert(plan, RequestLocale.DefaultDbLocale, content.Zh);
        var en = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(plan, "en", content.En);
        }
        else if (en is not null)
        {
            db.Remove(en);
        }
    }

    private void Upsert(MembershipPlan plan, string locale, AdminPlanLocaleContent content)
    {
        var row = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new MembershipPlansI18n { MembershipPlanId = plan.Id, Locale = locale };
            plan.MembershipPlansI18ns.Add(row);
            db.MembershipPlansI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.BenefitNote = string.IsNullOrWhiteSpace(content.BenefitNote) ? null : content.BenefitNote;
    }

    private async Task<Season> ResolveSeasonAsync(AdminClubScope scope, Guid seasonId, CancellationToken cancellationToken)
        => await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId && s.ClubId == scope.ClubId, cancellationToken)
           ?? throw new AdminValidationException("找不到指定的球季，請確認球季屬於目前的俱樂部。");

    private async Task EnsureCodeFreeAsync(AdminClubScope scope, Guid seasonId, string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.MembershipPlans.AsNoTracking().AnyAsync(
                p => p.ClubId == scope.ClubId && p.SeasonId == seasonId && p.Code == code && p.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("方案代碼重複", $"這個球季已經有代碼為「{code}」的方案了，請換一個代碼。");
        }
    }

    private static AdminPlanDetailDto ToDetail(MembershipPlan plan, int membershipCount)
    {
        var zh = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminPlanDetailDto
        {
            Id = plan.Id, SeasonId = plan.SeasonId, SeasonCode = plan.Season.Code, Code = plan.Code, Fee = plan.Fee,
            CardQuota = plan.CardQuota, JerseyQuota = plan.JerseyQuota, MidSeasonRule = plan.MidSeasonRule,
            StartsOn = plan.StartsOn, EndsOn = plan.EndsOn, SortOrder = plan.SortOrder, Status = plan.Status,
            StatusLabel = StatusLabel(plan.Status),
            Zh = new AdminPlanLocaleContent { Name = zh?.Name ?? "", BenefitNote = zh?.BenefitNote },
            En = en is null ? null : new AdminPlanLocaleContent { Name = en.Name ?? "", BenefitNote = en.BenefitNote },
            MembershipCount = membershipCount, CreatedAt = plan.CreatedAt, UpdatedAt = plan.UpdatedAt,
        };
    }
}
