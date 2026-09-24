using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.Pages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 版本歷程、還原、預覽連結三件事的自動化回歸測試（規劃書 §4.2 B1「版本歷程與還原」
/// 「預覽連結（未發布可分享）」）。還原行為是本輪的執行層判斷（見 apps/api/README.md
/// 「我的判斷」）：還原＝以舊版內容產生一個新版本，不覆蓋舊版本列本身。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPagesVersionsAndPreviewTests(AdminWriteApiFixture fixture)
{
    private static string UniqueSlug() => $"admin-write-page-version-{Guid.NewGuid():N}";

    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private async Task<AdminPageDetailDto> CreateDraftAsync(HttpClient client, string title = "版本測試")
    {
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = title } },
            Blocks = [PageBlockSamples.Text("第一版內文")],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
    }

    private static async Task DeleteBestEffortAsync(HttpClient client, Guid id, DateTime expectedUpdatedAt)
        => await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}");

    [Fact]
    public async Task 每次寫入都會新增一個版本_版本歷程可以列出來()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            var updateRequest = new UpdatePageRequest
            {
                Slug = created.Slug,
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "第二版標題" } },
                Blocks = [PageBlockSamples.Text("第二版內文")],
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{created.Id}", AdminPageMultipart.Build(updateRequest));
            updateResponse.EnsureSuccessStatusCode();
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);

            var versions = await client.GetFromJsonAsync<PagedResult<AdminPageVersionListItemDto>>(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions", TestJson.Options);
            Assert.NotNull(versions);
            Assert.Equal(2, versions!.TotalCount);
            Assert.Contains(versions.Items, v => v.VersionNo == 1);
            Assert.Contains(versions.Items, v => v.VersionNo == 2);
            Assert.All(versions.Items, v => Assert.False(string.IsNullOrWhiteSpace(v.PreviewToken)));

            var version1 = await client.GetFromJsonAsync<AdminPageVersionDetailDto>(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions/1", TestJson.Options);
            Assert.NotNull(version1);
            Assert.Equal("版本測試", version1!.Zh.SeoTitle);

            await DeleteBestEffortAsync(client, created.Id, updated!.UpdatedAt);
        }
        catch
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
            throw;
        }
    }

    [Fact]
    public async Task 還原舊版本_內容變回舊版_但產生新的版本編號_不覆蓋舊版本列()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            var updateRequest = new UpdatePageRequest
            {
                Slug = created.Slug,
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "第二版標題" } },
                Blocks = [PageBlockSamples.Text("第二版內文")],
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{created.Id}", AdminPageMultipart.Build(updateRequest));
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
            Assert.Equal(2, updated!.LatestVersionNo);

            var restoreResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions/1/restore",
                new RestorePageVersionRequest { ExpectedUpdatedAt = updated.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
            var restored = await restoreResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);

            Assert.NotNull(restored);
            Assert.Equal("版本測試", restored!.Zh.SeoTitle); // 內容變回第 1 版
            Assert.Equal(3, restored.LatestVersionNo); // 但產生的是第 3 版，不是覆蓋回第 1 版

            var versions = await client.GetFromJsonAsync<PagedResult<AdminPageVersionListItemDto>>(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions", TestJson.Options);
            Assert.Equal(3, versions!.TotalCount); // 舊版本列都還在，沒有被還原動作刪掉

            await DeleteBestEffortAsync(client, created.Id, restored.UpdatedAt);
        }
        catch
        {
            var probe = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, created.Id, probe.UpdatedAt);
            }

            throw;
        }
    }

    [Fact]
    public async Task 還原不存在的版本編號_回404()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions/999/restore",
                new RestorePageVersionRequest { ExpectedUpdatedAt = created.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 未發布的頁面_預覽連結仍然可以看到內容()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);
        Assert.Equal("draft", created.Status);
        Assert.False(string.IsNullOrWhiteSpace(created.PreviewToken));

        try
        {
            using var anonymousClient = fixture.CreateClient(); // 預覽連結不需要登入
            var previewResponse = await anonymousClient.GetAsync($"/api/v1/pages/preview/{created.PreviewToken}");
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

            Assert.Contains("noindex", previewResponse.Headers.GetValues("X-Robots-Tag").First());

            var preview = await previewResponse.Content.ReadFromJsonAsync<PagePreviewDto>(TestJson.Options);
            Assert.NotNull(preview);
            Assert.Equal(created.Id, preview!.PageId);
            Assert.Equal("draft", preview.Status);
            Assert.Equal("版本測試", preview.SeoTitle);
            Assert.Single(preview.Blocks);
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 猜測的權杖_回404()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/pages/preview/{Guid.NewGuid():N}-not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
