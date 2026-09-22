using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 驗證寫入後真的呼叫 <c>IQueryCache.InvalidateAsync</c>，不是只有介面存在沒有人呼叫——
/// 用 <see cref="AdminWriteRedisEnabledApiFixture"/>（真正的 <c>redis-server</c>），
/// 因為 no-op 快取下「寫入後立刻看得到新值」是必然成立（本來就沒有快取），驗不出「有沒有失效」。
/// </summary>
[Collection(AdminWriteRedisEnabledCollection.Name)]
public sealed class AdminNewsCacheInvalidationTests(AdminWriteRedisEnabledApiFixture fixture)
{
    [Fact]
    public async Task 更新已發布文章後_公開API立刻看到新標題不是快取住的舊標題()
    {
        using var client = fixture.CreateClient();
        var slug = $"cache-invalidate-test-{Guid.NewGuid():N}";

        var createRequest = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "快取失效測試：舊標題" } },
        };
        var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);

        try
        {
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created!.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
                TestJson.WriteOptions);
            publishResponse.EnsureSuccessStatusCode();
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(published);

            // 1. 打公開 API，把「舊標題」寫進 Redis 快取（article-detail entity）。
            var warmed = await client.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{slug}", TestJson.Options);
            Assert.NotNull(warmed);
            Assert.Equal("快取失效測試：舊標題", warmed!.Title);

            var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);
            var keysBeforeUpdate = server.Keys(pattern: $"*article-detail*{slug}*").ToArray();
            Assert.NotEmpty(keysBeforeUpdate); // 確認真的有寫入快取，不是本來就沒快取到。

            // 2. 透過後台把標題改掉。
            var updateRequest = new UpdateArticleRequest
            {
                Slug = slug,
                CategoryCode = "club",
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "快取失效測試：新標題" } },
                ExpectedUpdatedAt = published!.UpdatedAt,
            };
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            updateResponse.EnsureSuccessStatusCode();

            // 3. 🔴 關鍵斷言：公開 API 立刻回新標題，不是被 TTL 內的舊快取擋住。
            var afterUpdate = await client.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{slug}", TestJson.Options);
            Assert.NotNull(afterUpdate);
            Assert.Equal("快取失效測試：新標題", afterUpdate!.Title);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                var deleteUrl = $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(probe.UpdatedAt.ToString("o"))}";
                await client.DeleteAsync(deleteUrl);
            }
        }
    }
}
