using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminFaqs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-6：B4 常見問題（<c>faqs</c>／<c>faq_categories</c>）後台讀寫。
/// 打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>，不 mock。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminFaqsAndCategoriesTests(AdminWriteApiFixture fixture)
{
    // ───────────────────────────── FaqCategory（全域） ─────────────────────────────

    [Fact]
    public async Task FaqCategory_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/faq-categories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FaqCategory_檢視者角色_可讀不可寫()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        var listResponse = await client.GetAsync("/api/v1/admin/faq-categories");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var categories = await listResponse.Content.ReadFromJsonAsync<List<AdminFaqCategoryListItemDto>>(TestJson.Options);
        Assert.True(categories!.Count >= 10); // 種子的十個固定主題

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/faq-categories", NewCategoryRequest("測試分類"));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task FaqCategory_建立更新刪除完整流程_slug重複回409()
    {
        using var client = await CreateContentEditorClientAsync();
        var slug = $"s1-6-cat-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/faq-categories", NewCategoryRequest("測試分類", slug));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminFaqCategoryDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.True(created!.IsEnabled); // S1-7a：省略時預設啟用。

        try
        {
            var conflictResponse = await client.PostAsJsonAsync("/api/v1/admin/faq-categories", NewCategoryRequest("重複的分類", slug));
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/faq-categories/{created!.Id}", new UpdateAdminFaqCategoryRequest
            {
                Slug = slug,
                SortOrder = 99,
                IsEnabled = true,
                Content = new AdminFaqCategoryContentInput
                {
                    Zh = new AdminFaqCategoryLocaleContent { Name = "測試分類（已更新）" },
                    En = new AdminFaqCategoryLocaleContent { Name = "Test Category (Updated)" },
                },
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminFaqCategoryDetailDto>(TestJson.Options);
            Assert.Equal("測試分類（已更新）", updated!.Zh.Name);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/faq-categories/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getAfterDelete = await client.GetAsync($"/api/v1/admin/faq-categories/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/faq-categories/{created!.Id}");
        }
    }

    [Fact]
    public async Task FaqCategory_軟停用_公開端點不列_既有題目與關聯不受影響_可重新啟用()
    {
        // S1-7a：停用是 IsEnabled=false，不是 DELETE——題目與 faq_category_links 不受影響。
        using var client = await CreateContentEditorClientAsync();
        var slug = $"s1-7a-cat-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/faq-categories", NewCategoryRequest("軟停用測試分類", slug));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var category = await createResponse.Content.ReadFromJsonAsync<AdminFaqCategoryDetailDto>(TestJson.Options);

        var faq = await CreateFaqAsync(client, category!.Id, "軟停用分類底下的題目");

        try
        {
            // 停用前：公開分類清單看得到。
            var beforeDisable = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqCategoryDto>>(
                "/api/v1/faq-categories", TestJson.Options);
            Assert.Contains(beforeDisable!, c => c.Id == category.Id);

            // 停用。
            var disableResponse = await client.PutAsJsonAsync($"/api/v1/admin/faq-categories/{category.Id}", new UpdateAdminFaqCategoryRequest
            {
                Slug = slug,
                SortOrder = category.SortOrder,
                IsEnabled = false,
                Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = "軟停用測試分類" } },
            });
            Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
            var disabled = await disableResponse.Content.ReadFromJsonAsync<AdminFaqCategoryDetailDto>(TestJson.Options);
            Assert.False(disabled!.IsEnabled);

            // 停用後：公開分類清單消失。
            var afterDisable = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqCategoryDto>>(
                "/api/v1/faq-categories", TestJson.Options);
            Assert.DoesNotContain(afterDisable!, c => c.Id == category.Id);

            // 停用後：既有題目仍然存在、關聯不受影響（後台仍看得到這個分類掛在題目上）。
            var faqDetail = await client.GetFromJsonAsync<AdminFaqDetailDto>($"/api/v1/admin/tcrfc/faqs/{faq.Id}", TestJson.Options);
            Assert.Contains(faqDetail!.Categories, c => c.Id == category.Id);

            // 後台分類列表仍看得到（含 IsEnabled=false），不是刪除。
            var adminList = await client.GetFromJsonAsync<List<AdminFaqCategoryListItemDto>>("/api/v1/admin/faq-categories", TestJson.Options);
            Assert.Contains(adminList!, c => c.Id == category.Id && !c.IsEnabled);

            // 重新啟用。
            var enableResponse = await client.PutAsJsonAsync($"/api/v1/admin/faq-categories/{category.Id}", new UpdateAdminFaqCategoryRequest
            {
                Slug = slug,
                SortOrder = category.SortOrder,
                IsEnabled = true,
                Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = "軟停用測試分類" } },
            });
            Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

            var afterEnable = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqCategoryDto>>(
                "/api/v1/faq-categories", TestJson.Options);
            Assert.Contains(afterEnable!, c => c.Id == category.Id);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq.Id}");
            await client.DeleteAsync($"/api/v1/admin/faq-categories/{category.Id}");
        }
    }

    // ───────────────────────────── Faq（俱樂部範圍） ─────────────────────────────

    [Fact]
    public async Task Faq_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Faq_跨俱樂部_擋下_授權範圍內_成功()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test"));

        var tcrfcResponse = await client.GetAsync("/api/v1/admin/tcrfc/faqs");
        Assert.Equal(HttpStatusCode.Forbidden, tcrfcResponse.StatusCode);

        var bwResponse = await client.GetAsync("/api/v1/admin/bw/faqs");
        Assert.Equal(HttpStatusCode.OK, bwResponse.StatusCode);
    }

    [Fact]
    public async Task Faq_檢視者角色_唯讀()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/faqs");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var categoryId = await GetAnyFaqCategoryIdAsync();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs", NewFaqRequest($"s1-6-faq-{Guid.NewGuid():N}", [categoryId]));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Faq_建立時未選分類_400()
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs", NewFaqRequest($"s1-6-faq-{Guid.NewGuid():N}", []));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Faq_建立時分類不存在_400()
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs", NewFaqRequest($"s1-6-faq-{Guid.NewGuid():N}", [Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Faq_建立更新刪除完整流程_含狀態切換與分類調整_slug重複回409()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId1 = await GetAnyFaqCategoryIdAsync();
        var categoryId2 = await GetAnyFaqCategoryIdAsync(exclude: categoryId1);
        var slug = $"s1-6-faq-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs", NewFaqRequest(slug, [categoryId1]));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Equal("draft", created!.Status);
        Assert.Single(created.Categories);

        try
        {
            var conflictResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/faqs", NewFaqRequest(slug, [categoryId1]));
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            var badStatusResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/faqs", NewFaqRequest($"{slug}-bad-status", [categoryId1], status: "scheduled"));
            Assert.Equal(HttpStatusCode.BadRequest, badStatusResponse.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}", new UpdateFaqRequest
            {
                Slug = slug,
                CategoryIds = [categoryId1, categoryId2],
                SortOrder = 1,
                Status = "published",
                Content = new AdminFaqContentInput
                {
                    Zh = new AdminFaqLocaleContent { Question = "測試問題（已更新）？", Answer = "測試答案（已更新）。" },
                    En = new AdminFaqLocaleContent { Question = "Updated question?", Answer = "Updated answer." },
                },
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
            Assert.Equal("published", updated!.Status);
            Assert.Equal(2, updated.Categories.Count);
            Assert.Equal("測試問題（已更新）？", updated.Zh.Question);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getAfterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{created!.Id}");
        }
    }

    [Fact]
    public async Task Faq_共用內容唯讀_403()
    {
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var sharedFaqId = await InsertSharedFaqAsync(categoryId);

        try
        {
            using var client = await CreateContentEditorClientAsync();

            // 共用內容仍應在清單／詳情查得到（俱樂部專屬優先、回退共同）。
            var getResponse = await client.GetAsync($"/api/v1/admin/tcrfc/faqs/{sharedFaqId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var faq = await getResponse.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
            Assert.True(faq!.IsShared);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{sharedFaqId}", new UpdateFaqRequest
            {
                Slug = $"shared-{Guid.NewGuid():N}",
                CategoryIds = [categoryId],
                SortOrder = 0,
                Status = "published",
                Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "不該成功的更新？", Answer = "不該成功。" } },
            });
            Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{sharedFaqId}");
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }
        finally
        {
            await DeleteFaqByIdAsync(sharedFaqId);
        }
    }

    [Fact]
    public async Task Faq_低評價排序_負評最多排最前()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();

        var lowRated = await CreateFaqAsync(client, categoryId, "低評價測試題");
        var highRated = await CreateFaqAsync(client, categoryId, "高評價測試題");

        try
        {
            await SetFeedbackCountsAsync(lowRated.Id, helpful: 0, unhelpful: 10);
            await SetFeedbackCountsAsync(highRated.Id, helpful: 10, unhelpful: 0);

            var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs?sort=low_rating&pageSize=100");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminFaqListItemDto>>(TestJson.Options);
            var ids = result!.Items.Select(i => i.Id).ToList();
            Assert.True(ids.IndexOf(lowRated.Id) < ids.IndexOf(highRated.Id));
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{lowRated.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{highRated.Id}");
        }
    }

    // ───────────────────────────── FAQ 嵌入設定（G-12 掛載點，S1-7a） ─────────────────────

    [Fact]
    public async Task FaqEmbedSlot_未登入_擋下_已登入可列出四筆固定值()
    {
        using var anonymous = fixture.CreateClient();
        var unauthorizedResponse = await anonymous.GetAsync("/api/v1/admin/faq-embed-slots");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        using var client = await CreateContentEditorClientAsync();
        var slots = await client.GetFromJsonAsync<List<AdminFaqEmbedSlotDto>>("/api/v1/admin/faq-embed-slots", TestJson.Options);
        Assert.NotNull(slots);
        Assert.Equal(4, slots!.Count); // 種子四筆固定值（db/seed §21）。
        Assert.Contains(slots, s => s.Code == "trials");
        Assert.Contains(slots, s => s.Code == "sponsorship");
        Assert.Contains(slots, s => s.Code == "academy_admission");
        Assert.Contains(slots, s => s.Code == "program_detail");
    }

    [Fact]
    public async Task Faq_指定嵌入掛載點_新增_省略維持不變_空陣列清空_公開端點依掛載點查得到()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var trialsSlotId = await GetFaqEmbedSlotIdAsync("trials");
        var sponsorshipSlotId = await GetFaqEmbedSlotIdAsync("sponsorship");
        var slug = $"s1-7a-embed-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/faqs", new CreateFaqRequest
        {
            Slug = slug,
            CategoryIds = [categoryId],
            SortOrder = 0,
            Status = "published",
            EmbedSlotIds = [trialsSlotId],
            Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "試訓常見問題？", Answer = "測試答案。" } },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);

        try
        {
            Assert.Single(created!.EmbedSlots);
            Assert.Equal("trials", created.EmbedSlots[0].Code);

            // 公開端點依掛載點查詢：trials 查得到、sponsorship 查不到（還沒指定）。
            var publicTrials = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(
                "/api/v1/tcrfc/faqs/embeds/trials", TestJson.Options);
            Assert.Contains(publicTrials!, f => f.Id == created.Id);

            var publicSponsorshipBefore = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(
                "/api/v1/tcrfc/faqs/embeds/sponsorship", TestJson.Options);
            Assert.DoesNotContain(publicSponsorshipBefore!, f => f.Id == created.Id);

            // 省略 EmbedSlotIds＝維持不變。
            var updateOmitted = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}", new UpdateFaqRequest
            {
                Slug = slug,
                CategoryIds = [categoryId],
                SortOrder = 1,
                Status = "published",
                Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "試訓常見問題？", Answer = "測試答案（已更新排序）。" } },
            });
            Assert.Equal(HttpStatusCode.OK, updateOmitted.StatusCode);
            var afterOmitted = await updateOmitted.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
            Assert.Single(afterOmitted!.EmbedSlots);
            Assert.Equal("trials", afterOmitted.EmbedSlots[0].Code);

            // 明確指定新的掛載點清單（額外指定 sponsorship，移除 trials）＝整份取代。
            var updateReplace = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}", new UpdateFaqRequest
            {
                Slug = slug,
                CategoryIds = [categoryId],
                SortOrder = 1,
                Status = "published",
                EmbedSlotIds = [sponsorshipSlotId],
                Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "試訓常見問題？", Answer = "測試答案。" } },
            });
            Assert.Equal(HttpStatusCode.OK, updateReplace.StatusCode);
            var afterReplace = await updateReplace.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
            Assert.Single(afterReplace!.EmbedSlots);
            Assert.Equal("sponsorship", afterReplace.EmbedSlots[0].Code);

            var publicTrialsAfterReplace = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(
                "/api/v1/tcrfc/faqs/embeds/trials", TestJson.Options);
            Assert.DoesNotContain(publicTrialsAfterReplace!, f => f.Id == created.Id);
            var publicSponsorshipAfterReplace = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(
                "/api/v1/tcrfc/faqs/embeds/sponsorship", TestJson.Options);
            Assert.Contains(publicSponsorshipAfterReplace!, f => f.Id == created.Id);

            // 空陣列＝清空。
            var updateClear = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}", new UpdateFaqRequest
            {
                Slug = slug,
                CategoryIds = [categoryId],
                SortOrder = 1,
                Status = "published",
                EmbedSlotIds = [],
                Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "試訓常見問題？", Answer = "測試答案。" } },
            });
            Assert.Equal(HttpStatusCode.OK, updateClear.StatusCode);
            var afterClear = await updateClear.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);
            Assert.Empty(afterClear!.EmbedSlots);

            // 不存在的掛載點 id 要 400。
            var invalidSlotResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}", new UpdateFaqRequest
            {
                Slug = slug,
                CategoryIds = [categoryId],
                SortOrder = 1,
                Status = "published",
                EmbedSlotIds = [Guid.NewGuid()],
                Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "試訓常見問題？", Answer = "測試答案。" } },
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidSlotResponse.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{created!.Id}");
        }
    }

    [Fact]
    public async Task 公開嵌入端點_不回傳草稿或跨俱樂部題目_找不到的掛載點視為空清單()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var slotId = await GetFaqEmbedSlotIdAsync("program_detail");
        var slug = $"s1-7a-embed-draft-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/faqs", new CreateFaqRequest
        {
            Slug = slug,
            CategoryIds = [categoryId],
            SortOrder = 0,
            Status = "draft", // 草稿：不應出現在公開端點。
            EmbedSlotIds = [slotId],
            Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "草稿題目？", Answer = "測試答案。" } },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminFaqDetailDto>(TestJson.Options);

        try
        {
            var publicResult = await client.GetFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(
                "/api/v1/tcrfc/faqs/embeds/program_detail", TestJson.Options);
            Assert.DoesNotContain(publicResult!, f => f.Id == created!.Id);

            // 找不到的掛載點代碼視為空清單，不是 404。
            var unknownSlotResponse = await client.GetAsync("/api/v1/tcrfc/faqs/embeds/not-a-real-slot");
            Assert.Equal(HttpStatusCode.OK, unknownSlotResponse.StatusCode);
            var unknownSlotResult = await unknownSlotResponse.Content.ReadFromJsonAsync<List<Tcrfc.Api.Features.Faqs.FaqListItemDto>>(TestJson.Options);
            Assert.Empty(unknownSlotResult!);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{created!.Id}");
        }
    }

    // ───────────────────────────── 批次操作（規劃書 B4「批次改分類、批次顯示／隱藏」） ─────

    [Fact]
    public async Task Faq批次操作_未登入擋下_檢視者角色擋下()
    {
        using var anonymousClient = fixture.CreateClient();
        var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs/batch/show", new BatchFaqIdsRequest { Ids = [Guid.NewGuid()] });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var viewerClient = fixture.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));
        var viewerResponse = await viewerClient.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/faqs/batch/hide", new BatchFaqIdsRequest { Ids = [Guid.NewGuid()] });
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);
    }

    [Fact]
    public async Task Faq批次改分類_批次顯示隱藏_不存在的id列進Skipped()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryA = await GetAnyFaqCategoryIdAsync();
        var categoryB = await GetAnyFaqCategoryIdAsync(exclude: categoryA);

        var faq1 = await CreateFaqAsync(client, categoryA, "批次測試題目一");
        var faq2 = await CreateFaqAsync(client, categoryA, "批次測試題目二");
        var nonExistentId = Guid.NewGuid();

        try
        {
            var categoryResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/faqs/batch/category", new BatchChangeFaqCategoryRequest
            {
                Ids = [faq1.Id, faq2.Id, nonExistentId],
                CategoryIds = [categoryB],
            });
            Assert.Equal(HttpStatusCode.OK, categoryResponse.StatusCode);
            var categoryResult = await categoryResponse.Content.ReadFromJsonAsync<BatchFaqOperationResultDto>(TestJson.Options);
            Assert.Equal(2, categoryResult!.UpdatedCount);
            Assert.Single(categoryResult.Skipped);
            Assert.Equal(nonExistentId, categoryResult.Skipped[0].Id);

            var afterCategoryChange = await client.GetFromJsonAsync<AdminFaqDetailDto>($"/api/v1/admin/tcrfc/faqs/{faq1.Id}", TestJson.Options);
            Assert.Single(afterCategoryChange!.Categories);
            Assert.Equal(categoryB, afterCategoryChange.Categories[0].Id);

            var hideResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/faqs/batch/hide", new BatchFaqIdsRequest { Ids = [faq1.Id, faq2.Id] });
            Assert.Equal(HttpStatusCode.OK, hideResponse.StatusCode);
            var hideResult = await hideResponse.Content.ReadFromJsonAsync<BatchFaqOperationResultDto>(TestJson.Options);
            Assert.Equal(2, hideResult!.UpdatedCount);

            var showResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/faqs/batch/show", new BatchFaqIdsRequest { Ids = [faq1.Id] });
            var showResult = await showResponse.Content.ReadFromJsonAsync<BatchFaqOperationResultDto>(TestJson.Options);
            Assert.Equal(1, showResult!.UpdatedCount);

            var faq1AfterShow = await client.GetFromJsonAsync<AdminFaqDetailDto>($"/api/v1/admin/tcrfc/faqs/{faq1.Id}", TestJson.Options);
            var faq2AfterHide = await client.GetFromJsonAsync<AdminFaqDetailDto>($"/api/v1/admin/tcrfc/faqs/{faq2.Id}", TestJson.Options);
            Assert.Equal("published", faq1AfterShow!.Status);
            Assert.Equal("draft", faq2AfterHide!.Status);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq1.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq2.Id}");
        }
    }

    [Fact]
    public async Task Faq批次操作_共用內容列進Skipped_跨俱樂部列進Skipped()
    {
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var sharedFaqId = await InsertSharedFaqAsync(categoryId);

        try
        {
            using var client = await CreateContentEditorClientAsync();
            var response = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/faqs/batch/show", new BatchFaqIdsRequest { Ids = [sharedFaqId] });
            var result = await response.Content.ReadFromJsonAsync<BatchFaqOperationResultDto>(TestJson.Options);
            Assert.Equal(0, result!.UpdatedCount);
            Assert.Single(result.Skipped);
        }
        finally
        {
            await DeleteFaqByIdAsync(sharedFaqId);
        }
    }

    // ───────────────────────────── CSV 匯入／匯出（規劃書 B4） ─────────────────────────────

    [Fact]
    public async Task FaqCsv_未登入擋下_檢視者可匯出不可匯入()
    {
        using var anonymousClient = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/v1/admin/tcrfc/faqs/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.PostAsync(
            "/api/v1/admin/tcrfc/faqs/import", new StringContent("a"))).StatusCode);

        using var viewerClient = fixture.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        Assert.Equal(HttpStatusCode.OK, (await viewerClient.GetAsync("/api/v1/admin/tcrfc/faqs/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.PostAsync(
            "/api/v1/admin/tcrfc/faqs/import", new StringContent("a"))).StatusCode);
    }

    [Fact]
    public async Task FaqCsv_匯出格式為UTF8含BOM_只含這個俱樂部自己的題目()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var categoryName = await GetCategoryNameAsync(categoryId);
        var faq = await CreateFaqAsync(client, categoryId, "匯出格式測試題");

        try
        {
            var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var rows = Tcrfc.Api.Common.CsvUtils.Parse(text);
            Assert.Equal(new[] { "網址名稱", "所屬分類", "狀態", "排序", "中文問題", "中文答案", "英文問題", "英文答案" }, rows[0]);

            var exportedRow = rows.Skip(1).Single(r => r[0] == faq.Slug);
            Assert.Equal(categoryName, exportedRow[1]);
            Assert.Equal("顯示", exportedRow[2]);
            Assert.Equal("匯出格式測試題", exportedRow[4]);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{faq.Id}");
        }
    }

    [Fact]
    public async Task FaqCsv_匯入成功_新增與更新並存_upsert依網址名稱()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var categoryName = await GetCategoryNameAsync(categoryId);

        var existingFaq = await CreateFaqAsync(client, categoryId, "匯入前的舊問題");
        var newSlug = $"s1-6-csv-new-{Guid.NewGuid():N}";

        try
        {
            var csv = BuildFaqCsv(
                (existingFaq.Slug, categoryName, "隱藏", "9", "匯入後更新的問題？", "匯入後更新的答案。", "Updated by import?", "Updated by import answer."),
                (newSlug, categoryName, "顯示", "0", "全新匯入的問題？", "全新匯入的答案。", "", ""));

            var response = await client.PostAsync("/api/v1/admin/tcrfc/faqs/import", new StringContent(csv));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<FaqCsvImportResultDto>(TestJson.Options);
            Assert.Equal(2, result!.ImportedCount);
            Assert.Empty(result.Errors);

            var updated = await client.GetFromJsonAsync<AdminFaqDetailDto>($"/api/v1/admin/tcrfc/faqs/{existingFaq.Id}", TestJson.Options);
            Assert.Equal("draft", updated!.Status);
            Assert.Equal("匯入後更新的問題？", updated.Zh.Question);
            Assert.Equal("Updated by import?", updated.En!.Question);

            var listResponse = await client.GetAsync($"/api/v1/admin/tcrfc/faqs?keyword={Uri.EscapeDataString("全新匯入的問題")}");
            var listResult = await listResponse.Content.ReadFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminFaqListItemDto>>(TestJson.Options);
            var created = listResult!.Items.Single(i => i.Slug == newSlug);
            Assert.Equal("published", created.Status);

            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{created.Id}");
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/faqs/{existingFaq.Id}");
        }
    }

    [Fact]
    public async Task FaqCsv_任一列錯誤時整批不寫入_回傳逐列錯誤()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var categoryName = await GetCategoryNameAsync(categoryId);
        var goodSlug = $"s1-6-csv-good-{Guid.NewGuid():N}";

        var csv = BuildFaqCsv(
            (goodSlug, categoryName, "顯示", "0", "這一列格式正確？", "這一列格式正確。", "", ""),
            ("Bad Slug 大寫", categoryName, "顯示", "0", "網址名稱格式不對的題目？", "答案。", "", ""),
            ("s1-6-csv-bad-status", categoryName, "不知道", "0", "狀態欄位不合法？", "答案。", "", ""),
            ("s1-6-csv-bad-category", "不存在的分類", "顯示", "0", "分類不存在？", "答案。", "", ""));

        var response = await client.PostAsync("/api/v1/admin/tcrfc/faqs/import", new StringContent(csv));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FaqCsvImportResultDto>(TestJson.Options);
        Assert.Equal(0, result!.ImportedCount);
        Assert.Equal(3, result.Errors.Count); // 第 2、3、4 筆各一個錯誤（第 1 筆格式正確）

        // 整批都沒有寫入——連格式正確的那一列也不會被建立。
        var checkResponse = await client.GetAsync($"/api/v1/admin/tcrfc/faqs?keyword={Uri.EscapeDataString("這一列格式正確")}");
        var checkResult = await checkResponse.Content.ReadFromJsonAsync<Tcrfc.Api.Common.PagedResult<AdminFaqListItemDto>>(TestJson.Options);
        Assert.DoesNotContain(checkResult!.Items, i => i.Slug == goodSlug);
    }

    [Fact]
    public async Task FaqCsv_檔案內網址名稱重複_回傳錯誤且不寫入()
    {
        using var client = await CreateContentEditorClientAsync();
        var categoryId = await GetAnyFaqCategoryIdAsync();
        var categoryName = await GetCategoryNameAsync(categoryId);
        var slug = $"s1-6-csv-dup-{Guid.NewGuid():N}";

        var csv = BuildFaqCsv(
            (slug, categoryName, "顯示", "0", "重複的問題一？", "答案一。", "", ""),
            (slug, categoryName, "顯示", "1", "重複的問題二？", "答案二。", "", ""));

        var response = await client.PostAsync("/api/v1/admin/tcrfc/faqs/import", new StringContent(csv));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FaqCsvImportResultDto>(TestJson.Options);
        Assert.Equal(0, result!.ImportedCount);
        Assert.Contains(result.Errors, e => e.Reason.Contains("重複"));
    }

    [Fact]
    public async Task FaqCsv_表頭不正確_回400()
    {
        using var client = await CreateContentEditorClientAsync();
        var csv = "欄位一,欄位二\r\na,b\r\n";
        var response = await client.PostAsync("/api/v1/admin/tcrfc/faqs/import", new StringContent(csv));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── 搜尋無結果關鍵字排行（S1-8，E-51 補讀取端） ─────────────

    [Fact]
    public async Task FaqSearchMiss_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs/search-misses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FaqSearchMiss_角色沒有content_faq_view權限_403()
    {
        // team_competition（team.manager@tcrfc.test）本輪未指派 content.faq.* 權限碼
        // （見 apps/api/README.md S1-6 節「權限碼種子」），即使俱樂部範圍是 tcrfc 也該被擋。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("team.manager@tcrfc.test"));

        var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs/search-misses");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FaqSearchMiss_跨俱樂部_擋下_授權範圍內_成功()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test"));

        var tcrfcResponse = await client.GetAsync("/api/v1/admin/tcrfc/faqs/search-misses");
        Assert.Equal(HttpStatusCode.Forbidden, tcrfcResponse.StatusCode);

        var bwResponse = await client.GetAsync("/api/v1/admin/bw/faqs/search-misses");
        Assert.Equal(HttpStatusCode.OK, bwResponse.StatusCode);
    }

    [Fact]
    public async Task FaqSearchMiss_依count由多到少_同數依最後搜尋時間新到舊()
    {
        using var client = await CreateContentEditorClientAsync();
        var clubId = await GetClubIdAsync("tcrfc");
        var now = DateTime.UtcNow;
        var keywordHigh = $"s1-8-miss-high-{Guid.NewGuid():N}";
        var keywordLowOld = $"s1-8-miss-low-old-{Guid.NewGuid():N}";
        var keywordLowNew = $"s1-8-miss-low-new-{Guid.NewGuid():N}";

        await InsertSearchMissAsync(clubId, keywordHigh, hitCount: 10, lastSearchedAt: now.AddDays(-1));
        await InsertSearchMissAsync(clubId, keywordLowOld, hitCount: 3, lastSearchedAt: now.AddDays(-5));
        await InsertSearchMissAsync(clubId, keywordLowNew, hitCount: 3, lastSearchedAt: now.AddDays(-2));

        try
        {
            var result = await client.GetFromJsonAsync<List<AdminFaqSearchMissDto>>(
                "/api/v1/admin/tcrfc/faqs/search-misses?days=30&top=50", TestJson.Options);
            var keywords = result!.Select(r => r.Keyword).ToList();

            var indexHigh = keywords.IndexOf(keywordHigh);
            var indexLowNew = keywords.IndexOf(keywordLowNew);
            var indexLowOld = keywords.IndexOf(keywordLowOld);
            Assert.True(indexHigh >= 0 && indexLowNew >= 0 && indexLowOld >= 0);
            Assert.True(indexHigh < indexLowNew); // count 10 > count 3，排最前。
            Assert.True(indexLowNew < indexLowOld); // 同 count=3，較新的 lastSearchedAt 排前面。

            var highItem = result!.Single(r => r.Keyword == keywordHigh);
            Assert.Equal(10, highItem.Count);
        }
        finally
        {
            await DeleteSearchMissAsync(clubId, keywordHigh);
            await DeleteSearchMissAsync(clubId, keywordLowOld);
            await DeleteSearchMissAsync(clubId, keywordLowNew);
        }
    }

    [Fact]
    public async Task FaqSearchMiss_days篩選只依最後搜尋時間_不影響count的全站累計值()
    {
        using var client = await CreateContentEditorClientAsync();
        var clubId = await GetClubIdAsync("tcrfc");
        var now = DateTime.UtcNow;
        var withinWindow = $"s1-8-miss-within-{Guid.NewGuid():N}";
        var outsideWindow = $"s1-8-miss-outside-{Guid.NewGuid():N}";

        // outsideWindow 的 hit_count 故意比 withinWindow 高，驗證「count 是全站累計值，
        // days 只決定要不要列入，不會把 count 收斂成範圍內次數」（AdminFaqSearchMissDto.Count 說明）。
        await InsertSearchMissAsync(clubId, withinWindow, hitCount: 5, lastSearchedAt: now.AddDays(-10));
        await InsertSearchMissAsync(clubId, outsideWindow, hitCount: 99, lastSearchedAt: now.AddDays(-40));

        try
        {
            var result = await client.GetFromJsonAsync<List<AdminFaqSearchMissDto>>(
                "/api/v1/admin/tcrfc/faqs/search-misses?days=30&top=50", TestJson.Options);
            Assert.Contains(result!, r => r.Keyword == withinWindow && r.Count == 5);
            Assert.DoesNotContain(result!, r => r.Keyword == outsideWindow);
        }
        finally
        {
            await DeleteSearchMissAsync(clubId, withinWindow);
            await DeleteSearchMissAsync(clubId, outsideWindow);
        }
    }

    [Fact]
    public async Task FaqSearchMiss_top限制回傳筆數()
    {
        using var client = await CreateContentEditorClientAsync();
        var clubId = await GetClubIdAsync("tcrfc");
        var now = DateTime.UtcNow;
        var keywords = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var keyword = $"s1-8-miss-top-{i}-{Guid.NewGuid():N}";
            keywords.Add(keyword);
            await InsertSearchMissAsync(clubId, keyword, hitCount: 10 - i, lastSearchedAt: now.AddMinutes(-i));
        }

        try
        {
            var result = await client.GetFromJsonAsync<List<AdminFaqSearchMissDto>>(
                "/api/v1/admin/tcrfc/faqs/search-misses?days=30&top=2", TestJson.Options);
            Assert.Equal(2, result!.Count);
            Assert.Equal(keywords[0], result[0].Keyword); // hit_count 最高的兩筆（10、9）。
            Assert.Equal(keywords[1], result[1].Keyword);
        }
        finally
        {
            foreach (var keyword in keywords)
            {
                await DeleteSearchMissAsync(clubId, keyword);
            }
        }
    }

    [Theory]
    [InlineData("days=0")]
    [InlineData("days=366")]
    [InlineData("top=0")]
    [InlineData("top=201")]
    public async Task FaqSearchMiss_days或top超出範圍_400(string query)
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.GetAsync($"/api/v1/admin/tcrfc/faqs/search-misses?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public async Task FaqSearchMiss_days邊界值合法(int days)
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.GetAsync($"/api/v1/admin/tcrfc/faqs/search-misses?days={days}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public async Task FaqSearchMiss_top邊界值合法(int top)
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.GetAsync($"/api/v1/admin/tcrfc/faqs/search-misses?top={top}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FaqSearchMiss_days或top省略時採預設值30與50()
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/faqs/search-misses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static CreateAdminFaqCategoryRequest NewCategoryRequest(string nameZh, string? slug = null) => new()
    {
        Slug = slug ?? $"s1-6-cat-{Guid.NewGuid():N}",
        SortOrder = 99,
        Content = new AdminFaqCategoryContentInput { Zh = new AdminFaqCategoryLocaleContent { Name = nameZh } },
    };

    private static CreateFaqRequest NewFaqRequest(string slug, IReadOnlyList<Guid> categoryIds, string status = "draft") => new()
    {
        Slug = slug,
        CategoryIds = categoryIds,
        SortOrder = 0,
        Status = status,
        Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = "測試問題？", Answer = "測試答案。" } },
    };

    private async Task<AdminFaqDetailDto> CreateFaqAsync(HttpClient client, Guid categoryId, string questionZh)
    {
        var request = new CreateFaqRequest
        {
            Slug = $"s1-6-faq-{Guid.NewGuid():N}",
            CategoryIds = [categoryId],
            SortOrder = 0,
            Status = "published",
            Content = new AdminFaqContentInput { Zh = new AdminFaqLocaleContent { Question = questionZh, Answer = "測試答案。" } },
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

    private static async Task<Guid> GetClubIdAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM clubs WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>直接寫 SQL 造一筆 <c>faq_search_misses</c> 彙總列，繞過寫入端點——測試需要
    /// 控制 <c>hit_count</c>／<c>last_searched_at</c> 才能驗證排序與 <c>days</c> 篩選，
    /// 走 <c>POST .../search-misses</c> 端點沒辦法直接指定「過去第幾天」這種時間點。</summary>
    private static async Task InsertSearchMissAsync(Guid clubId, string keyword, int hitCount, DateTime lastSearchedAt)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO faq_search_misses (id, club_id, keyword, hit_count, last_searched_at)
            VALUES (NEWID(), @ClubId, @Keyword, @HitCount, @LastSearchedAt)
            """;
        command.Parameters.AddWithValue("@ClubId", clubId);
        command.Parameters.AddWithValue("@Keyword", keyword);
        command.Parameters.AddWithValue("@HitCount", hitCount);
        command.Parameters.AddWithValue("@LastSearchedAt", lastSearchedAt);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteSearchMissAsync(Guid clubId, string keyword)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM faq_search_misses WHERE club_id = @ClubId AND keyword = @Keyword";
        command.Parameters.AddWithValue("@ClubId", clubId);
        command.Parameters.AddWithValue("@Keyword", keyword);
        await command.ExecuteNonQueryAsync();
    }

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

    private static async Task<Guid> GetFaqEmbedSlotIdAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM faq_embed_slots WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> GetCategoryNameAsync(Guid categoryId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM faq_categories_i18n WHERE faq_category_id = @Id AND locale = N'zh-Hant'";
        command.Parameters.AddWithValue("@Id", categoryId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>組出一份符合 <c>AdminFaqsRepository.CsvHeader</c> 表頭的 CSV 文字，供匯入測試用。
    /// 每個 tuple 依序是：網址名稱、所屬分類（單一分類名稱字串，測試不涉及多分類拼接）、
    /// 狀態（「顯示」／「隱藏」）、排序、中文問題、中文答案、英文問題、英文答案。</summary>
    private static string BuildFaqCsv(params (string Slug, string CategoryName, string Status, string SortOrder,
        string QuestionZh, string AnswerZh, string QuestionEn, string AnswerEn)[] rows)
    {
        var allRows = new List<IEnumerable<string?>>
        {
            new string?[] { "網址名稱", "所屬分類", "狀態", "排序", "中文問題", "中文答案", "英文問題", "英文答案" },
        };
        allRows.AddRange(rows.Select(r => (IEnumerable<string?>)
            new string?[] { r.Slug, r.CategoryName, r.Status, r.SortOrder, r.QuestionZh, r.AnswerZh, r.QuestionEn, r.AnswerEn }));
        return Tcrfc.Api.Common.CsvUtils.BuildCsv(allRows);
    }

    /// <summary>直接寫 SQL 建一筆 <c>club_id IS NULL</c> 的共用常見問題——後台寫入路徑本身
    /// 永遠不會建立這種列（見 AdminFaqsRepository.CreateAsync 上的說明），要驗證「共用內容唯讀」
    /// 必須繞過 API 直接造資料，跟 <c>Features/AdminNews</c> 既有測試需要共用文章時的做法一致。</summary>
    private static async Task<Guid> InsertSharedFaqAsync(Guid categoryId)
    {
        var faqId = Guid.NewGuid();
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO faqs (id, club_id, slug, status, sort_order) VALUES (@Id, NULL, @Slug, 'published', 0);
            INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@Id, N'zh-Hant', N'共用測試問題？', N'共用測試答案。');
            INSERT INTO faq_category_links (faq_id, faq_category_id) VALUES (@Id, @CategoryId);
            """;
        command.Parameters.AddWithValue("@Id", faqId);
        command.Parameters.AddWithValue("@Slug", $"shared-faq-{Guid.NewGuid():N}");
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

    private static async Task SetFeedbackCountsAsync(Guid faqId, int helpful, int unhelpful)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE faqs SET helpful_count = @Helpful, unhelpful_count = @Unhelpful WHERE id = @Id";
        command.Parameters.AddWithValue("@Helpful", helpful);
        command.Parameters.AddWithValue("@Unhelpful", unhelpful);
        command.Parameters.AddWithValue("@Id", faqId);
        await command.ExecuteNonQueryAsync();
    }
}
