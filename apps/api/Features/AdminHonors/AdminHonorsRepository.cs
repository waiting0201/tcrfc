using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminHonors;

/// <summary>
/// C5「榮譽與里程碑」（主站規劃書 §4.3 C5，產出前台 02 關於台中磐石的榮譽與里程碑時間軸）。
/// <c>achievements.club_id</c>／<c>milestones.club_id</c> 皆必填，無共同列。
/// 🔴 榮譽帶 <c>team_id</c>，套用 C 模組既有的列級授權（<see cref="TeamRowScope"/>，權限碼 <c>team.achievement.*</c>：
/// 學院／課程管理只能寫學院梯隊的榮譽）；里程碑是俱樂部層級的時間軸，沒有球隊維度，不套列級授權
/// （權限碼 <c>team.milestone.*</c> 因此不指派給學院／課程管理，理由同 C4 積分榜）。
/// </summary>
public sealed class AdminHonorsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public const string CacheEntity = "honors";

    // ── 榮譽 ───────────────────────────────────────────────────────

    public Task<IReadOnlyList<AdminAchievementDto>> ListAchievementsAsync(
        AdminClubScope scope, Guid? teamId, Guid? seasonId, int? year, CancellationToken cancellationToken)
        => QueryAchievementsAsync(scope, null, teamId, seasonId, year, cancellationToken);

    private async Task<IReadOnlyList<AdminAchievementDto>> QueryAchievementsAsync(
        AdminClubScope scope, Guid? id, Guid? teamId, Guid? seasonId, int? year, CancellationToken cancellationToken)
    {
        var query = dbContext.Achievements.AsNoTracking().Where(a => a.ClubId == scope.ClubId);
        if (id is Guid i) { query = query.Where(a => a.Id == i); }
        if (teamId is Guid t) { query = query.Where(a => a.TeamId == t); }
        if (seasonId is Guid s) { query = query.Where(a => a.SeasonId == s); }
        if (year is int y) { query = query.Where(a => a.Year == y); }

        var rows = await query.OrderByDescending(a => a.Year).ThenByDescending(a => a.RowSeq)
            .Select(a => new
            {
                a.Id, a.SeasonId, SeasonCode = a.Season.Code, a.TeamId, TeamCode = a.Team.Code, a.Year, a.CompetitionName, a.Placing, a.UpdatedAt,
                TeamNameZh = a.Team.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        return rows.Select(r => new AdminAchievementDto
        {
            Id = r.Id, SeasonId = r.SeasonId, SeasonCode = r.SeasonCode, TeamId = r.TeamId, TeamCode = r.TeamCode, TeamNameZh = r.TeamNameZh,
            Year = r.Year, CompetitionName = r.CompetitionName, Placing = r.Placing, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminAchievementDto?> GetAchievementAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
        => (await QueryAchievementsAsync(scope, id, null, null, null, cancellationToken)).FirstOrDefault();

    public async Task<AdminAchievementDto> CreateAchievementAsync(
        AdminClubScope scope, TeamRowScope rowScope, UpsertAdminAchievementRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (season, team) = await ValidateAchievementAsync(scope, request, cancellationToken);
        if (!rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許建立這支球隊的榮譽。");
        }

        var now = DateTime.UtcNow;
        var achievement = new Achievement
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, SeasonId = season.Id, TeamId = team.Id,
            Year = request.Year ?? season.StartOn.Year,
            CompetitionName = request.CompetitionName.Trim(), Placing = request.Placing.Trim(),
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        dbContext.Achievements.Add(achievement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetAchievementAsync(scope, achievement.Id, cancellationToken))!;
    }

    public async Task<AdminAchievementDto?> UpdateAchievementAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid id, UpsertAdminAchievementRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (season, team) = await ValidateAchievementAsync(scope, request, cancellationToken);
        var achievement = await dbContext.Achievements.Include(a => a.Team)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClubId == scope.ClubId, cancellationToken);
        if (achievement is null)
        {
            return null;
        }

        // 既有的球隊與新指派的球隊都要在授權範圍內（防止把範圍內的榮譽改掛到範圍外的球隊逃脫限制）。
        if (!rowScope.Allows(achievement.TeamId, achievement.Team.Type) || !rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許修改這筆榮譽。");
        }

        achievement.SeasonId = season.Id;
        achievement.TeamId = team.Id;
        achievement.Year = request.Year ?? season.StartOn.Year;
        achievement.CompetitionName = request.CompetitionName.Trim();
        achievement.Placing = request.Placing.Trim();
        achievement.UpdatedAt = DateTime.UtcNow;
        achievement.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return await GetAchievementAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAchievementAsync(AdminClubScope scope, TeamRowScope rowScope, Guid id, CancellationToken cancellationToken)
    {
        var achievement = await dbContext.Achievements.Include(a => a.Team)
            .FirstOrDefaultAsync(a => a.Id == id && a.ClubId == scope.ClubId, cancellationToken);
        if (achievement is null)
        {
            return false;
        }

        if (!rowScope.Allows(achievement.TeamId, achievement.Team.Type))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許刪除這筆榮譽。");
        }

        dbContext.Achievements.Remove(achievement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    private async Task<(Season Season, Team Team)> ValidateAchievementAsync(
        AdminClubScope scope, UpsertAdminAchievementRequest request, CancellationToken cancellationToken)
    {
        AdminInput.RequireText(request.CompetitionName, "賽事名稱", 128, "competitionName");
        AdminInput.RequireText(request.Placing, "名次", 32, "placing");
        if (request.Year is < 1900 or > 2200)
        {
            throw new AdminValidationException("年份必須是合理的西元年份。", "year");
        }

        var season = await dbContext.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SeasonId && s.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到這個俱樂部的球季，請重新選擇。", "seasonId");
        var team = await dbContext.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TeamId && t.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到這個俱樂部的球隊，請重新選擇。", "teamId");
        return (season, team);
    }

    // ── 里程碑 ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminMilestoneDto>> ListMilestonesAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Milestones.AsNoTracking().Include(m => m.MilestonesI18ns)
            .Where(m => m.ClubId == scope.ClubId).OrderBy(m => m.HappenedOn).ThenBy(m => m.SortOrder).ThenBy(m => m.RowSeq)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminMilestoneDto?> GetMilestoneAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.Milestones.AsNoTracking().Include(m => m.MilestonesI18ns)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);
        return milestone is null ? null : ToDto(milestone);
    }

    public async Task<AdminMilestoneDto> CreateMilestoneAsync(
        AdminClubScope scope, Guid id, UpsertAdminMilestoneRequest request, UploadedImageInfo? image, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateMilestone(request);
        var now = DateTime.UtcNow;
        var milestone = new Milestone
        {
            Id = id, ClubId = scope.ClubId, HappenedOn = request.HappenedOn, SortOrder = request.SortOrder, IsVisible = request.IsVisible,
            ImageKey = image?.Key, ImageWidth = image?.Width, ImageHeight = image?.Height,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        dbContext.Milestones.Add(milestone);
        SetI18n(milestone, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetMilestoneAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminMilestoneDto?> UpdateMilestoneAsync(
        AdminClubScope scope, Guid id, UpsertAdminMilestoneRequest request, ImageFieldUpdate image, OrphanedObjects orphans,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateMilestone(request);
        var milestone = await dbContext.Milestones.Include(m => m.MilestonesI18ns)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);
        if (milestone is null)
        {
            return null;
        }

        if (image.Change)
        {
            orphans.Image(milestone.ImageKey);
            milestone.ImageKey = image.Key;
            milestone.ImageWidth = image.Width;
            milestone.ImageHeight = image.Height;
        }

        milestone.HappenedOn = request.HappenedOn;
        milestone.SortOrder = request.SortOrder;
        milestone.IsVisible = request.IsVisible;
        milestone.UpdatedAt = DateTime.UtcNow;
        milestone.UpdatedBy = operatorId;
        SetI18n(milestone, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return await GetMilestoneAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteMilestoneAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var milestone = await dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);
        if (milestone is null)
        {
            return false;
        }

        orphans.Image(milestone.ImageKey);
        dbContext.Milestones.Remove(milestone);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    private static void ValidateMilestone(UpsertAdminMilestoneRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.Title, "中文標題", 200, "titleZh");
        AdminInput.OptionalText(request.Content.Zh.ImageAlt, "圖片替代文字", 200, "altZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Title))
        {
            AdminInput.RequireText(request.Content.En.Title, "英文標題", 200, "titleEn");
            AdminInput.OptionalText(request.Content.En.ImageAlt, "圖片替代文字（英文）", 200, "altEn");
        }
    }

    private void SetI18n(Milestone milestone, AdminMilestoneContentInput content)
    {
        Upsert(milestone, RequestLocale.DefaultDbLocale, content.Zh);
        var en = milestone.MilestonesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Title))
        {
            Upsert(milestone, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(Milestone milestone, string locale, AdminMilestoneLocaleContent content)
    {
        var row = milestone.MilestonesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new MilestonesI18n { MilestoneId = milestone.Id, Locale = locale };
            milestone.MilestonesI18ns.Add(row);
            dbContext.MilestonesI18ns.Add(row);
        }

        row.Title = content.Title.Trim();
        row.Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description;
        row.ImageAlt = string.IsNullOrWhiteSpace(content.ImageAlt) ? null : content.ImageAlt.Trim();
    }

    private AdminMilestoneDto ToDto(Milestone milestone)
    {
        var zh = milestone.MilestonesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = milestone.MilestonesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminMilestoneDto
        {
            Id = milestone.Id, HappenedOn = milestone.HappenedOn, SortOrder = milestone.SortOrder, IsVisible = milestone.IsVisible,
            ImageKey = milestone.ImageKey, ImageUrl = imageUrls.Resolve(milestone.ImageKey),
            ImageThumbUrl = milestone.ImageKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(milestone.ImageKey)),
            ImageWidth = milestone.ImageWidth, ImageHeight = milestone.ImageHeight,
            Zh = new AdminMilestoneLocaleContent { Title = zh?.Title ?? "", Description = zh?.Description, ImageAlt = zh?.ImageAlt },
            En = en is null ? null : new AdminMilestoneLocaleContent { Title = en.Title ?? "", Description = en.Description, ImageAlt = en.ImageAlt },
            UpdatedAt = milestone.UpdatedAt,
        };
    }
}
