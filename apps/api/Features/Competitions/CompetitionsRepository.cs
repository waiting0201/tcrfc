using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Competitions;

public sealed class CompetitionsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    /// <summary>快取 entity 名稱。🔴 後台 J4（<c>AdminCompetitionsRepository</c>）寫入後必須失效這個與 <c>schedule</c>（賽程的 <c>competitionName</c>／<c>competitionCode</c> 來自這裡）。</summary>
    public const string CacheEntity = "competitions";

    private sealed record Row(Guid Id, string Code, string SeasonCode, string? CompType, int SortOrder);
    private sealed record I18nRow(Guid CompetitionId, string Locale, string? Name, string? Organizer);

    /// <summary>
    /// 俱樂部的已發布賽事系列，依球季起始日新→舊、再依排序。<paramref name="seasonCode"/> 給定時只列該球季。
    /// <c>competitions.club_id</c> 是必填 club_id 表，硬過濾，不回退共同內容。
    /// </summary>
    public async Task<IReadOnlyList<CompetitionDto>> ListAsync(ClubScope scope, string? seasonCode, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, seasonCode ?? CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();
                const string sql = """
                    SELECT c.id AS Id, c.code AS Code, se.code AS SeasonCode, c.comp_type AS CompType, c.sort_order AS SortOrder
                    FROM competitions c
                    JOIN seasons se ON se.id = c.season_id
                    WHERE c.club_id = @ClubId AND c.status = 'published'
                      AND (@SeasonCode IS NULL OR se.code = @SeasonCode)
                    ORDER BY se.start_on DESC, c.sort_order, c.code
                    """;
                var rows = (await connection.QueryAsync<Row>(new CommandDefinition(sql, new { scope.ClubId, SeasonCode = seasonCode }, cancellationToken: ct))).AsList();
                if (rows.Count == 0)
                {
                    return (IReadOnlyList<CompetitionDto>)[];
                }

                var locales = dbLocale == RequestLocale.DefaultDbLocale ? new[] { dbLocale } : new[] { dbLocale, RequestLocale.DefaultDbLocale };
                var i18n = (await connection.QueryAsync<I18nRow>(new CommandDefinition(
                    "SELECT competition_id AS CompetitionId, locale AS Locale, name AS Name, organizer AS Organizer FROM competitions_i18n WHERE competition_id IN @Ids AND locale IN @Locales",
                    new { Ids = rows.Select(r => r.Id).ToList(), Locales = locales }, cancellationToken: ct)))
                    .GroupBy(r => r.CompetitionId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale));

                return rows.Select(r =>
                {
                    var byLocale = i18n.GetValueOrDefault(r.Id);
                    var requested = byLocale?.GetValueOrDefault(dbLocale);
                    var fallback = byLocale?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                    return new CompetitionDto
                    {
                        Id = r.Id, Code = r.Code, ClubCode = scope.ClubCode, SeasonCode = r.SeasonCode, CompType = r.CompType,
                        Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
                        IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name),
                        Organizer = RequestLocale.Pick(requested?.Organizer, fallback?.Organizer),
                        SortOrder = r.SortOrder,
                    };
                }).ToList();
            },
            cancellationToken);
    }
}
