using Dapper;
using Tcrfc.Api.Data;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 孤立頁面偵測（S1-12，主站規劃書 §4.8 H「內部連結建議：依網站層級提示未被連結的孤立頁面」）。
///
/// 🔴 **這是字串比對的啟發式做法，不是完整的連結圖或 DOM 解析**：掃描已發布 <c>Page</c>／
/// <c>Article</c> 彼此的 <c>page_blocks.content</c>／<c>articles_i18n.body</c>（皆為 JSON，
/// 轉字串後直接做子字串比對）是否含有對方的公開網址子字串。任何形式的「連結」只要原始 JSON
/// 內容裡出現過那段網址文字，就視為「有被連到」——不解析 HTML／JSON 結構本身、不驗證那段文字
/// 真的是超連結的 <c>href</c> 而不是純文字提及，也**不知道前台目前尚未資料庫化的靜態導覽選單**
/// （B1 頁面雖是規格上「網站的靜態頁面路由」，但目前 <c>apps/web</c> 的 80 個既有單元頁仍是
/// mockup 搬遷來的靜態 Vue 檔案，不是查 <c>pages</c> 表渲染，見 apps/api/README.md「B1 頁面
/// 管理」「網址名稱」一節的既有落差說明）——這代表**這份報表目前只能反映「內容彼此之間的引用」，
/// 反映不出「這一頁有沒有被主選單或麵包屑連到」**。是本輪在資料現況下的最務實做法，不是完整方案，
/// 見 apps/api/README.md「S1-12」段。
///
/// 排除自我引用：檢查某一頁是否被引用時，排除該頁自己的內容（避免頁面內文剛好提到自己的網址
/// 就被誤判為「已被連結」）。
/// </summary>
public sealed class AdminSeoReportRepository(IClubSqlConnectionFactory connectionFactory)
{
    private sealed record LinkableEntityRow(Guid Id, string Slug, string? TitleZh);

    private sealed record ContentBlockRow(string SourceKey, string? Content);

    public async Task<OrphanPageReportDto> GetOrphanPagesAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        const string pagesSql = """
            SELECT p.id AS Id, p.slug AS Slug, pi.seo_title AS TitleZh
            FROM pages p
            LEFT JOIN pages_i18n pi ON pi.page_id = p.id AND pi.locale = 'zh-Hant'
            WHERE p.club_id = @ClubId AND p.status = 'published'
            """;

        // articles.club_id 可為空（兩隊共同），跟公開讀取一致採「俱樂部專屬優先、回退共同」
        // 的判斷範圍（ClubOrSharedSql 的同一個精神，這裡是後台報表，直接寫 club_id IS NULL 條件
        // 即可，不需要引入公開讀取那一整套快取與 DTO 形狀）。
        const string articlesSql = """
            SELECT a.id AS Id, a.slug AS Slug, ai.title AS TitleZh
            FROM articles a
            LEFT JOIN articles_i18n ai ON ai.article_id = a.id AND ai.locale = 'zh-Hant'
            WHERE (a.club_id = @ClubId OR a.club_id IS NULL) AND a.status = 'published'
            """;

        const string pageBlocksSql = """
            SELECT LOWER(CAST(p.id AS nvarchar(36))) AS SourceKey, CAST(pb.content AS nvarchar(max)) AS Content
            FROM page_blocks pb
            JOIN pages p ON p.id = pb.page_id
            WHERE p.club_id = @ClubId AND p.status = 'published'
            """;

        const string articleBodySql = """
            SELECT LOWER(CAST(a.id AS nvarchar(36))) AS SourceKey, CAST(ai.body AS nvarchar(max)) AS Content
            FROM articles_i18n ai
            JOIN articles a ON a.id = ai.article_id
            WHERE (a.club_id = @ClubId OR a.club_id IS NULL) AND a.status = 'published'
            """;

        var pageRows = (await connection.QueryAsync<LinkableEntityRow>(
            new CommandDefinition(pagesSql, new { scope.ClubId }, cancellationToken: cancellationToken))).ToList();
        var articleRows = (await connection.QueryAsync<LinkableEntityRow>(
            new CommandDefinition(articlesSql, new { scope.ClubId }, cancellationToken: cancellationToken))).ToList();

        var pageBlockRows = (await connection.QueryAsync<ContentBlockRow>(
            new CommandDefinition(pageBlocksSql, new { scope.ClubId }, cancellationToken: cancellationToken))).ToList();
        var articleBodyRows = (await connection.QueryAsync<ContentBlockRow>(
            new CommandDefinition(articleBodySql, new { scope.ClubId }, cancellationToken: cancellationToken))).ToList();

        var allBlocks = pageBlockRows.Concat(articleBodyRows)
            .Where(b => !string.IsNullOrEmpty(b.Content))
            .ToList();

        bool IsReferencedByOthers(Guid entityId, string path)
        {
            var selfKey = entityId.ToString().ToLowerInvariant();
            return allBlocks.Any(b =>
                !string.Equals(b.SourceKey, selfKey, StringComparison.Ordinal)
                && b.Content!.Contains(path, StringComparison.OrdinalIgnoreCase));
        }

        var orphans = new List<OrphanPageDto>();

        foreach (var row in pageRows)
        {
            var path = $"/zh/{row.Slug}/";
            if (!IsReferencedByOthers(row.Id, path))
            {
                orphans.Add(new OrphanPageDto { EntityType = "page", Id = row.Id, Path = path, TitleZh = row.TitleZh });
            }
        }

        foreach (var row in articleRows)
        {
            var path = $"/zh/news/{row.Slug}/";
            if (!IsReferencedByOthers(row.Id, path))
            {
                orphans.Add(new OrphanPageDto { EntityType = "article", Id = row.Id, Path = path, TitleZh = row.TitleZh });
            }
        }

        return new OrphanPageReportDto { Items = orphans };
    }
}
