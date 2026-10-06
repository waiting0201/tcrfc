using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeasons;

/// <summary>
/// 賽季管理（稽核 A-12，2026-10-06 使用者拍板新增）。賽季是俱樂部層級的設定（<c>seasons.club_id</c> 必填），
/// 賽事、積分榜、榮譽、球員賽季數據、會籍都依賴它；原本只有種子資料，後台無法開新賽季。
///
/// 規則：
/// - <c>code</c> 同俱樂部唯一（資料庫有 <c>UQ_seasons_club_code</c>，這裡先擋給出中文訊息）；格式限英數、<c>/</c>、<c>-</c>、<c>_</c>，16 字內。
/// - 結束日必須晚於開始日；<b>同俱樂部賽季期間不可重疊</b>（專案原本沒有這條規則，本輪新增）。
/// - 🔴 <b>被任何資料引用就不能刪</b>：七張表有外鍵指向賽季（competitions／matches／standings／achievements／
///   player_season_stats／memberships／membership_plans），逐表計數後回 409 並說明被誰引用，不讓呼叫端撞到資料庫的 547。
/// - 寫入後失效會顯示賽季代碼的公開快取（賽程、賽事系列、榮譽、行事曆）。
/// </summary>
public sealed partial class AdminSeasonsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    private static readonly string[] DependentCacheEntities = ["schedule", "competitions", "honors", "calendar"];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9/_-]{0,15}$")]
    private static partial Regex CodePattern();

    public async Task<IReadOnlyList<AdminSeasonDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var seasons = await dbContext.Seasons.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId).OrderByDescending(s => s.StartOn).ToListAsync(cancellationToken);
        var usage = await UsageAsync(seasons.Select(s => s.Id).ToList(), cancellationToken);
        return seasons.Select(s => ToDto(s, usage.GetValueOrDefault(s.Id) ?? [])).ToList();
    }

    public async Task<AdminSeasonDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var usage = await UsageAsync([season.Id], cancellationToken);
        return ToDto(season, usage.GetValueOrDefault(season.Id) ?? []);
    }

    public async Task<AdminSeasonDto> CreateAsync(AdminClubScope scope, CreateAdminSeasonRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var code = ValidateCode(request.Code);
        ValidateRange(request.StartOn, request.EndOn);
        await EnsureNoConflictAsync(scope, null, code, request.StartOn, request.EndOn, cancellationToken);

        var now = DateTime.UtcNow;
        var season = new Season
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Code = code, StartOn = request.StartOn, EndOn = request.EndOn,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return ToDto(season, []);
    }

    public async Task<AdminSeasonDto?> UpdateAsync(AdminClubScope scope, Guid id, UpdateAdminSeasonRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var code = ValidateCode(request.Code);
        ValidateRange(request.StartOn, request.EndOn);
        await EnsureNoConflictAsync(scope, id, code, request.StartOn, request.EndOn, cancellationToken);

        season.Code = code;
        season.StartOn = request.StartOn;
        season.EndOn = request.EndOn;
        season.UpdatedAt = DateTime.UtcNow;
        season.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);

        var usage = await UsageAsync([season.Id], cancellationToken);
        return ToDto(season, usage.GetValueOrDefault(season.Id) ?? []);
    }

    /// <returns><c>null</c>＝找不到（404）；<c>true</c>＝已刪除。</returns>
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (season is null)
        {
            return null;
        }

        var usage = (await UsageAsync([id], cancellationToken)).GetValueOrDefault(id) ?? [];
        if (usage.Count > 0)
        {
            var who = string.Join("、", usage.Select(u => $"{u.Label} {u.Count} 筆"));
            throw new AdminConflictException("賽季使用中", $"賽季「{season.Code}」已有資料使用（{who}），無法刪除。請先處理這些資料，或直接修改賽季的名稱與期間。");
        }

        dbContext.Seasons.Remove(season);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    // ───────────────────────────── 內部 ─────────────────────────────

    private async Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        foreach (var entity in DependentCacheEntities)
        {
            await cache.InvalidateAsync(entity, scope.ClubCode, cancellationToken);
        }
    }

    private static string ValidateCode(string? code)
    {
        var text = code?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new AdminValidationException("賽季名稱為必填欄位（例如 2026/27）。", "code");
        }

        if (!CodePattern().IsMatch(text))
        {
            throw new AdminValidationException("賽季名稱只能使用英文字母、數字、/、-、_，最多 16 個字（例如 2026/27）。", "code");
        }

        return text;
    }

    private static void ValidateRange(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            throw new AdminValidationException("賽季的結束日期必須晚於開始日期。", "endOn");
        }
    }

    private async Task EnsureNoConflictAsync(AdminClubScope scope, Guid? excludeId, string code, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var others = await dbContext.Seasons.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId && (excludeId == null || s.Id != excludeId))
            .Select(s => new { s.Code, s.StartOn, s.EndOn }).ToListAsync(cancellationToken);

        if (others.Any(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AdminConflictException("賽季名稱重複", $"賽季「{code}」已經存在，請換一個名稱。", "code");
        }

        var overlap = others.FirstOrDefault(o => o.StartOn <= end && start <= o.EndOn);
        if (overlap is not null)
        {
            throw new AdminConflictException(
                "賽季期間重疊",
                $"這個賽季的期間與「{overlap.Code}」（{overlap.StartOn:yyyy-MM-dd} 至 {overlap.EndOn:yyyy-MM-dd}）重疊，同一個俱樂部的賽季期間不可重疊。", "startOn");
        }
    }

    /// <summary>七張有外鍵指向賽季的表各計一次數（只回有引用的類別）。鍵＝賽季 id。</summary>
    private async Task<Dictionary<Guid, List<AdminSeasonUsageDto>>> UsageAsync(IReadOnlyList<Guid> seasonIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<AdminSeasonUsageDto>>();
        if (seasonIds.Count == 0)
        {
            return result;
        }

        void Add(string label, List<(Guid Id, int Count)> rows)
        {
            foreach (var (id, count) in rows.Where(r => r.Count > 0))
            {
                if (!result.TryGetValue(id, out var list))
                {
                    result[id] = list = [];
                }

                list.Add(new AdminSeasonUsageDto { Label = label, Count = count });
            }
        }

        async Task<List<(Guid, int)>> Count<T>(IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, Guid>> key)
            => (await query.GroupBy(key).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(cancellationToken))
                .Select(r => (r.Id, r.Count)).ToList();

        Add("賽事系列", await Count(dbContext.Competitions.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("賽事", await Count(dbContext.Matches.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("積分榜", await Count(dbContext.Standings.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("榮譽", await Count(dbContext.Achievements.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("球員賽季數據", await Count(dbContext.PlayerSeasonStats.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("會籍", await Count(dbContext.Memberships.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        Add("會籍方案", await Count(dbContext.MembershipPlans.AsNoTracking().Where(x => seasonIds.Contains(x.SeasonId)), x => x.SeasonId));
        return result;
    }

    private static AdminSeasonDto ToDto(Season s, IReadOnlyList<AdminSeasonUsageDto> usage) => new()
    {
        Id = s.Id, Code = s.Code, StartOn = s.StartOn, EndOn = s.EndOn, InUse = usage.Count > 0, Usage = usage, UpdatedAt = s.UpdatedAt,
    };
}
