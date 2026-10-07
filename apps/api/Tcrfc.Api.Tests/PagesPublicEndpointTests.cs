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
    // 固定頁之後不能新增頁面：公開讀取測試打測試專用版型頁 test/basic（見 TestPageTemplates）。
    private const string Slug = TestPageTemplates.Basic;

    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static Task<AdminPageDetailDto> CreateAsync(HttpClient adminClient, string seoTitle, string? seoDescription = null, string text = "測試內文")
    {
        // 草稿初始內容由 SQL 建立；SEO 說明以第二步 PUT 帶入（與後台編輯流程一致）
        return CreateWithSeoAsync(adminClient, seoTitle, seoDescription, text);
    }

    private static async Task<AdminPageDetailDto> CreateWithSeoAsync(HttpClient adminClient, string seoTitle, string? seoDescription, string text)
    {
        var page = await TestPages.CreateAsync(adminClient, "tcrfc", Slug, TestPages.BasicBlocks(text), seoTitle);
        if (seoDescription is null)
        {
            return page;
        }

        var request = TestPages.UpdateRequest(page, TestPages.BasicBlocks(text), seoTitle) with
        {
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = seoTitle, SeoDescription = seoDescription } },
        };
        var response = await TestPages.PutAsync(adminClient, "tcrfc", page, request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"更新頁面失敗：{response.StatusCode} {await response.Content.ReadAsStringAsync()}");
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
        var created = await CreateAsync(adminClient, "草稿頁面");

        try
        {
            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{Slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 排程中的頁面_公開端點看不到()
    {
        using var adminClient = await ContentEditorClientAsync();
        var created = await CreateAsync(adminClient, "排程頁面");

        try
        {
            var scheduled = await ScheduleAsync(adminClient, created, DateTime.UtcNow.AddHours(1));

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{Slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            await TestPages.DeleteAsync(created.Id);
        }
        catch
        {
            await TestPages.DeleteAsync(created.Id);
            throw;
        }
    }

    [Fact]
    public async Task 已發布頁面_公開端點看得到_只回已發布內容()
    {
        using var adminClient = await ContentEditorClientAsync();
        var created = await CreateAsync(adminClient, "公開頁面", "說明", text: "公開內文");

        try
        {
            var published = await PublishAsync(adminClient, created);

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/tcrfc/pages/{Slug}?lang=zh");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await response.Content.ReadFromJsonAsync<PageDetailDto>(TestJson.Options);
            Assert.NotNull(page);
            Assert.Equal("公開頁面", page!.SeoTitle);
            Assert.Equal("說明", page.SeoDescription);
            Assert.Equal(4, page.Blocks.Count);
            Assert.Equal("text", page.Blocks[0].BlockType);

            // 雙語物件已化簡成單一字串——不是 {"zh":"...","en":...} 巢狀物件。
            var bodyElement = page.Blocks[0].Content.GetProperty("body");
            Assert.Equal(System.Text.Json.JsonValueKind.String, bodyElement.ValueKind);
            Assert.Equal("公開內文", bodyElement.GetString());

            await TestPages.DeleteAsync(created.Id);
        }
        catch
        {
            await TestPages.DeleteAsync(created.Id);
            throw;
        }
    }

    [Fact]
    public async Task 已發布頁面_另一個俱樂部路由查不到()
    {
        using var adminClient = await ContentEditorClientAsync();
        var created = await CreateAsync(adminClient, "磐石限定頁面");

        try
        {
            var published = await PublishAsync(adminClient, created);

            using var publicClient = fixture.CreateClient();
            var response = await publicClient.GetAsync($"/api/v1/bw/pages/{Slug}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            await TestPages.DeleteAsync(created.Id);
        }
        catch
        {
            await TestPages.DeleteAsync(created.Id);
            throw;
        }
    }
}
