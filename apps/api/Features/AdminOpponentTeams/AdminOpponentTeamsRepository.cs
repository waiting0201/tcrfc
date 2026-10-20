using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminOpponentTeams;

/// <summary>
/// C4 對手球隊庫（主站規劃書 v3.31）：俱樂部範圍（<c>opponent_teams.club_id</c> 必填）。隊名中文在基礎表 <c>name</c>、
/// 英文在 <c>opponent_teams_i18n</c>（en 列，空白＝刪列）；隊徽走圖片欄位組（<c>crest_key</c>／<c>crest_width</c>／<c>crest_height</c>）。
///
/// 🔴 刪除：被賽事（<c>matches.opponent_team_id</c>）或積分榜（<c>standings.opponent_team_id</c>）引用就 409
/// <c>opponent_team_in_use</c>，不連帶刪除、不自動解除引用。
/// 🔴 改名會同步到引用它的賽事的 <c>matches.opponent</c>（與英文覆寫列）——賽事儲存時本來就把隊名帶進 <c>matches.opponent</c>
/// 作為後備文字（行事曆、App、搜尋等讀 <c>matches.opponent</c> 的路徑才不會顯示舊名）。
/// 🔴 快取：<c>schedule</c>、<c>calendar</c> 失效（球員數據／積分榜公開端點不快取，直接查庫）。
/// </summary>
public sealed class AdminOpponentTeamsRepository(ClubDbContext dbContext, IImagePublicUrlResolver imageUrls, IQueryCache cache)
{
    private const int NameMaxLength = 128;

    public async Task<IReadOnlyList<AdminOpponentTeamListItemDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.OpponentTeams.AsNoTracking()
            .Where(o => o.ClubId == scope.ClubId)
            .Select(o => new
            {
                o.Id, o.Name, o.CrestKey, o.SortOrder, o.UpdatedAt,
                NameEn = o.OpponentTeamsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                MatchCount = o.Matches.Count,
                StandingCount = o.Standings.Count,
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => new AdminOpponentTeamListItemDto
            {
                Id = r.Id, Name = r.Name, NameEn = r.NameEn,
                CrestUrl = imageUrls.Resolve(r.CrestKey), CrestThumbUrl = imageUrls.ResolveThumbnail(r.CrestKey),
                SortOrder = r.SortOrder, UsageCount = r.MatchCount + r.StandingCount, UpdatedAt = r.UpdatedAt,
            }).ToList();
    }

    public async Task<AdminOpponentTeamDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var team = await dbContext.OpponentTeams.AsNoTracking().Include(o => o.OpponentTeamsI18ns)
            .FirstOrDefaultAsync(o => o.Id == id && o.ClubId == scope.ClubId, cancellationToken);
        return team is null ? null : await ToDetailAsync(team, cancellationToken);
    }

    /// <summary>建立：<paramref name="id"/> 由端點先產生（隊徽物件鍵的路徑要用到）。</summary>
    public async Task<AdminOpponentTeamDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminOpponentTeamRequest request, ImageFieldUpdate crest, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (name, nameEn) = Validate(request);
        await EnsureNameUniqueAsync(scope, name, excludeId: null, cancellationToken);

        var now = DateTime.UtcNow;
        var team = new OpponentTeam
        {
            Id = id, ClubId = scope.ClubId, Name = name, SortOrder = request.SortOrder,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        if (crest.Change)
        {
            team.CrestKey = crest.Key;
            team.CrestWidth = crest.Width;
            team.CrestHeight = crest.Height;
        }

        dbContext.OpponentTeams.Add(team);
        SetEnglishName(team, nameEn);
        await SaveAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await ToDetailAsync(team, cancellationToken);
    }

    public async Task<AdminOpponentTeamDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminOpponentTeamRequest request, ImageFieldUpdate crest, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var team = await dbContext.OpponentTeams.Include(o => o.OpponentTeamsI18ns)
            .FirstOrDefaultAsync(o => o.Id == id && o.ClubId == scope.ClubId, cancellationToken);
        if (team is null)
        {
            return null;
        }

        var (name, nameEn) = Validate(request);
        await EnsureNameUniqueAsync(scope, name, excludeId: id, cancellationToken);

        var renamed = !string.Equals(team.Name, name, StringComparison.Ordinal);
        team.Name = name;
        team.SortOrder = request.SortOrder;
        if (crest.Change)
        {
            orphans.Image(team.CrestKey);
            team.CrestKey = crest.Key;
            team.CrestWidth = crest.Width;
            team.CrestHeight = crest.Height;
        }

        SetEnglishName(team, nameEn);
        team.UpdatedAt = DateTime.UtcNow;
        team.UpdatedBy = operatorId;

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await SaveAsync(cancellationToken);
        if (renamed)
        {
            // 引用它的賽事與積分榜把後備文字（matches.opponent／standings_i18n）一併改成新隊名。
            await dbContext.Matches.Where(m => m.OpponentTeamId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Opponent, name), cancellationToken);
        }

        if (renamed)
        {
            await dbContext.StandingsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale && i.Standing.OpponentTeamId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.TeamName, name), cancellationToken);
        }

        if (nameEn is not null)
        {
            await dbContext.StandingsI18ns.Where(i => i.Locale == "en" && i.Standing.OpponentTeamId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.TeamName, nameEn), cancellationToken);
            await dbContext.MatchesI18ns.Where(i => i.Locale == "en" && i.Match.OpponentTeamId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Opponent, nameEn), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await ToDetailAsync(team, cancellationToken);
    }

    /// <summary>刪除；仍被賽事或積分榜引用丟 409 <c>opponent_team_in_use</c>。回傳 <c>false</c>＝找不到。成功時把隊徽物件鍵登記到 <paramref name="orphans"/>。</summary>
    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var team = await dbContext.OpponentTeams.Include(o => o.OpponentTeamsI18ns)
            .FirstOrDefaultAsync(o => o.Id == id && o.ClubId == scope.ClubId, cancellationToken);
        if (team is null)
        {
            return false;
        }

        var (matches, standings) = await CountUsageAsync(id, cancellationToken);
        if (matches + standings > 0)
        {
            throw new AdminOpponentTeamConflictException(
                "對手球隊使用中",
                $"這支球隊仍被 {matches} 場賽事、{standings} 筆積分榜使用，請先把那些資料改選其他球隊或改回文字，再刪除。",
                "opponent_team_in_use");
        }

        orphans.Image(team.CrestKey);
        dbContext.OpponentTeamsI18ns.RemoveRange(team.OpponentTeamsI18ns.ToList());
        dbContext.OpponentTeams.Remove(team);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 547 })
        {
            // 檢查後、刪除前剛好有人引用：外鍵擋下，回報同一種衝突。
            throw new AdminOpponentTeamConflictException("對手球隊使用中", "這支球隊剛被賽事或積分榜使用，無法刪除。", "opponent_team_in_use");
        }

        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    // ───────────────────────────── 內部 ─────────────────────────────

    private async Task<(int Matches, int Standings)> CountUsageAsync(Guid id, CancellationToken cancellationToken)
        => (await dbContext.Matches.CountAsync(m => m.OpponentTeamId == id, cancellationToken),
            await dbContext.Standings.CountAsync(s => s.OpponentTeamId == id, cancellationToken));

    private async Task<AdminOpponentTeamDetailDto> ToDetailAsync(OpponentTeam team, CancellationToken cancellationToken)
    {
        var (matches, standings) = await CountUsageAsync(team.Id, cancellationToken);
        return new AdminOpponentTeamDetailDto
        {
            Id = team.Id, Name = team.Name,
            NameEn = team.OpponentTeamsI18ns.FirstOrDefault(i => i.Locale == "en")?.Name,
            CrestUrl = imageUrls.Resolve(team.CrestKey), CrestThumbUrl = imageUrls.ResolveThumbnail(team.CrestKey),
            CrestWidth = team.CrestKey is null ? null : team.CrestWidth,
            CrestHeight = team.CrestKey is null ? null : team.CrestHeight,
            SortOrder = team.SortOrder, MatchCount = matches, StandingCount = standings, UsageCount = matches + standings,
            CreatedAt = team.CreatedAt, UpdatedAt = team.UpdatedAt,
        };
    }

    private static (string Name, string? NameEn) Validate(UpsertAdminOpponentTeamRequest request)
    {
        var name = AdminInput.RequireText(request.Name, "球隊名稱（繁中）", NameMaxLength, "name");
        var nameEn = AdminInput.OptionalText(request.NameEn, "球隊名稱（英文）", NameMaxLength, "nameEn");
        if (request.SortOrder < 0)
        {
            throw new AdminValidationException("排序不可為負數。", "sortOrder");
        }

        return (name, nameEn);
    }

    private async Task EnsureNameUniqueAsync(AdminClubScope scope, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.OpponentTeams.AsNoTracking()
            .AnyAsync(o => o.ClubId == scope.ClubId && o.Name == name && (excludeId == null || o.Id != excludeId), cancellationToken);
        if (exists)
        {
            throw new AdminOpponentTeamConflictException("球隊名稱重複", "已經有同名的對手球隊，請直接使用既有的，或換一個名稱。", "opponent_team_name_duplicate", "name");
        }
    }

    private void SetEnglishName(OpponentTeam team, string? nameEn)
    {
        var row = team.OpponentTeamsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (nameEn is null)
        {
            if (row is not null)
            {
                dbContext.OpponentTeamsI18ns.Remove(row);
                team.OpponentTeamsI18ns.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new OpponentTeamsI18n { OpponentTeamId = team.Id, Locale = "en" };
            team.OpponentTeamsI18ns.Add(row);
            dbContext.OpponentTeamsI18ns.Add(row);
        }

        row.Name = nameEn;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            // 並行建立同名：唯一鍵擋下，與應用層檢查回報同一種衝突。
            throw new AdminOpponentTeamConflictException("球隊名稱重複", "已經有同名的對手球隊，請直接使用既有的，或換一個名稱。", "opponent_team_name_duplicate", "name");
        }
    }

    private async Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
    }
}
