using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-5（B2 新聞與故事後端補完）：標籤、核心價值標籤、多型關聯、批次操作。
/// 跟 <see cref="AdminNewsWriteTests"/> 同一種紀律——打真正的 HTTP 管線、真正的
/// <c>tcrfc_club_dev</c>，不 mock，每個測試自己建立、自己清乾淨。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminNewsTagsRelationsTests(AdminWriteApiFixture fixture)
{
    private const string CategoryCode = "club";

    private static string UniqueSlug(string label) => $"s1-5-{label}-{Guid.NewGuid():N}";

    private static string UniqueTagSlug(string label) => $"s1-5-tag-{label}-{Guid.NewGuid():N}";

    private static HttpClient AuthorizedClient(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> ContentEditorTokenAsync() =>
        await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");

    private async Task DeleteBestEffortAsync(HttpClient client, Guid id, DateTime expectedUpdatedAt)
    {
        var url = $"/api/v1/admin/tcrfc/news/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}";
        await client.DeleteAsync(url);
    }

    // ───────────────────────────── 標籤 ─────────────────────────────

    [Fact]
    public async Task 建立文章時可以同時新建標籤與掛已存在的標籤()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());
        var newTagSlug = UniqueTagSlug("new");

        var createRequest = new CreateArticleRequest
        {
            Slug = UniqueSlug("create-tags"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：新建標籤" } },
            Tags = [new AdminArticleTagInput { Slug = newTagSlug, NameZh = "測試標籤中文名稱" }],
        };
        var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var createdTag = Assert.Single(created.Tags);
            Assert.Equal(newTagSlug, createdTag.Slug);
            Assert.Equal("測試標籤中文名稱", createdTag.NameZh);

            // 第二篇文章掛同一個標籤（已存在）：不重新建立，也忽略這次送的中文名稱（沿用既有名稱）。
            var secondRequest = new CreateArticleRequest
            {
                Slug = UniqueSlug("reuse-tag"),
                CategoryCode = CategoryCode,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：沿用既有標籤" } },
                Tags = [new AdminArticleTagInput { Slug = newTagSlug, NameZh = "這個名稱應該被忽略" }],
            };
            var secondResponse = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(secondRequest));
            secondResponse.EnsureSuccessStatusCode();
            var second = (await secondResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

            try
            {
                var reusedTag = Assert.Single(second.Tags);
                Assert.Equal(newTagSlug, reusedTag.Slug);
                Assert.Equal("測試標籤中文名稱", reusedTag.NameZh); // 沒有被第二次的輸入覆寫

                var tagRowCount = await ScalarAsync<int>(
                    "SELECT COUNT(*) FROM tags WHERE slug = @Slug", ("@Slug", newTagSlug));
                Assert.Equal(1, tagRowCount); // 只建立一筆，不是兩筆
            }
            finally
            {
                await DeleteBestEffortAsync(client, second.Id, second.UpdatedAt);
            }
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
            await ExecuteAsync("DELETE FROM article_tags WHERE tag_id IN (SELECT id FROM tags WHERE slug = @Slug)", ("@Slug", newTagSlug));
            await ExecuteAsync("DELETE FROM tags_i18n WHERE tag_id IN (SELECT id FROM tags WHERE slug = @Slug)", ("@Slug", newTagSlug));
            await ExecuteAsync("DELETE FROM tags WHERE slug = @Slug", ("@Slug", newTagSlug));
        }
    }

    [Fact]
    public async Task 新標籤沒有中文名稱時回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("missing-name"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：缺中文名稱" } },
            Tags = [new AdminArticleTagInput { Slug = UniqueTagSlug("missing-name") }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 標籤網址名稱格式不正確回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("bad-tag-slug"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：格式不正確" } },
            Tags = [new AdminArticleTagInput { Slug = "Not_Valid_Slug", NameZh = "隨便" }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 更新時省略標籤欄位維持不變_空陣列才是清空()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());
        var tagSlug = UniqueTagSlug("keep-or-clear");
        var slug = UniqueSlug("keep-or-clear");

        var createRequest = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：維持或清空" } },
            Tags = [new AdminArticleTagInput { Slug = tagSlug, NameZh = "維持或清空測試標籤" }],
        };
        var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            // 第一次 PUT：省略 Tags（null）——改標題但不提標籤，標籤應該維持不變。
            var updateWithoutTags = new UpdateArticleRequest
            {
                Slug = slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：改了標題" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var afterNoTouch = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateWithoutTags));
            afterNoTouch.EnsureSuccessStatusCode();
            var afterNoTouchDto = (await afterNoTouch.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Single(afterNoTouchDto.Tags);
            Assert.Equal(tagSlug, afterNoTouchDto.Tags[0].Slug);

            // 第二次 PUT：明確傳空陣列——這才是「清空」。
            var updateWithEmptyTags = new UpdateArticleRequest
            {
                Slug = slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "標籤測試：改了標題" } },
                ExpectedUpdatedAt = afterNoTouchDto.UpdatedAt,
                Tags = [],
            };
            var afterClear = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateWithEmptyTags));
            afterClear.EnsureSuccessStatusCode();
            var afterClearDto = (await afterClear.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Empty(afterClearDto.Tags);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, created.Id, probe.UpdatedAt);
            }

            await ExecuteAsync("DELETE FROM article_tags WHERE tag_id IN (SELECT id FROM tags WHERE slug = @Slug)", ("@Slug", tagSlug));
            await ExecuteAsync("DELETE FROM tags_i18n WHERE tag_id IN (SELECT id FROM tags WHERE slug = @Slug)", ("@Slug", tagSlug));
            await ExecuteAsync("DELETE FROM tags WHERE slug = @Slug", ("@Slug", tagSlug));
        }
    }

    // ───────────────────────────── 核心價值標籤 ─────────────────────────────

    [Fact]
    public async Task 核心價值標籤合法值可以儲存並讀回()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("core-values"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "核心價值標籤測試" } },
            CoreValueTags = ["players_first", "community"],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            Assert.Equal(["community", "players_first"], created.CoreValueTags.OrderBy(v => v));

            var reread = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal(["community", "players_first"], reread!.CoreValueTags.OrderBy(v => v));
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 核心價值標籤不合法值回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("core-values-invalid"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "核心價值標籤測試：不合法值" } },
            CoreValueTags = ["not_a_real_value"],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── 多型關聯 ─────────────────────────────

    [Fact]
    public async Task 關聯到同俱樂部的球隊成功且可以讀回()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());
        var d1TeamId = await ScalarAsync<Guid>("SELECT id FROM teams WHERE code = 'D1'");

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("relation-same-club"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "關聯測試：同俱樂部球隊" } },
            Relations = [new AdminArticleRelationInput { TargetType = "team", TargetId = d1TeamId }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var relation = Assert.Single(created.Relations);
            Assert.Equal("team", relation.TargetType);
            Assert.Equal(d1TeamId, relation.TargetId);
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    /// <summary>🔴 多型關聯的跨俱樂部隔離：`tcrfc` 底下的文章不能關聯到 `bw`（藍鯨）的球隊，
    /// 即使那筆球隊資料真實存在——跨俱樂部就是不合法。</summary>
    [Fact]
    public async Task 關聯到跨俱樂部的球隊回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());
        var bw1TeamId = await ScalarAsync<Guid>("SELECT id FROM teams WHERE code = 'BW1'");

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("relation-cross-club"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "關聯測試：跨俱樂部球隊" } },
            Relations = [new AdminArticleRelationInput { TargetType = "team", TargetId = bw1TeamId }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // 驗證真的沒有留下孤兒資料列（驗證失敗時整篇文章都不該落地）。
        var articleCount = await ScalarAsync<int>(
            "SELECT COUNT(*) FROM articles WHERE slug = @Slug", ("@Slug", request.Slug));
        Assert.Equal(0, articleCount);
    }

    [Fact]
    public async Task 關聯類型不支援回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("relation-bad-type"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "關聯測試：不支援的類型" } },
            Relations = [new AdminArticleRelationInput { TargetType = "sponsor", TargetId = Guid.NewGuid() }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 關聯目標不存在回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug("relation-not-found"),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "關聯測試：目標不存在" } },
            Relations = [new AdminArticleRelationInput { TargetType = "team", TargetId = Guid.NewGuid() }],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── 批次操作 ─────────────────────────────

    [Fact]
    public async Task 批次改分類_跨俱樂部與不存在的一併略過()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());
        var ownDraft = await CreateDraftAsync(client, UniqueSlug("batch-category-own"));

        // 另建一支系統管理員用戶端（不受俱樂部授權範圍限制），建立一篇真正屬於 bw 的文章，
        // 用來驗證批次操作對跨俱樂部的文章一律略過（不是因為剛好查不到資料才通過）。
        // 🔴 不用 partner.club@tcrfc.test（僅藍鯨）——那個角色沒有 content.article.delete
        // 權限，測試自己的清理步驟會 403，這裡改用系統管理員同時負責建立與清理。
        using var bwClient = AuthorizedClient(fixture.CreateClient(),
            await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        var bwSlug = UniqueSlug("batch-category-bw");
        var bwCreateRequest = new CreateArticleRequest
        {
            Slug = bwSlug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "跨俱樂部批次測試（藍鯨）" } },
        };
        var bwCreateResponse = await bwClient.PostAsync("/api/v1/admin/bw/news", AdminArticleMultipart.Build(bwCreateRequest));
        bwCreateResponse.EnsureSuccessStatusCode();
        var bwArticle = (await bwCreateResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var ids = new List<Guid> { ownDraft.Id, Guid.NewGuid(), bwArticle.Id };

            var batchRequest = new BatchChangeCategoryRequest { Ids = ids, CategoryCode = "match" };
            var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news/batch/category", batchRequest, TestJson.WriteOptions);
            response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<BatchOperationResultDto>(TestJson.Options))!;

            Assert.Equal(1, result.UpdatedCount);
            Assert.Equal(2, result.Skipped.Count);
            Assert.Contains(result.Skipped, s => s.Id == bwArticle.Id);

            var reread = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{ownDraft.Id}", TestJson.Options);
            Assert.Equal("match", reread!.CategoryCode);

            // 藍鯨那篇文章完全沒被動到（分類還是原本的 club）。
            var bwReread = await bwClient.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/bw/news/{bwArticle.Id}", TestJson.Options);
            Assert.Equal(CategoryCode, bwReread!.CategoryCode);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{ownDraft.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, ownDraft.Id, probe.UpdatedAt);
            }

            var bwProbe = await bwClient.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/bw/news/{bwArticle.Id}", TestJson.Options);
            if (bwProbe is not null)
            {
                var deleteUrl = $"/api/v1/admin/bw/news/{bwArticle.Id}?expectedUpdatedAt={Uri.EscapeDataString(bwProbe.UpdatedAt.ToString("o"))}";
                await bwClient.DeleteAsync(deleteUrl);
            }
        }
    }

    [Fact]
    public async Task 批次改分類_ids為空回400()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var batchRequest = new BatchChangeCategoryRequest { Ids = [], CategoryCode = "match" };
        var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news/batch/category", batchRequest, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 批次發布_只有草稿與排程中的文章會被處理()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var draft = await CreateDraftAsync(client, UniqueSlug("batch-publish-draft"));
        var alreadyPublished = await CreateDraftAsync(client, UniqueSlug("batch-publish-already"));
        var publishAlready = await client.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{alreadyPublished.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = alreadyPublished.UpdatedAt },
            TestJson.WriteOptions);
        publishAlready.EnsureSuccessStatusCode();
        var alreadyPublishedDto = (await publishAlready.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var batchRequest = new BatchArticleIdsRequest { Ids = [draft.Id, alreadyPublishedDto.Id] };
            var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news/batch/publish", batchRequest, TestJson.WriteOptions);
            response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<BatchOperationResultDto>(TestJson.Options))!;

            Assert.Equal(1, result.UpdatedCount);
            Assert.Single(result.Skipped);
            Assert.Equal(alreadyPublishedDto.Id, result.Skipped[0].Id);

            var draftAfter = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{draft.Id}", TestJson.Options);
            Assert.Equal("published", draftAfter!.Status);
        }
        finally
        {
            var probe1 = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{draft.Id}", TestJson.Options);
            if (probe1 is not null) await DeleteBestEffortAsync(client, draft.Id, probe1.UpdatedAt);

            var probe2 = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{alreadyPublishedDto.Id}", TestJson.Options);
            if (probe2 is not null) await DeleteBestEffortAsync(client, alreadyPublishedDto.Id, probe2.UpdatedAt);
        }
    }

    [Fact]
    public async Task 批次下架_已發布的文章轉回草稿_草稿本身被略過()
    {
        using var client = AuthorizedClient(fixture.CreateClient(), await ContentEditorTokenAsync());

        var draft = await CreateDraftAsync(client, UniqueSlug("batch-unpublish-draft"));
        var toPublish = await CreateDraftAsync(client, UniqueSlug("batch-unpublish-target"));
        var publishResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{toPublish.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = toPublish.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();
        var published = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            var batchRequest = new BatchArticleIdsRequest { Ids = [draft.Id, published.Id] };
            var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news/batch/unpublish", batchRequest, TestJson.WriteOptions);
            response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<BatchOperationResultDto>(TestJson.Options))!;

            Assert.Equal(1, result.UpdatedCount);
            Assert.Single(result.Skipped);
            Assert.Equal(draft.Id, result.Skipped[0].Id);

            var publishedAfter = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{published.Id}", TestJson.Options);
            Assert.Equal("draft", publishedAfter!.Status);

            // 公開 API 應該立刻看不到這篇文章了（下架＝從公開站消失）。
            var publicResponse = await client.GetAsync($"/api/v1/tcrfc/news/{published.Slug}");
            Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);
        }
        finally
        {
            var probe1 = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{draft.Id}", TestJson.Options);
            if (probe1 is not null) await DeleteBestEffortAsync(client, draft.Id, probe1.UpdatedAt);

            var probe2 = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{published.Id}", TestJson.Options);
            if (probe2 is not null) await DeleteBestEffortAsync(client, published.Id, probe2.UpdatedAt);
        }
    }

    private async Task<AdminArticleDetailDto> CreateDraftAsync(HttpClient client, string slug)
    {
        var request = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"測試文章 {slug}" } },
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        if (result is null or DBNull)
        {
            return default!;
        }

        // 🔴 不用 Convert.ChangeType——Guid 沒有實作 IConvertible，會在執行期拋例外。
        // SqlClient 對已知的 SQL 型別（uniqueidentifier→Guid、int→Int32……）本來就回傳
        // 對應的正確 CLR 型別，直接轉型即可。
        return (T)result;
    }

    private static async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
