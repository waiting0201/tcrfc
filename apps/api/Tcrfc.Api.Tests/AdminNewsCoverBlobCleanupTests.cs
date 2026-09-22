using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Images;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 驗證圖片上傳共用元件跟後台新聞（B2）實際接線之後的行為（S0-8 任務指示：「用一個真實模組
/// 示範接線」）：換圖成功才刪舊物件、主檔與全部衍生檔一起刪、刪除資料列一併刪除其圖片物件、
/// 移除封面圖片（<c>RemoveCover</c>）也會刪掉物件。真的打 HTTP 管線、真的啟動
/// <c>azurite-blob</c>，不 mock（見 <see cref="AdminWriteAzuriteEnabledApiFixture"/>）。
///
/// 🔴🔴🔴 S0-8 修正（2026-09-22）：改用單一 <c>multipart/form-data</c> 請求（建立／更新時把
/// 封面圖片跟其餘欄位一起送出），取代舊版「先呼叫獨立上傳端點拿 key、再把 key 塞進純 JSON 請求」
/// 的兩段式做法。失敗時的孤兒物件回滾（補償交易）測試見 <see cref="AdminNewsCoverUploadTests"/>。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminNewsCoverBlobCleanupTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private const string CategoryCode = "club";

    private static string UniqueSlug() => $"admin-write-blob-cleanup-{Guid.NewGuid():N}";

    private async Task<AdminArticleDetailDto> CreateDraftWithCoverAsync(HttpClient client, string slug, byte[] coverBytes)
    {
        var request = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "圖片清理驗證用文章" } },
        };
        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request, coverBytes));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.NotNull(created!.CoverKey); // 🔴 建立時就已經夾檔案，回應的 CoverKey 一定非空
        return created;
    }

    private async Task AssertAllObjectsExistAsync(string mainKey, bool shouldExist)
    {
        foreach (var key in ImageObjectKey.AllObjectKeys(mainKey))
        {
            var exists = await fixture.InspectorContainer.GetBlobClient(key).ExistsAsync();
            Assert.Equal(shouldExist, exists.Value);
        }
    }

    [Fact]
    public async Task 建立文章時附封面圖片_五個物件都真的寫進儲存體_物件鍵含俱樂部與文章id()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.JpegWithExifAndGps());

        try
        {
            Assert.StartsWith($"tcrfc/articles/{created.Id}/cover/", created.CoverKey);
            Assert.EndsWith(".webp", created.CoverKey);
            await AssertAllObjectsExistAsync(created.CoverKey!, shouldExist: true);
        }
        finally
        {
            await client.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        }
    }

    [Fact]
    public async Task 換圖成功後_舊的主檔與全部衍生檔被刪除_新的完整保留()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());
        var oldKey = created.CoverKey!;
        await AssertAllObjectsExistAsync(oldKey, shouldExist: true);

        // 換一張新圖：PUT 夾新檔案，伺服器端先上傳成功才會更新資料列（規劃書 §4.0）。
        var putBody = new UpdateArticleRequest
        {
            Slug = created.Slug,
            CategoryCode = CategoryCode,
            IsFeatured = false,
            Content = new AdminArticleContentInput { Zh = created.Zh! },
            ExpectedUpdatedAt = created.UpdatedAt,
        };
        var putResponse = await client.PutAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(putBody, TestImages.SmallWebp()));
        putResponse.EnsureSuccessStatusCode();
        var afterPut = await putResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(afterPut);
        var newKey = afterPut!.CoverKey!;
        Assert.NotEqual(oldKey, newKey);

        // 🔴 核心斷言：舊圖的五個物件全部消失，新圖的五個物件仍然完整。
        await AssertAllObjectsExistAsync(oldKey, shouldExist: false);
        await AssertAllObjectsExistAsync(newKey, shouldExist: true);

        // 收尾清乾淨。
        await client.DeleteAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(afterPut.UpdatedAt.ToString("o"))}");
    }

    [Fact]
    public async Task 更新時勾選移除封面圖片且不夾檔案_封面清空_舊物件被刪除()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());
        var oldKey = created.CoverKey!;
        await AssertAllObjectsExistAsync(oldKey, shouldExist: true);

        var putBody = new UpdateArticleRequest
        {
            Slug = created.Slug,
            CategoryCode = CategoryCode,
            IsFeatured = false,
            Content = new AdminArticleContentInput { Zh = created.Zh! },
            ExpectedUpdatedAt = created.UpdatedAt,
            RemoveCover = true,
        };
        var putResponse = await client.PutAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(putBody)); // 沒有夾檔案
        putResponse.EnsureSuccessStatusCode();
        var afterPut = await putResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(afterPut);
        Assert.Null(afterPut!.CoverKey);

        await AssertAllObjectsExistAsync(oldKey, shouldExist: false);

        await client.DeleteAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(afterPut.UpdatedAt.ToString("o"))}");
    }

    [Fact]
    public async Task 更新時沒有夾檔案也沒有勾選移除_封面維持不變_物件不受影響()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());
        var coverKey = created.CoverKey!;

        var putBody = new UpdateArticleRequest
        {
            Slug = created.Slug,
            CategoryCode = CategoryCode,
            IsFeatured = false,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "只改標題，不動封面" } },
            ExpectedUpdatedAt = created.UpdatedAt,
        };
        var putResponse = await client.PutAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(putBody));
        putResponse.EnsureSuccessStatusCode();
        var afterPut = await putResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(afterPut);
        Assert.Equal(coverKey, afterPut!.CoverKey); // Keep 語意：完全沒變

        await AssertAllObjectsExistAsync(coverKey, shouldExist: true); // 舊物件沒有被誤刪

        await client.DeleteAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(afterPut.UpdatedAt.ToString("o"))}");
    }

    [Fact]
    public async Task 刪除文章後_圖片物件一併被刪除()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());
        var coverKey = created.CoverKey!;
        await AssertAllObjectsExistAsync(coverKey, shouldExist: true);

        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        deleteResponse.EnsureSuccessStatusCode();

        await AssertAllObjectsExistAsync(coverKey, shouldExist: false);
    }
}
