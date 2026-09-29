using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.SiteFacts;

/// <summary>
/// 公開讀取（S1-12d，`I` 網站設定的前台落點）：站台事實（成立年份、主場與場地、所屬聯賽、
/// 梯隊組成、聯絡方式）。設定鍵詞彙見 <c>Features/AdminSiteFacts/AdminSiteFactsRepository</c> 檔頭，
/// 這裡只是同一批鍵的唯讀版本，不重新定義一次鍵名字面值以外的邏輯。
///
/// 🔴 **寫入端（<c>Features/AdminSiteFacts</c>）刻意不呼叫 <see cref="IQueryCache.InvalidateAsync"/>**
/// ——比照既有 <c>Features/Seo/SeoRepository</c> 同一個取捨（見該檔頭說明），管理員改設定後最多
/// 延後一個 TTL（預設 300 秒）才會反映到公開端點。
/// </summary>
public sealed class SiteFactsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string Entity = "site-facts";

    private static readonly string[] SettingKeys =
    [
        "site.founded_year", "site.founding_date", "site.founding_date_display", "site.founding_title",
        "site.league_name", "site.league_short_name", "site.squad_structure_summary", "site.squad_codes",
        "site.contact_phone", "site.contact_hours", "site.home_venue_ids",
    ];

    private sealed record SettingValueRow(string SettingKey, string? SettingValue);
    private sealed record SettingI18nRow(string SettingKey, string Locale, string? Value);
    private sealed record VenueI18nRow(Guid VenueId, string Locale, string? Name, string? Address);

    public async Task<PublicSiteFactsDto> GetAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            Entity, scope.ClubCode, dbLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string valueSql = """
                    SELECT setting_key AS SettingKey, setting_value AS SettingValue
                    FROM settings
                    WHERE club_id = @ClubId AND setting_key IN @Keys
                    """;
                const string i18nSql = """
                    SELECT s.setting_key AS SettingKey, si.locale AS Locale, si.value AS Value
                    FROM settings s
                    JOIN settings_i18n si ON si.setting_id = s.id
                    WHERE s.club_id = @ClubId AND s.setting_key IN @Keys
                    """;

                var valueRows = (await connection.QueryAsync<SettingValueRow>(new CommandDefinition(
                    valueSql, new { scope.ClubId, Keys = SettingKeys }, cancellationToken: ct))).ToList();
                var i18nRows = (await connection.QueryAsync<SettingI18nRow>(new CommandDefinition(
                    i18nSql, new { scope.ClubId, Keys = SettingKeys }, cancellationToken: ct))).ToList();

                string? Value(string key) => valueRows.FirstOrDefault(r => r.SettingKey == key)?.SettingValue;
                string? I18nResolved(string key)
                {
                    var requested = i18nRows.FirstOrDefault(r => r.SettingKey == key && r.Locale == dbLocale)?.Value;
                    var fallback = i18nRows.FirstOrDefault(r => r.SettingKey == key && r.Locale == RequestLocale.DefaultDbLocale)?.Value;
                    return RequestLocale.Pick(requested, fallback);
                }

                var venues = await LoadHomeVenuesAsync(connection, Value("site.home_venue_ids"), dbLocale, ct);

                return new PublicSiteFactsDto
                {
                    FoundedYear = Value("site.founded_year"),
                    FoundingDateIso = Value("site.founding_date"),
                    FoundedDisplay = I18nResolved("site.founding_date_display"),
                    FoundingTitle = I18nResolved("site.founding_title"),
                    League = new PublicSiteFactLeagueDto
                    {
                        Name = I18nResolved("site.league_name"),
                        ShortName = I18nResolved("site.league_short_name"),
                    },
                    Venues = venues,
                    SquadStructureSummary = I18nResolved("site.squad_structure_summary"),
                    SquadCodes = SplitCodes(Value("site.squad_codes")),
                    Contact = new PublicSiteFactContactDto
                    {
                        Address = venues.Count > 0 ? venues[0].Address : null,
                        Phone = Value("site.contact_phone"),
                        Hours = I18nResolved("site.contact_hours"),
                    },
                };
            },
            cancellationToken);
    }

    private static IReadOnlyList<string> SplitCodes(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static List<Guid> ParseVenueIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var result = new List<Guid>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<PublicSiteFactVenueDto>> LoadHomeVenuesAsync(
        System.Data.IDbConnection connection, string? homeVenueIdsValue, string dbLocale, CancellationToken cancellationToken)
    {
        var ids = ParseVenueIds(homeVenueIdsValue);
        if (ids.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT venue_id AS VenueId, locale AS Locale, name AS Name, address AS Address
            FROM venues_i18n
            WHERE venue_id IN @Ids
            """;

        var rows = (await connection.QueryAsync<VenueI18nRow>(new CommandDefinition(
            sql, new { Ids = ids }, cancellationToken: cancellationToken))).ToList();

        var result = new List<PublicSiteFactVenueDto>();
        foreach (var id in ids)
        {
            var requested = rows.FirstOrDefault(r => r.VenueId == id && r.Locale == dbLocale);
            var fallback = rows.FirstOrDefault(r => r.VenueId == id && r.Locale == RequestLocale.DefaultDbLocale);
            if (fallback is null && requested is null)
            {
                // 引用的場地沒有任何 i18n 列（理論上不會發生），略過而不是輸出空白名稱的一筆。
                continue;
            }

            result.Add(new PublicSiteFactVenueDto
            {
                Name = RequestLocale.Pick(requested?.Name, fallback?.Name) ?? string.Empty,
                Address = RequestLocale.Pick(requested?.Address, fallback?.Address),
                IsHomeGround = true,
            });
        }

        return result;
    }
}
