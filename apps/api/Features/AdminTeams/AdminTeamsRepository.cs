using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.Features.AdminTeams;

/// <summary>C1 球隊管理俱樂部範圍 CRUD（S1-7 新增）與「我能寫哪些球隊」唯讀查詢。
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——S1-7 同時新增了公開唯讀端點
/// <c>Features/Teams/TeamsEndpoints.cs</c>（entity="teams"），寫入這裡卻不失效會讓公開頁面
/// 在 TTL 到期前一直顯示舊資料，是 docs/17 §4「五條實作硬規則」之一，逐字比照
/// <c>AdminArticlesRepository.InvalidatePublicCacheAsync</c> 的既有做法。</summary>
public sealed class AdminTeamsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private static readonly string[] TeamDependentCacheEntities = ["players", "staff", "schedule", "honors", "calendar"];

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
                t.HeroWidth,
                t.HeroHeight,
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
            HeroUrl = imageUrls.Resolve(r.HeroKey),
            HeroThumbUrl = imageUrls.ResolveThumbnail(r.HeroKey),
            HeroWidth = r.HeroKey is null ? null : r.HeroWidth,
            HeroHeight = r.HeroKey is null ? null : r.HeroHeight,
            SortOrder = r.SortOrder,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    /// <summary>
    /// 「我能寫哪些球隊」下拉選單（<c>/api/v1/admin/{club}/teams/writable</c>）——依
    /// <paramref name="rowScope"/>（呼叫端已針對某個模組的寫入權限碼解析好的
    /// <see cref="TeamRowScope"/>）過濾成呼叫端真的 <c>Allows</c> 的球隊，不是整份清單加旗標。
    /// 見 <c>AdminTeamsEndpoints</c> 檔頭「為什麼是獨立端點不是加旗標」的完整取捨說明。
    /// **先查全部再用 <c>Allows</c> 逐筆過濾，不下推到 SQL**——這支查詢一個俱樂部最多幾十筆球隊，
    /// 不是效能敏感路徑，且 <see cref="TeamRowScope"/> 的判斷邏輯（<c>academy_only</c>）刻意只活在這一個型別裡，逐筆呼叫 <c>Allows</c> 比把同一套邏輯翻譯成
    /// LINQ／SQL 再維護兩份更安全。
    /// </summary>
    public async Task<IReadOnlyList<AdminWritableTeamDto>> ListWritableForClubAsync(
        AdminClubScope scope, TeamRowScope rowScope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Teams.AsNoTracking()
            .Where(t => t.ClubId == scope.ClubId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Type,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = t.TeamsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => rowScope.Allows(r.Id, r.Type))
            .Select(r => new AdminWritableTeamDto
            {
                Id = r.Id,
                Code = r.Code,
                Type = r.Type,
                NameZh = r.NameZh,
                NameEn = r.NameEn,
            })
            .ToList();
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
        AdminClubScope scope, TeamRowScope rowScope, Guid teamId, CreateAdminTeamRequest request, string? heroKey, int? heroWidth, int? heroHeight, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateType(request.Type);
        ValidateGender(request.Gender);
        ValidateContent(request.Content);

        // 🔴 S1-8 新增：列級授權——見 TeamRowScope.AllowsCreatingTeamOfType 上的說明，
        // academy_only 範圍的帳號只能新建 academy 類型的球隊。
        if (!rowScope.AllowsCreatingTeamOfType(request.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許新建這個類型的球隊。");
        }

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
            HeroWidth = heroKey is null ? null : heroWidth,
            HeroHeight = heroKey is null ? null : heroHeight,
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
        AdminClubScope scope, TeamRowScope rowScope, Guid id, UpdateAdminTeamRequest request, HeroKeyUpdate heroUpdate, Guid? operatorId, CancellationToken cancellationToken)
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

        // 🔴 S1-8 新增：既有球隊本身（目前的類型）要允許；若這次連類型都要改，新類型也必須通過
        // 「新建這個類型」的檢查——防止 academy_only 範圍的帳號把一支學院梯隊改成一線隊藉此逃脫
        // 範圍限制。**只有類型真的改變時才多做這層檢查**：維持既有類型不變的更新不該被
        // 「新建球隊」的類型檢查擋下。
        var changingType = !string.Equals(team.Type, request.Type, StringComparison.Ordinal);
        if (!rowScope.Allows(team.Id, team.Type) || (changingType && !rowScope.AllowsCreatingTeamOfType(request.Type)))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許修改這支球隊。");
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
            team.HeroWidth = heroUpdate.NewKey is null ? null : heroUpdate.Width;
            team.HeroHeight = heroUpdate.NewKey is null ? null : heroUpdate.Height;
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
        // 球隊代碼、名稱、類型會直接出現在球員、職員、賽程、榮譽的公開回應裡（稽核 B-22），不一併失效就要等 TTL。
        foreach (var dependent in TeamDependentCacheEntities)
        {
            await cache.InvalidateAsync(dependent, scope.ClubCode, cancellationToken);
        }

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
        existing.HeroAlt = string.IsNullOrWhiteSpace(content.HeroAlt) ? null : content.HeroAlt.Trim();
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminTeamValidationException("隊別代號為必填欄位。", "code");
        }
        if (code.Length > 8)
        {
            throw new AdminTeamValidationException("隊別代號不可超過 8 個字元。", "code");
        }
    }

    private static void ValidateType(string type)
    {
        if (!AllowedTypes.Contains(type))
        {
            throw new AdminTeamValidationException("球隊類型只能選「一線隊」或「學院梯隊」。", "type");
        }
    }

    private static void ValidateGender(string gender)
    {
        if (!AllowedGenders.Contains(gender))
        {
            throw new AdminTeamValidationException("性別只能選「男子」「女子」或「男女混合」。", "gender");
        }
    }

    private static void ValidateContent(AdminTeamContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminTeamValidationException("中文名稱為必填欄位。", FieldKey.Bi("name", "zh"));
        }

        if ((content.Zh.HeroAlt?.Trim().Length ?? 0) > 200)
        {
            throw new AdminTeamValidationException("主視覺圖片替代文字（中文）不可超過 200 字。", FieldKey.Bi("heroAlt", "zh"));
        }

        if ((content.En?.HeroAlt?.Trim().Length ?? 0) > 200)
        {
            throw new AdminTeamValidationException("主視覺圖片替代文字（英文）不可超過 200 字。", FieldKey.Bi("heroAlt", "en"));
        }
    }

    private AdminTeamDetailDto ToDetailDto(Team team)
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
            HeroUrl = imageUrls.Resolve(team.HeroKey),
            HeroThumbUrl = imageUrls.ResolveThumbnail(team.HeroKey),
            HeroWidth = team.HeroKey is null ? null : team.HeroWidth,
            HeroHeight = team.HeroKey is null ? null : team.HeroHeight,
            SortOrder = team.SortOrder,
            Zh = new AdminTeamLocaleContent { Name = zh?.Name ?? team.Code, Intro = zh?.Intro, HeroAlt = zh?.HeroAlt },
            En = en is null ? null : new AdminTeamLocaleContent { Name = en.Name ?? "", Intro = en.Intro, HeroAlt = en.HeroAlt },
            CreatedAt = team.CreatedAt,
            UpdatedAt = team.UpdatedAt,
        };
    }
}
