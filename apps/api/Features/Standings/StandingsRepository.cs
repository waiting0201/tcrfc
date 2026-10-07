using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Standings;

/// <summary>
/// 賽事積分榜與球員數據自動彙總的公開讀取（主站規劃書 §3.3 3.1「Results &amp; Standings」、§4.3 C2「賽季數據：可手動輸入或由賽事自動彙總」、C4「積分榜」）。
/// 🔴 全部以 <c>club_id</c> 硬過濾（積分榜與賽事都是兩隊各自的資料）。<b>不快取</b>：後台維護積分榜與賽果尚未接公開快取失效，資料量小、直接查庫。
/// 球季的挑選：明確指定 <c>season</c>（代碼）→ 用它；否則取「今天落在起訖內」的球季，沒有就取<b>最新的有資料球季</b>。
/// <b>球員數據的彙總規則</b>（賽事紀錄沒有出場分鐘與助攻，所以規則是保守且可說明的）：只計狀態為已結束（<c>played</c>）的賽事；
/// 進球＝<c>match_goals</c> 筆數；黃／紅牌＝<c>match_cards</c> 筆數；<b>出賽＝先發名單的場次，加上雖列在替補但在該場有進球或牌的場次</b>。
/// 助攻沒有資料來源，自動彙總時為 null。後台若手動輸入了該球員該球季的數據（<c>player_season_stats</c>），以手動為準（<c>source = manual</c>）。
/// </summary>
public sealed class StandingsRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    // ═══════════════════════════ 球季 ═══════════════════════════

    private async Task<Season?> ResolveSeasonAsync(ClubScope scope, string? code, IReadOnlyCollection<Guid> seasonIdsWithData, CancellationToken cancellationToken)
    {
        var seasons = db.Seasons.AsNoTracking().Where(s => s.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var wanted = code.Trim();
            return await seasons.FirstOrDefaultAsync(s => s.Code == wanted, cancellationToken);
        }

        var today = TaiwanClock.Today;
        var current = await seasons.Where(s => s.StartOn <= today && s.EndOn >= today).OrderByDescending(s => s.StartOn).FirstOrDefaultAsync(cancellationToken);
        if (current is not null && (seasonIdsWithData.Count == 0 || seasonIdsWithData.Contains(current.Id)))
        {
            return current;
        }

        if (seasonIdsWithData.Count > 0)
        {
            return await seasons.Where(s => seasonIdsWithData.Contains(s.Id)).OrderByDescending(s => s.StartOn).FirstOrDefaultAsync(cancellationToken);
        }

        return current ?? await seasons.OrderByDescending(s => s.StartOn).FirstOrDefaultAsync(cancellationToken);
    }

    private static SeasonRefDto? Ref(Season? s) => s is null ? null : new SeasonRefDto { Code = s.Code, StartOn = s.StartOn, EndOn = s.EndOn };

    // ═══════════════════════════ 積分榜 ═══════════════════════════

    public async Task<StandingsDto> GetStandingsAsync(ClubScope scope, string? seasonCode, string dbLocale, CancellationToken cancellationToken)
    {
        var withData = await db.Standings.AsNoTracking().Where(s => s.ClubId == scope.ClubId).Select(s => s.SeasonId).Distinct().ToListAsync(cancellationToken);
        var season = await ResolveSeasonAsync(scope, seasonCode, withData, cancellationToken);
        var codes = await db.Seasons.AsNoTracking().Where(s => s.ClubId == scope.ClubId && withData.Contains(s.Id)).OrderByDescending(s => s.StartOn).Select(s => s.Code).ToListAsync(cancellationToken);
        if (season is null)
        {
            return new StandingsDto { Season = null, Seasons = codes, Items = [] };
        }

        // 球隊名稱走 standings_i18n：請求語系優先、空白回退繁中（RequestLocale.Pick）。
        var raw = await db.Standings.AsNoTracking().Where(s => s.ClubId == scope.ClubId && s.SeasonId == season.Id)
            .Select(s => new
            {
                s.Rank, s.Played, s.Points, s.UpdatedAt,
                Requested = s.StandingsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.TeamName).FirstOrDefault(),
                Default = s.StandingsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.TeamName).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var rows = raw.Select(r => new
            {
                r.Rank, r.Played, r.Points, r.UpdatedAt, TeamName = RequestLocale.Pick(r.Requested, r.Default) ?? string.Empty,
                IsFallbackLocale = RequestLocale.IsFallback(dbLocale, r.Requested),
            })
            .OrderBy(s => s.Rank == null).ThenBy(s => s.Rank).ThenBy(s => s.TeamName, StringComparer.Ordinal).ToList();
        return new StandingsDto
        {
            Season = Ref(season), Seasons = codes, UpdatedAt = rows.Count == 0 ? null : rows.Max(r => r.UpdatedAt),
            Items = rows.Select(r => new StandingRowDto { Rank = r.Rank, TeamName = r.TeamName, IsFallbackLocale = r.IsFallbackLocale, Played = r.Played, Points = r.Points }).ToList(),
        };
    }

    // ═══════════════════════════ 球員數據 ═══════════════════════════

    internal sealed record Totals(int Appearances, int Goals, int? Assists, int Yellow, int Red, string Source);

    /// <summary>只做「自動彙總」（不看手動數據）。後台球員賽季數據畫面也用它顯示「清除手動後會回到的數字」，兩邊同一份規則。
    /// 規則見類別檔頭；烏龍球不算該球員進球（稽核 A-1）。</summary>
    internal static async Task<Dictionary<Guid, Totals>> AutoTotalsAsync(
        ClubDbContext db, Guid clubId, Guid seasonId, Guid? onlyPlayerId, CancellationToken cancellationToken)
    {
        var matchIds = await db.Matches.AsNoTracking().Where(m => m.ClubId == clubId && m.SeasonId == seasonId && m.Status == "played").Select(m => m.Id).ToListAsync(cancellationToken);
        var goals = await db.MatchGoals.AsNoTracking().Where(g => matchIds.Contains(g.MatchId) && (onlyPlayerId == null || g.PlayerId == onlyPlayerId))
            .Select(g => new { g.MatchId, g.PlayerId, g.GoalType }).ToListAsync(cancellationToken);
        var cards = await db.MatchCards.AsNoTracking().Where(c => matchIds.Contains(c.MatchId) && (onlyPlayerId == null || c.PlayerId == onlyPlayerId))
            .Select(c => new { c.MatchId, c.PlayerId, c.CardType }).ToListAsync(cancellationToken);
        var lineups = await db.MatchLineups.AsNoTracking().Where(l => matchIds.Contains(l.MatchId) && (onlyPlayerId == null || l.PlayerId == onlyPlayerId))
            .Select(l => new { l.MatchId, l.PlayerId, l.IsStarter }).ToListAsync(cancellationToken);

        // 出賽場次：先發，或雖是替補但在該場有進球／牌。
        var appeared = new HashSet<(Guid Player, Guid Match)>();
        foreach (var l in lineups.Where(l => l.IsStarter))
        {
            appeared.Add((l.PlayerId, l.MatchId));
        }

        foreach (var g in goals)
        {
            appeared.Add((g.PlayerId, g.MatchId));
        }

        foreach (var c in cards)
        {
            appeared.Add((c.PlayerId, c.MatchId));
        }

        // 烏龍球記在對手名下、不算該球員的進球（稽核 A-1）；出賽仍算（上面 appeared 已含）。
        var countedGoals = goals.Where(g => !MatchGoalTypes.IsOwnGoal(g.GoalType)).ToList();

        var result = new Dictionary<Guid, Totals>();
        foreach (var playerId in appeared.Select(a => a.Player).Distinct())
        {
            result[playerId] = new Totals(
                appeared.Count(a => a.Player == playerId), countedGoals.Count(g => g.PlayerId == playerId), null,
                cards.Count(c => c.PlayerId == playerId && c.CardType == "yellow"), cards.Count(c => c.PlayerId == playerId && c.CardType == "red"), "auto");
        }

        return result;
    }

    /// <summary>某球季全部球員的數據（手動優先，否則自動彙總）。鍵＝球員 id。</summary>
    private async Task<Dictionary<Guid, Totals>> AggregateAsync(ClubScope scope, Guid seasonId, Guid? onlyPlayerId, CancellationToken cancellationToken)
    {
        var result = await AutoTotalsAsync(db, scope.ClubId, seasonId, onlyPlayerId, cancellationToken);

        var manual = await db.PlayerSeasonStats.AsNoTracking().Where(s => s.SeasonId == seasonId && (onlyPlayerId == null || s.PlayerId == onlyPlayerId)
                                                                          && s.Player.ClubId == scope.ClubId).ToListAsync(cancellationToken);
        foreach (var m in manual)
        {
            result[m.PlayerId] = new Totals(m.Appearances, m.Goals, m.Assists, m.YellowCards, m.RedCards, "manual");
        }

        return result;
    }

    public async Task<PlayerStatsDto> GetPlayerStatsAsync(ClubScope scope, string? seasonCode, string? teamCode, string dbLocale, CancellationToken cancellationToken)
    {
        // 有數據的球季＝有已結束賽事，或有手動數據。
        var withData = await db.Matches.AsNoTracking().Where(m => m.ClubId == scope.ClubId && m.Status == "played").Select(m => m.SeasonId).Distinct().ToListAsync(cancellationToken);
        var manualSeasons = await db.PlayerSeasonStats.AsNoTracking().Where(s => s.Player.ClubId == scope.ClubId).Select(s => s.SeasonId).Distinct().ToListAsync(cancellationToken);
        var seasonIds = withData.Union(manualSeasons).ToList();
        var season = await ResolveSeasonAsync(scope, seasonCode, seasonIds, cancellationToken);
        var codes = await db.Seasons.AsNoTracking().Where(s => s.ClubId == scope.ClubId && seasonIds.Contains(s.Id)).OrderByDescending(s => s.StartOn).Select(s => s.Code).ToListAsync(cancellationToken);
        if (season is null)
        {
            return new PlayerStatsDto { Season = null, Seasons = codes, Items = [] };
        }

        var totals = await AggregateAsync(scope, season.Id, null, cancellationToken);
        var ids = totals.Keys.ToList();
        var players = await db.Players.AsNoTracking().Include(p => p.Team)
            .Where(p => p.ClubId == scope.ClubId && ids.Contains(p.Id) && (teamCode == null || p.Team.Code == teamCode)).ToListAsync(cancellationToken);
        var playerIds = players.Select(p => p.Id).ToList();
        var names = await db.PlayersI18ns.AsNoTracking().Where(i => playerIds.Contains(i.PlayerId) && (i.Locale == dbLocale || i.Locale == RequestLocale.DefaultDbLocale)).ToListAsync(cancellationToken);
        var items = players.Select(p =>
        {
            var t = totals[p.Id];
            var requested = names.FirstOrDefault(n => n.PlayerId == p.Id && n.Locale == dbLocale);
            var fallback = names.FirstOrDefault(n => n.PlayerId == p.Id && n.Locale == RequestLocale.DefaultDbLocale);
            // 🔴 fail-closed：肖像同意未到位不輸出照片（同 PlayersRepository）。
            var photo = p.PortraitConsentStatus is "consented" or "consented_by_guardian" ? p.PhotoKey : null;
            return new PlayerSeasonStatDto
            {
                PlayerId = p.Id, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), TeamCode = p.Team.Code, ShirtNo = p.ShirtNo, Position = p.Position,
                PhotoUrl = photo is null ? null : imageUrls.Resolve(photo), Appearances = t.Appearances, Goals = t.Goals, Assists = t.Assists, YellowCards = t.Yellow, RedCards = t.Red, Source = t.Source,
            };
        }).OrderByDescending(i => i.Goals).ThenByDescending(i => i.Appearances).ThenBy(i => i.ShirtNo ?? int.MaxValue).ToList();
        return new PlayerStatsDto { Season = Ref(season), Seasons = codes, Items = items };
    }

    public async Task<PlayerCareerStatsDto?> GetPlayerCareerAsync(ClubScope scope, Guid playerId, CancellationToken cancellationToken)
    {
        if (!await db.Players.AsNoTracking().AnyAsync(p => p.Id == playerId && p.ClubId == scope.ClubId, cancellationToken))
        {
            return null;
        }

        var seasons = await db.Seasons.AsNoTracking().Where(s => s.ClubId == scope.ClubId).OrderByDescending(s => s.StartOn).ToListAsync(cancellationToken);
        var rows = new List<PlayerCareerStatDto>();
        foreach (var season in seasons)
        {
            var totals = await AggregateAsync(scope, season.Id, playerId, cancellationToken);
            if (totals.TryGetValue(playerId, out var t))
            {
                rows.Add(new PlayerCareerStatDto
                {
                    SeasonCode = season.Code, Appearances = t.Appearances, Goals = t.Goals, Assists = t.Assists, YellowCards = t.Yellow, RedCards = t.Red, Source = t.Source,
                });
            }
        }

        return new PlayerCareerStatsDto { PlayerId = playerId, Seasons = rows };
    }
}
