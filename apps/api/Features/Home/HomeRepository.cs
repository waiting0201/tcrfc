using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Home;

/// <summary>
/// 公開讀取：B3 首頁編排（Hero 輪播與九大區塊開關）。<c>banners</c>／<c>home_sections</c> 皆為
/// 50 張必填 <c>club_id</c> 之一，不套用 <c>ClubOrSharedSql</c>（那條規則只給 9 張可為空的表用）。
/// 唯讀路徑走 Dapper（比照 <c>Features/News/ArticlesRepository.cs</c> 的既有分工：寫入走
/// EF Core、公開唯讀走 Dapper）。
/// </summary>
public sealed class HomeRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string BannersEntity = "banners";
    private const string HomeSectionsEntity = "home-sections";

    /// <summary>只回目前在上架期間內的輪播（<c>start_at</c>／<c>end_at</c> 皆可為 <c>null</c>＝不限制
    /// 該端）。時間比較用 <c>SYSUTCDATETIME()</c>（資料庫時鐘），這裡沒有「寫入時取應用程式時鐘、
    /// 讀取時跟資料庫時鐘比較」這個 E-48 的坑——<c>start_at</c>／<c>end_at</c> 是後台人員自己選定的
    /// 未來或過去日期，不是「現在」這個時間點本身，不受兩個時鐘飄移影響。</summary>
    public async Task<IReadOnlyList<BannerDto>> ListBannersAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            BannersEntity, scope.ClubCode, dbLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string sql = """
                    SELECT b.id AS Id, b.media_type AS MediaType, b.image_key AS ImageKey,
                           b.image_width AS ImageWidth, b.image_height AS ImageHeight, b.video_key AS VideoKey,
                           b.sort_order AS SortOrder,
                           bi.title AS Title, bi.subtitle AS Subtitle, bi.image_alt AS ImageAlt,
                           bi.cta_1_label AS Cta1Label, bi.cta_1_url AS Cta1Url,
                           bi.cta_2_label AS Cta2Label, bi.cta_2_url AS Cta2Url,
                           bi.locale AS Locale
                    FROM banners b
                    LEFT JOIN banners_i18n bi ON bi.banner_id = b.id AND bi.locale IN @Locales
                    WHERE b.club_id = @ClubId
                      AND (b.start_at IS NULL OR b.start_at <= SYSUTCDATETIME())
                      AND (b.end_at IS NULL OR b.end_at >= SYSUTCDATETIME())
                    ORDER BY b.sort_order
                    """;
                var locales = dbLocale == RequestLocale.DefaultDbLocale
                    ? new[] { dbLocale }
                    : new[] { dbLocale, RequestLocale.DefaultDbLocale };

                var rows = (await connection.QueryAsync<BannerI18nJoinRow>(new CommandDefinition(
                    sql, new { scope.ClubId, Locales = locales }, cancellationToken: ct))).ToList();

                return rows
                    .GroupBy(r => r.Id)
                    .Select(g =>
                    {
                        var first = g.First();
                        var byLocale = g.Where(r => r.Locale is not null).ToDictionary(r => r.Locale!);
                        byLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);
                        byLocale.TryGetValue(dbLocale, out var requested);

                        return new BannerDto
                        {
                            Id = first.Id,
                            MediaType = first.MediaType,
                            ImageKey = first.ImageKey,
                            ImageWidth = first.ImageWidth,
                            ImageHeight = first.ImageHeight,
                            VideoKey = first.VideoKey,
                            SortOrder = first.SortOrder,
                            Title = RequestLocale.Pick(requested?.Title, fallback?.Title),
                            Subtitle = RequestLocale.Pick(requested?.Subtitle, fallback?.Subtitle),
                            ImageAlt = RequestLocale.Pick(requested?.ImageAlt, fallback?.ImageAlt),
                            Cta1Label = RequestLocale.Pick(requested?.Cta1Label, fallback?.Cta1Label),
                            Cta1Url = RequestLocale.Pick(requested?.Cta1Url, fallback?.Cta1Url),
                            Cta2Label = RequestLocale.Pick(requested?.Cta2Label, fallback?.Cta2Label),
                            Cta2Url = RequestLocale.Pick(requested?.Cta2Url, fallback?.Cta2Url),
                        };
                    })
                    .OrderBy(b => b.SortOrder)
                    .ToList() as IReadOnlyList<BannerDto>;
            },
            cancellationToken);
    }

    private sealed record BannerI18nJoinRow(
        Guid Id, string MediaType, string ImageKey, int? ImageWidth, int? ImageHeight, string? VideoKey, int SortOrder,
        string? Title, string? Subtitle, string? ImageAlt,
        string? Cta1Label, string? Cta1Url, string? Cta2Label, string? Cta2Url, string? Locale);

    private sealed record HomeSectionRow(string SectionCode, bool IsEnabled, int SortOrder, Guid? FeaturedBannerId);

    /// <summary>回傳全部九個區塊（含未啟用的），依 <c>sort_order</c> 排序——前台自行判斷
    /// <see cref="HomeSectionDto.IsEnabled"/> 決定要不要渲染，不在伺服器端先濾掉，讓前台之後
    /// 若要做「編輯預覽」這類需要看到停用區塊的情境時不需要另開一支端點。</summary>
    public async Task<IReadOnlyList<HomeSectionDto>> ListHomeSectionsAsync(ClubScope scope, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            HomeSectionsEntity, scope.ClubCode, CacheDimensions.AnyLocale, CacheDimensions.NoQualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string sql = """
                    SELECT section_code AS SectionCode, is_enabled AS IsEnabled, sort_order AS SortOrder,
                           featured_banner_id AS FeaturedBannerId
                    FROM home_sections
                    WHERE club_id = @ClubId
                    ORDER BY sort_order
                    """;

                var rows = (await connection.QueryAsync<HomeSectionRow>(new CommandDefinition(
                    sql, new { scope.ClubId }, cancellationToken: ct))).ToList();

                return rows
                    .Select(r => new HomeSectionDto
                    {
                        SectionCode = r.SectionCode,
                        IsEnabled = r.IsEnabled,
                        SortOrder = r.SortOrder,
                        FeaturedBannerId = r.FeaturedBannerId,
                    })
                    .ToList() as IReadOnlyList<HomeSectionDto>;
            },
            cancellationToken);
    }
}
