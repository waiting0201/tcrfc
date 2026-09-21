using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Staff;

public sealed class StaffRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CacheEntity = "staff";


    private sealed record StaffRow(Guid Id, bool IsShared, string? StaffGroup, string? Licence, string? PhotoKey);
    private sealed record StaffI18nRow(Guid StaffId, string Locale, string? Name, string? Title, string? Bio);
    private sealed record StaffTeamRow(Guid StaffId, string TeamCode);

    /// <summary>
    /// 教練與團隊成員清單。<c>staff.club_id</c> 是 9 張可為空表之一（行政與醫療多為兩隊共同）——
    /// 這裡是 <see cref="Data.ClubOrSharedSql"/> 唯一真實來源在 repository 層的用法示範。
    /// **快取**：club 維度用 <see cref="ClubScope.ClubCode"/>（即使這張表可能回退共同資料，
    /// key 仍以「請求方是哪個俱樂部」分——`tcrfc` 與 `bw` 各自的回應可能包含相同的共同教練，
    /// 但兩者是分開的快取項目，不會互相污染）；qualifier 涵蓋 <paramref name="teamCode"/>／
    /// <paramref name="page"/>／<paramref name="pageSize"/>。
    /// </summary>
    public async Task<PagedResult<StaffDto>> ListAsync(
        ClubScope scope, string? teamCode, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var qualifier = $"{teamCode ?? CacheDimensions.NoQualifier}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                // team 篩選需要 EXISTS 到 staff_teams，避免因 JOIN 造成同一位 staff 因多隊重複列出。
                var countSql = $"""
                    SELECT COUNT(*)
                    FROM staff s
                    WHERE {ClubOrSharedSql.WhereClubOrShared}
                      AND (@TeamCode IS NULL OR EXISTS (
                            SELECT 1 FROM staff_teams st JOIN teams t ON t.id = st.team_id
                            WHERE st.staff_id = s.id AND t.code = @TeamCode))
                    """;

                var listSql = $"""
                    SELECT s.id AS Id,
                           CASE WHEN s.club_id IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsShared,
                           s.staff_group AS StaffGroup, s.licence AS Licence, s.photo_key AS PhotoKey
                    FROM staff s
                    WHERE {ClubOrSharedSql.WhereClubOrShared}
                      AND (@TeamCode IS NULL OR EXISTS (
                            SELECT 1 FROM staff_teams st JOIN teams t ON t.id = st.team_id
                            WHERE st.staff_id = s.id AND t.code = @TeamCode))
                    ORDER BY {ClubOrSharedSql.OrderClubBeforeShared}, s.row_seq
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                var parameters = new { scope.ClubId, TeamCode = teamCode, Offset = (page - 1) * pageSize, PageSize = pageSize };

                var totalCount = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<StaffRow>(
                    new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var staffIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadI18nAsync(connection, staffIds, dbLocale, ct);
                var teamCodesById = await LoadTeamCodesAsync(connection, staffIds, ct);

                var items = rows
                    .Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), teamCodesById.GetValueOrDefault(r.Id, []), dbLocale))
                    .ToList();

                return new PagedResult<StaffDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    private static async Task<Dictionary<Guid, Dictionary<string, StaffI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> staffIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (staffIds.Count == 0)
        {
            return [];
        }

        const string i18nSql = """
            SELECT staff_id AS StaffId, locale AS Locale, name AS Name, title AS Title, bio AS Bio
            FROM staff_i18n
            WHERE staff_id IN @StaffIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<StaffI18nRow>(new CommandDefinition(
            i18nSql, new { StaffIds = staffIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.StaffId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private static async Task<Dictionary<Guid, List<string>>> LoadTeamCodesAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> staffIds, CancellationToken cancellationToken)
    {
        if (staffIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT st.staff_id AS StaffId, t.code AS TeamCode
            FROM staff_teams st
            JOIN teams t ON t.id = st.team_id
            WHERE st.staff_id IN @StaffIds
            """;
        var rows = await connection.QueryAsync<StaffTeamRow>(new CommandDefinition(
            sql, new { StaffIds = staffIds }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.StaffId).ToDictionary(g => g.Key, g => g.Select(r => r.TeamCode).ToList());
    }

    private static StaffDto Map(
        StaffRow row, Dictionary<string, StaffI18nRow>? i18n, IReadOnlyList<string> teamCodes, string dbLocale)
    {
        var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requested = i18n?.GetValueOrDefault(dbLocale);

        return new StaffDto
        {
            Id = row.Id,
            IsShared = row.IsShared,
            StaffGroup = row.StaffGroup,
            Licence = row.Licence,
            PhotoKey = row.PhotoKey,
            Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
            Title = RequestLocale.Pick(requested?.Title, fallback?.Title),
            Bio = RequestLocale.Pick(requested?.Bio, fallback?.Bio),
            TeamCodes = teamCodes,
        };
    }
}
