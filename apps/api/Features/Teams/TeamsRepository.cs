using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Teams;

public sealed class TeamsRepository(
    IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrlResolver)
{
    private const string CacheEntity = "teams";

    // GEO-05（S1-12f）：ClubDomain 只供 SchemaEligible 計算用，
    // 不進 TeamDto（既有的球隊清單欄位維持不變，這是新增計算，不是契約變更）。
    private sealed record TeamRow(
        Guid Id, string Code, string Type, string Gender, string? AgeBand, string? TeamColor, string? HeroKey,
        int? HeroWidth, int? HeroHeight, string ClubDomain);

    private sealed record TeamI18nRow(Guid TeamId, string Locale, string? Name, string? Intro, string? HeroAlt);

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
                    SELECT t.id AS Id, t.code AS Code, t.type AS Type, t.gender AS Gender,
                           t.age_band AS AgeBand, t.team_color AS TeamColor, t.hero_key AS HeroKey, t.hero_width AS HeroWidth, t.hero_height AS HeroHeight,
                           c.domain AS ClubDomain
                    FROM teams t
                    JOIN clubs c ON c.id = t.club_id
                    WHERE t.club_id = @ClubId
                    ORDER BY t.sort_order, t.code
                    """;

                var rows = (await connection.QueryAsync<TeamRow>(
                    new CommandDefinition(listSql, new { scope.ClubId }, cancellationToken: ct))).AsList();

                var i18nById = await LoadI18nAsync(connection, rows.Select(r => r.Id), dbLocale, ct);

                return rows.Select(r => Map(r, i18nById.GetValueOrDefault(r.Id), dbLocale, scope.ClubCode)).ToList();
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
            SELECT team_id AS TeamId, locale AS Locale, name AS Name, intro AS Intro, hero_alt AS HeroAlt
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

    private TeamDto Map(TeamRow row, Dictionary<string, TeamI18nRow>? i18n, string dbLocale, string clubCode)
    {
        var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requested = i18n?.GetValueOrDefault(dbLocale);
        var name = RequestLocale.Pick(requested?.Name, fallback?.Name);

        // GEO-05（S1-12f）：url 用所屬俱樂部網域（teams 本身沒有獨立網域，一個俱樂部一個網站）；標誌由前台靜態資產輸出（v3.20）。
        var schemaEligible = SchemaRequiredFields.IsComplete(SchemaType.SportsTeam, new Dictionary<string, object?>
        {
            ["name"] = name,
            ["url"] = row.ClubDomain,
        });

        return new TeamDto
        {
            Id = row.Id,
            Code = row.Code,
            ClubCode = clubCode,
            Type = row.Type,
            Gender = row.Gender,
            AgeBand = row.AgeBand,
            TeamColor = row.TeamColor,
            HeroKey = row.HeroKey,
            Name = name,
            IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name),
            Intro = RequestLocale.Pick(requested?.Intro, fallback?.Intro),
            HeroUrl = imageUrlResolver.Resolve(row.HeroKey),
            HeroWidth = row.HeroKey is null ? null : row.HeroWidth,
            HeroHeight = row.HeroKey is null ? null : row.HeroHeight,
            HeroAlt = row.HeroKey is null ? null : RequestLocale.Pick(requested?.HeroAlt, fallback?.HeroAlt),
            SchemaEligible = schemaEligible,
        };
    }
}
