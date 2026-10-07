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
    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static Task<AdminPageDetailDto> CreateDraftAsync(HttpClient client, string title = "版本測試")
        => TestPages.CreateAsync(client, "tcrfc", TestPageTemplates.Basic, TestPages.BasicBlocks("第一版內文"), title);

    private static UpdatePageRequest SecondVersion(AdminPageDetailDto page)
        => TestPages.UpdateRequest(page, TestPages.BasicBlocks("第二版內文"), "第二版標題");

    [Fact]
    public async Task 每次寫入都會新增一個版本_版本歷程可以列出來()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            var updateResponse = await TestPages.PutAsync(client, "tcrfc", created, SecondVersion(created));
            updateResponse.EnsureSuccessStatusCode();

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
            Assert.True(version1.StructureMatchesTemplate);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 還原舊版本_內容變回舊版_但產生新的版本編號_不覆蓋舊版本列()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            var updateResponse = await TestPages.PutAsync(client, "tcrfc", created, SecondVersion(created));
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
            Assert.Equal(4, restored.Blocks.Count);

            var versions = await client.GetFromJsonAsync<PagedResult<AdminPageVersionListItemDto>>(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions", TestJson.Options);
            Assert.Equal(3, versions!.TotalCount); // 舊版本列都還在，沒有被還原動作刪掉
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 還原結構與現行版型不符的舊版本_回400鍵為versionNo_舊版本仍可閱覽_頁面不變()
    {
        using var client = await ContentEditorClientAsync();
        var created = await CreateDraftAsync(client);

        try
        {
            // 模擬「版型改版前留下的舊版本」：第 2 版快照只有 1 個文字區塊，與版型（4 個區塊）不符。
            var snapshot = new System.Text.Json.Nodes.JsonObject
            {
                ["seo"] = new System.Text.Json.Nodes.JsonObject { ["zh"] = new System.Text.Json.Nodes.JsonObject { ["seoTitle"] = "舊版型", ["seoDescription"] = null } },
                ["blocks"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["blockType"] = "text", ["content"] = PageBlockSamples.Text("舊版型的唯一區塊").Content.DeepClone(),
                }),
            };
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (NEWID(), @Id, 2, @Snap, @Token)",
                ("@Id", created.Id), ("@Snap", snapshot.ToJsonString()), ("@Token", Guid.NewGuid().ToString("N")));

            var version2 = await client.GetFromJsonAsync<AdminPageVersionDetailDto>(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions/2", TestJson.Options);
            Assert.False(version2!.StructureMatchesTemplate); // 仍可閱覽，但標示不相容

            var restoreResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/versions/2/restore",
                new RestorePageVersionRequest { ExpectedUpdatedAt = created.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
            Assert.Contains("versionNo", (await TestPages.ErrorKeysAsync(restoreResponse)).Keys);

            var current = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            Assert.Equal(4, current!.Blocks.Count); // 頁面沒有被改動
            Assert.Equal(created.UpdatedAt, current.UpdatedAt);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
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
            await TestPages.DeleteAsync(created.Id);
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
            Assert.Equal(4, preview.Blocks.Count);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
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
