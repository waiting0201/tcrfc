using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1 頁面區塊圖片欄位（圖文左右／圖片藝廊）的上傳、換圖、刪除、補償交易。真的啟動
/// <c>azurite-blob</c>（見 <see cref="AdminWriteAzuriteEnabledApiFixture"/>），不 mock 儲存體。
/// 契約見 <c>Features/AdminPages/AdminPageRequestForm.cs</c>：檔案欄位命名
/// <c>file:{區塊索引}:{圖片路徑}</c>。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminPagesImageTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static string UniqueSlug() => $"admin-write-page-image-{Guid.NewGuid():N}";

    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private async Task AssertAllObjectsExistAsync(string mainKey, bool shouldExist)
    {
        foreach (var key in ImageObjectKey.AllObjectKeys(mainKey))
        {
            var exists = await fixture.InspectorContainer.GetBlobClient(key).ExistsAsync();
            Assert.Equal(shouldExist, exists.Value);
        }
    }

    private static JsonObject PendingImage(string altZh = "圖片說明") => new() { ["pendingUpload"] = true, ["altZh"] = altZh, ["altEn"] = "alt" };

    private static AdminPageBlockInput TextImageBlock() => new()
    {
        BlockType = PageBlockTypes.TextImage,
        Content = new JsonObject { ["body"] = PageBlockSamples.Bilingual("圖文左右"), ["imagePosition"] = "left", ["image"] = PendingImage() },
    };

    private static AdminPageBlockInput GalleryBlock(int count) => new()
    {
        BlockType = PageBlockTypes.Gallery,
        Content = new JsonObject
        {
            ["images"] = new JsonArray(Enumerable.Range(0, count).Select(i => (JsonNode)PendingImage($"第 {i} 張")).ToArray()),
        },
    };

    [Fact]
    public async Task 圖文左右_夾檔案上傳成功_物件鍵含俱樂部頁面與區塊索引()
    {
        using var client = await ContentEditorClientAsync();
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "圖文左右測試" } },
            Blocks = [TextImageBlock()],
        };

        var files = new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallPng() };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request, files));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        Assert.NotNull(created);

        try
        {
            var imageNode = created!.Blocks[0].Content.GetProperty("image");
            var key = imageNode.GetProperty("key").GetString();
            Assert.NotNull(key);
            Assert.StartsWith($"tcrfc/pages/{created.Id}/blocks/0/image/", key);
            Assert.EndsWith(".webp", key);
            await AssertAllObjectsExistAsync(key!, shouldExist: true);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created!.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        }
    }

    [Fact]
    public async Task 圖片藝廊_一次請求夾兩張圖_各自獨立的物件鍵()
    {
        using var client = await ContentEditorClientAsync();
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "圖片藝廊測試" } },
            Blocks = [GalleryBlock(2)],
        };

        var files = new Dictionary<string, byte[]>
        {
            ["file:0:images:0"] = TestImages.SmallPng(),
            ["file:0:images:1"] = TestImages.SmallWebp(),
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request, files));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        Assert.NotNull(created);

        try
        {
            var images = created!.Blocks[0].Content.GetProperty("images");
            Assert.Equal(2, images.GetArrayLength());
            var key0 = images[0].GetProperty("key").GetString();
            var key1 = images[1].GetProperty("key").GetString();
            Assert.NotEqual(key0, key1);
            Assert.StartsWith($"tcrfc/pages/{created.Id}/blocks/0/images-0/", key0);
            Assert.StartsWith($"tcrfc/pages/{created.Id}/blocks/0/images-1/", key1);
            await AssertAllObjectsExistAsync(key0!, shouldExist: true);
            await AssertAllObjectsExistAsync(key1!, shouldExist: true);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created!.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        }
    }

    [Fact]
    public async Task 補償交易_第一個區塊上傳成功但第二個區塊驗證失敗_已上傳的物件會被清掉()
    {
        using var client = await ContentEditorClientAsync();
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "補償交易測試" } },
            Blocks = [TextImageBlock(), PageBlockSamples.CtaInvalid()], // 第二個區塊缺必填欄位
        };

        var files = new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallPng() };

        // 先用一個獨立的請求量出「這次會產生的物件鍵長什麼樣子」不可行（鍵含隨機值），
        // 改用「請求整體失敗、資料庫沒有任何一筆頁面被建立」加上「容器裡這個前綴底下沒有殘留物件」
        // 兩個斷言合起來證明補償交易生效。
        var beforeCount = await CountBlobsUnderPrefixAsync($"tcrfc/pages/");

        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request, files));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var afterCount = await CountBlobsUnderPrefixAsync("tcrfc/pages/");
        Assert.Equal(beforeCount, afterCount); // 沒有任何物件因為這次失敗的請求而多出來
    }

    [Fact]
    public async Task 換圖成功後_舊圖被刪除_新圖保留()
    {
        using var client = await ContentEditorClientAsync();
        var createRequest = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "換圖測試" } },
            Blocks = [TextImageBlock()],
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(createRequest, new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallPng() }));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        var oldKey = created!.Blocks[0].Content.GetProperty("image").GetProperty("key").GetString()!;
        await AssertAllObjectsExistAsync(oldKey, shouldExist: true);

        try
        {
            var updateRequest = new UpdatePageRequest
            {
                Slug = created.Slug,
                Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "換圖測試" } },
                Blocks = [TextImageBlock()], // 新的 pendingUpload，換一張圖
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var updateResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/pages/{created.Id}",
                AdminPageMultipart.Build(updateRequest, new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallWebp() }));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
            var newKey = updated!.Blocks[0].Content.GetProperty("image").GetProperty("key").GetString()!;

            Assert.NotEqual(oldKey, newKey);
            await AssertAllObjectsExistAsync(oldKey, shouldExist: false);
            await AssertAllObjectsExistAsync(newKey, shouldExist: true);

            await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(updated.UpdatedAt.ToString("o"))}");
        }
        catch
        {
            var probe = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/tcrfc/pages/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(probe.UpdatedAt.ToString("o"))}");
            }

            throw;
        }
    }

    [Fact]
    public async Task 刪除頁面後_區塊圖片物件一併被刪除()
    {
        using var client = await ContentEditorClientAsync();
        var createRequest = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "刪除測試" } },
            Blocks = [TextImageBlock()],
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(createRequest, new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallPng() }));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        var key = created!.Blocks[0].Content.GetProperty("image").GetProperty("key").GetString()!;
        await AssertAllObjectsExistAsync(key, shouldExist: true);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        deleteResponse.EnsureSuccessStatusCode();

        await AssertAllObjectsExistAsync(key, shouldExist: false);
    }

    /// <summary>
    /// E-47 的教訓：補償刪除必須用 <see cref="CancellationToken.None"/>，不能沿用觸發失敗當下
    /// 已經被取消的請求 token。這裡直接呼叫 <see cref="AdminPagesRepository"/>（略過 HTTP 層），
    /// 用一個裝飾器包住真正在跑的 <see cref="BlobImageStorageService"/>：第一個區塊的圖片真的上傳
    /// 成功後，立刻取消這次呼叫共用的 <see cref="CancellationTokenSource"/>，模擬「圖片上傳完成的
    /// 瞬間使用者恰好斷線」；第二個區塊內容不合法，導致整個 <c>CreateAsync</c> 失敗——驗證即使
    /// 呼叫端傳入的 token 此時已經取消，第一個區塊已上傳的物件仍然會被補償刪除乾淨，不會因為
    /// <c>DeleteAsync</c> 對已取消的 token fail-open 吞例外而留下孤兒物件。
    /// </summary>
    [Fact]
    public async Task 補償刪除沿用CancellationTokenNone_即使外層token已取消仍能清乾淨()
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IQueryCache>();

        using var cts = new CancellationTokenSource();
        var realStorage = new BlobImageStorageService(fixture.InspectorContainer, NullLogger<BlobImageStorageService>.Instance);
        var cancelAfterFirstUploadStorage = new CancelAfterFirstUploadImageStorageService(realStorage, cts);

        var repository = new AdminPagesRepository(dbContext, cache, cancelAfterFirstUploadStorage);

        // 直接呼叫 repository 層才能精準控制「圖片剛上傳完成的瞬間」，HTTP 客戶端做不到這種時序
        // 控制。仍然要走真正的 IAdminClubAuthorizer.AuthorizeAsync 才能拿到合法的 AdminClubScope
        // （建構子 internal，見該型別上的說明），只是用 TestAdminHttpContext 組一個不必真的跑
        // 完整 JWT 中介軟體管線的 HttpContext——AdminClubAuthorizer 的相依項全部來自建構子注入，
        // 不需要 HttpContext.RequestServices。
        var authorizer = scope.ServiceProvider.GetRequiredService<IAdminClubAuthorizer>();
        var httpContext = await TestAdminHttpContext.CreateAuthenticatedAsync("content.editor@tcrfc.test");
        var adminScope = await authorizer.AuthorizeAsync(httpContext, "tcrfc", "content.page.create", CancellationToken.None);

        var pageId = Guid.NewGuid();
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "取消測試" } },
            Blocks = [TextImageBlock(), PageBlockSamples.CtaInvalid()],
        };

        var files = new Fixtures.FakeFormFileCollection(new Dictionary<string, byte[]> { ["file:0:image"] = TestImages.SmallPng() });

        string? uploadedKeyBeforeCancel = null;
        cancelAfterFirstUploadStorage.OnFirstUploadCompleted = key => uploadedKeyBeforeCancel = key;

        await Assert.ThrowsAsync<AdminPageValidationException>(
            () => repository.CreateAsync(adminScope, pageId, request, files, adminScope.Identity.AdminUserId, cts.Token));

        Assert.NotNull(uploadedKeyBeforeCancel);
        Assert.True(cts.IsCancellationRequested); // 前提：呼叫結束時外層 token 確實已經是取消狀態
        await AssertAllObjectsExistAsync(uploadedKeyBeforeCancel!, shouldExist: false); // 但補償刪除仍然成功
    }

    private async Task<int> CountBlobsUnderPrefixAsync(string prefix)
    {
        await fixture.InspectorContainer.CreateIfNotExistsAsync(cancellationToken: CancellationToken.None);
        var count = 0;
        await foreach (var _ in fixture.InspectorContainer.GetBlobsAsync(
            Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    /// <summary>裝飾 <see cref="IImageStorageService"/>：第一次呼叫 <see cref="UploadAsync"/> 真的
    /// 委派給底下的實作完成上傳後，立刻取消 <paramref name="cts"/>，模擬「圖片剛上傳完成，
    /// 使用者就在這個瞬間斷線」。<see cref="DeleteAsync"/> 原封委派，用來驗證補償刪除呼叫進來時
    /// 用的是不是 <see cref="CancellationToken.None"/>（若沿用已取消的 token，
    /// <see cref="BlobImageStorageService.DeleteAsync"/> 內部會在第一步就丟
    /// <see cref="OperationCanceledException"/>，fail-open 邏輯會吞掉它、不會真的刪除物件——
    /// 這支測試的斷言就是抓這個「物件到底有沒有真的消失」的最終結果，不需要另外攔截 token 本身）。</summary>
    private sealed class CancelAfterFirstUploadImageStorageService(IImageStorageService inner, CancellationTokenSource cts) : IImageStorageService
    {
        private bool _firstCallDone;
        public Action<string>? OnFirstUploadCompleted { get; set; }

        public async Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        {
            var result = await inner.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);

            if (!_firstCallDone)
            {
                _firstCallDone = true;
                OnFirstUploadCompleted?.Invoke(result.Key);
                await cts.CancelAsync();
            }

            return result;
        }

        public Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken) => inner.DeleteAsync(mainObjectKey, cancellationToken);
    }
}
