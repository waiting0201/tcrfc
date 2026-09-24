using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Pages;

/// <summary>
/// 公開頁面讀取（B1 頁面管理的前台落點）。形狀比照 <c>Features/News/ArticlesRepository.cs</c>，
/// 差異：<c>pages.club_id</c> 必填（不是 9 張可為空表之一），**沒有「俱樂部專屬優先、回退共同」
/// 這條路由規則**——每個頁面都明確屬於一個俱樂部，直接 <c>WHERE club_id = @ClubId</c> 即可
/// （見 apps/api/README.md「B1 頁面管理」「我的判斷」一節對這點的說明）。
/// </summary>
public sealed class PagesRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    /// <summary>與 <c>Features/AdminPages/AdminPagesRepository.PublicDetailEntity</c>、
    /// <c>Features/News/ScheduledPublishRunner</c> 三處字面值必須完全一致（docs/17 §4）。</summary>
    private const string DetailEntity = "page-detail";

    private sealed record PageRow(Guid Id, string Slug, DateTime? PublishedAt);
    private sealed record SeoRow(string Locale, string? SeoTitle, string? SeoDescription);
    private sealed record BlockRow(string BlockType, string? Content, int SortOrder);
    private sealed record PreviewRow(Guid PageId, int VersionNo, string? Snapshot, string Slug, string Status);

    /// <summary>
    /// 單一頁面。⛔ 只回傳 <c>status = 'published'</c> 且已到發布時間的頁面——草稿與排程中的頁面
    /// 不對外，即使猜得到網址名稱（跟 <c>ArticlesRepository.GetBySlugAsync</c> 同一條業務規則）。
    /// **快取**：qualifier 是 <paramref name="slug"/>；🔴 排程發布的 TTL 延遲說明與
    /// <c>ArticlesRepository.ListAsync</c> 同一份（見該處註解），這裡不重複貼一次。
    /// </summary>
    public async Task<PageDetailDto?> GetBySlugAsync(ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            DetailEntity, scope.ClubCode, dbLocale, slug,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string sql = """
                    SELECT p.id AS Id, p.slug AS Slug, p.published_at AS PublishedAt
                    FROM pages p
                    WHERE p.club_id = @ClubId AND p.slug = @Slug
                      AND p.status = 'published' AND (p.published_at IS NULL OR p.published_at <= SYSUTCDATETIME())
                    """;

                var pageRow = await connection.QuerySingleOrDefaultAsync<PageRow>(new CommandDefinition(
                    sql, new { scope.ClubId, Slug = slug }, cancellationToken: ct));

                if (pageRow is null)
                {
                    return null;
                }

                var (seoTitle, seoDescription) = await LoadSeoAsync(connection, pageRow.Id, dbLocale, ct);
                var blocks = await LoadBlocksAsync(connection, pageRow.Id, dbLocale, ct);

                return new PageDetailDto
                {
                    Id = pageRow.Id,
                    Slug = pageRow.Slug,
                    SeoTitle = seoTitle,
                    SeoDescription = seoDescription,
                    PublishedAt = pageRow.PublishedAt,
                    Blocks = blocks,
                };
            },
            cancellationToken);
    }

    /// <summary>
    /// 未發布可分享的預覽（規劃書 §4.2 B1「預覽連結（未發布可分享）」）。權杖本身就是授權——
    /// 猜不到就看不到，不需要登入也不需要俱樂部路由段。**刻意不接快取**：預覽的使用情境是
    /// 「編輯者剛存檔、馬上把連結傳給利害關係人確認」，快取住舊內容會讓「已經改過了但連結還是
    /// 顯示舊的」這種情況發生在最不該發生的場景——分享預覽的當下。而且權杖本身沒有到期或撤銷欄位
    /// （見 apps/api/README.md「已知缺口」），流量極低，直接回源沒有效能疑慮。
    /// </summary>
    public async Task<PagePreviewDto?> GetPreviewAsync(string token, string dbLocale, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string sql = """
            SELECT v.page_id AS PageId, v.version_no AS VersionNo, CAST(v.snapshot AS nvarchar(max)) AS Snapshot,
                   p.slug AS Slug, p.status AS Status
            FROM page_versions v
            JOIN pages p ON p.id = v.page_id
            WHERE v.preview_token = @Token
            """;

        var row = await connection.QuerySingleOrDefaultAsync<PreviewRow>(
            new CommandDefinition(sql, new { Token = token }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var snapshot = JsonNode.Parse(row.Snapshot ?? "{}") as JsonObject ?? new JsonObject();
        var seoNode = snapshot["seo"] as JsonObject;
        var zhSeo = seoNode?["zh"] as JsonObject;
        var enSeo = seoNode?["en"] as JsonObject;

        var seoTitle = PickLocaleString(dbLocale, GetStringValue(zhSeo, "seoTitle"), GetStringValue(enSeo, "seoTitle"));
        var seoDescription = PickLocaleString(dbLocale, GetStringValue(zhSeo, "seoDescription"), GetStringValue(enSeo, "seoDescription"));

        var blocks = new List<PageBlockPublicDto>();
        if (snapshot["blocks"] is JsonArray blockArray)
        {
            for (var i = 0; i < blockArray.Count; i++)
            {
                if (blockArray[i] is not JsonObject blockObj)
                {
                    continue;
                }

                var blockType = GetStringValue(blockObj, "blockType") ?? string.Empty;
                var localizedContent = PageContentLocalizer.Localize(blockObj["content"]?.DeepClone(), dbLocale);
                blocks.Add(new PageBlockPublicDto
                {
                    BlockType = blockType,
                    Content = ToJsonElement(localizedContent),
                    SortOrder = i,
                });
            }
        }

        return new PagePreviewDto
        {
            PageId = row.PageId,
            VersionNo = row.VersionNo,
            Status = row.Status,
            Slug = row.Slug,
            SeoTitle = seoTitle,
            SeoDescription = seoDescription,
            Blocks = blocks,
        };
    }

    private static async Task<(string? SeoTitle, string? SeoDescription)> LoadSeoAsync(
        System.Data.IDbConnection connection, Guid pageId, string dbLocale, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT locale AS Locale, seo_title AS SeoTitle, seo_description AS SeoDescription
            FROM pages_i18n
            WHERE page_id = @PageId AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = (await connection.QueryAsync<SeoRow>(new CommandDefinition(
            sql, new { PageId = pageId, Locales = locales }, cancellationToken: cancellationToken))).ToList();

        var byLocale = rows.ToDictionary(r => r.Locale);
        byLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);
        byLocale.TryGetValue(dbLocale, out var requested);

        return (RequestLocale.Pick(requested?.SeoTitle, fallback?.SeoTitle), RequestLocale.Pick(requested?.SeoDescription, fallback?.SeoDescription));
    }

    private static async Task<IReadOnlyList<PageBlockPublicDto>> LoadBlocksAsync(
        System.Data.IDbConnection connection, Guid pageId, string dbLocale, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT block_type AS BlockType, CAST(content AS nvarchar(max)) AS Content, sort_order AS SortOrder
            FROM page_blocks
            WHERE page_id = @PageId
            ORDER BY sort_order ASC
            """;

        var rows = (await connection.QueryAsync<BlockRow>(new CommandDefinition(
            sql, new { PageId = pageId }, cancellationToken: cancellationToken))).ToList();

        return rows.Select(r =>
        {
            var contentNode = string.IsNullOrWhiteSpace(r.Content) ? null : JsonNode.Parse(r.Content);
            var localized = PageContentLocalizer.Localize(contentNode, dbLocale);
            return new PageBlockPublicDto { BlockType = r.BlockType, Content = ToJsonElement(localized), SortOrder = r.SortOrder };
        }).ToList();
    }

    private static string? GetStringValue(JsonObject? obj, string property)
        => obj?[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? PickLocaleString(string dbLocale, string? zh, string? en)
        => RequestLocale.Pick(dbLocale == "en" ? en : zh, zh);

    private static JsonElement ToJsonElement(JsonNode? node)
        => node is null ? default : JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
}
