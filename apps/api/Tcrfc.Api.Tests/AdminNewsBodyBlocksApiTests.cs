using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Images;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 新聞 v3.28 端對端：封面焦點、內文區塊（圖片多檔上傳 <c>file:{i}:image</c>／<c>file:{i}:images:{j}</c>、換圖與刪除的物件清理、
/// 補償刪除、舊 body 轉 text 區塊）。真的 HTTP 管線＋真實 <c>tcrfc_club</c>＋真實 Azurite（不 mock）。
/// 純邏輯（驗證、拆合往返）見 <see cref="NewsBodyBlocksTests"/>。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminNewsBodyBlocksApiTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static string UniqueSlug() => $"news-body-test-{Guid.NewGuid():N}";

    private async Task<HttpClient> EditorAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static JsonObject Bi(string zh, string? en = null) => new() { ["zh"] = zh, ["en"] = en };

    private static NewsBlockInput Block(string type, JsonObject content) => new() { BlockType = type, Content = content };

    private static CreateArticleRequest NewRequest(string slug, IReadOnlyList<NewsBlockInput>? blocks = null, decimal? fx = null, decimal? fy = null)
        => new()
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "內文區塊測試" } },
            BodyBlocks = blocks,
            CoverFocalX = fx,
            CoverFocalY = fy,
        };

    private static UpdateArticleRequest UpdateOf(AdminArticleDetailDto d, IReadOnlyList<NewsBlockInput>? blocks, bool removeCover = false, decimal? fx = null, decimal? fy = null)
        => new()
        {
            Slug = d.Slug,
            CategoryCode = d.CategoryCode,
            Content = new AdminArticleContentInput { Zh = d.Zh },
            ExpectedUpdatedAt = d.UpdatedAt,
            BodyBlocks = blocks,
            RemoveCover = removeCover,
            CoverFocalX = fx,
            CoverFocalY = fy,
        };

    private static MultipartFormDataContent Form(object payload, byte[]? cover = null, params (string Field, byte[] Bytes)[] files)
    {
        var form = AdminArticleMultipart.Build(payload, cover);
        foreach (var (field, bytes) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(content, field, "body.jpg");
        }

        return form;
    }

    private static JsonObject PendingImage(string altZh = "內文圖") => new() { ["pendingUpload"] = true, ["altZh"] = altZh, ["altEn"] = "alt" };

    private async Task<AdminArticleDetailDto> CreateAsync(HttpClient client, CreateArticleRequest request, byte[]? cover = null, params (string, byte[])[] files)
    {
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", Form(request, cover, files));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, AdminArticleDetailDto d)
        => client.DeleteAsync($"/api/v1/admin/tcrfc/news/{d.Id}?expectedUpdatedAt={Uri.EscapeDataString(d.UpdatedAt.ToString("o"))}");

    private async Task<bool> AllObjectsExistAsync(string mainKey)
    {
        foreach (var key in ImageObjectKey.AllObjectKeys(mainKey))
        {
            if (!(await fixture.InspectorContainer.GetBlobClient(key).ExistsAsync()).Value)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> NoObjectExistsAsync(string mainKey)
    {
        foreach (var key in ImageObjectKey.AllObjectKeys(mainKey))
        {
            if ((await fixture.InspectorContainer.GetBlobClient(key).ExistsAsync()).Value)
            {
                return false;
            }
        }

        return true;
    }

    private Task<int> CountBodyBlobsAsync(Guid articleId) => CountBlobsAsync($"tcrfc/articles/{articleId}/body/");

    private async Task<int> CountBlobsAsync(string prefix)
    {
        var count = 0;
        await foreach (var _ in fixture.InspectorContainer.GetBlobsAsync(
            Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    // ───────────────────────────── 封面焦點 ─────────────────────────────

    [AzuriteFact]
    public async Task 封面焦點_照存_回傳_移除封面時強制清空()
    {
        using var client = await EditorAsync();
        var created = await CreateAsync(client, NewRequest(UniqueSlug(), fx: 30.5m, fy: 70m), TestImages.SmallPng());
        try
        {
            Assert.Equal(30.5m, created.CoverFocalX);
            Assert.Equal(70m, created.CoverFocalY);

            // 帶焦點、不動封面 → 照存
            var kept = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", Form(UpdateOf(created, null, fx: 10m, fy: 20m)));
            kept.EnsureSuccessStatusCode();
            var afterKeep = (await kept.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Equal(10m, afterKeep.CoverFocalX);

            // 移除封面，payload 仍帶焦點 → 強制 NULL
            var removed = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", Form(UpdateOf(afterKeep, null, removeCover: true, fx: 10m, fy: 20m)));
            removed.EnsureSuccessStatusCode();
            var afterRemove = (await removed.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Null(afterRemove.CoverKey);
            Assert.Null(afterRemove.CoverFocalX);
            Assert.Null(afterRemove.CoverFocalY);
            created = afterRemove;
        }
        finally
        {
            await DeleteAsync(client, created);
        }
    }

    [AzuriteFact]
    public async Task 封面焦點_只給一個或超出範圍或超過兩位小數_回400()
    {
        using var client = await EditorAsync();
        foreach (var (x, y) in new (decimal?, decimal?)[] { (50m, null), (null, 50m), (-1m, 50m), (50m, 100.5m), (10.123m, 20m) })
        {
            var response = await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(UniqueSlug(), fx: x, fy: y)));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    // ───────────────────────────── 內文區塊 ─────────────────────────────

    [AzuriteFact]
    public async Task 內文區塊_圖片與圖集上傳_物件鍵含區塊索引_更新移除後清理_刪除文章清全部內文圖()
    {
        using var client = await EditorAsync();
        var blocks = new[]
        {
            Block("text", new JsonObject { ["body"] = Bi("段落", "Paragraph") }),
            Block("image", new JsonObject { ["image"] = PendingImage("單張"), ["caption"] = Bi("說明") }),
            Block("gallery", new JsonObject { ["images"] = new JsonArray(PendingImage("甲"), PendingImage("乙")) }),
        };
        var created = await CreateAsync(client, NewRequest(UniqueSlug(), blocks), null,
            ("file:1:image", TestImages.JpegWithGps(1200, 800)),
            ("file:2:images:0", TestImages.JpegWithGps(1000, 1500)),
            ("file:2:images:1", TestImages.JpegWithGps(900, 900)));

        try
        {
            Assert.Equal(3, created.BodyBlocks.Count);
            var single = created.BodyBlocks[1]["content"]!["image"]!;
            var singleKey = single["key"]!.GetValue<string>();
            Assert.StartsWith($"tcrfc/articles/{created.Id}/body/1/image/", singleKey);
            Assert.NotNull(single["url"]);
            var galleryKeys = created.BodyBlocks[2]["content"]!["images"]!.AsArray().Select(i => i!["key"]!.GetValue<string>()).ToList();
            Assert.All(galleryKeys, k => Assert.StartsWith($"tcrfc/articles/{created.Id}/body/2/images-", k));
            foreach (var key in galleryKeys.Append(singleKey))
            {
                Assert.True(await AllObjectsExistAsync(key));
            }

            // 更新：保留單張、移除圖集 → 圖集主檔與衍生檔刪除，單張還在
            var keepSingle = new[]
            {
                System.Text.Json.JsonSerializer.Deserialize<NewsBlockInput>(created.BodyBlocks[0], TestJson.Options)!,
                System.Text.Json.JsonSerializer.Deserialize<NewsBlockInput>(created.BodyBlocks[1], TestJson.Options)!,
            };
            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", Form(UpdateOf(created, keepSingle)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Equal(2, updated.BodyBlocks.Count);
            Assert.True(await AllObjectsExistAsync(singleKey));
            foreach (var key in galleryKeys)
            {
                Assert.True(await NoObjectExistsAsync(key));
            }

            // 刪除文章 → 內文圖全清
            Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(client, updated)).StatusCode);
            Assert.True(await NoObjectExistsAsync(singleKey));
            Assert.Equal(0, await CountBodyBlobsAsync(created.Id));
        }
        finally
        {
            await DeleteAsync(client, created);
        }
    }

    [AzuriteFact]
    public async Task 內文圖片寬度低於800_回400_不留下任何物件_不建立文章()
    {
        using var client = await EditorAsync();
        var slug = UniqueSlug();
        var blocks = new[] { Block("image", new JsonObject { ["image"] = PendingImage() }) };

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(slug, blocks), null, ("file:0:image", TestImages.JpegWithGps(700, 900))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("寬度太小", await response.Content.ReadAsStringAsync());
    }

    [AzuriteFact]
    public async Task 建立失敗_網址名稱重複_內文圖片補償刪除()
    {
        using var client = await EditorAsync();
        var slug = UniqueSlug();
        var first = await CreateAsync(client, NewRequest(slug));
        try
        {
            var before = await CountBlobsAsync("tcrfc/articles/");
            var blocks = new[] { Block("image", new JsonObject { ["image"] = PendingImage() }) };
            var response = await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(slug, blocks), null, ("file:0:image", TestImages.JpegWithGps(1200, 800))));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            // 上傳成功、資料列寫入失敗（slug 重複）→ 補償刪除，物件數量不增加。
            Assert.Equal(before, await CountBlobsAsync("tcrfc/articles/"));
        }
        finally
        {
            await DeleteAsync(client, first);
        }
    }

    [AzuriteFact]
    public async Task 引用不存在的既有圖片key_回400()
    {
        using var client = await EditorAsync();
        var blocks = new[]
        {
            Block("image", new JsonObject
            {
                ["image"] = new JsonObject { ["key"] = "tcrfc/articles/other/cover/x.webp", ["width"] = 1000, ["height"] = 800, ["altZh"] = "外來圖" },
            }),
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(UniqueSlug(), blocks)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [AzuriteFact]
    public async Task 舊body純文字_detail回傳單一text區塊_不帶bodyBlocks的更新不動內文()
    {
        using var client = await EditorAsync();
        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug(),
            CategoryCode = "club",
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = "舊內文", Body = "這是舊的純文字內文" },
                En = new AdminArticleLocaleContent { Title = "Legacy", Body = "Old plain text" },
            },
        };
        var created = await CreateAsync(client, request);
        try
        {
            var only = Assert.Single(created.BodyBlocks);
            Assert.Equal("text", only["blockType"]!.GetValue<string>());
            Assert.Equal("這是舊的純文字內文", only["content"]!["body"]!["zh"]!.GetValue<string>());
            Assert.Equal("Old plain text", only["content"]!["body"]!["en"]!.GetValue<string>());

            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", Form(UpdateOf(created, null)));
            response.EnsureSuccessStatusCode();
        }
        finally
        {
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            await DeleteAsync(client, current!);
        }
    }

    [AzuriteFact]
    public async Task 未知區塊型別與不合法影片代碼_回400()
    {
        using var client = await EditorAsync();
        var unknown = new[] { Block("cta", new JsonObject()) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(UniqueSlug(), unknown)))).StatusCode);

        var badVideo = new[] { Block("video_embed", new JsonObject { ["provider"] = "youtube", ["videoId"] = "a b<c>" }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/news", Form(NewRequest(UniqueSlug(), badVideo)))).StatusCode);
    }
}
