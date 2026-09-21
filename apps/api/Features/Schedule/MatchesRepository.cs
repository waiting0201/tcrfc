using Dapper;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Schedule;

public sealed class MatchesRepository(IClubSqlConnectionFactory connectionFactory)
{
    // ⚠️ MatchOn 用 DateTime 不是 DateOnly：見 PlayersRepository.PlayerRow 同款註解
    // （docs/18-work-errors.md E-20）——Microsoft.Data.SqlClient 對 SQL `date` 欄位回報的 CLR
    // 型別是 DateTime，Dapper 的 record 建構子具現化要求型別逐一相符。Map() 裡再轉成 DateOnly。
    private sealed record MatchRow(
        Guid Id, Guid? CompetitionId, string SeasonCode, string TeamCode, DateTime MatchOn, string? Kickoff,
        string? HomeAway, string? Opponent, string? CompetitionTag, string? Status,
        int? ScoreHome, int? ScoreAway, int? RoundNo, int? MatchNo);

    private sealed record MatchI18nRow(Guid MatchId, string Locale, string? Opponent, string? Venue);
    private sealed record CompetitionI18nRow(Guid CompetitionId, string Locale, string? Name);

    /// <summary>
    /// 賽程與賽果。<c>matches.club_id</c> 是 50 張必填表之一（不是回退共同的 9 張），直接 <c>=</c> 過濾。
    /// ⚠️ 對手／場地的英文回退規則與其他實體不同：<c>matches.opponent</c> 是**基礎表**上的欄位
    /// （本檔既有中文內容，不是側表），<c>matches_i18n</c> 只在需要覆寫其他語系時才有列——
    /// 這裡的回退鏈是「請求語系側表值 → 基礎表 opponent（等同中文預設）→ null」，
    /// 與其他實體「請求語系側表 → zh-Hant 側表 → null」的鏈不同，因為 matches 沒有 opponent 側表底值。
    /// </summary>
    public async Task<PagedResult<MatchDto>> ListAsync(
        ClubScope scope, string? teamCode, string? seasonCode, string? status,
        string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var countSql = """
            SELECT COUNT(*)
            FROM matches m
            JOIN seasons se ON se.id = m.season_id
            JOIN match_teams mt ON mt.match_id = m.id
            JOIN teams t ON t.id = mt.team_id
            WHERE m.club_id = @ClubId
              AND (@TeamCode IS NULL OR t.code = @TeamCode)
              AND (@SeasonCode IS NULL OR se.code = @SeasonCode)
              AND (@Status IS NULL OR m.status = @Status)
            """;

        var listSql = """
            SELECT m.id AS Id, m.competition_id AS CompetitionId, se.code AS SeasonCode, t.code AS TeamCode,
                   m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway, m.opponent AS Opponent,
                   m.competition AS CompetitionTag,
                   m.status AS Status, m.score_home AS ScoreHome, m.score_away AS ScoreAway, m.round_no AS RoundNo,
                   m.match_no AS MatchNo
            FROM matches m
            JOIN seasons se ON se.id = m.season_id
            JOIN match_teams mt ON mt.match_id = m.id
            JOIN teams t ON t.id = mt.team_id
            WHERE m.club_id = @ClubId
              AND (@TeamCode IS NULL OR t.code = @TeamCode)
              AND (@SeasonCode IS NULL OR se.code = @SeasonCode)
              AND (@Status IS NULL OR m.status = @Status)
            ORDER BY m.match_on, m.kickoff
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var parameters = new
        {
            scope.ClubId, TeamCode = teamCode, SeasonCode = seasonCode, Status = status,
            Offset = (page - 1) * pageSize, PageSize = pageSize,
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<MatchRow>(
            new CommandDefinition(listSql, parameters, cancellationToken: cancellationToken))).AsList();

        var matchIds = rows.Select(r => r.Id).ToList();
        var i18nById = await LoadI18nAsync(connection, matchIds, dbLocale, cancellationToken);
        var competitionIds = rows.Select(r => r.CompetitionId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var competitionNameById = await LoadCompetitionNamesAsync(connection, competitionIds, dbLocale, cancellationToken);

        var items = rows
            .Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), competitionNameById, dbLocale))
            .ToList();

        return new PagedResult<MatchDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    private static async Task<Dictionary<Guid, Dictionary<string, MatchI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> matchIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (matchIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT match_id AS MatchId, locale AS Locale, opponent AS Opponent, venue AS Venue
            FROM matches_i18n
            WHERE match_id IN @MatchIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<MatchI18nRow>(new CommandDefinition(
            sql, new { MatchIds = matchIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.MatchId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    /// <summary>
    /// 賽事系列名稱（<c>competitions_i18n</c>）的請求語系 → zh-Hant → null 回退，
    /// 與其他實體使用同一套規則——⚠️ 種子資料的 <c>competitions_i18n</c> 目前只有 zh-Hant 一列，
    /// 沒有直接 JOIN + 語系相等條件，避免請求 en 時因為 en 列不存在而整欄靜默變成 null。
    /// </summary>
    private static async Task<Dictionary<Guid, string?>> LoadCompetitionNamesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> competitionIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (competitionIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT competition_id AS CompetitionId, locale AS Locale, name AS Name
            FROM competitions_i18n
            WHERE competition_id IN @CompetitionIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<CompetitionI18nRow>(new CommandDefinition(
            sql, new { CompetitionIds = competitionIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows
            .GroupBy(r => r.CompetitionId)
            .ToDictionary(g => g.Key, g =>
            {
                var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
                return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
            });
    }

    private static MatchDto Map(
        MatchRow row, Dictionary<string, MatchI18nRow>? i18n, Dictionary<Guid, string?> competitionNameById, string dbLocale)
    {
        var fallbackI18n = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requestedI18n = i18n?.GetValueOrDefault(dbLocale);

        // opponent：請求語系側表 → 基礎表 opponent（等同中文預設，見類別註解）→ null。
        var opponent = RequestLocale.Pick(requestedI18n?.Opponent, row.Opponent);
        // venue：沒有基礎表欄位，只能請求語系側表 → zh-Hant 側表 → null。
        var venue = RequestLocale.Pick(requestedI18n?.Venue, fallbackI18n?.Venue);
        var competitionName = row.CompetitionId is { } competitionId
            ? competitionNameById.GetValueOrDefault(competitionId)
            : null;

        return new MatchDto
        {
            Id = row.Id,
            SeasonCode = row.SeasonCode,
            TeamCode = row.TeamCode,
            MatchOn = DateOnly.FromDateTime(row.MatchOn),
            Kickoff = row.Kickoff,
            HomeAway = row.HomeAway,
            Opponent = opponent,
            Venue = venue,
            CompetitionTag = row.CompetitionTag,
            CompetitionName = competitionName,
            Status = row.Status,
            ScoreHome = row.ScoreHome,
            ScoreAway = row.ScoreAway,
            RoundNo = row.RoundNo,
            MatchNo = row.MatchNo,
        };
    }
}
