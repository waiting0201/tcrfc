using Dapper;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Clubs;

public sealed class ClubsRepository(IClubSqlConnectionFactory connectionFactory)
{
    private sealed record ClubRow(
        Guid Id, string Code, string Domain, string? LogoLightKey, string? LogoDarkKey,
        string? FaviconKey, string? OgImageKey, string? BrandColor, string? BrandSecondaryColor,
        string DefaultLocale);

    private sealed record ClubI18nRow(Guid ClubId, string Locale, string Name, string? Description);

    /// <summary>俱樂部清單（前台的站台選擇／導覽用）。<c>clubs</c> 不是 club_id 範圍內的資料——
    /// 它本身就是「有哪些俱樂部」的定義來源，因此這裡沒有 <see cref="ClubScope"/> 參數。</summary>
    public async Task<IReadOnlyList<ClubDto>> ListAsync(string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string clubSql = """
            SELECT id AS Id, code AS Code, domain AS Domain,
                   logo_light_key AS LogoLightKey, logo_dark_key AS LogoDarkKey,
                   favicon_key AS FaviconKey, og_image_key AS OgImageKey,
                   brand_color AS BrandColor, brand_secondary_color AS BrandSecondaryColor,
                   default_locale AS DefaultLocale
            FROM clubs
            WHERE status = 'active'
            ORDER BY sort_order, code
            """;
        var clubs = (await connection.QueryAsync<ClubRow>(
            new CommandDefinition(clubSql, cancellationToken: cancellationToken))).AsList();

        if (clubs.Count == 0)
        {
            return [];
        }

        var i18nByClub = await LoadI18nAsync(connection, clubs.Select(c => c.Id), dbLocale, cancellationToken);

        return clubs.Select(c => Map(c, i18nByClub.GetValueOrDefault(c.Id), dbLocale)).ToList();
    }

    /// <summary>單一俱樂部詳細資料。<paramref name="scope"/> 已由 <see cref="IClubResolver"/> 驗證過，
    /// 這裡直接用它的 <see cref="ClubScope.ClubId"/> 查，不再重複解析 code。</summary>
    public async Task<ClubDto?> GetAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string clubSql = """
            SELECT id AS Id, code AS Code, domain AS Domain,
                   logo_light_key AS LogoLightKey, logo_dark_key AS LogoDarkKey,
                   favicon_key AS FaviconKey, og_image_key AS OgImageKey,
                   brand_color AS BrandColor, brand_secondary_color AS BrandSecondaryColor,
                   default_locale AS DefaultLocale
            FROM clubs
            WHERE id = @ClubId
            """;
        var club = await connection.QuerySingleOrDefaultAsync<ClubRow>(
            new CommandDefinition(clubSql, new { scope.ClubId }, cancellationToken: cancellationToken));

        if (club is null)
        {
            return null;
        }

        var i18nByClub = await LoadI18nAsync(connection, [club.Id], dbLocale, cancellationToken);
        return Map(club, i18nByClub.GetValueOrDefault(club.Id), dbLocale);
    }

    private static async Task<Dictionary<Guid, Dictionary<string, ClubI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IEnumerable<Guid> clubIds, string dbLocale, CancellationToken cancellationToken)
    {
        const string i18nSql = """
            SELECT club_id AS ClubId, locale AS Locale, name AS Name, description AS Description
            FROM clubs_i18n
            WHERE club_id IN @ClubIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<ClubI18nRow>(new CommandDefinition(
            i18nSql, new { ClubIds = clubIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows
            .GroupBy(r => r.ClubId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }

    private static ClubDto Map(ClubRow club, Dictionary<string, ClubI18nRow>? i18n, string dbLocale)
    {
        var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
        var requested = i18n?.GetValueOrDefault(dbLocale);

        return new ClubDto
        {
            Code = club.Code,
            Name = RequestLocale.Pick(requested?.Name, fallback?.Name) ?? club.Code,
            Description = RequestLocale.Pick(requested?.Description, fallback?.Description),
            Domain = club.Domain,
            LogoLightKey = club.LogoLightKey,
            LogoDarkKey = club.LogoDarkKey,
            FaviconKey = club.FaviconKey,
            OgImageKey = club.OgImageKey,
            BrandColor = club.BrandColor,
            BrandSecondaryColor = club.BrandSecondaryColor,
            DefaultLocale = club.DefaultLocale,
        };
    }
}
