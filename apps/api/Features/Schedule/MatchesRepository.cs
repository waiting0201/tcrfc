using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Schedule;

public sealed class MatchesRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private const string CacheEntity = "schedule";


    // ⚠️ MatchOn 用 DateTime 不是 DateOnly：見 PlayersRepository.PlayerRow 同款註解
    // （docs/18-work-errors.md E-20）——Microsoft.Data.SqlClient 對 SQL `date` 欄位回報的 CLR
    // 型別是 DateTime，Dapper 的 record 建構子具現化要求型別逐一相符。Map() 裡再轉成 DateOnly。
    // ⚠️ OriginalMatchOn 同樣用 DateTime?（不是 DateOnly?）——同一個 E-20 理由，SQL Server 的
    // `date` 欄位（含可為 NULL 的）由 Microsoft.Data.SqlClient 回報的 CLR 型別仍是 DateTime。
    private sealed record MatchRow(
        Guid Id, Guid? CompetitionId, string SeasonCode, string TeamCode, DateTime MatchOn, string? Kickoff,
        string? HomeAway, string? Opponent, string? CompetitionTag, string? Status,
        int? ScoreHome, int? ScoreAway, int? RoundNo, int? MatchNo,
        DateTime? OriginalMatchOn, string? OriginalKickoff,
        Guid? OpponentTeamId, string? OpponentTeamName, string? CrestKey, int? CrestWidth, int? CrestHeight, Guid? VenueId);

    private sealed record OpponentTeamI18nRow(Guid OpponentTeamId, string? Name);
    private sealed record VenueNameRow(Guid VenueId, string Locale, string? Name);

    /// <summary>列表與單場共用的欄位清單與對手球隊庫 JOIN（球隊庫須與賽事同一個俱樂部）。</summary>
    private const string SelectColumns = """
        m.id AS Id, m.competition_id AS CompetitionId, se.code AS SeasonCode, t.code AS TeamCode,
        m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway, m.opponent AS Opponent,
        m.competition AS CompetitionTag,
        m.status AS Status, m.score_home AS ScoreHome, m.score_away AS ScoreAway, m.round_no AS RoundNo,
        m.match_no AS MatchNo, m.original_match_on AS OriginalMatchOn, m.original_kickoff AS OriginalKickoff,
        ot.id AS OpponentTeamId, ot.name AS OpponentTeamName, ot.crest_key AS CrestKey, ot.crest_width AS CrestWidth, ot.crest_height AS CrestHeight,
        m.venue_id AS VenueId
        """;

    private sealed record MatchI18nRow(Guid MatchId, string Locale, string? Opponent, string? Venue);
    private sealed record CompetitionI18nRow(Guid CompetitionId, string Locale, string? Name);
    private sealed record CompetitionCodeRow(Guid Id, string Code);

    /// <summary>
    /// 賽程與賽果。<c>matches.club_id</c> 是 50 張必填表之一（不是回退共同的 9 張），直接 <c>=</c> 過濾。
    /// ⚠️ 對手／場地的英文回退規則與其他實體不同：<c>matches.opponent</c> 是**基礎表**上的欄位
    /// （本檔既有中文內容，不是側表），<c>matches_i18n</c> 只在需要覆寫其他語系時才有列——
    /// 這裡的回退鏈是「請求語系側表值 → 基礎表 opponent（等同中文預設）→ null」，
    /// 與其他實體「請求語系側表 → zh-Hant 側表 → null」的鏈不同，因為 matches 沒有 opponent 側表底值。
    /// 🔴 <b>一場賽事只出現一列</b>：賽事可複選本方球隊（<c>match_teams</c>），所以不能直接 JOIN <c>match_teams</c>（多隊賽事會重複列、
    /// <c>TotalCount</c> 也會算多）；改成每場賽事挑一支球隊——有 <paramref name="teamCode"/> 篩選就是該隊，沒有就是排序最前的一隊
    /// （與 <see cref="GetAsync"/> 的「回第一支」一致）。
    /// **快取**：qualifier 涵蓋 <paramref name="teamCode"/>／<paramref name="seasonCode"/>／
    /// <paramref name="status"/>／<paramref name="page"/>／<paramref name="pageSize"/>——五個都會
    /// 改變回傳結果，缺一個都會讓不同篩選條件的請求彼此互相拿到對方的快取結果。
    /// </summary>
    public async Task<PagedResult<MatchDto>> ListAsync(
        ClubScope scope, string? teamCode, string? seasonCode, string? status,
        string dbLocale, int page, int pageSize, CancellationToken cancellationToken,
        string? competitionCode = null, DateOnly? from = null, DateOnly? to = null, bool descending = false)
    {
        // status 可用逗號帶多個值（如 scheduled,live,postponed）；空白項略過。
        var statuses = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();
        if (statuses is { Length: 0 })
        {
            statuses = null;
        }

        var qualifier = $"{teamCode ?? CacheDimensions.NoQualifier}:{seasonCode ?? CacheDimensions.NoQualifier}:" +
            $"{(statuses is null ? CacheDimensions.NoQualifier : string.Join('+', statuses))}:{page}:{pageSize}:" +
            $"{competitionCode ?? CacheDimensions.NoQualifier}:{from?.ToString("yyyy-MM-dd") ?? CacheDimensions.NoQualifier}:{to?.ToString("yyyy-MM-dd") ?? CacheDimensions.NoQualifier}:" +
            $"{(descending ? "desc" : "asc")}";

        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var countSql = """
                    SELECT COUNT(*)
                    FROM matches m
                    JOIN seasons se ON se.id = m.season_id
                    JOIN teams t ON t.id = (
                        SELECT TOP 1 mt.team_id FROM match_teams mt JOIN teams t2 ON t2.id = mt.team_id
                        WHERE mt.match_id = m.id AND (@TeamCode IS NULL OR t2.code = @TeamCode)
                        ORDER BY t2.sort_order, t2.code)
                    WHERE m.club_id = @ClubId
                      AND (@TeamCode IS NULL OR t.code = @TeamCode)
                      AND (@SeasonCode IS NULL OR se.code = @SeasonCode)
                      AND (@HasStatus = 0 OR m.status IN @Statuses)
                      AND (@CompetitionCode IS NULL OR m.competition_id IN (SELECT cp.id FROM competitions cp WHERE cp.club_id = m.club_id AND cp.code = @CompetitionCode))
                      AND (@From IS NULL OR m.match_on >= @From)
                      AND (@To IS NULL OR m.match_on <= @To)
                    """;

                var orderBy = descending
                    ? "ORDER BY m.match_on DESC, m.kickoff DESC, m.row_seq DESC"
                    : "ORDER BY m.match_on, m.kickoff, m.row_seq";

                var listSql = $"""
                    SELECT {SelectColumns}
                    FROM matches m
                    JOIN seasons se ON se.id = m.season_id
                    LEFT JOIN opponent_teams ot ON ot.id = m.opponent_team_id AND ot.club_id = m.club_id
                    JOIN teams t ON t.id = (
                        SELECT TOP 1 mt.team_id FROM match_teams mt JOIN teams t2 ON t2.id = mt.team_id
                        WHERE mt.match_id = m.id AND (@TeamCode IS NULL OR t2.code = @TeamCode)
                        ORDER BY t2.sort_order, t2.code)
                    WHERE m.club_id = @ClubId
                      AND (@TeamCode IS NULL OR t.code = @TeamCode)
                      AND (@SeasonCode IS NULL OR se.code = @SeasonCode)
                      AND (@HasStatus = 0 OR m.status IN @Statuses)
                      AND (@CompetitionCode IS NULL OR m.competition_id IN (SELECT cp.id FROM competitions cp WHERE cp.club_id = m.club_id AND cp.code = @CompetitionCode))
                      AND (@From IS NULL OR m.match_on >= @From)
                      AND (@To IS NULL OR m.match_on <= @To)
                    {orderBy}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                var parameters = new
                {
                    scope.ClubId, TeamCode = teamCode, SeasonCode = seasonCode,
                    HasStatus = statuses is null ? 0 : 1, Statuses = statuses ?? [string.Empty],
                    CompetitionCode = competitionCode, From = from?.ToDateTime(TimeOnly.MinValue), To = to?.ToDateTime(TimeOnly.MinValue),
                    Offset = (page - 1) * pageSize, PageSize = pageSize,
                };

                var totalCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<MatchRow>(
                    new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var matchIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadI18nAsync(connection, matchIds, dbLocale, ct);
                var competitionIds = rows.Select(r => r.CompetitionId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
                var competitionNameById = await LoadCompetitionNamesAsync(connection, competitionIds, dbLocale, ct);
                var competitionCodeById = await LoadCompetitionCodesAsync(connection, competitionIds, ct);
                var library = await LoadLibraryAsync(connection, rows, dbLocale, ct);

                // 賽果卡片要的進球者與賽後報導：整頁一次批次查，不逐場查。
                var extras = await LoadListExtrasAsync(connection, rows.Select(r => (r.Id, r.Status)).ToList(), scope.ClubId, dbLocale, ct);
                var items = rows
                    .Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), competitionNameById, competitionCodeById, library, dbLocale, scope.ClubCode) with
                    {
                        Scorers = extras.Scorers.GetValueOrDefault(r.Id) ?? [],
                        MatchReport = extras.Reports.GetValueOrDefault(r.Id),
                    })
                    .ToList();

                return new PagedResult<MatchDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    /// <summary>
    /// 單場賽事（App 規劃書 §9.2「賽事 列表／單筆」、深連結 <c>tcrfc://match/{id}</c>），v3.31 起回 <see cref="MatchDetailDto"/>：
    /// 列表欄位 ＋ 進球、牌、出賽名單。<paramref name="id"/> 是賽事 id（Guid）；別的俱樂部的賽事一律 <c>null</c>（硬過濾 <c>club_id</c>）。
    /// 一場賽事掛在多支球隊時回第一支（依球隊排序）。找不到不快取。
    /// 🔴 球員欄位只輸出公開安全的（id、網址代稱、當前語系姓名、背號、位置）；只認現役球員（同公開球員名單：<c>status</c> 為 <c>active</c> 或尚未填寫）——
    /// 非現役球員的名單列整列略過，進球／牌的列保留（比分對得上）但球員欄位為 <c>null</c>。
    /// 快取實體仍是 <c>schedule</c>（賽事、球隊庫、出賽名單寫入都會失效；球員改名／離隊另由球員寫入失效）。
    /// </summary>
    public async Task<MatchDetailDto?> GetAsync(ClubScope scope, Guid id, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<MatchDetailDto?>(
            CacheEntity, scope.ClubCode, dbLocale, $"detail:{id:N}",
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();
                var sql = $"""
                    SELECT TOP 1 {SelectColumns}
                    FROM matches m
                    JOIN seasons se ON se.id = m.season_id
                    LEFT JOIN opponent_teams ot ON ot.id = m.opponent_team_id AND ot.club_id = m.club_id
                    JOIN match_teams mt ON mt.match_id = m.id
                    JOIN teams t ON t.id = mt.team_id
                    WHERE m.club_id = @ClubId AND m.id = @Id
                    ORDER BY t.sort_order, t.code
                    """;
                var row = await connection.QueryFirstOrDefaultAsync<MatchRow>(
                    new CommandDefinition(sql, new { scope.ClubId, Id = id }, cancellationToken: ct));
                if (row is null)
                {
                    return null;
                }

                var i18nById = await LoadI18nAsync(connection, [row.Id], dbLocale, ct);
                var competitionIds = row.CompetitionId is { } cid ? new List<Guid> { cid } : [];
                var names = await LoadCompetitionNamesAsync(connection, competitionIds, dbLocale, ct);
                var codes = await LoadCompetitionCodesAsync(connection, competitionIds, ct);
                var library = await LoadLibraryAsync(connection, [row], dbLocale, ct);
                var dto = Map(row, i18nById.GetValueOrDefault(row.Id), names, codes, library, dbLocale, scope.ClubCode);
                return await BuildDetailAsync(connection, dto, row.Id, scope.ClubId, dbLocale, ct);
            },
            cancellationToken);
    }

    private sealed record DetailPlayerRow(
        Guid PlayerId, string? Slug, int? ShirtNo, string? Position, string? Status);

    private sealed record GoalRow(Guid PlayerId, int? Minute, string? GoalType);
    private sealed record CardRow(Guid PlayerId, int? Minute, string? CardType);
    private sealed record LineupRow(Guid PlayerId, bool IsStarter, bool CameOn);
    private sealed record PlayerNameRow(Guid PlayerId, string Locale, string? Name);

    private async Task<MatchDetailDto> BuildDetailAsync(
        System.Data.IDbConnection connection, MatchDto dto, Guid matchId, Guid clubId, string dbLocale, CancellationToken ct)
    {
        var goals = (await connection.QueryAsync<GoalRow>(new CommandDefinition(
            """
            SELECT g.player_id AS PlayerId, g.minute AS Minute, g.goal_type AS GoalType
            FROM match_goals g WHERE g.match_id = @MatchId
            ORDER BY CASE WHEN g.minute IS NULL THEN 1 ELSE 0 END, g.minute, g.row_seq
            """, new { MatchId = matchId }, cancellationToken: ct))).AsList();
        var cards = (await connection.QueryAsync<CardRow>(new CommandDefinition(
            """
            SELECT c.player_id AS PlayerId, c.minute AS Minute, c.card_type AS CardType
            FROM match_cards c WHERE c.match_id = @MatchId
            ORDER BY CASE WHEN c.minute IS NULL THEN 1 ELSE 0 END, c.minute, c.row_seq
            """, new { MatchId = matchId }, cancellationToken: ct))).AsList();
        var lineups = (await connection.QueryAsync<LineupRow>(new CommandDefinition(
            """
            SELECT l.player_id AS PlayerId, l.is_starter AS IsStarter, l.came_on AS CameOn
            FROM match_lineups l WHERE l.match_id = @MatchId
            """, new { MatchId = matchId }, cancellationToken: ct))).AsList();

        var playerIds = goals.Select(g => g.PlayerId).Concat(cards.Select(c => c.PlayerId)).Concat(lineups.Select(l => l.PlayerId)).Distinct().ToList();
        var players = new Dictionary<Guid, DetailPlayerRow>();
        var names = new Dictionary<Guid, string?>();
        if (playerIds.Count > 0)
        {
            // 只認現役球員（同 PlayersRepository）；club_id 硬過濾。
            var playerRows = await connection.QueryAsync<DetailPlayerRow>(new CommandDefinition(
                """
                SELECT p.id AS PlayerId, p.slug AS Slug, p.shirt_no AS ShirtNo, p.position AS Position, p.status AS Status
                FROM players p
                WHERE p.club_id = @ClubId AND p.id IN @Ids AND (p.status IS NULL OR p.status = 'active')
                """, new { ClubId = clubId, Ids = playerIds }, cancellationToken: ct));
            players = playerRows.ToDictionary(r => r.PlayerId);

            var locales = dbLocale == RequestLocale.DefaultDbLocale ? new[] { dbLocale } : new[] { dbLocale, RequestLocale.DefaultDbLocale };
            var nameRows = await connection.QueryAsync<PlayerNameRow>(new CommandDefinition(
                "SELECT player_id AS PlayerId, locale AS Locale, name AS Name FROM players_i18n WHERE player_id IN @Ids AND locale IN @Locales",
                new { Ids = players.Keys.ToList(), Locales = locales }, cancellationToken: ct));
            names = nameRows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g =>
            {
                var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
                return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
            });
        }

        string? NameOf(Guid playerId) => names.GetValueOrDefault(playerId);

        var goalDtos = goals.Select(g =>
        {
            var visible = players.TryGetValue(g.PlayerId, out var p);
            return new MatchGoalDto
            {
                Minute = g.Minute,
                PlayerId = visible ? g.PlayerId : null,
                PlayerSlug = visible ? p!.Slug : null,
                PlayerName = visible ? NameOf(g.PlayerId) : null,
                ShirtNumber = visible ? p!.ShirtNo : null,
                GoalType = string.IsNullOrWhiteSpace(g.GoalType) ? null : g.GoalType,
                // 烏龍球算對手的進球；其餘算本隊。
                Side = AdminMatches.MatchGoalTypes.IsOwnGoal(g.GoalType) ? "opponent" : "club",
            };
        }).ToList();

        var cardDtos = cards.Select(c =>
        {
            var visible = players.TryGetValue(c.PlayerId, out var p);
            return new MatchCardDto
            {
                Minute = c.Minute,
                PlayerId = visible ? c.PlayerId : null,
                PlayerSlug = visible ? p!.Slug : null,
                PlayerName = visible ? NameOf(c.PlayerId) : null,
                CardType = c.CardType ?? "yellow",
            };
        }).ToList();

        var lineupDtos = lineups
            .Where(l => players.ContainsKey(l.PlayerId))
            .Select(l =>
            {
                var p = players[l.PlayerId];
                return new MatchLineupDto
                {
                    PlayerId = l.PlayerId, PlayerSlug = p.Slug, PlayerName = NameOf(l.PlayerId), ShirtNumber = p.ShirtNo, Position = p.Position,
                    IsStarter = l.IsStarter, CameOn = !l.IsStarter && l.CameOn,
                };
            })
            .OrderByDescending(l => l.IsStarter).ThenByDescending(l => l.CameOn).ThenBy(l => l.ShirtNumber ?? int.MaxValue)
            .ToList();

        var extras = await LoadListExtrasAsync(connection, [(matchId, dto.Status)], clubId, dbLocale, ct);
        return new MatchDetailDto(dto) { Goals = goalDtos, Cards = cardDtos, Lineup = lineupDtos, Scorers = extras.Scorers.GetValueOrDefault(matchId) ?? [], MatchReport = extras.Reports.GetValueOrDefault(matchId) };
    }


    private sealed record ScorerGoalRow(Guid MatchId, Guid PlayerId, int? Minute, string? GoalType);
    private sealed record ReportLinkRow(Guid MatchId, Guid Id, string Slug);
    private sealed record ReportTitleBatchRow(Guid ArticleId, string Locale, string? Title);

    private sealed record ListExtras(
        Dictionary<Guid, IReadOnlyList<MatchScorerDto>> Scorers, Dictionary<Guid, MatchReportDto> Reports);

    /// <summary>
    /// 列表與詳情共用的批次查詢（固定 6 次以內，與頁面場數無關，不 N+1）：
    /// ① 我方進球者——只針對已結束（<c>played</c>）的賽事，排除烏龍球，依分鐘排序；球員只認現役（同公開球員名單），非現役者保留進球但 <c>playerName</c> 為 null；
    /// ② 賽後報導——文章「關聯」（<c>article_relations</c>，<c>target_type='match'</c>）指到該賽事、分類 <c>match</c>、已發布且已到發布時間、俱樂部專屬或共同，取最新一篇；標題請求語系 → 繁中，都沒有就不輸出。
    /// </summary>
    private static async Task<ListExtras> LoadListExtrasAsync(
        System.Data.IDbConnection connection, IReadOnlyList<(Guid Id, string? Status)> matches, Guid clubId, string dbLocale, CancellationToken ct)
    {
        var scorers = new Dictionary<Guid, IReadOnlyList<MatchScorerDto>>();
        var reports = new Dictionary<Guid, MatchReportDto>();
        if (matches.Count == 0)
        {
            return new ListExtras(scorers, reports);
        }

        var locales = dbLocale == RequestLocale.DefaultDbLocale ? new[] { dbLocale } : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var playedIds = matches.Where(m => m.Status == "played").Select(m => m.Id).ToList();
        if (playedIds.Count > 0)
        {
            var goals = (await connection.QueryAsync<ScorerGoalRow>(new CommandDefinition(
                """
                SELECT g.match_id AS MatchId, g.player_id AS PlayerId, g.minute AS Minute, g.goal_type AS GoalType
                FROM match_goals g WHERE g.match_id IN @Ids
                ORDER BY CASE WHEN g.minute IS NULL THEN 1 ELSE 0 END, g.minute, g.row_seq
                """, new { Ids = playedIds }, cancellationToken: ct)))
                .Where(g => !AdminMatches.MatchGoalTypes.IsOwnGoal(g.GoalType)).ToList();
            var playerIds = goals.Select(g => g.PlayerId).Distinct().ToList();
            var names = new Dictionary<Guid, string?>();
            if (playerIds.Count > 0)
            {
                var activeIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
                    "SELECT p.id FROM players p WHERE p.club_id = @ClubId AND p.id IN @Ids AND (p.status IS NULL OR p.status = 'active')",
                    new { ClubId = clubId, Ids = playerIds }, cancellationToken: ct))).ToList();
                if (activeIds.Count > 0)
                {
                    var nameRows = await connection.QueryAsync<PlayerNameRow>(new CommandDefinition(
                        "SELECT player_id AS PlayerId, locale AS Locale, name AS Name FROM players_i18n WHERE player_id IN @Ids AND locale IN @Locales",
                        new { Ids = activeIds, Locales = locales }, cancellationToken: ct));
                    var byPlayer = nameRows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g =>
                    {
                        var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
                        return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
                    });
                    foreach (var id in activeIds)
                    {
                        names[id] = byPlayer.GetValueOrDefault(id);
                    }
                }
            }

            foreach (var group in goals.GroupBy(g => g.MatchId))
            {
                scorers[group.Key] = group.Select(g => new MatchScorerDto
                {
                    Minute = g.Minute,
                    PlayerName = names.GetValueOrDefault(g.PlayerId),
                    GoalType = string.IsNullOrWhiteSpace(g.GoalType) ? null : g.GoalType,
                }).ToList();
            }
        }

        var links = (await connection.QueryAsync<ReportLinkRow>(new CommandDefinition(
            """
            SELECT r.target_id AS MatchId, a.id AS Id, a.slug AS Slug
            FROM article_relations r
            JOIN articles a ON a.id = r.article_id
            JOIN article_categories ac ON ac.id = a.article_category_id
            WHERE r.target_type = N'match' AND r.target_id IN @Ids AND ac.code = N'match'
              AND (a.club_id = @ClubId OR a.club_id IS NULL)
              AND a.status = 'published' AND (a.published_at IS NULL OR a.published_at <= SYSUTCDATETIME())
            ORDER BY a.published_at DESC, a.row_seq DESC
            """, new { Ids = matches.Select(m => m.Id).ToList(), ClubId = clubId }, cancellationToken: ct))).ToList();
        if (links.Count > 0)
        {
            // 每場取排序最前（最新）的一篇。
            var latest = links.GroupBy(l => l.MatchId).ToDictionary(g => g.Key, g => g.First());
            var titles = (await connection.QueryAsync<ReportTitleBatchRow>(new CommandDefinition(
                "SELECT article_id AS ArticleId, locale AS Locale, title AS Title FROM articles_i18n WHERE article_id IN @Ids AND locale IN @Locales",
                new { Ids = latest.Values.Select(l => l.Id).ToList(), Locales = locales }, cancellationToken: ct)))
                .GroupBy(t => t.ArticleId).ToDictionary(g => g.Key, g => g.ToDictionary(t => t.Locale, t => t.Title));
            foreach (var (matchId, link) in latest)
            {
                var t = titles.GetValueOrDefault(link.Id);
                var title = t is null ? null : RequestLocale.Pick(t.GetValueOrDefault(dbLocale), t.GetValueOrDefault(RequestLocale.DefaultDbLocale));
                if (!string.IsNullOrWhiteSpace(title))
                {
                    reports[matchId] = new MatchReportDto { Slug = link.Slug, Title = title };
                }
            }
        }

        return new ListExtras(scorers, reports);
    }

    /// <summary>對手球隊庫英文名（<c>opponent_teams_i18n</c>）與賽事場地庫名稱（<c>venues_i18n</c>），一次查完。</summary>
    private sealed record Library(
        Dictionary<Guid, string?> TeamNameRequested, Dictionary<Guid, string?> VenueName);

    private static async Task<Library> LoadLibraryAsync(
        System.Data.IDbConnection connection, IReadOnlyList<MatchRow> rows, string dbLocale, CancellationToken cancellationToken)
    {
        var teamIds = rows.Where(r => r.OpponentTeamId.HasValue).Select(r => r.OpponentTeamId!.Value).Distinct().ToList();
        var venueIds = rows.Where(r => r.VenueId.HasValue).Select(r => r.VenueId!.Value).Distinct().ToList();

        var teamNames = new Dictionary<Guid, string?>();
        if (teamIds.Count > 0 && dbLocale != RequestLocale.DefaultDbLocale)
        {
            var teamRows = await connection.QueryAsync<OpponentTeamI18nRow>(new CommandDefinition(
                "SELECT opponent_team_id AS OpponentTeamId, name AS Name FROM opponent_teams_i18n WHERE opponent_team_id IN @Ids AND locale = @Locale",
                new { Ids = teamIds, Locale = dbLocale }, cancellationToken: cancellationToken));
            teamNames = teamRows.ToDictionary(r => r.OpponentTeamId, r => r.Name);
        }

        var venueNames = new Dictionary<Guid, string?>();
        if (venueIds.Count > 0)
        {
            var locales = dbLocale == RequestLocale.DefaultDbLocale ? new[] { dbLocale } : new[] { dbLocale, RequestLocale.DefaultDbLocale };
            var venueRows = await connection.QueryAsync<VenueNameRow>(new CommandDefinition(
                "SELECT venue_id AS VenueId, locale AS Locale, name AS Name FROM venues_i18n WHERE venue_id IN @Ids AND locale IN @Locales",
                new { Ids = venueIds, Locales = locales }, cancellationToken: cancellationToken));
            venueNames = venueRows.GroupBy(r => r.VenueId).ToDictionary(g => g.Key, g =>
            {
                var byLocale = g.ToDictionary(r => r.Locale, r => r.Name);
                return RequestLocale.Pick(byLocale.GetValueOrDefault(dbLocale), byLocale.GetValueOrDefault(RequestLocale.DefaultDbLocale));
            });
        }

        return new Library(teamNames, venueNames);
    }

    private static async Task<Dictionary<Guid, string>> LoadCompetitionCodesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> competitionIds, CancellationToken cancellationToken)
    {
        if (competitionIds.Count == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<CompetitionCodeRow>(new CommandDefinition(
            "SELECT id AS Id, code AS Code FROM competitions WHERE id IN @Ids AND status = 'published'", new { Ids = competitionIds }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Id, r => r.Code);
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
            SELECT ci.competition_id AS CompetitionId, ci.locale AS Locale, ci.name AS Name
            FROM competitions_i18n ci
            JOIN competitions c ON c.id = ci.competition_id
            WHERE ci.competition_id IN @CompetitionIds AND ci.locale IN @Locales
              AND c.status = 'published' -- 草稿賽事系列不得出現在公開賽程（稽核 B-12）
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

    private MatchDto Map(
        MatchRow row, Dictionary<string, MatchI18nRow>? i18n, Dictionary<Guid, string?> competitionNameById,
        Dictionary<Guid, string> competitionCodeById, Library library, string dbLocale, string clubCode)
    {
        var fallbackI18n = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requestedI18n = i18n?.GetValueOrDefault(dbLocale);

        // opponent：有對手球隊庫就用庫內隊名（請求語系 → 繁中）；否則請求語系側表 → 基礎表 opponent（等同中文預設，見類別註解）→ null。
        string? requestedOpponent;
        string? opponent;
        if (row.OpponentTeamId is { } teamId && row.OpponentTeamName is not null)
        {
            requestedOpponent = library.TeamNameRequested.GetValueOrDefault(teamId);
            opponent = RequestLocale.Pick(requestedOpponent, row.OpponentTeamName);
        }
        else
        {
            requestedOpponent = requestedI18n?.Opponent;
            opponent = RequestLocale.Pick(requestedOpponent, row.Opponent);
        }

        // venue：有場地庫（venue_id）就用庫內名稱（請求語系 → 繁中）；沒有就退回側表文字：請求語系側表 → zh-Hant 側表 → null。
        var venue = row.VenueId is { } venueId && library.VenueName.GetValueOrDefault(venueId) is { Length: > 0 } libraryVenue
            ? libraryVenue
            : RequestLocale.Pick(requestedI18n?.Venue, fallbackI18n?.Venue);

        var crestUrl = imageUrls.Resolve(row.CrestKey);
        var crest = row.OpponentTeamId is null || row.CrestKey is null || crestUrl is null
            ? null
            : new OpponentCrestDto
            {
                Url = crestUrl,
                Url1280 = imageUrls.Resolve(ImageObjectKey.ForLongEdge(row.CrestKey, 1280)),
                Url640 = imageUrls.Resolve(ImageObjectKey.ForLongEdge(row.CrestKey, 640)),
                Url320 = imageUrls.Resolve(ImageObjectKey.ForLongEdge(row.CrestKey, 320)),
                Width = row.CrestWidth, Height = row.CrestHeight,
            };
        var competitionName = row.CompetitionId is { } competitionId
            ? competitionNameById.GetValueOrDefault(competitionId)
            : null;

        // GEO-05（S1-12c）：單一來源見 SchemaRequiredFields 檔頭。鍵名逐一對應
        // SchemaType.SportsEvent 的必填欄位清單，缺一個都不放進字典——GetMissingFields 對
        // 沒有出現在字典裡的鍵一律視為缺漏，兩者要保持一致。
        var schemaEligible = SchemaRequiredFields.IsComplete(SchemaType.SportsEvent, new Dictionary<string, object?>
        {
            ["matchOn"] = row.MatchOn,
            ["kickoff"] = row.Kickoff,
            ["homeAway"] = row.HomeAway,
            ["opponent"] = opponent,
            ["venue"] = venue,
            ["competitionName"] = competitionName,
        });

        return new MatchDto
        {
            Id = row.Id,
            SeasonCode = row.SeasonCode,
            TeamCode = row.TeamCode,
            ClubCode = clubCode,
            CompetitionCode = row.CompetitionId is { } ccid ? competitionCodeById.GetValueOrDefault(ccid) : null,
            MatchOn = DateOnly.FromDateTime(row.MatchOn),
            Kickoff = row.Kickoff,
            KickoffAt = TaiwanClock.KickoffToUtc(DateOnly.FromDateTime(row.MatchOn), row.Kickoff),
            HomeAway = row.HomeAway,
            Opponent = opponent,
            OpponentTeamId = row.OpponentTeamId,
            OpponentName = opponent,
            OpponentCrest = crest,
            IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requestedOpponent),
            Venue = venue,
            CompetitionTag = row.CompetitionTag,
            CompetitionName = competitionName,
            Status = row.Status,
            ScoreHome = row.ScoreHome,
            ScoreAway = row.ScoreAway,
            RoundNo = row.RoundNo,
            MatchNo = row.MatchNo,
            OriginalMatchOn = row.OriginalMatchOn is { } originalMatchOn ? DateOnly.FromDateTime(originalMatchOn) : null,
            OriginalKickoff = row.OriginalKickoff,
            SchemaEligible = schemaEligible,
        };
    }
}
