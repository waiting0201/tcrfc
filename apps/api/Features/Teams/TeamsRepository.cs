using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Teams;

public sealed class TeamsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CacheEntity = "teams";

    private sealed record TeamRow(
        Guid Id, string Code, string Type, string Gender, string? AgeBand, string? TeamColor, string? HeroKey);

    private sealed record TeamI18nRow(Guid TeamId, string Locale, string? Name, string? Intro);

    /// <summary><c>teams.club_id</c> 是 50 張必填 club_id 表之一，硬過濾不回退共同內容
    /// （比照 <see cref="Players.PlayersRepository"/> 同一種寫法）。</summary>
    public async Task<IReadOnlyList<TeamDto>> ListAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string listSql = """
                    SELECT id AS Id, code AS Code, type AS Type, gender AS Gender,
                           age_band AS AgeBand, team_color AS TeamColor, hero_key AS HeroKey
                    FROM teams
                    WHERE club_id = @ClubId
                    ORDER BY sort_order, code
                    """;

                var rows = (await connection.QueryAsync<TeamRow>(
                    new CommandDefinition(listSql, new { scope.ClubId }, cancellationToken: ct))).AsList();

                var i18nById = await LoadI18nAsync(connection, rows.Select(r => r.Id), dbLocale, ct);

                return rows.Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), dbLocale)).ToList();
            },
            cancellationToken);
    }

    private static async Task<Dictionary<Guid, Dictionary<string, TeamI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IEnumerable<Guid> teamIds, string dbLocale, CancellationToken cancellationToken)
    {
        var ids = teamIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        const string i18nSql = """
            SELECT team_id AS TeamId, locale AS Locale, name AS Name, intro AS Intro
            FROM teams_i18n
            WHERE team_id IN @TeamIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<TeamI18nRow>(new CommandDefinition(
            i18nSql, new { TeamIds = ids, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.TeamId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private static TeamDto Map(TeamRow row, Dictionary<string, TeamI18nRow>? i18n, string dbLocale)
    {
        var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requested = i18n?.GetValueOrDefault(dbLocale);

        return new TeamDto
        {
            Id = row.Id,
            Code = row.Code,
            Type = row.Type,
            Gender = row.Gender,
            AgeBand = row.AgeBand,
            TeamColor = row.TeamColor,
            HeroKey = row.HeroKey,
            Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
            Intro = RequestLocale.Pick(requested?.Intro, fallback?.Intro),
        };
    }
}
