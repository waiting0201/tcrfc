using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminFaqs;
using Tcrfc.Api.Features.Faqs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S1-6：公開讀取端點（B4 常見問題）＋輕量互動端點（瀏覽數、回饋、零結果搜尋）。
/// 用 <see cref="AdminWriteApiFixture"/> 是因為需要先用後台寫入端點建立測試資料，
/// 公開端點本身不需要登入。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class PublicFaqsTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task FaqCategories_公開列表至少十筆_含雙語名稱()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/faq-categories?lang=en");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<FaqCategoryDto>>(TestJson.Options);
        Assert.True(categories!.Count >= 10);
        Assert.Contains(categories, c => c.Slug == "other" && c.Name == "Other");
    }

    [Fact]
    public async Task Faqs_公開列表只回已發布且回退共用內容_未發布不可見()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var sharedFaqId = await InsertSharedFaqAsync(categoryId, "published");

        var published = await CreateFaqAsync(client, categoryId, status: "published");
        var draft = await CreateFaqAsync(client, categoryId, status: "draft");

        try
        {
            var response = await client.GetAsync("/api/v1/tcrfc/faqs?pageSize=200");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<PagedResult<FaqListItemDto>>(TestJson.Options);
            var ids = result!.Items.Select(i => i.Id).ToList();

            Assert.Contains(published.Id, ids);
            Assert.Contains(sharedFaqId, ids); // 俱樂部專屬 ＋ 回退共同
            Assert.DoesNotContain(draft.Id, ids); // 草稿不可見

            var sharedItem = result.Items.Single(i => i.Id == sharedFaqId);
            Assert.True(sharedItem.IsShared);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{published.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{draft.Id}");
            await DeleteFaqByIdAsync(sharedFaqId);
        }
    }

    [Fact]
    public async Task Faqs_公開列表依分類篩選()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryA = await GetAnyFaqCategoryIdAsync();
        var categoryB = await GetAnyFaqCategoryIdAsync(exclude: categoryA);
        var categoryASlug = await GetCategorySlugAsync(categoryA);

        var inCategoryA = await CreateFaqAsync(client, categoryA, status: "published");
        var inCategoryB = await CreateFaqAsync(client, categoryB, status: "published");

        try
        {
            var response = await client.GetAsync($"/api/v1/tcrfc/faqs?category={categoryASlug}&pageSize=200");
            var result = await response.Content.ReadFromJsonAsync<PagedResult<FaqListItemDto>>(TestJson.Options);
            var ids = result!.Items.Select(i => i.Id).ToList();

            Assert.Contains(inCategoryA.Id, ids);
            Assert.DoesNotContain(inCategoryB.Id, ids);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{inCategoryA.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{inCategoryB.Id}");
        }
    }

    [Fact]
    public async Task Faqs_公開單題_未發布查不到_跨俱樂部查不到()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var slug = $"s1-6-public-{Guid.NewGuid():N}";
        var faq = await CreateFaqAsync(client, categoryId, status: "published", slug: slug);

        try
        {
            var okResponse = await client.GetAsync($"/api/v1/tcrfc/faqs/{slug}");
            Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

            var crossClubResponse = await client.GetAsync($"/api/v1/bw/faqs/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, crossClubResponse.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq.Id}");
        }
    }

    [Fact]
    public async Task Faqs_瀏覽數與回饋端點會遞增_對不存在的題目回404()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var slug = $"s1-6-public-{Guid.NewGuid():N}";
        var faq = await CreateFaqAsync(client, categoryId, status: "published", slug: slug);

        try
        {
            var viewResponse = await client.PostAsync($"/api/v1/tcrfc/faqs/{slug}/views", content: null);
            Assert.Equal(HttpStatusCode.NoContent, viewResponse.StatusCode);

            var helpfulResponse = await client.PostAsJsonAsync($"/api/v1/tcrfc/faqs/{slug}/feedback", new FaqFeedbackRequest { Helpful = true });
            Assert.Equal(HttpStatusCode.NoContent, helpfulResponse.StatusCode);

            var (viewCount, helpfulCount) = await GetFaqCountsAsync(faq.Id);
            Assert.Equal(1, viewCount);
            Assert.Equal(1, helpfulCount);

            var notFoundResponse = await client.PostAsync($"/api/v1/tcrfc/faqs/not-a-real-slug/views", content: null);
            Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq.Id}");
        }
    }

    [Fact]
    public async Task 零結果搜尋回報_第一次建立第二次累加()
    {
        using var client = fixture.CreateClient();
        var keyword = $"s1-6-nohit-{Guid.NewGuid():N}";

        try
        {
            var first = await client.PostAsJsonAsync("/api/v1/tcrfc/faqs/search-misses", new FaqSearchMissRequest { Keyword = keyword });
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/v1/tcrfc/faqs/search-misses", new FaqSearchMissRequest { Keyword = keyword });
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

            var hitCount = await GetSearchMissHitCountAsync(keyword);
            Assert.Equal(2, hitCount);
        }
        finally
        {
            await DeleteSearchMissAsync(keyword);
        }
    }

    [Fact]
    public async Task 零結果搜尋回報_大小寫前後空白全形半形正規化後合併計數()
    {
        // S1-8（docs/18-work-errors.md E-51 補強）：faq_search_misses 是 (club_id, keyword) 彙總列，
        // 寫入前沒正規化就會被拆成好幾筆不同的列，排行因此失真。這裡驗證三種變形
        // （大寫＋前後空白、全形）最終都正規化成同一個關鍵字、合併進同一筆的 hit_count。
        using var client = fixture.CreateClient();
        var baseKeyword = $"norm{Guid.NewGuid():N}"; // 純小寫英數，字元都落在全半形轉換範圍內。

        var variants = new[]
        {
            baseKeyword,
            $"  {baseKeyword.ToUpperInvariant()}  ",
            ToFullWidth(baseKeyword),
        };

        try
        {
            foreach (var variant in variants)
            {
                var response = await client.PostAsJsonAsync("/api/v1/tcrfc/faqs/search-misses", new FaqSearchMissRequest { Keyword = variant });
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }

            var hitCount = await GetSearchMissHitCountAsync(baseKeyword);
            Assert.Equal(variants.Length, hitCount);
        }
        finally
        {
            await DeleteSearchMissAsync(baseKeyword);
        }
    }

    [Fact]
    public async Task 零結果搜尋回報_空白關鍵字不寫入任何列()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/tcrfc/faqs/search-misses", new FaqSearchMissRequest { Keyword = "   " });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var hitCount = await GetSearchMissHitCountAsync("   ".Trim());
        Assert.Null(hitCount);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    /// <summary>把 ASCII 可列印字元（<c>!</c>–<c>~</c>）轉成對應的全形字元，供正規化測試用——
    /// 逐字對應 <c>Common.SearchKeywordNormalizer</c> 全形轉半形那段轉換範圍的反向操作。</summary>
    private static string ToFullWidth(string value)
        => new(value.Select(c => c is >= '!' and <= '~' ? (char)(c + 0xFEE0) : c).ToArray());

    private async Task<AdminFaqDetailDto> CreateFaqAsync(HttpClient client, Guid categoryId, string status, string? slug = null)
    {
        var request = new CreateFaqRequest
        {
            Slug = slug ?? $"s1-6-public-{Guid.NewGuid():N}",
            CategoryIds = [categoryId],
            SortOrder = 0,
            Status = status,
            Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "公開測試問題？", Answer = "公開測試答案。" } },
        };
        var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/faqs", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options))!;
    }

    private async Task<HttpClient> CreateContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetAnyFaqCategoryIdAsync(Guid? exclude = null)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = exclude is null
            ? "SELECT TOP 1 id FROM faq_categories ORDER BY sort_order"
            : "SELECT TOP 1 id FROM faq_categories WHERE id <> @Exclude ORDER BY sort_order";
        if (exclude is Guid excludeId)
        {
            command.Parameters.AddWithValue("@Exclude", excludeId);
        }
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> GetCategorySlugAsync(Guid categoryId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT slug FROM faq_categories WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", categoryId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> InsertSharedFaqAsync(Guid categoryId, string status)
    {
        var faqId = Guid.NewGuid();
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO faqs (id, club_id, slug, status, sort_order) VALUES (@Id, NULL, @Slug, @Status, 0);
            INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@Id, N'zh-Hant', N'公開共用測試問題？', N'公開共用測試答案。');
            INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@Id, @CategoryId);
            """;
        command.Parameters.AddWithValue("@Id", faqId);
        command.Parameters.AddWithValue("@Slug", $"shared-public-faq-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@CategoryId", categoryId);
        await command.ExecuteNonQueryAsync();
        return faqId;
    }

    private static async Task DeleteFaqByIdAsync(Guid faqId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM faq_category_links WHERE faq_id = @Id;
            DELETE FROM faqs_i18n WHERE faq_id = @Id;
            DELETE FROM faqs WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", faqId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(int ViewCount, int HelpfulCount)> GetFaqCountsAsync(Guid faqId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT view_count, helpful_count FROM faqs WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", faqId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<int?> GetSearchMissHitCountAsync(string keyword)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT hit_count FROM faq_search_misses WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND keyword = @Keyword";
        command.Parameters.AddWithValue("@Keyword", keyword);
        var result = await command.ExecuteScalarAsync();
        return result is null ? null : (int)result;
    }

    /// <summary>清掉測試寫入的 <c>faq_search_misses</c> 列。沒清的話每跑一次就在開發庫累積一筆，
    /// 累積到 50 筆會把 <c>AdminFaqsAndCategoriesTests</c> 的排行測試（取前 50 名）擠出榜外。</summary>
    private static async Task DeleteSearchMissAsync(string keyword)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM faq_search_misses WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND keyword = @Keyword";
        command.Parameters.AddWithValue("@Keyword", keyword);
        await command.ExecuteNonQueryAsync();
    }
}
