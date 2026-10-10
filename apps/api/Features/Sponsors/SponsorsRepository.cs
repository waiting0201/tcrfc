using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Partners;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Sponsors;

/// <summary>
/// 公開讀取：E2 贊助商與贊助方案（前台 09.2、09.4）。全部 <c>club_id</c> 必填。快取 entity <c>sponsors</c>
/// （後台贊助商／方案／活動寫入都會失效）；key 帶上「今天」日期，讓合約到期在跨日時生效。
/// 🔴 聯絡窗口、合約期間、到期提醒是商務內部資料，**不進公開 DTO**；未公開價格的方案完全不輸出價格。
/// </summary>
public sealed class SponsorsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private sealed record SponsorRow(
        Guid Id, string Slug, string? Tier, int SortOrder, string? LogoDarkKey, string? LogoLightKey,
        int? LogoDarkWidth, int? LogoDarkHeight, int? LogoLightWidth, int? LogoLightHeight, string? LogoAlt, string? Name, string? Content, bool IsFallbackLocale);
    private sealed record StoryRow(Guid SponsorId, string Slug, string? Title, string? Summary);
    private sealed record ActivationRow(Guid Id, Guid SponsorId, DateTime? HappenedOn, string? Title, string? ResultSummary);
    private sealed record ActivationImageRow(Guid ActivationId, string ImageKey, int? ImageWidth, int? ImageHeight, string? ImageAltZh, string? ImageAltEn);
    private sealed record ProgramLinkRow(Guid SponsorId, string Slug, string? Name);
    private sealed record PackageRow(
        Guid Id, string Slug, int SortOrder, bool IsPricePublic, int? PriceMin, int? PriceMax,
        string? Name, string? Content, string? BenefitList, string? Audience, bool IsFallbackLocale);

    public async Task<IReadOnlyList<SponsorDto>> ListSponsorsAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await cache.GetOrCreateAsync("sponsors", scope.ClubCode, dbLocale, $"list:{today:yyyyMMdd}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var p = new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, Today = today.ToDateTime(TimeOnly.MinValue) };

            const string sponsorSql = """
                SELECT s.id AS Id, s.slug AS Slug, s.tier AS Tier, s.sort_order AS SortOrder,
                       s.logo_dark_key AS LogoDarkKey, s.logo_light_key AS LogoLightKey,
                       s.logo_dark_width AS LogoDarkWidth, s.logo_dark_height AS LogoDarkHeight,
                       s.logo_light_width AS LogoLightWidth, s.logo_light_height AS LogoLightHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.content, N''), d.content) AS Content,
                       CAST(CASE WHEN @Locale <> @DefaultLocale AND NULLIF(r.name, N'') IS NULL THEN 1 ELSE 0 END AS bit) AS IsFallbackLocale
                FROM sponsors s
                LEFT JOIN sponsors_i18n r ON r.sponsor_id = s.id AND r.locale = @Locale
                LEFT JOIN sponsors_i18n d ON d.sponsor_id = s.id AND d.locale = @DefaultLocale
                WHERE s.club_id = @ClubId AND (s.contract_end_on IS NULL OR s.contract_end_on >= @Today)
                ORDER BY CASE s.tier WHEN N'主贊助' THEN 0 WHEN N'官方贊助' THEN 1 ELSE 2 END, s.sort_order, s.row_seq
                """;
            var sponsors = (await connection.QueryAsync<SponsorRow>(new CommandDefinition(sponsorSql, p, cancellationToken: ct))).AsList();
            if (sponsors.Count == 0)
            {
                return (IReadOnlyList<SponsorDto>)[];
            }

            var ids = sponsors.Select(s => s.Id).ToList();
            var q = new { p.ClubId, p.Locale, p.DefaultLocale, Ids = ids };

            const string storySql = """
                SELECT sa.sponsor_id AS SponsorId, a.slug AS Slug,
                       COALESCE(NULLIF(r.title, N''), d.title) AS Title, COALESCE(NULLIF(r.summary, N''), d.summary) AS Summary
                FROM sponsor_articles sa
                JOIN articles a ON a.id = sa.article_id AND a.status = 'published' AND (a.club_id = @ClubId OR a.club_id IS NULL)
                LEFT JOIN articles_i18n r ON r.article_id = a.id AND r.locale = @Locale
                LEFT JOIN articles_i18n d ON d.article_id = a.id AND d.locale = @DefaultLocale
                WHERE sa.sponsor_id IN @Ids
                ORDER BY sa.sort_order
                """;
            var stories = (await connection.QueryAsync<StoryRow>(new CommandDefinition(storySql, q, cancellationToken: ct))).AsList();

            const string activationSql = """
                SELECT sa.id AS Id, sa.sponsor_id AS SponsorId, sa.happened_on AS HappenedOn,
                       COALESCE(NULLIF(r.title, N''), d.title) AS Title, COALESCE(NULLIF(r.result_summary, N''), d.result_summary) AS ResultSummary
                FROM sponsor_activations sa
                LEFT JOIN sponsor_activations_i18n r ON r.sponsor_activation_id = sa.id AND r.locale = @Locale
                LEFT JOIN sponsor_activations_i18n d ON d.sponsor_activation_id = sa.id AND d.locale = @DefaultLocale
                WHERE sa.sponsor_id IN @Ids AND sa.club_id = @ClubId
                ORDER BY sa.happened_on DESC, sa.sort_order, sa.row_seq
                """;
            var activations = (await connection.QueryAsync<ActivationRow>(new CommandDefinition(activationSql, q, cancellationToken: ct))).AsList();

            var images = new List<ActivationImageRow>();
            if (activations.Count > 0)
            {
                const string imageSql = """
                    SELECT sponsor_activation_id AS ActivationId, image_key AS ImageKey, image_width AS ImageWidth, image_height AS ImageHeight,
                           image_alt_zh AS ImageAltZh, image_alt_en AS ImageAltEn
                    FROM sponsor_activation_images WHERE sponsor_activation_id IN @ActivationIds ORDER BY sort_order, row_seq
                    """;
                images = (await connection.QueryAsync<ActivationImageRow>(new CommandDefinition(
                    imageSql, new { ActivationIds = activations.Select(a => a.Id).ToList() }, cancellationToken: ct))).AsList();
            }

            const string linkSql = """
                SELECT l.sponsor_id AS SponsorId, cp.slug AS Slug, COALESCE(NULLIF(r.name, N''), d.name) AS Name
                FROM charity_program_sponsors l
                JOIN charity_programs cp ON cp.id = l.charity_program_id AND cp.status = 'published' AND (cp.club_id = @ClubId OR cp.club_id IS NULL)
                LEFT JOIN charity_programs_i18n r ON r.charity_program_id = cp.id AND r.locale = @Locale
                LEFT JOIN charity_programs_i18n d ON d.charity_program_id = cp.id AND d.locale = @DefaultLocale
                WHERE l.sponsor_id IN @Ids
                ORDER BY cp.is_pinned DESC, cp.sort_order, cp.start_on DESC
                """;
            var links = (await connection.QueryAsync<ProgramLinkRow>(new CommandDefinition(linkSql, q, cancellationToken: ct))).AsList();

            return (IReadOnlyList<SponsorDto>)sponsors.Select(s => new SponsorDto
            {
                Id = s.Id, Slug = s.Slug, Tier = s.Tier, SortOrder = s.SortOrder, Name = s.Name, IsFallbackLocale = s.IsFallbackLocale, Content = s.Content,
                LogoDarkUrl = imageUrls.Resolve(s.LogoDarkKey), LogoLightUrl = imageUrls.Resolve(s.LogoLightKey),
                LogoDarkWidth = s.LogoDarkKey is null ? null : s.LogoDarkWidth, LogoDarkHeight = s.LogoDarkKey is null ? null : s.LogoDarkHeight,
                LogoLightWidth = s.LogoLightKey is null ? null : s.LogoLightWidth, LogoLightHeight = s.LogoLightKey is null ? null : s.LogoLightHeight,
                LogoAlt = s.LogoDarkKey is null && s.LogoLightKey is null ? null : s.LogoAlt,
                Stories = stories.Where(x => x.SponsorId == s.Id).Select(x => new SponsorStoryDto { Slug = x.Slug, Title = x.Title, Summary = x.Summary }).ToList(),
                Activations = activations.Where(a => a.SponsorId == s.Id).Select(a => new SponsorActivationDto
                {
                    Id = a.Id, Title = a.Title, ResultSummary = a.ResultSummary,
                    HappenedOn = a.HappenedOn is null ? null : DateOnly.FromDateTime(a.HappenedOn.Value),
                    Images = images.Where(i => i.ActivationId == a.Id).Select(i => new SponsorActivationImageDto
                    {
                        ImageUrl = imageUrls.Resolve(i.ImageKey)!, ThumbUrl = imageUrls.Resolve(Images.ImageObjectKey.ForThumbnail(i.ImageKey)),
                        ImageWidth = i.ImageWidth, ImageHeight = i.ImageHeight, Alt = GalleryImageAlt.Pick(dbLocale, i.ImageAltZh, i.ImageAltEn),
                    }).ToList(),
                }).ToList(),
                CharityPrograms = links.Where(l => l.SponsorId == s.Id).Select(l => new PartnerCharityProgramDto { Slug = l.Slug, Name = l.Name }).ToList(),
            }).ToList();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SponsorPackageDto>> ListPackagesAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync("sponsors", scope.ClubCode, dbLocale, "packages", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string sql = """
                SELECT p.id AS Id, p.slug AS Slug, p.sort_order AS SortOrder, p.is_price_public AS IsPricePublic,
                       p.price_min AS PriceMin, p.price_max AS PriceMax,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.content, N''), d.content) AS Content,
                       COALESCE(NULLIF(r.benefit_list, N''), d.benefit_list) AS BenefitList,
                       COALESCE(NULLIF(r.audience, N''), d.audience) AS Audience,
                       CAST(CASE WHEN @Locale <> @DefaultLocale AND NULLIF(r.name, N'') IS NULL THEN 1 ELSE 0 END AS bit) AS IsFallbackLocale
                FROM sponsor_packages p
                LEFT JOIN sponsor_packages_i18n r ON r.sponsor_package_id = p.id AND r.locale = @Locale
                LEFT JOIN sponsor_packages_i18n d ON d.sponsor_package_id = p.id AND d.locale = @DefaultLocale
                WHERE p.club_id = @ClubId AND p.status = 'published'
                ORDER BY p.sort_order, p.row_seq
                """;
            var rows = await connection.QueryAsync<PackageRow>(new CommandDefinition(
                sql, new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));
            return (IReadOnlyList<SponsorPackageDto>)rows.Select(r => new SponsorPackageDto
            {
                Id = r.Id, Slug = r.Slug, SortOrder = r.SortOrder, Name = r.Name, IsFallbackLocale = r.IsFallbackLocale, Content = r.Content, BenefitList = r.BenefitList, Audience = r.Audience,
                PriceMin = r.IsPricePublic ? r.PriceMin : null, PriceMax = r.IsPricePublic ? r.PriceMax : null,
            }).ToList();
        }, cancellationToken);
}
