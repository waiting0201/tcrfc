using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1 頁面管理寫入垂直切片的自動化回歸測試——涵蓋授權（401／403／跨俱樂部）、狀態轉換、
/// 樂觀並行、slug 重複、雙語 SEO。形狀比照 <c>AdminNewsWriteTests</c>。全部打真正的 HTTP 管線、
/// 真正的 <c>tcrfc_club_dev</c>，不 mock。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPagesWriteTests(AdminWriteApiFixture fixture)
{
    private static string UniqueSlug() => $"admin-write-page-{Guid.NewGuid():N}";

    private static CreatePageRequest NewDraftRequest(string slug) => new()
    {
        Slug = slug,
        Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "測試頁面" } },
        Blocks = [PageBlockSamples.Text()],
    };

    private async Task<HttpClient> AuthorizedClientAsync(string username)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username));
        return client;
    }

    private async Task<AdminPageDetailDto> CreateDraftAsync(HttpClient client, string club, string slug)
    {
        var response = await client.PostAsync($"/api/v1/admin/{club}/pages", AdminPageMultipart.Build(NewDraftRequest(slug)));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        return created!;
    }

    private static async Task DeleteBestEffortAsync(HttpClient client, string club, Guid id, DateTime expectedUpdatedAt)
    {
        var url = $"/api/v1/admin/{club}/pages/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}";
        await client.DeleteAsync(url);
    }

    [Fact]
    public async Task 沒有登入_建立頁面回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(NewDraftRequest(UniqueSlug())));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 檢視者角色_建立頁面回403()
    {
        using var client = await AuthorizedClientAsync("viewer@tcrfc.test");
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(NewDraftRequest(UniqueSlug())));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 沒有該俱樂部授權_建立頁面回403()
    {
        // content.editor@tcrfc.test 只被授權 tcrfc，打 bw 應該在 IAdminClubAuthorizer 那一關被擋下。
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var response = await client.PostAsync("/api/v1/admin/bw/pages", AdminPageMultipart.Build(NewDraftRequest(UniqueSlug())));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 完整生命週期_建立草稿到刪除()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var slug = UniqueSlug();

        var created = await CreateDraftAsync(client, "tcrfc", slug);
        Assert.Equal("draft", created.Status);
        Assert.Equal(slug, created.Slug);
        Assert.Null(created.PublishedAt);
        Assert.Equal(1, created.LatestVersionNo);
        Assert.False(string.IsNullOrWhiteSpace(created.PreviewToken));
        Assert.Single(created.Blocks);

        try
        {
            // 列表看得到
            var list = await client.GetFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminPageListItemDto>>(
                $"/api/v1/admin/tcrfc/pages?keyword={slug}", TestJson.Options);
            Assert.NotNull(list);
            Assert.Contains(list!.Items, i => i.Id == created.Id);

            // 更新：整份取代區塊清單（換成 2 個區塊），SEO 加上英文
            var updateRequest = new UpdatePageRequest
            {
                Slug = slug,
                Seo = new AdminPageSeoInput
                {
                    Zh = new AdminPageSeoLocaleContent { SeoTitle = "改過的標題" },
                    En = new AdminPageSeoLocaleContent { SeoTitle = "Updated Title" },
                },
                Blocks = [PageBlockSamples.Text("改過的內文"), PageBlockSamples.Quote()],
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{created.Id}", AdminPageMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
            Assert.NotNull(updated);
            Assert.Equal("改過的標題", updated!.Zh.SeoTitle);
            Assert.Equal("Updated Title", updated.En?.SeoTitle);
            Assert.Equal(2, updated.Blocks.Count);
            Assert.Equal(2, updated.LatestVersionNo); // 版本歷程：建立算第 1 版，更新產生第 2 版
            Assert.True(updated.UpdatedAt > created.UpdatedAt);

            // 排程發布
            var publishAt = DateTime.UtcNow.AddMinutes(30);
            var scheduleResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/schedule",
                new SchedulePageRequest { ExpectedUpdatedAt = updated.UpdatedAt, PublishAt = publishAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
            var scheduled = await scheduleResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
            Assert.NotNull(scheduled);
            Assert.Equal("scheduled", scheduled!.Status);

            // 發布
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/publish",
                new PublishPageRequest { ExpectedUpdatedAt = scheduled.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
            Assert.NotNull(published);
            Assert.Equal("published", published!.Status);
            Assert.NotNull(published.PublishedAt);

            // 刪除
            var deleteUrl = $"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(published.UpdatedAt.ToString("o"))}";
            var deleteResponse = await client.DeleteAsync(deleteUrl);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var afterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/pages/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
        finally
        {
            var probe = await client.GetAsync($"/api/v1/admin/tcrfc/pages/{created.Id}");
            if (probe.StatusCode == HttpStatusCode.OK)
            {
                var current = await probe.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
                if (current is not null)
                {
                    await DeleteBestEffortAsync(client, "tcrfc", created.Id, current.UpdatedAt);
                }
            }
        }
    }

    [Fact]
    public async Task 網址名稱重複_回409()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var slug = UniqueSlug();
        var first = await CreateDraftAsync(client, "tcrfc", slug);

        try
        {
            var second = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(NewDraftRequest(slug)));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", first.Id, first.UpdatedAt);
        }
    }

    [Fact]
    public async Task 網址名稱格式錯誤_回400()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(NewDraftRequest("Not Valid Slug!")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 允許多層路徑的網址名稱()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var slug = $"academy/{UniqueSlug()}";
        var created = await CreateDraftAsync(client, "tcrfc", slug);

        try
        {
            Assert.Equal(slug, created.Slug);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 俱樂部範圍_用另一俱樂部路由更新_回404()
    {
        using var client = await AuthorizedClientAsync("super.admin@tcrfc.test");
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var updateRequest = new UpdatePageRequest
            {
                Slug = created.Slug,
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "偷改" } },
                Blocks = [PageBlockSamples.Text()],
                ExpectedUpdatedAt = created.UpdatedAt,
            };

            var response = await client.PutAsync($"/api/v1/admin/bw/pages/{created.Id}", AdminPageMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var getResponse = await client.GetAsync($"/api/v1/admin/bw/pages/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            var stillTcrfc = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            Assert.Equal("測試頁面", stillTcrfc!.Zh.SeoTitle);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 樂觀並行控制_用過期的updatedAt更新_回409()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var firstUpdate = new UpdatePageRequest
            {
                Slug = created.Slug,
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "第一次修改" } },
                Blocks = [PageBlockSamples.Text()],
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var firstResponse = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{created.Id}", AdminPageMultipart.Build(firstUpdate));
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var staleUpdate = firstUpdate with
            {
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "過期的第二次修改" } },
            };
            var staleResponse = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{created.Id}", AdminPageMultipart.Build(staleUpdate));
            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

            var current = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            Assert.Equal("第一次修改", current!.Zh.SeoTitle);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, "tcrfc", created.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 三態轉換_已發布的頁面不能再排程_回409()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/publish",
                new PublishPageRequest { ExpectedUpdatedAt = created.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);

            var scheduleResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/schedule",
                new SchedulePageRequest { ExpectedUpdatedAt = published!.UpdatedAt, PublishAt = DateTime.UtcNow.AddHours(1) },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, scheduleResponse.StatusCode);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, "tcrfc", created.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 排程時間不在未來_回400()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/schedule",
                new SchedulePageRequest { ExpectedUpdatedAt = created.UpdatedAt, PublishAt = DateTime.UtcNow.AddMinutes(-5) },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }
}
