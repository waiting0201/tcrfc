using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Players;

public sealed class PlayersRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CacheEntity = "players";


    // ⚠️ BirthOn 用 DateTime? 不是 DateOnly?：Microsoft.Data.SqlClient 對 SQL `date` 欄位回報的
    // CLR 型別是 DateTime，Dapper 用建構子做 record 具現化時要求逐一參數型別與 reader 回報型別
    // 完全相符，型別對不上會整個 QueryAsync<PlayerRow> 丟 InvalidOperationException（見 docs/18
    // work-errors E-20）。在 Map() 裡再轉成 DTO 要的 DateOnly。
    private sealed record PlayerRow(
        Guid Id, string TeamCode, int? ShirtNo, string? Position, DateTime? BirthOn,
        int? HeightCm, int? WeightKg, string? Nationality, string? PreferredFoot, string? PhotoKey,
        string PortraitConsentStatus);

    private sealed record PlayerI18nRow(Guid PlayerId, string Locale, string? Name, string? Bio);

    /// <summary>
    /// 球員名單。<paramref name="scope"/> 型別是 <see cref="ClubScope"/>——不是 Guid、不是 string，
    /// 呼叫端唯一的取得方式是先經過 <see cref="IClubResolver"/>。<c>players.club_id</c> 是 50 張
    /// 必填 club_id 表之一，這裡用 <c>=</c> 硬過濾，不是「可為空、需回退共同內容」的 9 張表之一。
    /// **快取**：qualifier 涵蓋 <paramref name="teamCode"/>／<paramref name="page"/>／
    /// <paramref name="pageSize"/>——這三個都會改變回傳結果，缺一個就會讓換了篩選條件的請求
    /// 拿到別的篩選條件快取住的結果。
    /// </summary>
    public async Task<PagedResult<PlayerDto>> ListAsync(
        ClubScope scope, string? teamCode, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var qualifier = $"{teamCode ?? CacheDimensions.NoQualifier}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string countSql = """
                    SELECT COUNT(*)
                    FROM players p
                    JOIN teams t ON t.id = p.team_id
                    WHERE p.club_id = @ClubId
                      AND (@TeamCode IS NULL OR t.code = @TeamCode)
                    """;

                const string listSql = """
                    SELECT p.id AS Id, t.code AS TeamCode, p.shirt_no AS ShirtNo, p.position AS Position,
                           p.birth_on AS BirthOn, p.height_cm AS HeightCm, p.weight_kg AS WeightKg,
                           p.nationality AS Nationality, p.preferred_foot AS PreferredFoot, p.photo_key AS PhotoKey,
                           p.portrait_consent_status AS PortraitConsentStatus
                    FROM players p
                    JOIN teams t ON t.id = p.team_id
                    WHERE p.club_id = @ClubId
                      AND (@TeamCode IS NULL OR t.code = @TeamCode)
                    ORDER BY t.sort_order, p.shirt_no
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                var parameters = new { scope.ClubId, TeamCode = teamCode, Offset = (page - 1) * pageSize, PageSize = pageSize };

                var totalCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<PlayerRow>(
                    new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var i18nById = await LoadI18nAsync(connection, rows.Select(r => r.Id), dbLocale, ct);

                var items = rows.Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), dbLocale)).ToList();

                return new PagedResult<PlayerDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    private static async Task<Dictionary<Guid, Dictionary<string, PlayerI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IEnumerable<Guid> playerIds, string dbLocale, CancellationToken cancellationToken)
    {
        var ids = playerIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        const string i18nSql = """
            SELECT player_id AS PlayerId, locale AS Locale, name AS Name, bio AS Bio
            FROM players_i18n
            WHERE player_id IN @PlayerIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<PlayerI18nRow>(new CommandDefinition(
            i18nSql, new { PlayerIds = ids, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private static PlayerDto Map(PlayerRow row, Dictionary<string, PlayerI18nRow>? i18n, string dbLocale)
    {
        var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requested = i18n?.GetValueOrDefault(dbLocale);

        return new PlayerDto
        {
            Id = row.Id,
            TeamCode = row.TeamCode,
            ShirtNo = row.ShirtNo,
            Position = row.Position,
            BirthOn = row.BirthOn is { } birthOn ? DateOnly.FromDateTime(birthOn) : null,
            HeightCm = row.HeightCm,
            WeightKg = row.WeightKg,
            Nationality = row.Nationality,
            PreferredFoot = row.PreferredFoot,
            // 🔴 fail-closed（S1-7a）：肖像同意未到位不得輸出照片，前台以預設圖或純文字卡呈現。
            PhotoKey = row.PortraitConsentStatus is "consented" or "consented_by_guardian" ? row.PhotoKey : null, // 白名單：只有確認同意才輸出（fail-closed）
            Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
            Bio = RequestLocale.Pick(requested?.Bio, fallback?.Bio),
        };
    }
}
