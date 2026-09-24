using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.Pages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 公開頁面讀取端點（<c>GET /api/v1/{club}/pages/{slug}</c>）——只回已發布內容，且
/// <c>pages.club_id</c> 必填（不像文章有「共同內容」），俱樂部範圍直接比對，不套用
/// <c>ClubOrSharedSql</c> 那條「俱樂部專屬優先、回退共同」規則（頁面永遠只屬於一個俱樂部）。
///
/// 🔴 每一步寫入（建立／發布／排程）都要 assert 回應狀態碼——2026-09-24 排查
/// 「已發布頁面_公開端點看得到_只回已發布內容」間歇性失敗時，正是因為 publish 呼叫沒有斷言，
/// 一旦寫入本身失敗（或如本次根因：寫入成功但下一行讀取因故看不到），錯誤只會在後面幾行之外的
/// 斷言冒出來，訊息完全對不上真正出錯的那一步。根因與修法見
/// <c>Common/DatabaseClock.cs</c> 檔頭與 <c>AdminPagesRepository.PublishAsync</c> 上的註解。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class PagesPublicEndpointTests(AdminWriteApiFixture fixture)
{
    private static string UniqueSlug() => $"public-page-{Guid.NewGuid():N}";

    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static async Task DeleteBestEffortAsync(HttpClient client, string club, Guid id, DateTime expectedUpdatedAt)
        => await client.DeleteAsync($"/api/v1/admin/{club}/pages/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}");

    private static async Task<AdminPageDetailDto> CreateAsync(HttpClient adminClient, CreatePageRequest request)
    {
        var response = await adminClient.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"建立頁面失敗：{response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
    }

    private static async Task<AdminPageDetailDto> PublishAsync(HttpClient adminClient, AdminPageDetailDto page)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/pages/{page.Id}/publish",
            new PublishPageRequest { ExpectedUpdatedAt = page.UpdatedAt },
            TestJson.WriteOptions);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"發布頁面失敗：{response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
    }

    private static async Task<AdminPageDetailDto> ScheduleAsync(HttpClient adminClient, AdminPageDetailDto page, DateTime publishAt)
    {
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/pages/{page.Id}/schedule",
            new SchedulePageRequest { ExpectedUpdatedAt = page.UpdatedAt, PublishAt = publishAt },
            TestJson.WriteOptions);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"排程頁面失敗：{response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task 草稿頁面_公開端點看不到()
    {
        using var adminClient = await ContentEditorClientAsync();
        var slug = UniqueSlug();
        var created = await CreateAsync(adminClient, new CreatePageRequest
        {
            Slug = slug,
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "草稿頁面" } },
            Blocks = [PageBlockSamples.Text()],
        });

        try
        {
            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 排程中的頁面_公開端點看不到()
    {
        using var adminClient = await ContentEditorClientAsync();
        var slug = UniqueSlug();
        var created = await CreateAsync(adminClient, new CreatePageRequest
        {
            Slug = slug,
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "排程頁面" } },
            Blocks = [PageBlockSamples.Text()],
        });

        try
        {
            var scheduled = await ScheduleAsync(adminClient, created, DateTime.UtcNow.AddHours(1));

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, scheduled.UpdatedAt);
        }
        catch
        {
            var probe = await adminClient.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, probe.UpdatedAt);
            }

            throw;
        }
    }

    [Fact]
    public async Task 已發布頁面_公開端點看得到_只回已發布內容()
    {
        using var adminClient = await ContentEditorClientAsync();
        var slug = UniqueSlug();
        var created = await CreateAsync(adminClient, new CreatePageRequest
        {
            Slug = slug,
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "公開頁面", SeoDescription = "說明" } },
            Blocks = [PageBlockSamples.Text("公開內文")],
        });

        try
        {
            var published = await PublishAsync(adminClient, created);

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{slug}?lang=zh");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await response.Content.ReadFromJsonAsync<PageDetailDto>(TestJson.Options);
            Assert.NotNull(page);
            Assert.Equal("公開頁面", page!.SeoTitle);
            Assert.Equal("說明", page.SeoDescription);
            Assert.Single(page.Blocks);
            Assert.Equal("text", page.Blocks[0].BlockType);

            // 雙語物件已化簡成單一字串——不是 {"zh":"...","en":...} 巢狀物件。
            var bodyElement = page.Blocks[0].Content.GetProperty("body");
            Assert.Equal(System.Text.Json.JsonValueKind.String, bodyElement.ValueKind);
            Assert.Equal("公開內文", bodyElement.GetString());

            await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, published.UpdatedAt);
        }
        catch
        {
            var probe = await adminClient.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, probe.UpdatedAt);
            }

            throw;
        }
    }

    [Fact]
    public async Task 已發布頁面_另一個俱樂部路由查不到()
    {
        using var adminClient = await ContentEditorClientAsync();
        var slug = UniqueSlug();
        var created = await CreateAsync(adminClient, new CreatePageRequest
        {
            Slug = slug,
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "磐石限定頁面" } },
            Blocks = [PageBlockSamples.Text()],
        });

        try
        {
            var published = await PublishAsync(adminClient, created);

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/bw/pages/{slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, published.UpdatedAt);
        }
        catch
        {
            var probe = await adminClient.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(adminClient, "tcrfc", created.Id, probe.UpdatedAt);
            }

            throw;
        }
    }
}
