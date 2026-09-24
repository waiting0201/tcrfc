using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCompetitions;

/// <summary>J4「Competition 型別的後台維護端點」——俱樂部範圍（<c>competitions.club_id</c> 必填，
/// 主站規劃書 §5.4），比照 <c>Features/AdminNews</c> 用 <see cref="AdminClubScope"/>。</summary>
public sealed class AdminCompetitionsRepository(ClubDbContext dbContext)
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal) { "draft", "published" };

    public async Task<IReadOnlyList<AdminCompetitionListItemDto>> ListAsync(
        AdminClubScope scope, Guid? seasonId, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Competitions.AsNoTracking().Where(c => c.ClubId == scope.ClubId);

        if (seasonId is Guid s)
        {
            query = query.Where(c => c.SeasonId == s);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var rows = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(c => new
            {
                c.Id,
                c.SeasonId,
                SeasonCode = c.Season.Code,
                c.Code,
                c.CompType,
                c.SortOrder,
                c.Status,
                c.UpdatedAt,
                NameZh = c.CompetitionsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = c.CompetitionsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminCompetitionListItemDto
        {
            Id = r.Id,
            SeasonId = r.SeasonId,
            SeasonCode = r.SeasonCode,
            Code = r.Code,
            CompType = r.CompType,
            SortOrder = r.SortOrder,
            Status = r.Status,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminCompetitionDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var competition = await dbContext.Competitions.AsNoTracking()
            .Include(c => c.Season)
            .Include(c => c.CompetitionsI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);

        return competition is null ? null : ToDetailDto(competition);
    }

    public async Task<AdminCompetitionDetailDto> CreateAsync(
        AdminClubScope scope, CreateAdminCompetitionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateStatus(request.Status);
        ValidateContent(request.Content);
        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);

        if (await dbContext.Competitions.AsNoTracking().AnyAsync(
                c => c.ClubId == scope.ClubId && c.Code == request.Code, cancellationToken))
        {
            throw new AdminCompetitionCodeConflictException(request.Code);
        }

        var now = DateTime.UtcNow;
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            ClubId = scope.ClubId,
            SeasonId = season.Id,
            Code = request.Code,
            CompType = request.CompType,
            SortOrder = request.SortOrder,
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Competitions.Add(competition);
        AddOrReplaceI18n(competition, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(competition, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, competition.Id, cancellationToken))!;
    }

    public async Task<AdminCompetitionDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminCompetitionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateStatus(request.Status);
        ValidateContent(request.Content);

        var competition = await dbContext.Competitions
            .Include(c => c.CompetitionsI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);

        if (competition is null)
        {
            return null;
        }

        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);

        if (!string.Equals(competition.Code, request.Code, StringComparison.Ordinal)
            && await dbContext.Competitions.AsNoTracking().AnyAsync(
                c => c.ClubId == scope.ClubId && c.Code == request.Code && c.Id != id, cancellationToken))
        {
            throw new AdminCompetitionCodeConflictException(request.Code);
        }

        competition.SeasonId = season.Id;
        competition.Code = request.Code;
        competition.CompType = request.CompType;
        competition.SortOrder = request.SortOrder;
        competition.Status = request.Status;
        competition.UpdatedAt = DateTime.UtcNow;
        competition.UpdatedBy = operatorId;

        AddOrReplaceI18n(competition, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = competition.CompetitionsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(competition, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    private async Task<Season> ResolveSeasonAsync(AdminClubScope scope, Guid seasonId, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
        if (season is null || season.ClubId != scope.ClubId)
        {
            throw new AdminCompetitionValidationException($"找不到這個俱樂部的球季（id={seasonId}）。");
        }
        return season;
    }

    private void AddOrReplaceI18n(Competition competition, string locale, AdminCompetitionLocaleContent content)
    {
        var existing = competition.CompetitionsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new CompetitionsI18n { CompetitionId = competition.Id, Locale = locale };
            competition.CompetitionsI18ns.Add(existing);
            dbContext.CompetitionsI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Organizer = content.Organizer;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminCompetitionValidationException("賽事系列代號為必填欄位。");
        }
        if (code.Length > 16)
        {
            throw new AdminCompetitionValidationException("賽事系列代號長度不能超過 16 個字元。");
        }
    }

    private static void ValidateStatus(string status)
    {
        if (!AllowedStatuses.Contains(status))
        {
            throw new AdminCompetitionValidationException(
                "狀態只能是「draft」（草稿）或「published」（已發布）——這個型別不支援排程發布" +
                "（docs/14-invariants.md「S0-7g」：沒有 published_at 欄位可以記排定時間）。");
        }
    }

    private static void ValidateContent(AdminCompetitionContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminCompetitionValidationException("中文名稱為必填欄位。");
        }
    }

    private static AdminCompetitionDetailDto ToDetailDto(Competition competition)
    {
        var zh = competition.CompetitionsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = competition.CompetitionsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminCompetitionDetailDto
        {
            Id = competition.Id,
            SeasonId = competition.SeasonId,
            SeasonCode = competition.Season.Code,
            Code = competition.Code,
            CompType = competition.CompType,
            SortOrder = competition.SortOrder,
            Status = competition.Status,
            Zh = new AdminCompetitionLocaleContent { Name = zh?.Name ?? competition.Code, Organizer = zh?.Organizer },
            En = en is null ? null : new AdminCompetitionLocaleContent { Name = en.Name, Organizer = en.Organizer },
            CreatedAt = competition.CreatedAt,
            UpdatedAt = competition.UpdatedAt,
        };
    }
}
