using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Partners;

/// <summary>
/// 公開讀取：E1 合作夥伴（前台 09.1、首頁夥伴 Logo 牆、頁尾）。<c>partners.club_id</c> 必填，只回本俱樂部的資料。
/// 快取 entity <c>partners</c>（後台寫入 <c>AdminPartnersRepository</c> 會失效）；key 帶上「今天」日期，
/// 讓合作期間在跨日時不會等到 TTL 才更新。
/// </summary>
public sealed class PartnersRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private sealed record PartnerRow(
        Guid Id, string Slug, string? PartnerType, string? Country, DateTime? StartOn, DateTime? EndOn, string? WebsiteUrl,
        bool ShowInFooter, bool ShowOnHome, int SortOrder, string? LogoDarkKey, string? LogoLightKey,
        int? LogoDarkWidth, int? LogoDarkHeight, int? LogoLightWidth, int? LogoLightHeight, string? LogoAlt, string? Name, string? Content, bool IsFallbackLocale);

    private sealed record ProgramLinkRow(Guid PartnerId, string Slug, string? Name);

    public async Task<IReadOnlyList<PartnerDto>> ListAsync(
        ClubScope scope, string? partnerType, bool homeOnly, bool footerOnly, string dbLocale, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var qualifier = $"{today:yyyyMMdd}:{partnerType ?? CacheDimensions.NoQualifier}:{(homeOnly ? 1 : 0)}{(footerOnly ? 1 : 0)}";

        return await cache.GetOrCreateAsync("partners", scope.ClubCode, dbLocale, qualifier, async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string sql = """
                SELECT p.id AS Id, p.slug AS Slug, p.partner_type AS PartnerType, p.country AS Country,
                       p.start_on AS StartOn, p.end_on AS EndOn, p.website_url AS WebsiteUrl,
                       p.show_in_footer AS ShowInFooter, p.show_on_home AS ShowOnHome, p.sort_order AS SortOrder,
                       p.logo_dark_key AS LogoDarkKey, p.logo_light_key AS LogoLightKey,
                       p.logo_dark_width AS LogoDarkWidth, p.logo_dark_height AS LogoDarkHeight,
                       p.logo_light_width AS LogoLightWidth, p.logo_light_height AS LogoLightHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name,
                       COALESCE(NULLIF(r.content, N''), d.content) AS Content,
                       CAST(CASE WHEN @Locale <> @DefaultLocale AND NULLIF(r.name, N'') IS NULL THEN 1 ELSE 0 END AS bit) AS IsFallbackLocale
                FROM partners p
                LEFT JOIN partners_i18n r ON r.partner_id = p.id AND r.locale = @Locale
                LEFT JOIN partners_i18n d ON d.partner_id = p.id AND d.locale = @DefaultLocale
                WHERE p.club_id = @ClubId
                  AND (@PartnerType IS NULL OR p.partner_type = @PartnerType)
                  AND (@HomeOnly = 0 OR p.show_on_home = 1)
                  AND (@FooterOnly = 0 OR p.show_in_footer = 1)
                  AND (p.start_on IS NULL OR p.start_on <= @Today)
                  AND (p.end_on IS NULL OR p.end_on >= @Today)
                ORDER BY p.sort_order, p.row_seq
                """;
            var parameters = new
            {
                scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, PartnerType = partnerType,
                HomeOnly = homeOnly ? 1 : 0, FooterOnly = footerOnly ? 1 : 0, Today = today.ToDateTime(TimeOnly.MinValue),
            };
            var rows = (await connection.QueryAsync<PartnerRow>(new CommandDefinition(sql, parameters, cancellationToken: ct))).AsList();

            var links = new Dictionary<Guid, List<PartnerCharityProgramDto>>();
            if (rows.Count > 0)
            {
                const string linkSql = """
                    SELECT l.partner_id AS PartnerId, cp.slug AS Slug, COALESCE(NULLIF(r.name, N''), d.name) AS Name
                    FROM charity_program_partners l
                    JOIN charity_programs cp ON cp.id = l.charity_program_id AND cp.status = 'published'
                                            AND (cp.club_id = @ClubId OR cp.club_id IS NULL)
                    LEFT JOIN charity_programs_i18n r ON r.charity_program_id = cp.id AND r.locale = @Locale
                    LEFT JOIN charity_programs_i18n d ON d.charity_program_id = cp.id AND d.locale = @DefaultLocale
                    WHERE l.partner_id IN @Ids
                    ORDER BY cp.is_pinned DESC, cp.sort_order, cp.start_on DESC
                    """;
                var linkRows = await connection.QueryAsync<ProgramLinkRow>(new CommandDefinition(
                    linkSql, new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, Ids = rows.Select(r => r.Id).ToList() },
                    cancellationToken: ct));
                foreach (var g in linkRows.GroupBy(l => l.PartnerId))
                {
                    links[g.Key] = g.Select(l => new PartnerCharityProgramDto { Slug = l.Slug, Name = l.Name }).ToList();
                }
            }

            return (IReadOnlyList<PartnerDto>)rows.Select(r => new PartnerDto
            {
                Id = r.Id, Slug = r.Slug, PartnerType = r.PartnerType, Country = r.Country,
                StartOn = r.StartOn is null ? null : DateOnly.FromDateTime(r.StartOn.Value),
                EndOn = r.EndOn is null ? null : DateOnly.FromDateTime(r.EndOn.Value),
                WebsiteUrl = r.WebsiteUrl, ShowInFooter = r.ShowInFooter, ShowOnHome = r.ShowOnHome, SortOrder = r.SortOrder,
                Name = r.Name, IsFallbackLocale = r.IsFallbackLocale, Content = r.Content,
                LogoDarkUrl = imageUrls.Resolve(r.LogoDarkKey), LogoLightUrl = imageUrls.Resolve(r.LogoLightKey),
                LogoDarkWidth = r.LogoDarkKey is null ? null : r.LogoDarkWidth, LogoDarkHeight = r.LogoDarkKey is null ? null : r.LogoDarkHeight,
                LogoLightWidth = r.LogoLightKey is null ? null : r.LogoLightWidth, LogoLightHeight = r.LogoLightKey is null ? null : r.LogoLightHeight,
                LogoAlt = r.LogoDarkKey is null && r.LogoLightKey is null ? null : r.LogoAlt,
                CharityPrograms = links.GetValueOrDefault(r.Id) ?? [],
            }).ToList();
        }, cancellationToken);
    }
}
