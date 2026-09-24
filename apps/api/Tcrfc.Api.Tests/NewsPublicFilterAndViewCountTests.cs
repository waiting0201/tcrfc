using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-5：公開新聞端點的「公開端點對應的篩選」（標籤）與瀏覽數統計。用 <see cref="AdminWriteApiFixture"/>
/// 是因為需要先用後台寫入端點建立＋發布測試文章，公開端點本身不需要登入。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class NewsPublicFilterAndViewCountTests(AdminWriteApiFixture fixture)
{
    private static string UniqueSlug(string label) => $"s1-5-public-{label}-{Guid.NewGuid():N}";

    private static string UniqueTagSlug(string label) => $"s1-5-public-tag-{label}-{Guid.NewGuid():N}";

    private static async Task<string> ContentEditorTokenAsync() =>
        await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");

    private async Task<AdminArticleDetailDto> CreateAndPublishAsync(
        HttpClient adminClient, string slug, IReadOnlyList<AdminArticleTagInput>? tags = null)
    {
        var createRequest = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"公開篩選測試 {slug}" } },
            Tags = tags,
        };
        var createResponse = await adminClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        var publishResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();
        return (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
    }

    private async Task DeleteBestEffortAsync(HttpClient adminClient, Guid id, DateTime expectedUpdatedAt)
    {
        var url = $"/api/v1/admin/tcrfc/news/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}";
        await adminClient.DeleteAsync(url);
    }

    [Fact]
    public async Task 公開新聞列表可以用標籤篩選_只回傳掛了那個標籤的文章()
    {
        using var adminClient = fixture.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ContentEditorTokenAsync());
        using var publicClient = fixture.CreateClient();

        var tagSlug = UniqueTagSlug("filter");
        var tagged = await CreateAndPublishAsync(
            adminClient, UniqueSlug("tagged"),
            [new AdminArticleTagInput { Slug = tagSlug, NameZh = "篩選測試標籤" }]);
        var untagged = await CreateAndPublishAsync(adminClient, UniqueSlug("untagged"));

        try
        {
            var response = await publicClient.GetFromJsonAsync<PagedResult<ArticleListItemDto>>(
                $"/api/v1/tcrfc/news?tag={tagSlug}&pageSize=50", TestJson.Options);

            Assert.NotNull(response);
            Assert.Contains(response!.Items, i => i.Id == tagged.Id);
            Assert.DoesNotContain(response.Items, i => i.Id == untagged.Id);

            var taggedItem = response.Items.Single(i => i.Id == tagged.Id);
            Assert.Contains(taggedItem.Tags, t => t.Slug == tagSlug);
        }
        finally
        {
            await DeleteBestEffortAsync(adminClient, tagged.Id, tagged.UpdatedAt);
            await DeleteBestEffortAsync(adminClient, untagged.Id, untagged.UpdatedAt);
        }
    }

    [Fact]
    public async Task 文章詳情頁回傳標籤_核心價值標籤與關聯()
    {
        using var adminClient = fixture.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ContentEditorTokenAsync());
        using var publicClient = fixture.CreateClient();

        var tagSlug = UniqueTagSlug("detail");
        var d1TeamIdResponse = await publicClient.GetAsync("/api/v1/tcrfc/players"); // 熱身：確保 club 已解析成功，非必要斷言
        Assert.True(d1TeamIdResponse.IsSuccessStatusCode);

        var createRequest = new CreateArticleRequest
        {
            Slug = UniqueSlug("detail-full"),
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "公開詳情頁完整欄位測試" } },
            Tags = [new AdminArticleTagInput { Slug = tagSlug, NameZh = "詳情頁測試標籤" }],
            CoreValueTags = ["excellence"],
        };
        var createResponse = await adminClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
        var publishResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();
        var published = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var detail = await publicClient.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{created.Slug}", TestJson.Options);
            Assert.NotNull(detail);
            Assert.Contains(detail!.Tags, t => t.Slug == tagSlug);
            Assert.Contains("excellence", detail.CoreValueTags);
        }
        finally
        {
            await DeleteBestEffortAsync(adminClient, published.Id, published.UpdatedAt);
        }
    }

    [Fact]
    public async Task 瀏覽數端點呼叫後真的遞增_不影響公開列表能不能查到()
    {
        using var adminClient = fixture.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ContentEditorTokenAsync());
        using var publicClient = fixture.CreateClient();

        var published = await CreateAndPublishAsync(adminClient, UniqueSlug("view-count"));

        try
        {
            var before = await publicClient.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{published.Slug}", TestJson.Options);
            Assert.Equal(0, before!.ViewCount);

            var incrementResponse1 = await publicClient.PostAsync($"/api/v1/tcrfc/news/{published.Slug}/views", content: null);
            Assert.Equal(HttpStatusCode.NoContent, incrementResponse1.StatusCode);
            var incrementResponse2 = await publicClient.PostAsync($"/api/v1/tcrfc/news/{published.Slug}/views", content: null);
            Assert.Equal(HttpStatusCode.NoContent, incrementResponse2.StatusCode);

            // ⚠️ ApiFixture／AdminWriteApiFixture 都是 NoOp 快取（REDIS_HOST 清空），公開讀取
            // 沒有快取在中間擋，這裡才能直接用下一次 GET 斷言遞增後的值——真正接了 Redis 快取的
            // 環境下，瀏覽數容許最多一個 TTL 的顯示延遲（見 ArticlesRepository.IncrementViewCountAsync
            // 上的說明），不是這支測試要驗證的範圍。
            var afterAdmin = await adminClient.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{published.Id}", TestJson.Options);
            Assert.Equal(2, afterAdmin!.ViewCount);
        }
        finally
        {
            var probe = await adminClient.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{published.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(adminClient, published.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 瀏覽數端點對草稿或不存在的文章回404_且不會建立任何列()
    {
        using var adminClient = fixture.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ContentEditorTokenAsync());
        using var publicClient = fixture.CreateClient();

        var draftRequest = new CreateArticleRequest
        {
            Slug = UniqueSlug("view-count-draft"),
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "瀏覽數測試：草稿不應該被加" } },
        };
        var draftResponse = await adminClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(draftRequest));
        draftResponse.EnsureSuccessStatusCode();
        var draft = (await draftResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var draftViewResponse = await publicClient.PostAsync($"/api/v1/tcrfc/news/{draft.Slug}/views", content: null);
            Assert.Equal(HttpStatusCode.NotFound, draftViewResponse.StatusCode);

            var nonExistentResponse = await publicClient.PostAsync($"/api/v1/tcrfc/news/{UniqueSlug("does-not-exist")}/views", content: null);
            Assert.Equal(HttpStatusCode.NotFound, nonExistentResponse.StatusCode);

            var reread = await adminClient.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{draft.Id}", TestJson.Options);
            Assert.Equal(0, reread!.ViewCount);
        }
        finally
        {
            await DeleteBestEffortAsync(adminClient, draft.Id, draft.UpdatedAt);
        }
    }
}
