using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1 頁面管理（固定頁＋固定欄位）寫入切片的自動化回歸測試——授權（401／403／跨俱樂部）、新增與刪除被拒、
/// 網址名稱不可變、區塊結構與固定列數鎖定（400 帶欄位鍵）、狀態轉換、樂觀並行、雙語 SEO。
/// 全部打真正的 HTTP 管線、真正的 <c>tcrfc_club</c>，不 mock；會改資料的測試一律打 <c>test/basic</c> 測試頁
/// （<see cref="TestPageTemplates"/>，SQL 建立、測完刪除），不動真正的固定頁。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPagesWriteTests(AdminWriteApiFixture fixture)
{
    private async Task<HttpClient> AuthorizedClientAsync(string username)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username));
        return client;
    }

    private static Task<AdminPageDetailDto> CreateBasicAsync(HttpClient client, string club = "tcrfc")
        => TestPages.CreateAsync(client, club, TestPageTemplates.Basic, TestPages.BasicBlocks());

    private static UpdatePageRequest BasicUpdate(AdminPageDetailDto page, string text = "第二版內文", int timelineItems = 1)
        => TestPages.UpdateRequest(page, TestPages.BasicBlocks(text, timelineItems));

    // ───────────────────────────── 授權 ─────────────────────────────

    [Fact]
    public async Task 沒有登入_更新頁面回401()
    {
        using var client = fixture.CreateClient();
        var response = await client.PutAsync($"/api/v1/admin/tcrfc/pages/{Guid.NewGuid()}",
            AdminPageMultipart.Build(new UpdatePageRequest
            {
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent() }, Blocks = [], ExpectedUpdatedAt = DateTime.UtcNow,
            }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 檢視者角色_更新頁面回403()
    {
        using var editor = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(editor);
        try
        {
            using var viewer = await AuthorizedClientAsync("viewer@tcrfc.test");
            var response = await TestPages.PutAsync(viewer, "tcrfc", page, BasicUpdate(page));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 沒有該俱樂部授權_更新頁面回403()
    {
        // content.editor@tcrfc.test 只被授權 tcrfc，打 bw 應該在 IAdminClubAuthorizer 那一關被擋下。
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var response = await client.PutAsync($"/api/v1/admin/bw/pages/{Guid.NewGuid()}",
            AdminPageMultipart.Build(new UpdatePageRequest
            {
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent() }, Blocks = [], ExpectedUpdatedAt = DateTime.UtcNow,
            }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ───────────────────────────── 固定頁：不能新增、不能刪除 ─────────────────────────────

    [Fact]
    public async Task 新增頁面被拒_POST回405_即使是最高權限帳號()
    {
        using var client = await AuthorizedClientAsync("super.admin@tcrfc.test");
        var body = new MultipartFormDataContent { { new StringContent("{}"), "payload" } };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", body);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task 刪除頁面被拒_DELETE回405_頁面毫髮無傷()
    {
        using var client = await AuthorizedClientAsync("super.admin@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            var response = await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{page.Id}?expectedUpdatedAt={Uri.EscapeDataString(page.UpdatedAt.ToString("o"))}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

            var still = await client.GetAsync($"/api/v1/admin/tcrfc/pages/{page.Id}");
            Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    // ───────────────────────────── 生命週期 ─────────────────────────────

    [Fact]
    public async Task 完整生命週期_編輯到排程到發布_列表看得到()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateBasicAsync(client);
        Assert.Equal("draft", created.Status);
        Assert.Equal(1, created.LatestVersionNo);
        Assert.False(string.IsNullOrWhiteSpace(created.PreviewToken));
        Assert.Equal(TestPageTemplates.Basic, created.Template.Key);
        Assert.Equal(4, created.Blocks.Count);
        Assert.Equal(["intro", "quote", "cards", "years"], created.Blocks.Select(b => b.Key!).ToArray());

        try
        {
            // 列表：版型清單合併實際頁的狀態，測試頁（資料庫有列）看得到，帶頁名與版型鍵
            var list = await client.GetFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminPageListItemDto>>(
                $"/api/v1/admin/tcrfc/pages?keyword=test/basic", TestJson.Options);
            var item = Assert.Single(list!.Items, i => i.Id == created.Id);
            Assert.Equal("測試頁（基本）", item.TitleZh);
            Assert.Equal(TestPageTemplates.Basic, item.TemplateKey);

            // 更新：內容、雙語 SEO；時間軸可增刪列（1 → 3 列）
            var updateRequest = BasicUpdate(created, "改過的內文", timelineItems: 3) with
            {
                Seo = new AdminPageSeoInput
                {
                    Zh = new AdminPageSeoLocaleContent { SeoTitle = "改過的標題" },
                    En = new AdminPageSeoLocaleContent { SeoTitle = "Updated Title" },
                },
            };
            var updateResponse = await TestPages.PutAsync(client, "tcrfc", created, updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = (await updateResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
            Assert.Equal("改過的標題", updated.Zh.SeoTitle);
            Assert.Equal("Updated Title", updated.En?.SeoTitle);
            Assert.Equal(3, updated.Blocks[3].Content.GetProperty("items").GetArrayLength());
            Assert.Equal(2, updated.LatestVersionNo); // 版本歷程：初始算第 1 版，更新產生第 2 版
            Assert.True(updated.UpdatedAt > created.UpdatedAt);

            // 排程發布
            var scheduleResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/schedule",
                new SchedulePageRequest { ExpectedUpdatedAt = updated.UpdatedAt, PublishAt = DateTime.UtcNow.AddMinutes(30) },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
            var scheduled = (await scheduleResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
            Assert.Equal("scheduled", scheduled.Status);

            // 發布
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/publish",
                new PublishPageRequest { ExpectedUpdatedAt = scheduled.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = (await publishResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
            Assert.Equal("published", published.Status);
            Assert.NotNull(published.PublishedAt);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 網址名稱不可變更_帶不同slug回400鍵為slug_帶相同slug通過()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            var changed = BasicUpdate(page) with { Slug = "test/renamed" };
            var response = await TestPages.PutAsync(client, "tcrfc", page, changed);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("slug", (await TestPages.ErrorKeysAsync(response)).Keys);

            var same = BasicUpdate(page) with { Slug = page.Slug };
            Assert.Equal(HttpStatusCode.OK, (await TestPages.PutAsync(client, "tcrfc", page, same)).StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    // ───────────────────────────── 結構鎖定 ─────────────────────────────

    [Fact]
    public async Task 區塊數量不符_少一個或多一個_回400鍵為blocks()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            var fewer = TestPages.UpdateRequest(page, TestPages.BasicBlocks().Take(3).ToList());
            var response = await TestPages.PutAsync(client, "tcrfc", page, fewer);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks", (await TestPages.ErrorKeysAsync(response)).Keys);

            var more = TestPages.UpdateRequest(page, [.. TestPages.BasicBlocks(), PageBlockSamples.Text("多出來的")]);
            response = await TestPages.PutAsync(client, "tcrfc", page, more);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks", (await TestPages.ErrorKeysAsync(response)).Keys);

            var empty = TestPages.UpdateRequest(page, []);
            response = await TestPages.PutAsync(client, "tcrfc", page, empty);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 區塊類型不符或順序調換_回400鍵為blocks索引()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            // 調換第 1、2 個區塊（文字 ↔ 引言）：位置 0 的類型不符
            var swapped = TestPages.BasicBlocks();
            (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, swapped));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[0]", (await TestPages.ErrorKeysAsync(response)).Keys);

            // 把第 3 個區塊（步驟）換成別的類型
            var replaced = TestPages.BasicBlocks();
            replaced[2] = PageBlockSamples.Text("換掉步驟");
            response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, replaced));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[2]", (await TestPages.ErrorKeysAsync(response)).Keys);

            // 區塊代號帶錯（類型對但 key 不是版型該位置的）
            var wrongKey = TestPages.BasicBlocks();
            wrongKey[3] = new AdminPageBlockInput { BlockType = wrongKey[3].BlockType, Content = wrongKey[3].Content, Key = "intro" };
            response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, wrongKey));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[3]", (await TestPages.ErrorKeysAsync(response)).Keys);

            // 帶對的 key 通過
            var rightKey = TestPages.BasicBlocks();
            rightKey[0] = new AdminPageBlockInput { BlockType = rightKey[0].BlockType, Content = rightKey[0].Content, Key = "intro" };
            Assert.Equal(HttpStatusCode.OK, (await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, rightKey))).StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 不允許增刪列的區塊_列數與版型不同_回400鍵為區塊列欄位()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            var tooMany = TestPages.BasicBlocks();
            tooMany[2] = PageBlockSamples.StepsOf(3); // 版型固定 2 列
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, tooMany));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[2].items", (await TestPages.ErrorKeysAsync(response)).Keys);

            var tooFew = TestPages.BasicBlocks();
            tooFew[2] = PageBlockSamples.StepsOf(1);
            response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, tooFew));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[2].items", (await TestPages.ErrorKeysAsync(response)).Keys);

            // 失敗的請求不留下任何痕跡：版本仍是第 1 版
            var current = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{page.Id}", TestJson.Options);
            Assert.Equal(1, current!.LatestVersionNo);
            Assert.Equal(page.UpdatedAt, current.UpdatedAt);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 允許增刪列的區塊_可以增加也可以減到只剩一列_但不能是零列()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var page = await CreateBasicAsync(client);
        try
        {
            var grown = await TestPages.PutAsync(client, "tcrfc", page, BasicUpdate(page, timelineItems: 5));
            Assert.Equal(HttpStatusCode.OK, grown.StatusCode);
            var grownPage = (await grown.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
            Assert.Equal(5, grownPage.Blocks[3].Content.GetProperty("items").GetArrayLength());

            var shrunk = await TestPages.PutAsync(client, "tcrfc", grownPage, BasicUpdate(grownPage, timelineItems: 1));
            Assert.Equal(HttpStatusCode.OK, shrunk.StatusCode);
            var shrunkPage = (await shrunk.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;

            var zero = await TestPages.PutAsync(client, "tcrfc", shrunkPage, BasicUpdate(shrunkPage, timelineItems: 0));
            Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode); // 既有的內容驗證：時間軸至少 1 筆
            Assert.Contains("blocks[3].items", (await TestPages.ErrorKeysAsync(zero)).Keys);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 版型外的舊頁面_查不到也改不了_清單不顯示()
    {
        using var client = await AuthorizedClientAsync("super.admin@tcrfc.test");
        var slug = $"legacy/orphan-{Guid.NewGuid():N}";
        var blocks = TestPages.BasicBlocks();
        // 先以測試版型的 slug 建立，再改 slug 成不在版型內的值，模擬已退場的舊頁面
        var page = await TestPages.CreateAsync(client, "tcrfc", TestPageTemplates.Basic, blocks);
        try
        {
            await BizTest.ExecuteSqlAsync("UPDATE pages SET slug = @Slug WHERE id = @Id", ("@Slug", slug), ("@Id", page.Id));

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/pages/{page.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestPages.PutAsync(client, "tcrfc", page, BasicUpdate(page))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/pages/{page.Id}/versions")).StatusCode);

            var list = await client.GetFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminPageListItemDto>>("/api/v1/admin/tcrfc/pages?pageSize=100", TestJson.Options);
            Assert.DoesNotContain(list!.Items, i => i.Id == page.Id);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    // ───────────────────────────── 其他 ─────────────────────────────

    [Fact]
    public async Task 俱樂部範圍_用另一俱樂部路由更新_回404()
    {
        using var client = await AuthorizedClientAsync("super.admin@tcrfc.test");
        var created = await CreateBasicAsync(client);
        try
        {
            var response = await client.PutAsync($"/api/v1/admin/bw/pages/{created.Id}", AdminPageMultipart.Build(BasicUpdate(created)));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/bw/pages/{created.Id}")).StatusCode);

            var stillTcrfc = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            Assert.Equal("版本測試", stillTcrfc!.Zh.SeoTitle);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 樂觀並行控制_用過期的updatedAt更新_回409()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateBasicAsync(client);
        try
        {
            var first = BasicUpdate(created) with { Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "第一次修改" } } };
            Assert.Equal(HttpStatusCode.OK, (await TestPages.PutAsync(client, "tcrfc", created, first)).StatusCode);

            var stale = first with { Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "過期的第二次修改" } } };
            Assert.Equal(HttpStatusCode.Conflict, (await TestPages.PutAsync(client, "tcrfc", created, stale)).StatusCode);

            var current = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            Assert.Equal("第一次修改", current!.Zh.SeoTitle);
        }
        finally
        {
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 三態轉換_已發布的頁面不能再排程_回409()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateBasicAsync(client);
        try
        {
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}/publish", new PublishPageRequest { ExpectedUpdatedAt = created.UpdatedAt }, TestJson.WriteOptions);
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
            await TestPages.DeleteAsync(created.Id);
        }
    }

    [Fact]
    public async Task 排程時間不在未來_回400()
    {
        using var client = await AuthorizedClientAsync("content.editor@tcrfc.test");
        var created = await CreateBasicAsync(client);
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
            await TestPages.DeleteAsync(created.Id);
        }
    }
}
