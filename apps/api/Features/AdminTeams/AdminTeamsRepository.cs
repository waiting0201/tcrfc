using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminTeams;

// S1-7 新增：本檔案同時保留 J4 球隊授權下拉選單的唯讀查詢（下方 AdminTeamsRepository 類別開頭）
// 與新增的 C1 球隊管理俱樂部範圍 CRUD（ListForClubAsync 以下）。兩組方法都圍繞 teams 這張表，
// 分開成兩個 repository 只會製造「同一張表兩個維護入口」的假象，故意合併在同一個類別。

/// <summary>J4「球隊授權」（<c>admin_user_teams</c>）畫面的唯讀支援資料——只有一個查詢，
/// **以及 S1-7 新增的 C1 球隊管理俱樂部範圍 CRUD**（見下方 <c>ListForClubAsync</c> 以下）。
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——S1-7 同時新增了公開唯讀端點
/// <c>Features/Teams/TeamsEndpoints.cs</c>（entity="teams"），寫入這裡卻不失效會讓公開頁面
/// 在 TTL 到期前一直顯示舊資料，是 docs/17 §4「五條實作硬規則」之一，逐字比照
/// <c>AdminArticlesRepository.InvalidatePublicCacheAsync</c> 的既有做法。</summary>
public sealed class AdminTeamsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    /// <summary>不分俱樂部列出全部球隊——見 <c>AdminTeamsEndpoints</c> 檔頭「跨俱樂部」的說明。
    /// <paramref name="clubCode"/> 給定時縮小到單一俱樂部（畫面已經選定俱樂部時可以少拉一點資料，
    /// 不給就是完整跨俱樂部清單）。</summary>
    public async Task<IReadOnlyList<AdminTeamListItemDto>> ListAsync(string? clubCode, CancellationToken cancellationToken)
    {
        var query = dbContext.Teams.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(clubCode))
        {
            query = query.Where(t => t.Club.Code == clubCode);
        }

        var rows = await query
            .OrderBy(t => t.Club.SortOrder).ThenBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.ClubId,
                ClubCode = t.Club.Code,
                ClubNameZh = t.Club.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                t.Code,
                t.Type,
                t.Gender,
                t.AgeBand,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = t.TeamsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminTeamListItemDto
        {
            Id = r.Id,
            ClubId = r.ClubId,
            ClubCode = r.ClubCode,
            ClubNameZh = r.ClubNameZh,
            Code = r.Code,
            Type = r.Type,
            Gender = r.Gender,
            AgeBand = r.AgeBand,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
        }).ToList();
    }

    // ───────────────────────────── C1 球隊管理（俱樂部範圍 CRUD，S1-7 新增） ─────────────────────────────

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal) { "first_team", "academy" };
    private static readonly HashSet<string> AllowedGenders = new(StringComparer.Ordinal) { "men", "women", "mixed" };

    public async Task<IReadOnlyList<AdminTeamAdminListItemDto>> ListForClubAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ClubId == scope.ClubId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Type,
                t.Gender,
                t.AgeBand,
                t.TeamColor,
                t.HeroKey,
                t.SortOrder,
                t.UpdatedAt,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = t.TeamsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminTeamAdminListItemDto
        {
            Id = r.Id,
            Code = r.Code,
            Type = r.Type,
            Gender = r.Gender,
            AgeBand = r.AgeBand,
            TeamColor = r.TeamColor,
            HeroKey = r.HeroKey,
            SortOrder = r.SortOrder,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminTeamDetailDto?> GetForClubAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.AsNoTracking()
            .Include(t => t.TeamsI18ns)
            .FirstOrDefaultAsync(t => t.Id == id && t.ClubId == scope.ClubId, cancellationToken);

        return team is null ? null : ToDetailDto(team);
    }

    /// <summary>
    /// 🔴 建立一律歸屬 <paramref name="scope"/> 當下的俱樂部（<c>teams.club_id</c> 必填，不像
    /// <c>staff</c> 有「共同」語意），不接受呼叫端指定其他俱樂部——見 <c>AdminArticlesRepository.CreateAsync</c>
    /// 同一種寫法的說明。<paramref name="teamId"/> 由端點先決定好（供圖片物件鍵組字串使用，
    /// 比照 <c>AdminArticlesEndpoints</c> 的模式），不是資料庫自動產生後才知道。
    /// </summary>
    public async Task<AdminTeamDetailDto> CreateAsync(
        AdminClubScope scope, Guid teamId, CreateAdminTeamRequest request, string? heroKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateType(request.Type);
        ValidateGender(request.Gender);
        ValidateContent(request.Content);

        if (await dbContext.Teams.AsNoTracking().AnyAsync(t => t.Code == request.Code, cancellationToken))
        {
            throw new AdminTeamCodeConflictException(request.Code);
        }

        if (request.Type == "first_team")
        {
            await EnsureNoOtherFirstTeamAsync(scope, excludeTeamId: null, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var team = new Team
        {
            Id = teamId,
            ClubId = scope.ClubId,
            Code = request.Code,
            Type = request.Type,
            Gender = request.Gender,
            AgeBand = request.AgeBand,
            TeamColor = request.TeamColor,
            HeroKey = heroKey,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Teams.Add(team);
        AddOrReplaceI18n(team, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(team, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("teams", scope.ClubCode, cancellationToken);
        return (await GetForClubAsync(scope, team.Id, cancellationToken))!;
    }

    public async Task<AdminTeamDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminTeamRequest request, HeroKeyUpdate heroUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateType(request.Type);
        ValidateGender(request.Gender);
        ValidateContent(request.Content);

        var team = await dbContext.Teams
            .Include(t => t.TeamsI18ns)
            .FirstOrDefaultAsync(t => t.Id == id && t.ClubId == scope.ClubId, cancellationToken);

        if (team is null)
        {
            return null;
        }

        if (!string.Equals(team.Code, request.Code, StringComparison.Ordinal)
            && await dbContext.Teams.AsNoTracking().AnyAsync(t => t.Code == request.Code && t.Id != id, cancellationToken))
        {
            throw new AdminTeamCodeConflictException(request.Code);
        }

        if (request.Type == "first_team")
        {
            await EnsureNoOtherFirstTeamAsync(scope, excludeTeamId: id, cancellationToken);
        }

        team.Code = request.Code;
        team.Type = request.Type;
        team.Gender = request.Gender;
        team.AgeBand = request.AgeBand;
        team.TeamColor = request.TeamColor;
        team.SortOrder = request.SortOrder;
        team.UpdatedAt = DateTime.UtcNow;
        team.UpdatedBy = operatorId;

        if (heroUpdate.Change)
        {
            team.HeroKey = heroUpdate.NewKey;
        }

        AddOrReplaceI18n(team, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = team.TeamsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(team, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("teams", scope.ClubCode, cancellationToken);
        return await GetForClubAsync(scope, id, cancellationToken);
    }

    /// <summary>主站規劃書 §4.3 C1：「<c>type = first_team</c> 為每個俱樂部至多一筆」。
    /// 這裡是資料庫沒有 CHECK／唯一索引可以擋（一線隊之外還有多筆 <c>academy</c> 列，
    /// 無法用一個唯一鍵表達「至多一筆」這種條件式約束）的地方，故在 API 層檢查。</summary>
    private async Task EnsureNoOtherFirstTeamAsync(AdminClubScope scope, Guid? excludeTeamId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ClubId == scope.ClubId && t.Type == "first_team")
            .Where(t => excludeTeamId == null || t.Id != excludeTeamId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            throw new AdminTeamFirstTeamAlreadyExistsException(existing);
        }
    }

    private void AddOrReplaceI18n(Team team, string locale, AdminTeamLocaleContent content)
    {
        var existing = team.TeamsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new TeamsI18n { TeamId = team.Id, Locale = locale };
            team.TeamsI18ns.Add(existing);
            dbContext.TeamsI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Intro = content.Intro;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminTeamValidationException("隊別代號為必填欄位。");
        }
        if (code.Length > 8)
        {
            throw new AdminTeamValidationException("隊別代號長度不能超過 8 個字元（資料庫欄位上限）。");
        }
    }

    private static void ValidateType(string type)
    {
        if (!AllowedTypes.Contains(type))
        {
            throw new AdminTeamValidationException("球隊類型只能是「first_team」（一線隊）或「academy」（學院梯隊）。");
        }
    }

    private static void ValidateGender(string gender)
    {
        if (!AllowedGenders.Contains(gender))
        {
            throw new AdminTeamValidationException("性別欄位只能是「men」「women」或「mixed」。");
        }
    }

    private static void ValidateContent(AdminTeamContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminTeamValidationException("中文名稱為必填欄位。");
        }
    }

    private static AdminTeamDetailDto ToDetailDto(Team team)
    {
        var zh = team.TeamsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = team.TeamsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminTeamDetailDto
        {
            Id = team.Id,
            Code = team.Code,
            Type = team.Type,
            Gender = team.Gender,
            AgeBand = team.AgeBand,
            TeamColor = team.TeamColor,
            HeroKey = team.HeroKey,
            SortOrder = team.SortOrder,
            Zh = new AdminTeamLocaleContent { Name = zh?.Name ?? team.Code, Intro = zh?.Intro },
            En = en is null ? null : new AdminTeamLocaleContent { Name = en.Name ?? "", Intro = en.Intro },
            CreatedAt = team.CreatedAt,
            UpdatedAt = team.UpdatedAt,
        };
    }
}
