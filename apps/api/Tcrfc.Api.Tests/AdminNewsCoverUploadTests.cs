using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 S0-8 修正（2026-09-22）核心驗收：建立／更新文章的封面圖片改成單一
/// <c>multipart/form-data</c> 請求（規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」），取代
/// 「先呼叫獨立上傳端點拿 key、使用者取消表單就留下孤兒物件」的兩段式做法。這支測試檔專門釘住
/// 「取消／失敗不留下任何檔案」這個驗收點的可自動化等價形式：
///
/// 1. 圖片驗證失敗（空檔案／格式不支援／超過大小上限）發生在**碰到資料庫之前**，本來就不會有
///    任何物件寫進儲存體，也不會建立任何資料列。
/// 2. 🔴 真正需要驗證的是「圖片已經上傳成功，但資料列寫入失敗」這個交錯情境——物件儲存跟 SQL
///    是兩個系統，做不到真正的跨系統 atomic transaction，伺服器端用補償交易（失敗時刪掉剛剛
///    上傳的物件）模擬「兩者要嘛都成功、要嘛都不留痕跡」，這正是「取消表單不留下任何檔案」
///    在後端可被自動化驗證的等價說法——前端「使用者按下取消」的唯一後果就是**從來沒有發生過
///    這次請求**，天然不會留下任何檔案；真正可能留下孤兒物件的唯一情境是「請求送出了，圖片
///    上傳成功，但後面的驗證／並行檢查失敗」，這支測試專門釘住這個情境。
///
/// 真的打 HTTP 管線、真的啟動 <c>azurite-blob</c>，不 mock（見 <see cref="AdminWriteAzuriteEnabledApiFixture"/>）。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminNewsCoverUploadTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private const string CategoryCode = "club";
    private const string BlobPrefix = "tcrfc/articles/";

    private static string UniqueSlug() => $"admin-write-cover-upload-{Guid.NewGuid():N}";

    private static CreateArticleRequest NewDraftRequest(string slug, string title = "封面圖片驗證用文章")
        => new()
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = title } },
        };

    private async Task<AdminArticleDetailDto> CreateDraftWithCoverAsync(HttpClient client, string slug, byte[] coverBytes)
    {
        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(slug), coverBytes));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        return created!;
    }

    private async Task<int> CountBlobsUnderPrefixAsync()
    {
        var count = 0;
        await foreach (var _ in fixture.InspectorContainer.GetBlobsAsync(
            Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, BlobPrefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    // ───────────────────────────── 圖片驗證失敗：碰不到資料庫，本來就不會留下任何東西 ─────────────────────────────

    [Fact]
    public async Task 建立文章_夾假副檔名文字檔_回400_不建立文章_不留下任何物件()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var before = await CountBlobsUnderPrefixAsync();

        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(UniqueSlug()), TestImages.FakeImageBytes()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("格式不支援", text);
        Assert.DoesNotContain("Exception", text); // ⛔ 不外洩內部例外訊息

        Assert.Equal(before, await CountBlobsUnderPrefixAsync()); // 沒有任何物件被寫進去
    }

    [Fact]
    public async Task 建立文章_夾空檔案_回400_不留下任何物件()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var before = await CountBlobsUnderPrefixAsync();

        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(UniqueSlug()), []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountBlobsUnderPrefixAsync());
    }

    [Fact]
    public async Task 建立文章_夾超過10MB的檔案_回400_不留下任何物件()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var before = await CountBlobsUnderPrefixAsync();

        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(UniqueSlug()), TestImages.OversizedBytes()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("圖片檔案太大", text);
        Assert.Equal(before, await CountBlobsUnderPrefixAsync());
    }

    [Fact]
    public async Task 不存在的俱樂部代碼_夾檔案_回404_不嘗試上傳()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var before = await CountBlobsUnderPrefixAsync();

        var response = await client.PostAsync(
            "/api/v1/admin/does-not-exist/news", AdminArticleMultipart.Build(NewDraftRequest(UniqueSlug()), TestImages.SmallPng()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await CountBlobsUnderPrefixAsync()); // club 先解析失敗，根本不會碰上傳
    }

    // ───────────────────────────── 🔴 核心：圖片已上傳成功，但資料列寫入失敗 → 回滾，不留孤兒物件 ─────────────────────────────

    [Fact]
    public async Task 建立文章_網址名稱重複但夾了正常圖片_圖片已上傳成功但建立失敗_回滾不留孤兒物件()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var slug = UniqueSlug();
        var first = await CreateDraftWithCoverAsync(client, slug, TestImages.SmallPng()); // 先佔用這個 slug，不夾圖片幹擾計數更單純可讀

        try
        {
            var before = await CountBlobsUnderPrefixAsync();

            // 第二次用同一個 slug 建立，夾一張完全正常的圖片——伺服器端的順序是「先上傳成功，
            // 再呼叫 repository.CreateAsync」，slug 衝突要到 repository 內部才會被發現，
            // 這正是「上傳成功、資料列寫入失敗」的真實情境。
            var response = await client.PostAsync(
                "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(slug), TestImages.SmallWebp()));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            // 🔴 核心斷言：儲存體裡的物件數量沒有增加——剛剛上傳成功的那 5 個物件被補償交易清掉了，
            // 沒有留下任何「沒有資料列引用」的孤兒物件。
            Assert.Equal(before, await CountBlobsUnderPrefixAsync());
        }
        finally
        {
            await client.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{first.Id}?expectedUpdatedAt={Uri.EscapeDataString(first.UpdatedAt.ToString("o"))}");
        }
    }

    [Fact]
    public async Task 更新文章_並行衝突但夾了正常圖片_圖片已上傳成功但更新失敗_回滾不留孤兒物件_舊封面圖片不受影響()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());

        try
        {
            var before = await CountBlobsUnderPrefixAsync(); // 此時應該只有「舊封面」的 5 個物件

            var staleUpdate = new UpdateArticleRequest
            {
                Slug = created.Slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = created.Zh! },
                // 🔴 刻意用一個過期的 ExpectedUpdatedAt（往前推一小時，保證跟資料庫目前的值對不起來），
                // 模擬「圖片上傳成功之後，樂觀並行檢查才發現這是一次過期的請求」。
                ExpectedUpdatedAt = created.UpdatedAt.AddHours(-1),
            };

            var response = await client.PutAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(staleUpdate, TestImages.SmallWebp()));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            // 🔴 核心斷言：沒有新增任何物件（剛剛上傳成功的新封面被回滾清掉了）。
            Assert.Equal(before, await CountBlobsUnderPrefixAsync());

            // 舊封面完全沒被動到——更新根本沒有真的寫進資料庫，不該刪舊物件。
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal(created.CoverKey, current!.CoverKey);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await client.DeleteAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(probe.UpdatedAt.ToString("o"))}");
            }
        }
    }

    [Fact]
    public async Task 更新文章_同時夾檔案又勾選移除封面_回400_不嘗試上傳()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftWithCoverAsync(client, UniqueSlug(), TestImages.SmallPng());

        try
        {
            var before = await CountBlobsUnderPrefixAsync();

            var contradictoryUpdate = new UpdateArticleRequest
            {
                Slug = created.Slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = created.Zh! },
                ExpectedUpdatedAt = created.UpdatedAt,
                RemoveCover = true,
            };

            var response = await client.PutAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(contradictoryUpdate, TestImages.SmallWebp()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.Contains("不能同時", text);

            // 🔴 這個檢查發生在真的呼叫上傳之前（見 AdminArticlesEndpoints），連一次上傳嘗試都不會有。
            Assert.Equal(before, await CountBlobsUnderPrefixAsync());
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await client.DeleteAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(probe.UpdatedAt.ToString("o"))}");
            }
        }
    }
}
