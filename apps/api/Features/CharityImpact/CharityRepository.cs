using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.CharityImpact;

/// <summary>
/// 公開讀取：B5 慈善與社會影響（前台 11.2–11.4 與捐款導流）。慈善表 <c>club_id</c> 可為空（共同列）——
/// 套用「俱樂部專屬優先、回退共同」（<see cref="ClubOrSharedSql"/>）。⚠️ 慈善單元只屬於磐石主站
/// （藍鯨規劃書 §1.3 不設 11），但端點本身依請求的俱樂部回資料，藍鯨沒有資料就是空清單。
/// 快取 entity <c>charity</c>（後台 B5 寫入與贊助商／夥伴關聯異動都會失效）。
/// 🔴 只輸出：已發布計畫、標記公開的統計項目；不輸出任何金額類未公開數字、聯絡窗口、款項紀錄。
/// </summary>
public sealed class CharityRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private const string Entity = "charity";

    private sealed record ProgramRow(
        Guid Id, string Slug, DateTime? StartOn, DateTime? EndOn, bool IsPinned, string? CoverKey, int? CoverWidth, int? CoverHeight, string? CoverAlt, Guid CharityId, string? Name,
        string? TargetAudience, string? Content, string? DonationContent, string? CharityName);
    private sealed record OrgRow(Guid Id, string Slug, string? LogoKey, int? LogoWidth, int? LogoHeight, string? LogoAlt, string? WebsiteUrl, string? Name, string? Intro);
    private sealed record ImageRow(Guid ParentId, string ImageKey, int? ImageWidth, int? ImageHeight, string? ImageAltZh, string? ImageAltEn);
    private sealed record LinkedRow(
        Guid ProgramId, string Slug, string? Name, string? LogoDarkKey, string? LogoLightKey,
        int? LogoDarkWidth, int? LogoDarkHeight, int? LogoLightWidth, int? LogoLightHeight, string? LogoAlt);
    private sealed record ArticleRow(Guid ProgramId, string Slug, string? Title);
    private sealed record RecordRow(
        Guid Id, DateTime? HappenedOn, Guid CharityId, string? ImageKey, int? ImageWidth, int? ImageHeight, string? DonationContent,
        string? Location, string? BriefDescription, string? ImageAlt, string? ProgramSlug, string? ProgramName, string? CharityName, string? CharityLogoKey,
        int? CharityLogoWidth, int? CharityLogoHeight, string? CharityLogoAlt);
    private sealed record MetricRow(string? Name, string? Unit, int? Value, string? ProgramSlug);

    private static object P(ClubScope scope, string dbLocale) => new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale };

    private static string Progress(DateTime? endOn, DateOnly today) => endOn is not null && DateOnly.FromDateTime(endOn.Value) < today ? "completed" : "ongoing";

    private static DateOnly? D(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    public async Task<PagedResult<CharityProgramListItemDto>> ListProgramsAsync(
        ClubScope scope, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await cache.GetOrCreateAsync(Entity, scope.ClubCode, dbLocale, $"programs:{page}:{pageSize}:{today:yyyyMMdd}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string countSql = "SELECT COUNT(*) FROM charity_programs WHERE (club_id = @ClubId OR club_id IS NULL) AND status = 'published'";
            const string sql = """
                SELECT cp.id AS Id, cp.slug AS Slug, cp.start_on AS StartOn, cp.end_on AS EndOn, cp.is_pinned AS IsPinned,
                       cp.cover_key AS CoverKey, cp.cover_width AS CoverWidth, cp.cover_height AS CoverHeight,
                       COALESCE(NULLIF(r.cover_alt, N''), d.cover_alt) AS CoverAlt, cp.charity_id AS CharityId,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.target_audience, N''), d.target_audience) AS TargetAudience,
                       CAST(NULL AS nvarchar(max)) AS Content, CAST(NULL AS nvarchar(max)) AS DonationContent,
                       COALESCE(NULLIF(cr.name, N''), cd.name) AS CharityName
                FROM charity_programs cp
                LEFT JOIN charity_programs_i18n r ON r.charity_program_id = cp.id AND r.locale = @Locale
                LEFT JOIN charity_programs_i18n d ON d.charity_program_id = cp.id AND d.locale = @DefaultLocale
                LEFT JOIN charities_i18n cr ON cr.charity_id = cp.charity_id AND cr.locale = @Locale
                LEFT JOIN charities_i18n cd ON cd.charity_id = cp.charity_id AND cd.locale = @DefaultLocale
                WHERE (cp.club_id = @ClubId OR cp.club_id IS NULL) AND cp.status = 'published'
                ORDER BY cp.is_pinned DESC, cp.sort_order, cp.start_on DESC, cp.row_seq DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                """;
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, P(scope, dbLocale), cancellationToken: ct));
            var rows = await connection.QueryAsync<ProgramRow>(new CommandDefinition(
                sql, new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, Offset = (page - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
            var items = rows.Select(r => new CharityProgramListItemDto
            {
                Id = r.Id, Slug = r.Slug, Name = r.Name, TargetAudience = r.TargetAudience, StartOn = D(r.StartOn), EndOn = D(r.EndOn),
                Progress = Progress(r.EndOn, today), IsPinned = r.IsPinned, CoverUrl = imageUrls.Resolve(r.CoverKey), CharityName = r.CharityName,
                CoverWidth = r.CoverKey is null ? null : r.CoverWidth, CoverHeight = r.CoverKey is null ? null : r.CoverHeight,
                CoverAlt = r.CoverKey is null ? null : r.CoverAlt,
            }).ToList();
            return new PagedResult<CharityProgramListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
        }, cancellationToken);
    }

    public async Task<CharityProgramDetailDto?> GetProgramAsync(ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await cache.GetOrCreateAsync(Entity, scope.ClubCode, dbLocale, $"program:{slug}:{today:yyyyMMdd}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string sql = """
                SELECT TOP 1 cp.id AS Id, cp.slug AS Slug, cp.start_on AS StartOn, cp.end_on AS EndOn, cp.is_pinned AS IsPinned,
                       cp.cover_key AS CoverKey, cp.cover_width AS CoverWidth, cp.cover_height AS CoverHeight,
                       COALESCE(NULLIF(r.cover_alt, N''), d.cover_alt) AS CoverAlt, cp.charity_id AS CharityId,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.target_audience, N''), d.target_audience) AS TargetAudience,
                       COALESCE(NULLIF(CAST(r.content AS nvarchar(max)), N''), CAST(d.content AS nvarchar(max))) AS Content,
                       COALESCE(NULLIF(r.donation_content, N''), d.donation_content) AS DonationContent,
                       CAST(NULL AS nvarchar(200)) AS CharityName
                FROM charity_programs cp
                LEFT JOIN charity_programs_i18n r ON r.charity_program_id = cp.id AND r.locale = @Locale
                LEFT JOIN charity_programs_i18n d ON d.charity_program_id = cp.id AND d.locale = @DefaultLocale
                WHERE cp.slug = @Slug AND (cp.club_id = @ClubId OR cp.club_id IS NULL) AND cp.status = 'published'
                ORDER BY CASE WHEN cp.club_id IS NULL THEN 1 ELSE 0 END
                """;
            var p = new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, Slug = slug };
            var program = await connection.QueryFirstOrDefaultAsync<ProgramRow>(new CommandDefinition(sql, p, cancellationToken: ct));
            if (program is null)
            {
                return null;
            }

            var org = await connection.QueryFirstOrDefaultAsync<OrgRow>(new CommandDefinition("""
                SELECT c.id AS Id, c.slug AS Slug, c.logo_key AS LogoKey, c.logo_width AS LogoWidth, c.logo_height AS LogoHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt, c.website_url AS WebsiteUrl,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.intro, N''), d.intro) AS Intro
                FROM charities c
                LEFT JOIN charities_i18n r ON r.charity_id = c.id AND r.locale = @Locale
                LEFT JOIN charities_i18n d ON d.charity_id = c.id AND d.locale = @DefaultLocale
                WHERE c.id = @CharityId
                """, new { program.CharityId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));

            var images = await connection.QueryAsync<ImageRow>(new CommandDefinition(
                "SELECT charity_program_id AS ParentId, image_key AS ImageKey, image_width AS ImageWidth, image_height AS ImageHeight, image_alt_zh AS ImageAltZh, image_alt_en AS ImageAltEn FROM charity_program_images WHERE charity_program_id = @Id ORDER BY sort_order, row_seq",
                new { program.Id }, cancellationToken: ct));

            var partners = await connection.QueryAsync<LinkedRow>(new CommandDefinition("""
                SELECT l.charity_program_id AS ProgramId, x.slug AS Slug, COALESCE(NULLIF(r.name, N''), d.name) AS Name,
                       x.logo_dark_key AS LogoDarkKey, x.logo_light_key AS LogoLightKey,
                       x.logo_dark_width AS LogoDarkWidth, x.logo_dark_height AS LogoDarkHeight,
                       x.logo_light_width AS LogoLightWidth, x.logo_light_height AS LogoLightHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt
                FROM charity_program_partners l JOIN partners x ON x.id = l.partner_id
                LEFT JOIN partners_i18n r ON r.partner_id = x.id AND r.locale = @Locale
                LEFT JOIN partners_i18n d ON d.partner_id = x.id AND d.locale = @DefaultLocale
                WHERE l.charity_program_id = @Id ORDER BY x.sort_order, x.row_seq
                """, new { program.Id, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));

            var sponsors = await connection.QueryAsync<LinkedRow>(new CommandDefinition("""
                SELECT l.charity_program_id AS ProgramId, x.slug AS Slug, COALESCE(NULLIF(r.name, N''), d.name) AS Name,
                       x.logo_dark_key AS LogoDarkKey, x.logo_light_key AS LogoLightKey,
                       x.logo_dark_width AS LogoDarkWidth, x.logo_dark_height AS LogoDarkHeight,
                       x.logo_light_width AS LogoLightWidth, x.logo_light_height AS LogoLightHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt
                FROM charity_program_sponsors l JOIN sponsors x ON x.id = l.sponsor_id
                LEFT JOIN sponsors_i18n r ON r.sponsor_id = x.id AND r.locale = @Locale
                LEFT JOIN sponsors_i18n d ON d.sponsor_id = x.id AND d.locale = @DefaultLocale
                WHERE l.charity_program_id = @Id ORDER BY x.sort_order, x.row_seq
                """, new { program.Id, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));

            var articles = await connection.QueryAsync<ArticleRow>(new CommandDefinition("""
                SELECT l.charity_program_id AS ProgramId, a.slug AS Slug, COALESCE(NULLIF(r.title, N''), d.title) AS Title
                FROM charity_program_articles l
                JOIN articles a ON a.id = l.article_id AND a.status = 'published' AND (a.club_id = @ClubId OR a.club_id IS NULL)
                LEFT JOIN articles_i18n r ON r.article_id = a.id AND r.locale = @Locale
                LEFT JOIN articles_i18n d ON d.article_id = a.id AND d.locale = @DefaultLocale
                WHERE l.charity_program_id = @Id ORDER BY l.sort_order
                """, new { program.Id, scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));

            return new CharityProgramDetailDto
            {
                Id = program.Id, Slug = program.Slug, Name = program.Name, TargetAudience = program.TargetAudience,
                StartOn = D(program.StartOn), EndOn = D(program.EndOn), Progress = Progress(program.EndOn, today),
                CoverUrl = imageUrls.Resolve(program.CoverKey), Content = program.Content,
                CoverWidth = program.CoverKey is null ? null : program.CoverWidth, CoverHeight = program.CoverKey is null ? null : program.CoverHeight,
                CoverAlt = program.CoverKey is null ? null : program.CoverAlt, DonationContent = program.DonationContent,
                Charity = org is null ? null : new PublicCharityOrgDto
                {
                    Slug = org.Slug, Name = org.Name, Intro = org.Intro, LogoUrl = imageUrls.Resolve(org.LogoKey), WebsiteUrl = org.WebsiteUrl,
                    LogoWidth = org.LogoKey is null ? null : org.LogoWidth, LogoHeight = org.LogoKey is null ? null : org.LogoHeight,
                    LogoAlt = org.LogoKey is null ? null : org.LogoAlt,
                },
                Images = images.Select(i => new PublicCharityImageDto
                {
                    ImageUrl = imageUrls.Resolve(i.ImageKey)!, ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)),
                    ImageWidth = i.ImageWidth, ImageHeight = i.ImageHeight, Alt = GalleryImageAlt.Pick(dbLocale, i.ImageAltZh, i.ImageAltEn),
                }).ToList(),
                Partners = partners.Select(x => new CharityLinkedItemDto
                {
                    Slug = x.Slug, Name = x.Name, LogoDarkUrl = imageUrls.Resolve(x.LogoDarkKey), LogoLightUrl = imageUrls.Resolve(x.LogoLightKey),
                    LogoDarkWidth = x.LogoDarkKey is null ? null : x.LogoDarkWidth, LogoDarkHeight = x.LogoDarkKey is null ? null : x.LogoDarkHeight,
                    LogoLightWidth = x.LogoLightKey is null ? null : x.LogoLightWidth, LogoLightHeight = x.LogoLightKey is null ? null : x.LogoLightHeight,
                    LogoAlt = x.LogoDarkKey is null && x.LogoLightKey is null ? null : x.LogoAlt,
                }).ToList(),
                Sponsors = sponsors.Select(x => new CharityLinkedItemDto
                {
                    Slug = x.Slug, Name = x.Name, LogoDarkUrl = imageUrls.Resolve(x.LogoDarkKey), LogoLightUrl = imageUrls.Resolve(x.LogoLightKey),
                    LogoDarkWidth = x.LogoDarkKey is null ? null : x.LogoDarkWidth, LogoDarkHeight = x.LogoDarkKey is null ? null : x.LogoDarkHeight,
                    LogoLightWidth = x.LogoLightKey is null ? null : x.LogoLightWidth, LogoLightHeight = x.LogoLightKey is null ? null : x.LogoLightHeight,
                    LogoAlt = x.LogoDarkKey is null && x.LogoLightKey is null ? null : x.LogoAlt,
                }).ToList(),
                Articles = articles.Select(a => new CharityArticleLinkDto { Slug = a.Slug, Title = a.Title }).ToList(),
            };
        }, cancellationToken);
    }

    public async Task<PagedResult<ImpactRecordDto>> ListRecordsAsync(
        ClubScope scope, string? programSlug, int? year, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(Entity, scope.ClubCode, dbLocale, $"records:{programSlug}:{year}:{page}:{pageSize}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string where = """
                WHERE (ir.club_id = @ClubId OR ir.club_id IS NULL)
                  AND (@Year IS NULL OR YEAR(ir.happened_on) = @Year)
                  AND (@ProgramSlug IS NULL OR cp.slug = @ProgramSlug)
                """;
            var countSql = $"SELECT COUNT(*) FROM impact_records ir LEFT JOIN charity_programs cp ON cp.id = ir.charity_program_id {where}";
            var sql = $"""
                SELECT ir.id AS Id, ir.happened_on AS HappenedOn, ir.charity_id AS CharityId, ir.image_key AS ImageKey,
                       ir.image_width AS ImageWidth, ir.image_height AS ImageHeight,
                       COALESCE(NULLIF(r.donation_content, N''), d.donation_content) AS DonationContent,
                       COALESCE(NULLIF(r.location, N''), d.location) AS Location,
                       COALESCE(NULLIF(r.brief_description, N''), d.brief_description) AS BriefDescription,
                       COALESCE(NULLIF(r.image_alt, N''), d.image_alt) AS ImageAlt,
                       cp.slug AS ProgramSlug,
                       COALESCE(NULLIF(pr.name, N''), pd.name) AS ProgramName,
                       COALESCE(NULLIF(cr.name, N''), cd.name) AS CharityName, c.logo_key AS CharityLogoKey, c.logo_width AS CharityLogoWidth, c.logo_height AS CharityLogoHeight,
                       COALESCE(NULLIF(cr.logo_alt, N''), cd.logo_alt) AS CharityLogoAlt
                FROM impact_records ir
                JOIN charities c ON c.id = ir.charity_id
                LEFT JOIN charity_programs cp ON cp.id = ir.charity_program_id
                LEFT JOIN impact_records_i18n r ON r.impact_record_id = ir.id AND r.locale = @Locale
                LEFT JOIN impact_records_i18n d ON d.impact_record_id = ir.id AND d.locale = @DefaultLocale
                LEFT JOIN charities_i18n cr ON cr.charity_id = c.id AND cr.locale = @Locale
                LEFT JOIN charities_i18n cd ON cd.charity_id = c.id AND cd.locale = @DefaultLocale
                LEFT JOIN charity_programs_i18n pr ON pr.charity_program_id = cp.id AND pr.locale = @Locale
                LEFT JOIN charity_programs_i18n pd ON pd.charity_program_id = cp.id AND pd.locale = @DefaultLocale
                {where}
                ORDER BY ir.is_pinned DESC, ir.sort_order, ir.happened_on DESC, ir.row_seq DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                """;
            var p = new
            {
                scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale, Year = year, ProgramSlug = programSlug,
                Offset = (page - 1) * pageSize, PageSize = pageSize,
            };
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, p, cancellationToken: ct));
            var rows = (await connection.QueryAsync<RecordRow>(new CommandDefinition(sql, p, cancellationToken: ct))).AsList();

            var galleries = new Dictionary<Guid, List<PublicCharityImageDto>>();
            if (rows.Count > 0)
            {
                var images = await connection.QueryAsync<ImageRow>(new CommandDefinition(
                    "SELECT impact_record_id AS ParentId, image_key AS ImageKey, image_width AS ImageWidth, image_height AS ImageHeight, image_alt_zh AS ImageAltZh, image_alt_en AS ImageAltEn FROM impact_record_images WHERE impact_record_id IN @Ids ORDER BY sort_order, row_seq",
                    new { Ids = rows.Select(r => r.Id).ToList() }, cancellationToken: ct));
                foreach (var g in images.GroupBy(i => i.ParentId))
                {
                    galleries[g.Key] = g.Select(i => new PublicCharityImageDto
                    {
                        ImageUrl = imageUrls.Resolve(i.ImageKey)!, ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)),
                        ImageWidth = i.ImageWidth, ImageHeight = i.ImageHeight, Alt = GalleryImageAlt.Pick(dbLocale, i.ImageAltZh, i.ImageAltEn),
                    }).ToList();
                }
            }

            var items = rows.Select(r => new ImpactRecordDto
            {
                Id = r.Id, HappenedOn = D(r.HappenedOn), CharityName = r.CharityName, CharityLogoUrl = imageUrls.Resolve(r.CharityLogoKey),
                DonationContent = r.DonationContent, Location = r.Location, BriefDescription = r.BriefDescription,
                ImageUrl = imageUrls.Resolve(r.ImageKey), ImageWidth = r.ImageWidth, ImageHeight = r.ImageHeight,
                ImageAlt = r.ImageKey is null ? null : r.ImageAlt,
                CharityLogoWidth = r.CharityLogoKey is null ? null : r.CharityLogoWidth, CharityLogoHeight = r.CharityLogoKey is null ? null : r.CharityLogoHeight,
                CharityLogoAlt = r.CharityLogoKey is null ? null : r.CharityLogoAlt,
                Images = galleries.GetValueOrDefault(r.Id) ?? [], ProgramSlug = r.ProgramSlug, ProgramName = r.ProgramName,
            }).ToList();
            return new PagedResult<ImpactRecordDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
        }, cancellationToken);

    /// <summary>年份篩選用：有事蹟紀錄的年份（新到舊）。</summary>
    public async Task<IReadOnlyList<int>> ListRecordYearsAsync(ClubScope scope, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(Entity, scope.ClubCode, CacheDimensions.AnyLocale, "record-years", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var years = await connection.QueryAsync<int>(new CommandDefinition(
                "SELECT DISTINCT YEAR(happened_on) FROM impact_records WHERE happened_on IS NOT NULL AND (club_id = @ClubId OR club_id IS NULL) ORDER BY 1 DESC",
                new { scope.ClubId }, cancellationToken: ct));
            return (IReadOnlyList<int>)years.ToList();
        }, cancellationToken);

    public async Task<ImpactSummaryDto> GetImpactAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(Entity, scope.ClubCode, dbLocale, "impact", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var p = P(scope, dbLocale);
            // 🔴 只取 is_public = 1；金額類項目預設不公開（後台預設 false）。
            const string metricSql = """
                SELECT COALESCE(NULLIF(r.name, N''), d.name) AS Name, COALESCE(NULLIF(r.unit, N''), d.unit) AS Unit,
                       m.metric_value AS Value, cp.slug AS ProgramSlug
                FROM impact_metrics m
                LEFT JOIN impact_metrics_i18n r ON r.impact_metric_id = m.id AND r.locale = @Locale
                LEFT JOIN impact_metrics_i18n d ON d.impact_metric_id = m.id AND d.locale = @DefaultLocale
                LEFT JOIN charity_programs cp ON cp.id = m.charity_program_id
                WHERE m.is_public = 1 AND (m.club_id = @ClubId OR m.club_id IS NULL)
                  AND (cp.id IS NULL OR cp.status = 'published')
                ORDER BY m.sort_order, m.row_seq
                """;
            var metrics = await connection.QueryAsync<MetricRow>(new CommandDefinition(metricSql, p, cancellationToken: ct));

            const string orgSql = """
                SELECT c.id AS Id, c.slug AS Slug, c.logo_key AS LogoKey, c.logo_width AS LogoWidth, c.logo_height AS LogoHeight,
                       COALESCE(NULLIF(r.logo_alt, N''), d.logo_alt) AS LogoAlt, c.website_url AS WebsiteUrl,
                       COALESCE(NULLIF(r.name, N''), d.name) AS Name, CAST(NULL AS nvarchar(max)) AS Intro
                FROM charities c
                LEFT JOIN charities_i18n r ON r.charity_id = c.id AND r.locale = @Locale
                LEFT JOIN charities_i18n d ON d.charity_id = c.id AND d.locale = @DefaultLocale
                WHERE (c.club_id = @ClubId OR c.club_id IS NULL)
                  AND (EXISTS (SELECT 1 FROM impact_records ir WHERE ir.charity_id = c.id AND (ir.club_id = @ClubId OR ir.club_id IS NULL))
                    OR EXISTS (SELECT 1 FROM charity_programs cp WHERE cp.charity_id = c.id AND cp.status = 'published' AND (cp.club_id = @ClubId OR cp.club_id IS NULL)))
                ORDER BY c.row_seq
                """;
            var orgs = (await connection.QueryAsync<OrgRow>(new CommandDefinition(orgSql, p, cancellationToken: ct))).AsList();

            var recordCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM impact_records WHERE club_id = @ClubId OR club_id IS NULL", p, cancellationToken: ct));
            var regions = await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT DISTINCT COALESCE(NULLIF(r.location, N''), d.location)
                FROM impact_records ir
                LEFT JOIN impact_records_i18n r ON r.impact_record_id = ir.id AND r.locale = @Locale
                LEFT JOIN impact_records_i18n d ON d.impact_record_id = ir.id AND d.locale = @DefaultLocale
                WHERE (ir.club_id = @ClubId OR ir.club_id IS NULL) AND COALESCE(NULLIF(r.location, N''), d.location) IS NOT NULL
                ORDER BY 1
                """, p, cancellationToken: ct));

            return new ImpactSummaryDto
            {
                Metrics = metrics.Select(m => new ImpactMetricDto { Name = m.Name, Unit = m.Unit, Value = m.Value, ProgramSlug = m.ProgramSlug }).ToList(),
                CharityCount = orgs.Count, DonationItemCount = recordCount, Regions = regions.ToList(),
                Charities = orgs.Select(o => new PublicCharityOrgDto
                {
                    Slug = o.Slug, Name = o.Name, LogoUrl = imageUrls.Resolve(o.LogoKey), WebsiteUrl = o.WebsiteUrl,
                    LogoWidth = o.LogoKey is null ? null : o.LogoWidth, LogoHeight = o.LogoKey is null ? null : o.LogoHeight,
                    LogoAlt = o.LogoKey is null ? null : o.LogoAlt,
                }).ToList(),
            };
        }, cancellationToken);

    public async Task<CharityCtaDto> GetCtaAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(Entity, scope.ClubCode, dbLocale, "cta", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var rows = (await connection.QueryAsync<(string SettingKey, string? SettingValue, string? Zh, string? Local)>(new CommandDefinition("""
                SELECT s.setting_key AS SettingKey, s.setting_value AS SettingValue, d.value AS Zh, r.value AS Local
                FROM settings s
                LEFT JOIN settings_i18n r ON r.setting_id = s.id AND r.locale = @Locale
                LEFT JOIN settings_i18n d ON d.setting_id = s.id AND d.locale = @DefaultLocale
                WHERE s.club_id = @ClubId AND s.setting_key IN
                  (N'charity.donation_url', N'charity.donation_cta', N'charity.fan_cta', N'charity.corporate_cta', N'charity.corporate_url')
                """, P(scope, dbLocale), cancellationToken: ct))).ToDictionary(x => x.SettingKey);

            string? Text(string key) => rows.TryGetValue(key, out var r) ? RequestLocale.Pick(r.Local, r.Zh) : null;
            var url = rows.TryGetValue("charity.donation_url", out var u) ? u.SettingValue : null;
            return new CharityCtaDto
            {
                DonationUrl = url,
                // 沒有捐款平台網址就沒有導流，也就不輸出按鈕文案（避免前台顯示一顆沒有去處的按鈕）。
                DonationCta = url is null ? null : Text("charity.donation_cta"),
                FanCta = url is null ? null : Text("charity.fan_cta") ?? Text("charity.donation_cta"),
                CorporateCta = Text("charity.corporate_cta"),
                CorporateUrl = rows.TryGetValue("charity.corporate_url", out var c) ? c.SettingValue : null,
            };
        }, cancellationToken);
}
