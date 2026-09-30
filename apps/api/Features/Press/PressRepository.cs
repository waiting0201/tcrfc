using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Press;

/// <summary>
/// 公開讀取：B6 媒體專區（前台 7.8）。<c>press_resources.club_id</c> 可為空（共同列）——套用
/// 「俱樂部專屬優先、回退共同」。只回 <c>status = 'published'</c>。快取 entity <c>press</c>；
/// 下載次數累加是資料庫端原子遞增，不經快取（比照瀏覽數，見 <c>FaqsRepository</c>）。
/// </summary>
public sealed class PressRepository(
    IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls, IDocumentPublicUrlResolver documentUrls)
{
    private sealed record Row(
        Guid Id, string Slug, string ResourceType, DateTime? PublishedOn, int? FileBytes, string FileKey, string? CoverKey, string? Title, string? Description);

    public async Task<PagedResult<PressResourceDto>> ListAsync(
        ClubScope scope, string? resourceType, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync("press", scope.ClubCode, dbLocale, $"{resourceType}:{page}:{pageSize}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            // 同一 slug 若同時有本俱樂部與共同列，只留本俱樂部那筆（專屬優先）。
            const string cte = """
                WITH visible AS (
                  SELECT pr.*, ROW_NUMBER() OVER (PARTITION BY pr.slug ORDER BY CASE WHEN pr.club_id IS NULL THEN 1 ELSE 0 END) AS rn
                  FROM press_resources pr
                  WHERE (pr.club_id = @ClubId OR pr.club_id IS NULL) AND pr.status = 'published'
                    AND (@ResourceType IS NULL OR pr.resource_type = @ResourceType)
                )
                """;
            var countSql = cte + " SELECT COUNT(*) FROM visible WHERE rn = 1";
            var listSql = cte + """

                SELECT v.id AS Id, v.slug AS Slug, v.resource_type AS ResourceType, v.published_on AS PublishedOn, v.file_bytes AS FileBytes,
                       v.file_key AS FileKey, v.cover_key AS CoverKey,
                       COALESCE(NULLIF(r.title, N''), d.title) AS Title, COALESCE(NULLIF(r.description, N''), d.description) AS Description
                FROM visible v
                LEFT JOIN press_resources_i18n r ON r.press_resource_id = v.id AND r.locale = @Locale
                LEFT JOIN press_resources_i18n d ON d.press_resource_id = v.id AND d.locale = @DefaultLocale
                WHERE v.rn = 1
                ORDER BY v.sort_order, v.published_on DESC, v.row_seq DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                """;
            var p = new
            {
                scope.ClubId, ResourceType = resourceType, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale,
                Offset = (page - 1) * pageSize, PageSize = pageSize,
            };
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, p, cancellationToken: ct));
            var rows = await connection.QueryAsync<Row>(new CommandDefinition(listSql, p, cancellationToken: ct));
            var items = rows.Select(r => new PressResourceDto
            {
                Id = r.Id, Slug = r.Slug, ResourceType = r.ResourceType, Title = r.Title, Description = r.Description,
                PublishedOn = r.PublishedOn is null ? null : DateOnly.FromDateTime(r.PublishedOn.Value),
                FileBytes = r.FileBytes, FileExtension = Path.GetExtension(r.FileKey),
                // 高解析圖沒有獨立封面，用主檔 640px 衍生檔。
                CoverUrl = r.ResourceType == "hires_image"
                    ? imageUrls.Resolve(ImageObjectKey.ForLongEdge(r.FileKey, 640))
                    : imageUrls.Resolve(r.CoverKey),
                DownloadPath = $"/api/v1/{scope.ClubCode}/press/{r.Slug}/download",
            }).ToList();
            return new PagedResult<PressResourceDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
        }, cancellationToken);

    /// <summary>累計一次下載並回傳檔案網址；找不到（或未發布、或儲存體沒有設定網址）回傳 <c>null</c>，此時不累計。</summary>
    public async Task<string?> RegisterDownloadAsync(ClubScope scope, string slug, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<(Guid Id, string ResourceType, string FileKey)?>(new CommandDefinition("""
            SELECT TOP 1 id AS Id, resource_type AS ResourceType, file_key AS FileKey
            FROM press_resources
            WHERE slug = @Slug AND status = 'published' AND (club_id = @ClubId OR club_id IS NULL)
            ORDER BY CASE WHEN club_id IS NULL THEN 1 ELSE 0 END
            """, new { Slug = slug, scope.ClubId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var url = row.Value.ResourceType == "hires_image" ? imageUrls.Resolve(row.Value.FileKey) : documentUrls.Resolve(row.Value.FileKey);
        if (url is null)
        {
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE press_resources SET download_count = download_count + 1 WHERE id = @Id", new { row.Value.Id }, cancellationToken: cancellationToken));
        return url;
    }
}
