using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMatches;

/// <summary>
/// C4「賽程與賽果」——俱樂部範圍 CRUD（主站規劃書 §4.3 C4）。**資料維護方式：全部人工維護，
/// 不串接外部聯賽 API**（規劃書原文），故本檔沒有任何對外部系統的呼叫，純粹是一般的 CRUD ＋
/// CSV 批次匯入。
///
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——公開唯讀端點 <c>Features/Schedule/MatchesEndpoints.cs</c>
/// （entity="schedule"）已接快取，寫入這裡不失效會讓公開頁面在 TTL 到期前顯示舊資料，逐字比照
/// <c>AdminTeamsRepository</c> 對 <c>teams</c> 快取實體的既有處理。
///
/// 🔴 **列級授權（S1-8 新增）**：每一筆賽事至少關聯一支本方球隊（<c>match_teams</c>，可複選——
/// 跨梯隊友誼賽），寫入前呼叫端一律要用 <see cref="TeamRowScope.AllowsAll"/> 檢查**全部**關聯球隊，
/// 任何一支不在授權範圍內就整筆擋下（見 <see cref="TeamRowScope"/> 檔頭「可重用」的設計目的）。
/// </summary>
public sealed class AdminMatchesRepository(ClubDbContext dbContext, IQueryCache cache)
{
    // ⚠️ 值域定案見 AdminMatchDtos.cs 的 CreateAdminMatchRequest 檔頭說明——逐字沿用種子資料與
    // 既有測試已經在用的三個字串（scheduled／played／postponed），新增 live。
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.Ordinal) { "scheduled", "live", "played", "postponed" };

    // 主客場：逐字沿用 site/src/data/schedule.json 既有種子資料的大寫慣例（"HOME"／"AWAY"）。
    private static readonly HashSet<string> AllowedHomeAway = new(StringComparer.Ordinal) { "HOME", "AWAY" };

    // 賽事類型自由文字標籤：沿用種子資料已出現的 "league"／"cup"，比照補上 "friendly"／"other"
    // 對應規劃書 §3.13 篩選項「全部／聯賽／盃賽／友誼賽／其他」。
    private static readonly HashSet<string> AllowedCompetitionTags =
        new(StringComparer.Ordinal) { "league", "cup", "friendly", "other" };

    public static readonly IReadOnlyDictionary<string, string> StatusZhLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["未開始"] = "scheduled",
        ["進行中"] = "live",
        ["已結束"] = "played",
        ["延賽"] = "postponed",
    };

    public static readonly IReadOnlyDictionary<string, string> HomeAwayZhLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["主場"] = "HOME",
        ["客場"] = "AWAY",
    };

    public static readonly IReadOnlyDictionary<string, string> CompetitionTagZhLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["聯賽"] = "league",
        ["盃賽"] = "cup",
        ["友誼賽"] = "friendly",
        ["其他"] = "other",
    };

    public async Task<IReadOnlyList<AdminMatchListItemDto>> ListAsync(
        AdminClubScope scope, Guid? seasonId, Guid? teamId, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Matches.AsNoTracking().Where(m => m.ClubId == scope.ClubId);

        if (seasonId is Guid s)
        {
            query = query.Where(m => m.SeasonId == s);
        }
        if (teamId is Guid t)
        {
            query = query.Where(m => m.Teams.Any(team => team.Id == t));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(m => m.Status == status);
        }

        var rows = await query
            .OrderByDescending(m => m.MatchOn).ThenByDescending(m => m.Kickoff)
            .Select(m => new
            {
                m.Id,
                m.SeasonId,
                SeasonCode = m.Season.Code,
                m.CompetitionId,
                CompetitionCode = m.CompetitionNavigation != null ? m.CompetitionNavigation.Code : null,
                TeamIds = m.Teams.Select(team => team.Id).ToList(),
                TeamCodes = m.Teams.Select(team => team.Code).ToList(),
                m.MatchOn,
                m.Kickoff,
                m.HomeAway,
                m.Opponent,
                CompetitionTag = m.Competition,
                m.Status,
                m.ScoreHome,
                m.ScoreAway,
                m.RoundNo,
                m.MatchNo,
                m.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminMatchListItemDto
        {
            Id = r.Id,
            SeasonId = r.SeasonId,
            SeasonCode = r.SeasonCode,
            CompetitionId = r.CompetitionId,
            CompetitionCode = r.CompetitionCode,
            TeamIds = r.TeamIds,
            TeamCodes = r.TeamCodes,
            MatchOn = r.MatchOn,
            Kickoff = r.Kickoff,
            HomeAway = r.HomeAway,
            Opponent = r.Opponent,
            CompetitionTag = r.CompetitionTag,
            Status = r.Status,
            ScoreHome = r.ScoreHome,
            ScoreAway = r.ScoreAway,
            RoundNo = r.RoundNo,
            MatchNo = r.MatchNo,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminMatchDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var match = await dbContext.Matches.AsNoTracking()
            .Include(m => m.Season)
            .Include(m => m.CompetitionNavigation)
            .Include(m => m.Teams)
            .Include(m => m.MatchesI18ns)
            .Include(m => m.MatchGoals).ThenInclude(g => g.Player).ThenInclude(p => p.PlayersI18ns)
            .Include(m => m.MatchCards).ThenInclude(c => c.Player).ThenInclude(p => p.PlayersI18ns)
            .Include(m => m.MatchLineups).ThenInclude(l => l.Player).ThenInclude(p => p.PlayersI18ns)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);

        return match is null ? null : ToDetailDto(match);
    }

    /// <summary>🔴 建立一律歸屬 <paramref name="scope"/> 當下的俱樂部（<c>matches.club_id</c> 必填）。
    /// <paramref name="rowScope"/> 檢查全部 <see cref="CreateAdminMatchRequest.TeamIds"/>。</summary>
    public async Task<AdminMatchDetailDto> CreateAsync(
        AdminClubScope scope, TeamRowScope rowScope, CreateAdminMatchRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStatus(request.Status);
        ValidateHomeAway(request.HomeAway);
        ValidateCompetitionTag(request.CompetitionTag);
        ValidateOpponent(request.Opponent);
        ValidatePostponedFields(request.Status, request.OriginalMatchOn, request.OriginalKickoff);

        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);
        var competition = await ResolveCompetitionAsync(scope, request.CompetitionId, cancellationToken);
        await ResolveVenueAsync(request.VenueId, cancellationToken);
        var teams = await ResolveTeamsAsync(scope, request.TeamIds, cancellationToken);

        if (!rowScope.AllowsAll(teams.Select(t => (t.Id, t.Type)).ToList()))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許為這些球隊建立賽事。");
        }

        if (request.MatchNo is int matchNo)
        {
            await EnsureMatchNoUniqueAsync(scope, season.Id, competition?.Id, matchNo, excludeMatchId: null, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var match = new Match
        {
            Id = Guid.NewGuid(),
            ClubId = scope.ClubId,
            SeasonId = season.Id,
            CompetitionId = competition?.Id,
            VenueId = request.VenueId,
            MatchOn = request.MatchOn,
            Kickoff = request.Kickoff,
            HomeAway = request.HomeAway,
            Opponent = request.Opponent,
            Competition = request.CompetitionTag,
            Status = request.Status,
            ScoreHome = request.ScoreHome,
            ScoreAway = request.ScoreAway,
            RoundNo = request.RoundNo,
            MatchNo = request.MatchNo,
            OriginalMatchOn = request.OriginalMatchOn,
            OriginalKickoff = request.OriginalKickoff,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        foreach (var team in teams)
        {
            match.Teams.Add(team);
        }

        dbContext.Matches.Add(match);
        ApplyI18n(match, request.Venue, request.OpponentEn, request.VenueEn);

        if (request.Goals is not null)
        {
            await ApplyGoalsAsync(match, teams, request.Goals, cancellationToken);
        }
        if (request.Cards is not null)
        {
            await ApplyCardsAsync(match, teams, request.Cards, cancellationToken);
        }
        if (request.Lineups is not null)
        {
            await ApplyLineupsAsync(match, teams, request.Lineups, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, match.Id, cancellationToken))!;
    }

    public async Task<AdminMatchDetailDto?> UpdateAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid id, UpdateAdminMatchRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStatus(request.Status);
        ValidateHomeAway(request.HomeAway);
        ValidateCompetitionTag(request.CompetitionTag);
        ValidateOpponent(request.Opponent);
        ValidatePostponedFields(request.Status, request.OriginalMatchOn, request.OriginalKickoff);

        var match = await dbContext.Matches
            .Include(m => m.Teams)
            .Include(m => m.MatchesI18ns)
            .Include(m => m.MatchGoals)
            .Include(m => m.MatchCards)
            .Include(m => m.MatchLineups)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        // 既有關聯的球隊要允許——防止範圍受限帳號碰到不屬於自己範圍的既有賽事。
        if (!rowScope.AllowsAll(match.Teams.Select(t => (t.Id, t.Type)).ToList()))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許修改這筆賽事。");
        }

        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);
        var competition = await ResolveCompetitionAsync(scope, request.CompetitionId, cancellationToken);
        await ResolveVenueAsync(request.VenueId, cancellationToken);
        var teams = await ResolveTeamsAsync(scope, request.TeamIds, cancellationToken);

        // 新的球隊清單也要整批通過——防止把賽事改指派到範圍外的球隊藉此逃脫限制。
        if (!rowScope.AllowsAll(teams.Select(t => (t.Id, t.Type)).ToList()))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許把這筆賽事指派到這些球隊。");
        }

        if (request.MatchNo is int matchNo)
        {
            await EnsureMatchNoUniqueAsync(scope, season.Id, competition?.Id, matchNo, excludeMatchId: id, cancellationToken);
        }

        match.SeasonId = season.Id;
        match.CompetitionId = competition?.Id;
        match.VenueId = request.VenueId;
        match.MatchOn = request.MatchOn;
        match.Kickoff = request.Kickoff;
        match.HomeAway = request.HomeAway;
        match.Opponent = request.Opponent;
        match.Competition = request.CompetitionTag;
        match.Status = request.Status;
        match.ScoreHome = request.ScoreHome;
        match.ScoreAway = request.ScoreAway;
        match.RoundNo = request.RoundNo;
        match.MatchNo = request.MatchNo;
        match.OriginalMatchOn = request.OriginalMatchOn;
        match.OriginalKickoff = request.OriginalKickoff;
        match.UpdatedAt = DateTime.UtcNow;
        match.UpdatedBy = operatorId;

        match.Teams.Clear();
        foreach (var team in teams)
        {
            match.Teams.Add(team);
        }

        ApplyI18n(match, request.Venue, request.OpponentEn, request.VenueEn);

        if (request.Goals is not null)
        {
            dbContext.MatchGoals.RemoveRange(match.MatchGoals);
            match.MatchGoals.Clear();
            await ApplyGoalsAsync(match, teams, request.Goals, cancellationToken);
        }
        if (request.Cards is not null)
        {
            dbContext.MatchCards.RemoveRange(match.MatchCards);
            match.MatchCards.Clear();
            await ApplyCardsAsync(match, teams, request.Cards, cancellationToken);
        }
        if (request.Lineups is not null)
        {
            dbContext.MatchLineups.RemoveRange(match.MatchLineups);
            match.MatchLineups.Clear();
            await ApplyLineupsAsync(match, teams, request.Lineups, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>🔴 硬刪除（不是狀態轉換）——跟 C2／C3 的「人」不同，賽事是純資料紀錄，資料輸入
    /// 錯誤時直接刪掉重建即可，沒有「離隊」這種需要保留歷史狀態的語意。<c>match_goals</c>／
    /// <c>match_cards</c>／<c>match_lineups</c>／<c>match_teams</c>／<c>matches_i18n</c> 皆為
    /// <c>ON DELETE CASCADE</c>（docs/12b §11.3），刪除主表列會自動清掉全部關聯明細，不需要
    /// 手動逐一刪除子表。</summary>
    public async Task<bool> DeleteAsync(AdminClubScope scope, TeamRowScope rowScope, Guid id, CancellationToken cancellationToken)
    {
        var match = await dbContext.Matches
            .Include(m => m.Teams)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);

        if (match is null)
        {
            return false;
        }

        if (!rowScope.AllowsAll(match.Teams.Select(t => (t.Id, t.Type)).ToList()))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許刪除這筆賽事。");
        }

        dbContext.Matches.Remove(match);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        return true;
    }

    // ───────────────────────────── CSV 批次匯入（整季賽程） ─────────────────────────────

    /// <summary>
    /// CSV 格式（UTF-8 BOM，中文表頭）：
    /// <c>所屬球隊,賽季代碼,賽事系列代碼,日期,時間,主客場,對手,對手英文,場地,場地英文,賽事類型,場次編號,輪次,狀態</c>。
    ///
    /// - **所屬球隊**：球隊代號（<c>D1</c>／<c>U15</c>…），多支用全形頓號「、」相接（跨梯隊友誼賽），
    ///   球隊代號全站唯一，沿用既有 `Team.Code` 直接查（不像 FAQ 分類要另外配一份中文名稱對照表）。
    /// - **賽季代碼**／**賽事系列代碼**：對應 <c>seasons.code</c>／<c>competitions.code</c>，
    ///   賽事系列代碼可留空＝這場沒有結構化的賽事系列（規劃書 C4「賽事名稱（聯賽／盃賽）」本來就
    ///   有「賽事類型」自由文字欄位可以單獨承載，不強制每場都掛結構化賽事系列）。
    /// - **日期**：<c>yyyy-MM-dd</c>。**時間**：<c>HH:mm</c>，可留空。
    /// - **主客場**：`主場`／`客場`，可留空。**賽事類型**：`聯賽`／`盃賽`／`友誼賽`／`其他`，可留空。
    /// - **狀態**：`未開始`／`進行中`／`已結束`／`延賽`（規劃書用詞，不是 `scheduled`／`played` 這種
    ///   技術代碼——CLAUDE.md 全域規定 9「後台介面用日常中文」在 CSV 值域上的延伸）。
    /// - **場次編號**／**輪次**：整數字串，可留空。
    ///
    /// 🔴 **本方法是「整批新建」，不是 upsert**：規劃書原文「提供整季賽程 CSV 批次匯入」描述的是
    /// 賽季開打前一次性建立整季賽程的情境，不是既有 FAQ／後續會反覆修改內容那種情境。`matches`
    /// 沒有任何一個欄位組合可以穩定當作「這是同一場賽事」的自然鍵（<c>match_no</c> 可為空、對手
    /// 名稱可能同賽季撞名重賽），勉強挑一個當 upsert 鍵風險是「同名對手打第二回合時被誤判成同一場
    /// 覆蓋掉」，比「允許使用者用同一份檔案重複匯入而造成重複資料」的風險更隱蔽也更難察覺——因此
    /// 選擇不做 upsert，重複匯入需要的話請先用列表畫面清空該賽季的舊資料。
    ///
    /// 🔴 **整批驗證，任一列有錯就整批不寫入**（任務指示明文）；**列級授權在驗證階段就套用**——
    /// 球隊不在 <paramref name="rowScope"/> 允許範圍內視同一種驗證錯誤，不是等到要寫入時才拋
    /// <see cref="AdminForbiddenException"/>（CSV 匯入是使用者主動送出一個檔案，比較貼近「這份檔案
    /// 裡有問題」的錯誤回報形狀，而不是單一動作被擋下）。**不寫入任何 log 表**（CLAUDE.md 全域規定、
    /// docs/18 `E-44`）。
    /// </summary>
    private static readonly string[] CsvHeader =
        ["所屬球隊", "賽季代碼", "賽事系列代碼", "日期", "時間", "主客場", "對手", "對手英文", "場地", "場地英文", "賽事類型", "場次編號", "輪次", "狀態"];

    public async Task<MatchCsvImportResultDto> ImportCsvAsync(
        AdminClubScope scope, TeamRowScope rowScope, string csvContent, Guid? operatorId, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvContent);
        if (rows.Count == 0)
        {
            throw new AdminMatchValidationException("檔案是空的，找不到任何資料列。");
        }

        var header = rows[0];
        if (header.Count != CsvHeader.Length || !header.SequenceEqual(CsvHeader, StringComparer.Ordinal))
        {
            throw new AdminMatchValidationException($"檔案格式不正確，表頭必須依序是「{string.Join("、", CsvHeader)}」。");
        }

        // 🔴 team 不能 AsNoTracking：下面會把這裡查到的 Team 實體直接掛進 match.Teams
        // （skip navigation）。EF Core 對「新增的實體＋它引用的未追蹤實體」預設會把整個可達圖都
        // 標成 Added，若 Team 是 AsNoTracking 查來的，SaveChanges 會誤把既有球隊當成新球隊重複
        // INSERT，撞上 teams.row_seq 的 IDENTITY 欄位（「Cannot insert explicit value for
        // identity column」）——這是實測撞到的錯誤，不是預先設想的風險。season／competition
        // 這裡只取用其 Id 純量值（不掛物件參考到 match 的導覽屬性），沒有這個問題，維持
        // AsNoTracking 换取這兩者的查詢效能。
        var teamByCode = await dbContext.Teams
            .Where(t => t.ClubId == scope.ClubId)
            .ToDictionaryAsync(t => t.Code, StringComparer.Ordinal, cancellationToken);
        var seasonByCode = await dbContext.Seasons.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId)
            .ToDictionaryAsync(s => s.Code, StringComparer.Ordinal, cancellationToken);
        var competitionByCode = await dbContext.Competitions.AsNoTracking()
            .Where(c => c.ClubId == scope.ClubId)
            .ToDictionaryAsync(c => c.Code, StringComparer.Ordinal, cancellationToken);

        var errors = new List<MatchCsvImportRowErrorDto>();
        var parsedRows = new List<ParsedMatchRow>();

        // 場次編號在「同一份檔案內」也不能撞（不是只查資料庫既有列）——同一批一次匯入一整季，
        // 兩者都要顧到。Key 是 (SeasonId, CompetitionId, MatchNo)，CompetitionId 可能是 null。
        var seenMatchNoInFile = new HashSet<(Guid SeasonId, Guid? CompetitionId, int MatchNo)>();

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];

            if (row.Count != CsvHeader.Length)
            {
                errors.Add(new MatchCsvImportRowErrorDto { RowNumber = rowNumber, Reason = $"欄位數不正確，應為 {CsvHeader.Length} 欄，實際 {row.Count} 欄。" });
                continue;
            }

            var teamCodesRaw = row[0].Trim();
            var seasonCode = row[1].Trim();
            var competitionCode = row[2].Trim();
            var matchOnText = row[3].Trim();
            var kickoffText = row[4].Trim();
            var homeAwayText = row[5].Trim();
            var opponent = row[6].Trim();
            var opponentEn = row[7].Trim();
            var venue = row[8].Trim();
            var venueEn = row[9].Trim();
            var competitionTagText = row[10].Trim();
            var matchNoText = row[11].Trim();
            var roundNoText = row[12].Trim();
            var statusText = row[13].Trim();

            var rowErrors = new List<string>();

            var teamCodes = teamCodesRaw.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var teams = new List<Team>();
            if (teamCodes.Count == 0)
            {
                rowErrors.Add("至少要指定一支所屬球隊。");
            }
            else
            {
                foreach (var code in teamCodes)
                {
                    if (teamByCode.TryGetValue(code, out var team))
                    {
                        teams.Add(team);
                    }
                    else
                    {
                        rowErrors.Add($"球隊代號「{code}」不存在於這個俱樂部。");
                    }
                }
            }

            if (teams.Count > 0 && !rowScope.AllowsAll(teams.Select(t => (t.Id, t.Type)).ToList()))
            {
                rowErrors.Add("你的球隊授權範圍不允許為這些球隊建立賽事。");
            }

            Season? season = null;
            if (!seasonByCode.TryGetValue(seasonCode, out season))
            {
                rowErrors.Add($"賽季代碼「{seasonCode}」不存在於這個俱樂部。");
            }

            Competition? competition = null;
            if (competitionCode.Length > 0 && !competitionByCode.TryGetValue(competitionCode, out competition))
            {
                rowErrors.Add($"賽事系列代碼「{competitionCode}」不存在於這個俱樂部。");
            }

            if (!DateOnly.TryParse(matchOnText, out var matchOn))
            {
                rowErrors.Add("日期格式不正確，必須是 yyyy-MM-dd。");
            }

            string? homeAway = null;
            if (homeAwayText.Length > 0)
            {
                if (!HomeAwayZhLabels.TryGetValue(homeAwayText, out homeAway))
                {
                    rowErrors.Add("主客場欄位只能是「主場」「客場」或留空。");
                }
            }

            string? competitionTag = null;
            if (competitionTagText.Length > 0)
            {
                if (!CompetitionTagZhLabels.TryGetValue(competitionTagText, out competitionTag))
                {
                    rowErrors.Add("賽事類型欄位只能是「聯賽」「盃賽」「友誼賽」「其他」或留空。");
                }
            }

            if (opponent.Length == 0)
            {
                rowErrors.Add("對手為必填欄位。");
            }

            if (!StatusZhLabels.TryGetValue(statusText, out var status))
            {
                rowErrors.Add("狀態欄位只能是「未開始」「進行中」「已結束」或「延賽」。");
            }

            int? matchNo = null;
            if (matchNoText.Length > 0)
            {
                if (!int.TryParse(matchNoText, out var parsedMatchNo))
                {
                    rowErrors.Add("場次編號必須是整數。");
                }
                else
                {
                    matchNo = parsedMatchNo;
                }
            }

            int? roundNo = null;
            if (roundNoText.Length > 0)
            {
                if (!int.TryParse(roundNoText, out var parsedRoundNo))
                {
                    rowErrors.Add("輪次必須是整數。");
                }
                else
                {
                    roundNo = parsedRoundNo;
                }
            }

            if (matchNo is int mn && season is not null)
            {
                var key = (season.Id, competition?.Id, mn);
                if (!seenMatchNoInFile.Add(key))
                {
                    rowErrors.Add($"場次編號「{mn}」在這份檔案的同一個賽季、同一個賽事系列內重複出現。");
                }
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new MatchCsvImportRowErrorDto { RowNumber = rowNumber, Reason = string.Join("；", rowErrors) });
                continue;
            }

            parsedRows.Add(new ParsedMatchRow(
                teams, season!, competition, matchOn, kickoffText.Length == 0 ? null : kickoffText, homeAway,
                opponent, opponentEn.Length == 0 ? null : opponentEn, venue.Length == 0 ? null : venue,
                venueEn.Length == 0 ? null : venueEn, competitionTag, status!, matchNo, roundNo));
        }

        if (errors.Count > 0)
        {
            return new MatchCsvImportResultDto { ImportedCount = 0, Errors = errors };
        }

        // 場次編號跟資料庫既有列的唯一性——上面只查了「檔案內部」不重複，這裡再逐筆核對「資料庫裡
        // 是否已經有這個場次編號」，同樣整批驗證完才寫入（不要驗證一半就開始寫）。
        var dbConflictErrors = new List<MatchCsvImportRowErrorDto>();
        for (var i = 0; i < parsedRows.Count; i++)
        {
            var parsed = parsedRows[i];
            if (parsed.MatchNo is int mn)
            {
                var competitionId = parsed.Competition?.Id;
                var exists = await dbContext.Matches.AsNoTracking().AnyAsync(
                    m => m.ClubId == scope.ClubId && m.SeasonId == parsed.Season.Id
                      && m.CompetitionId == competitionId && m.MatchNo == mn,
                    cancellationToken);
                if (exists)
                {
                    dbConflictErrors.Add(new MatchCsvImportRowErrorDto
                    {
                        RowNumber = i + 2,
                        Reason = $"場次編號「{mn}」在這個賽季、這個賽事系列已經被使用（資料庫既有資料）。",
                    });
                }
            }
        }

        if (dbConflictErrors.Count > 0)
        {
            return new MatchCsvImportResultDto { ImportedCount = 0, Errors = dbConflictErrors };
        }

        var now = DateTime.UtcNow;
        foreach (var parsed in parsedRows)
        {
            var match = new Match
            {
                Id = Guid.NewGuid(),
                ClubId = scope.ClubId,
                SeasonId = parsed.Season.Id,
                CompetitionId = parsed.Competition?.Id,
                MatchOn = parsed.MatchOn,
                Kickoff = parsed.Kickoff,
                HomeAway = parsed.HomeAway,
                Opponent = parsed.Opponent,
                Competition = parsed.CompetitionTag,
                Status = parsed.Status,
                RoundNo = parsed.RoundNo,
                MatchNo = parsed.MatchNo,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            };

            foreach (var team in parsed.Teams)
            {
                match.Teams.Add(team);
            }

            dbContext.Matches.Add(match);
            ApplyI18n(match, parsed.Venue, parsed.OpponentEn, parsed.VenueEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        return new MatchCsvImportResultDto { ImportedCount = parsedRows.Count, Errors = [] };
    }

    private sealed record ParsedMatchRow(
        List<Team> Teams, Season Season, Competition? Competition, DateOnly MatchOn, string? Kickoff, string? HomeAway,
        string Opponent, string? OpponentEn, string? Venue, string? VenueEn, string? CompetitionTag, string Status,
        int? MatchNo, int? RoundNo);

    // ───────────────────────────── 共用私有方法 ─────────────────────────────

    private MatchesI18n EnsureI18nRow(Match match, string locale)
    {
        var existing = match.MatchesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new MatchesI18n { MatchId = match.Id, Locale = locale };
            match.MatchesI18ns.Add(existing);
            dbContext.MatchesI18ns.Add(existing);
        }
        return existing;
    }

    /// <summary>
    /// 側表兩列的完整覆寫（不是逐欄局部更新）：
    /// - **zh-Hant 列**：只承載 <paramref name="venueZh"/>（中文場地——<c>matches</c> 基礎表沒有
    ///   場地欄位可放，逐字比照既有種子腳本「中文場地寫進 <c>matches_i18n</c> 的 zh-Hant 列」的
    ///   既有寫法，中文對手則走基礎表 <c>matches.opponent</c>，兩者不是同一張表）。
    /// - **en 列**：<see cref="AdminMatchDtos"/> 檔頭「跟其餘模組形狀不同」說明的英文覆寫
    ///   （<paramref name="opponentEn"/>／<paramref name="venueEn"/> 皆可各自獨立留空）。
    /// 兩列各自「有任何內容就寫入該列、兩者皆空就整列刪除」，互不影響。
    /// </summary>
    private void ApplyI18n(Match match, string? venueZh, string? opponentEn, string? venueEn)
    {
        var existingZh = match.MatchesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        if (venueZh is not null)
        {
            var zh = existingZh ?? EnsureI18nRow(match, RequestLocale.DefaultDbLocale);
            zh.Venue = venueZh;
        }
        else if (existingZh is not null)
        {
            dbContext.Remove(existingZh);
        }

        var existingEn = match.MatchesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (opponentEn is not null || venueEn is not null)
        {
            var en = existingEn ?? EnsureI18nRow(match, "en");
            en.Opponent = opponentEn;
            en.Venue = venueEn;
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }
    }

    private async Task<Season> ResolveSeasonAsync(AdminClubScope scope, Guid seasonId, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
        if (season is null || season.ClubId != scope.ClubId)
        {
            throw new AdminMatchValidationException($"找不到這個俱樂部的球季（id={seasonId}）。");
        }
        return season;
    }

    private async Task<Competition?> ResolveCompetitionAsync(AdminClubScope scope, Guid? competitionId, CancellationToken cancellationToken)
    {
        if (competitionId is null)
        {
            return null;
        }

        var competition = await dbContext.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId, cancellationToken);
        if (competition is null || competition.ClubId != scope.ClubId)
        {
            throw new AdminMatchValidationException($"找不到這個俱樂部的賽事系列（id={competitionId}）。");
        }
        return competition;
    }

    /// <summary><c>venues</c> 沒有 <c>club_id</c>（場地是共用主檔，不分俱樂部），只檢查存在。</summary>
    private async Task ResolveVenueAsync(Guid? venueId, CancellationToken cancellationToken)
    {
        if (venueId is null)
        {
            return;
        }

        var exists = await dbContext.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken);
        if (!exists)
        {
            throw new AdminMatchValidationException($"找不到這個場地（id={venueId}）。");
        }
    }

    private static readonly HashSet<string> AllowedCardTypes = new(StringComparer.Ordinal) { "yellow", "red" };

    /// <summary>進球者必須是這場賽事其中一支所屬球隊底下的球員——見 <see cref="AdminMatchGoalInput"/>
    /// 上的說明，這同時是資料正確性檢查，也順帶把球員限制在已通過列級授權的球隊範圍內。</summary>
    private async Task<Player> ResolveMatchPlayerAsync(IReadOnlyList<Team> matchTeams, Guid playerId, CancellationToken cancellationToken)
    {
        var teamIds = matchTeams.Select(t => t.Id).ToHashSet();
        var player = await dbContext.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        if (player is null || !teamIds.Contains(player.TeamId))
        {
            throw new AdminMatchValidationException($"找不到這場賽事所屬球隊底下的球員（id={playerId}）。");
        }
        return player;
    }

    private async Task ApplyGoalsAsync(Match match, IReadOnlyList<Team> matchTeams, IReadOnlyList<AdminMatchGoalInput> goals, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var input in goals)
        {
            var player = await ResolveMatchPlayerAsync(matchTeams, input.PlayerId, cancellationToken);
            var goal = new MatchGoal
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                PlayerId = player.Id,
                Minute = input.Minute,
                GoalType = input.GoalType,
                CreatedAt = now,
                UpdatedAt = now,
            };
            match.MatchGoals.Add(goal);
            dbContext.MatchGoals.Add(goal);
        }
    }

    private async Task ApplyCardsAsync(Match match, IReadOnlyList<Team> matchTeams, IReadOnlyList<AdminMatchCardInput> cards, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var input in cards)
        {
            if (!AllowedCardTypes.Contains(input.CardType))
            {
                throw new AdminMatchValidationException("卡牌類型只能是「yellow」（黃牌）或「red」（紅牌）。");
            }

            var player = await ResolveMatchPlayerAsync(matchTeams, input.PlayerId, cancellationToken);
            var card = new MatchCard
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                PlayerId = player.Id,
                CardType = input.CardType,
                Minute = input.Minute,
                CreatedAt = now,
                UpdatedAt = now,
            };
            match.MatchCards.Add(card);
            dbContext.MatchCards.Add(card);
        }
    }

    private async Task ApplyLineupsAsync(Match match, IReadOnlyList<Team> matchTeams, IReadOnlyList<AdminMatchLineupInput> lineups, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var input in lineups)
        {
            var player = await ResolveMatchPlayerAsync(matchTeams, input.PlayerId, cancellationToken);
            var lineup = new MatchLineup
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                PlayerId = player.Id,
                IsStarter = input.IsStarter,
                CreatedAt = now,
                UpdatedAt = now,
            };
            match.MatchLineups.Add(lineup);
            dbContext.MatchLineups.Add(lineup);
        }
    }

    /// <summary>🔴 跨俱樂部指派球隊在這裡擋下，逐字比照 <c>AdminPlayersRepository.ResolveTeamAsync</c>。
    /// 至少一支——見 <see cref="CreateAdminMatchRequest.TeamIds"/> 上的說明。</summary>
    private async Task<List<Team>> ResolveTeamsAsync(AdminClubScope scope, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            throw new AdminMatchValidationException("至少要指定一支所屬球隊。");
        }

        var teams = new List<Team>();
        foreach (var teamId in teamIds.Distinct())
        {
            var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
            if (team is null || team.ClubId != scope.ClubId)
            {
                throw new AdminMatchValidationException($"找不到這個俱樂部的球隊（id={teamId}）。");
            }
            teams.Add(team);
        }
        return teams;
    }

    /// <summary>「場次編號同季同聯賽唯一」（主站規劃書 §4.3 C4）。<c>matches.match_no</c> 沒有 DB
    /// 唯一索引（docs/12b §11.1 只列了五個維持全站唯一的既有唯一鍵，這條是本輪新增的業務規則，
    /// 且範圍是「同俱樂部、同賽季、同賽事系列」的複合條件，比 DB 唯一索引更適合在應用層表達），
    /// 故在這裡查詢比對。<paramref name="competitionId"/> 為 <c>null</c> 時，只跟同樣沒有掛
    /// 賽事系列的賽事比較（不會跟已掛賽事系列的賽事衝突——不同賽事系列的場次編號本來就是各自
    /// 獨立的序號空間）。</summary>
    private async Task EnsureMatchNoUniqueAsync(
        AdminClubScope scope, Guid seasonId, Guid? competitionId, int matchNo, Guid? excludeMatchId, CancellationToken cancellationToken)
    {
        var query = dbContext.Matches.AsNoTracking()
            .Where(m => m.ClubId == scope.ClubId && m.SeasonId == seasonId && m.MatchNo == matchNo && m.CompetitionId == competitionId);

        if (excludeMatchId is Guid exclude)
        {
            query = query.Where(m => m.Id != exclude);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new AdminMatchNoConflictException(matchNo);
        }
    }

    private static void ValidateStatus(string status)
    {
        if (!AllowedStatuses.Contains(status))
        {
            throw new AdminMatchValidationException("狀態只能是「scheduled」（未開始）、「live」（進行中）、「played」（已結束）或「postponed」（延賽）。");
        }
    }

    private static void ValidateHomeAway(string? homeAway)
    {
        if (homeAway is not null && !AllowedHomeAway.Contains(homeAway))
        {
            throw new AdminMatchValidationException("主客場只能是「HOME」或「AWAY」，或留空。");
        }
    }

    private static void ValidateCompetitionTag(string? competitionTag)
    {
        if (competitionTag is not null && !AllowedCompetitionTags.Contains(competitionTag))
        {
            throw new AdminMatchValidationException("賽事類型只能是「league」「cup」「friendly」「other」之一，或留空。");
        }
    }

    private static void ValidateOpponent(string opponent)
    {
        if (string.IsNullOrWhiteSpace(opponent))
        {
            throw new AdminMatchValidationException("對手為必填欄位。");
        }
    }

    /// <summary>🔴 「延賽須填原定日期時間」（主站規劃書 §3.13／§4.3 C4／§5.1 `Match`：
    /// db/club-schema.sql <c>matches</c> 表註解「兩欄皆可為空、不加 CHECK……是否必填交後台 C4
    /// 表單驗證」）——狀態為 <c>postponed</c> 時，<see cref="Match.OriginalMatchOn"/> 與
    /// <see cref="Match.OriginalKickoff"/>（僅日期，時間欄位規劃書沒有強制要求，維持可省略）
    /// 至少要有原定日期；狀態不是 <c>postponed</c> 時，這兩欄必須是空的，不留下「明明沒延賽卻有
    /// 原定時間」這種矛盾資料。</summary>
    private static void ValidatePostponedFields(string status, DateOnly? originalMatchOn, string? originalKickoff)
    {
        if (status == "postponed")
        {
            if (originalMatchOn is null)
            {
                throw new AdminMatchValidationException("狀態為「延賽」時，必須填寫原定日期。");
            }
        }
        else if (originalMatchOn is not null || originalKickoff is not null)
        {
            throw new AdminMatchValidationException("只有狀態為「延賽」時才能填寫原定日期／原定時間。");
        }
    }

    private static AdminMatchDetailDto ToDetailDto(Match match)
    {
        var en = match.MatchesI18ns.FirstOrDefault(i => i.Locale == "en");
        var zh = match.MatchesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);

        return new AdminMatchDetailDto
        {
            Id = match.Id,
            SeasonId = match.SeasonId,
            SeasonCode = match.Season.Code,
            CompetitionId = match.CompetitionId,
            CompetitionCode = match.CompetitionNavigation?.Code,
            VenueId = match.VenueId,
            TeamIds = match.Teams.Select(t => t.Id).ToList(),
            TeamCodes = match.Teams.Select(t => t.Code).ToList(),
            MatchOn = match.MatchOn,
            Kickoff = match.Kickoff,
            HomeAway = match.HomeAway,
            Opponent = match.Opponent,
            OpponentEn = en?.Opponent,
            Venue = zh?.Venue,
            VenueEn = en?.Venue,
            CompetitionTag = match.Competition,
            Status = match.Status ?? "scheduled",
            ScoreHome = match.ScoreHome,
            ScoreAway = match.ScoreAway,
            RoundNo = match.RoundNo,
            MatchNo = match.MatchNo,
            OriginalMatchOn = match.OriginalMatchOn,
            OriginalKickoff = match.OriginalKickoff,
            Goals = match.MatchGoals.Select(g => new AdminMatchGoalDto
            {
                Id = g.Id,
                PlayerId = g.PlayerId,
                PlayerName = PlayerZhName(g.Player),
                Minute = g.Minute,
                GoalType = g.GoalType,
            }).ToList(),
            Cards = match.MatchCards.Select(c => new AdminMatchCardDto
            {
                Id = c.Id,
                PlayerId = c.PlayerId,
                PlayerName = PlayerZhName(c.Player),
                CardType = c.CardType ?? "yellow",
                Minute = c.Minute,
            }).ToList(),
            Lineups = match.MatchLineups.Select(l => new AdminMatchLineupDto
            {
                Id = l.Id,
                PlayerId = l.PlayerId,
                PlayerName = PlayerZhName(l.Player),
                IsStarter = l.IsStarter,
            }).ToList(),
            CreatedAt = match.CreatedAt,
            UpdatedAt = match.UpdatedAt,
        };
    }

    private static string? PlayerZhName(Player player)
        => player.PlayersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name;
}
