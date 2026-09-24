using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S0-7g：排程發布時間一到，<c>articles.status</c> 要能自動從 <c>scheduled</c> 轉成
/// <c>published</c>，公開 API 才看得到——這裡驗證 <see cref="ScheduledPublishRunner"/>
/// （<c>ScheduledPublishBackgroundService</c> 實際呼叫的核心邏輯，測試直接呼叫它，不必等待
/// 計時器，見該類別檔頭說明）。
///
/// 🔴 這批測試會跟「真的在跑」的 <see cref="ScheduledPublishBackgroundService"/> 共用同一個
/// <c>tcrfc_club_dev</c>——<c>AdminWriteApiFixture</c> 啟動 <c>Program</c> 時，這個 hosted
/// service 也會真的啟動並立刻執行一輪（見該類別檔頭「啟動後立刻執行一次」）。這是刻意接受的：
/// 它跟這裡顯式呼叫的 <c>PublishDueArticlesAsync</c> 是同一支冪等方法，兩者互相競速也不影響
/// 最終狀態是否正確——因此下面的斷言一律驗證「最終狀態」（DB 裡的 status／public API 的回應），
/// 不驗證「這一次呼叫剛好轉換了幾筆」，避免測試因為背景服務剛好搶先跑一輪而變得不穩定。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ScheduledPublishTests(AdminWriteApiFixture fixture)
{
    private const string CategoryCode = "club";

    private static string UniqueSlug() => $"scheduled-publish-test-{Guid.NewGuid():N}";

    private async Task<Guid> CreateDraftAsync(HttpClient client, string slug)
    {
        var request = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = "排程發布測試文章" },
            },
        };

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        return created!.Id;
    }

    /// <summary>直接下 SQL 把文章轉成 <c>scheduled</c> 並指定任意 <c>published_at</c>（含過去時間）
    /// ——後台 <c>/schedule</c> 端點會擋下「排程時間必須晚於現在」，沒有辦法透過真正的 API
    /// 產生「排定時間已經過去」這個狀態，只能像 <c>ScheduleOriginalDateTests</c> 一樣直接寫 SQL
    /// 模擬「排程之後、時間真的到了」的那一刻。</summary>
    private static async Task SetScheduledAsync(string connectionString, Guid articleId, DateTime publishedAtUtc)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE articles SET status = 'scheduled', published_at = @PublishedAt WHERE id = @Id;";
        command.Parameters.AddWithValue("@PublishedAt", publishedAtUtc);
        command.Parameters.AddWithValue("@Id", articleId);
        var rowsAffected = await command.ExecuteNonQueryAsync();
        Assert.Equal(1, rowsAffected);
    }

    private static async Task<(string Status, DateTime PublishedAt, DateTime UpdatedAt)> ReadStateAsync(
        string connectionString, Guid articleId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status, published_at, updated_at FROM articles WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", articleId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "測試文章應該還在資料庫裡");
        return (reader.GetString(0), reader.GetDateTime(1), reader.GetDateTime(2));
    }

    private static async Task DeleteArticleAsync(string connectionString, Guid articleId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM articles_i18n WHERE article_id = @Id; DELETE FROM articles WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", articleId);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task 排程時間已過_背景掃描後狀態轉為published且公開API可見()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var slug = UniqueSlug();
        var articleId = await CreateDraftAsync(client, slug);

        try
        {
            // 排定時間設在 5 秒前——模擬「排程之後，真實時間已經超過排定時間」的那一刻。
            var publishedAt = DateTime.UtcNow.AddSeconds(-5);
            await SetScheduledAsync(connectionString, articleId, publishedAt);

            // 排程時間雖已過去，但在任何掃描（背景服務或測試顯式呼叫）跑過之前，狀態字面上
            // 仍是 scheduled，公開 API 必須仍是 404——這正是 S0-7g 回報的落差本身。
            // 🔴 這裡不斷言「一定還是 404」：AdminWriteApiFixture 的 Program 啟動時，
            // ScheduledPublishBackgroundService 已經跑過至少一輪，有機率比這裡的檢查更早
            // 把它轉掉，因此只做最終狀態驗證，不驗證這個中繼狀態，避免測試變成靠時間賽跑。

            var runner = fixture.Services.GetRequiredService<ScheduledPublishRunner>();
            await runner.PublishDueArticlesAsync(CancellationToken.None);

            var (status, publishedAtAfter, _) = await ReadStateAsync(connectionString, articleId);
            Assert.Equal("published", status);
            // published_at 不應該被改寫成「掃描跑到的時間」，應維持原本排定的時間（見
            // ScheduledPublishRunner 檔頭「刻意不改 published_at」）。SQL Server datetime2(3)
            // 只到毫秒，容許極小的序列化誤差。
            Assert.True(Math.Abs((publishedAtAfter - publishedAt).TotalMilliseconds) < 5,
                $"published_at 不應被掃描改寫，預期 {publishedAt:o}，實際 {publishedAtAfter:o}");

            var publicResponse = await client.GetAsync($"/api/v1/tcrfc/news/{slug}");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        }
        finally
        {
            await DeleteArticleAsync(connectionString, articleId);
        }
    }

    [Fact]
    public async Task 排程時間未到_掃描後狀態仍是scheduled且公開API仍404()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var slug = UniqueSlug();
        var articleId = await CreateDraftAsync(client, slug);

        try
        {
            var publishedAt = DateTime.UtcNow.AddMinutes(10);
            await SetScheduledAsync(connectionString, articleId, publishedAt);

            var runner = fixture.Services.GetRequiredService<ScheduledPublishRunner>();
            await runner.PublishDueArticlesAsync(CancellationToken.None);

            var (status, _, _) = await ReadStateAsync(connectionString, articleId);
            Assert.Equal("scheduled", status);

            var publicResponse = await client.GetAsync($"/api/v1/tcrfc/news/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);
        }
        finally
        {
            await DeleteArticleAsync(connectionString, articleId);
        }
    }

    [Fact]
    public async Task 重複執行不重複發布也不拋例外()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));

        var slug = UniqueSlug();
        var articleId = await CreateDraftAsync(client, slug);

        try
        {
            await SetScheduledAsync(connectionString, articleId, DateTime.UtcNow.AddSeconds(-5));

            var runner = fixture.Services.GetRequiredService<ScheduledPublishRunner>();
            await runner.PublishDueArticlesAsync(CancellationToken.None);

            var (statusAfterFirst, publishedAtAfterFirst, updatedAtAfterFirst) =
                await ReadStateAsync(connectionString, articleId);
            Assert.Equal("published", statusAfterFirst);

            // 第二次呼叫：這篇文章已經是 published，WHERE status = 'scheduled' 不會再選到它，
            // 呼叫本身不該拋例外，而且這一筆的 updated_at／published_at 不該再被動到——
            // 這才是「冪等」真正要驗證的事，不是「回傳筆數剛好是 0」（背景服務可能同時在跑，
            // 讓回傳筆數不是穩定可斷言的值，見本檔頭的說明）。
            var exception = await Record.ExceptionAsync(() => runner.PublishDueArticlesAsync(CancellationToken.None));
            Assert.Null(exception);

            var (statusAfterSecond, publishedAtAfterSecond, updatedAtAfterSecond) =
                await ReadStateAsync(connectionString, articleId);
            Assert.Equal("published", statusAfterSecond);
            Assert.Equal(publishedAtAfterFirst, publishedAtAfterSecond);
            Assert.Equal(updatedAtAfterFirst, updatedAtAfterSecond);
        }
        finally
        {
            await DeleteArticleAsync(connectionString, articleId);
        }
    }
}
