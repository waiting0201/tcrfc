using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S0-7h（2026-10-02）：<c>articles</c> 封面圖片欄位組補齊（規劃書 v3.5 §4.0「圖片欄位是一組：物件鍵、寬、高、雙語 Alt」）。
/// 驗證：上傳流程算出的主檔寬高寫回 <c>cover_width</c>／<c>cover_height</c>；換圖更新、不動封面、移除封面三態；
/// 雙語 <c>cover_alt</c> 可獨立更新（不換圖也能改 Alt）；公開 API 帶出寬高與 Alt；封面當 OG 圖片回退時改用封面 Alt。
/// 真的啟動 Azurite（同 <see cref="AdminNewsCoverUploadTests"/>）。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminNewsCoverFieldGroupTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static string UniqueSlug() => $"cover-fields-{Guid.NewGuid():N}";

    private async Task<HttpClient> EditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static CreateArticleRequest NewRequest(string slug, string? altZh, string? altEn)
        => new()
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = "封面欄位組驗證", CoverAlt = altZh },
                En = new AdminArticleLocaleContent { Title = "Cover field group", CoverAlt = altEn },
            },
        };

    private static UpdateArticleRequest UpdateOf(AdminArticleDetailDto current, string? altZh, string? altEn, bool removeCover = false)
        => new()
        {
            Slug = current.Slug,
            CategoryCode = current.CategoryCode,
            ExpectedUpdatedAt = current.UpdatedAt,
            RemoveCover = removeCover,
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = "封面欄位組驗證", CoverAlt = altZh },
                En = new AdminArticleLocaleContent { Title = "Cover field group", CoverAlt = altEn },
            },
        };

    private static async Task<AdminArticleDetailDto> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
    }

    private static bool NullOrMissing(JsonElement element, string name)
        => !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    private static async Task DeleteAsync(HttpClient client, Guid id)
    {
        var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{id}", TestJson.Options);
        if (probe is not null)
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/news/{id}?expectedUpdatedAt={Uri.EscapeDataString(probe.UpdatedAt.ToString("o"))}");
        }
    }

    [AzuriteFact]
    public async Task 建立_上傳封面_寬高寫回資料列_雙語Alt一併保存()
    {
        using var client = await EditorClientAsync();
        var created = await ReadAsync(await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewRequest(UniqueSlug(), "球員合照", "Team photo"), TestImages.SmallPng())));
        try
        {
            Assert.NotNull(created.CoverKey);
            Assert.Equal(500, created.CoverWidth);   // SmallPng 為 500×400，小於 2560 長邊上限，主檔不縮小
            Assert.Equal(400, created.CoverHeight);
            Assert.Equal("球員合照", created.Zh.CoverAlt);
            Assert.Equal("Team photo", created.En!.CoverAlt);

            var reloaded = (await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options))!;
            Assert.Equal(500, reloaded.CoverWidth);
            Assert.Equal(400, reloaded.CoverHeight);
        }
        finally
        {
            await DeleteAsync(client, created.Id);
        }
    }

    [AzuriteFact]
    public async Task 大圖上傳_寬高存的是縮小後主檔尺寸_不是原始尺寸()
    {
        using var client = await EditorClientAsync();
        var created = await ReadAsync(await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewRequest(UniqueSlug(), null, null), TestImages.JpegWithExifAndGps(), "cover.jpg", "image/jpeg")));
        try
        {
            // 來源 3000×2000、EXIF 方向 6（轉正後 2000×3000），長邊超過 2560 → 縮成 1707×2560
            Assert.NotNull(created.CoverWidth);
            Assert.NotNull(created.CoverHeight);
            Assert.True(Math.Max(created.CoverWidth!.Value, created.CoverHeight!.Value) <= 2560);
            Assert.True(created.CoverHeight > created.CoverWidth, "EXIF 方向 6 應轉正為直式。");
        }
        finally
        {
            await DeleteAsync(client, created.Id);
        }
    }

    [AzuriteFact]
    public async Task 更新_不換圖只改Alt_寬高維持_換圖則更新寬高_移除封面則清空寬高但Alt仍可保留()
    {
        using var client = await EditorClientAsync();
        var created = await ReadAsync(await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewRequest(UniqueSlug(), "舊 Alt", "Old alt"), TestImages.SmallPng())));
        try
        {
            // 1. 不夾檔案、只改 Alt：封面與寬高維持不變
            var altOnly = await ReadAsync(await client.PutAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(UpdateOf(created, "新 Alt", "New alt"))));
            Assert.Equal(created.CoverKey, altOnly.CoverKey);
            Assert.Equal(500, altOnly.CoverWidth);
            Assert.Equal(400, altOnly.CoverHeight);
            Assert.Equal("新 Alt", altOnly.Zh.CoverAlt);
            Assert.Equal("New alt", altOnly.En!.CoverAlt);

            // 2. 換成 200×200 的 WebP：寬高跟著換
            var replaced = await ReadAsync(await client.PutAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(UpdateOf(altOnly, "新 Alt", "New alt"), TestImages.SmallWebp(), "cover.webp", "image/webp")));
            Assert.NotEqual(created.CoverKey, replaced.CoverKey);
            Assert.Equal(200, replaced.CoverWidth);
            Assert.Equal(200, replaced.CoverHeight);

            // 3. 移除封面：物件鍵與寬高一起清空
            var removed = await ReadAsync(await client.PutAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(UpdateOf(replaced, "新 Alt", "New alt", removeCover: true))));
            Assert.Null(removed.CoverKey);
            Assert.Null(removed.CoverWidth);
            Assert.Null(removed.CoverHeight);
        }
        finally
        {
            await DeleteAsync(client, created.Id);
        }
    }

    [AzuriteFact]
    public async Task 公開API_列表與詳情帶出封面寬高與依語系回退的Alt_封面當OG回退時用封面Alt()
    {
        using var client = await EditorClientAsync();
        var slug = UniqueSlug();
        var created = await ReadAsync(await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewRequest(slug, "中文替代文字", null), TestImages.SmallPng())));
        try
        {
            var publish = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt }, TestJson.WriteOptions);
            publish.EnsureSuccessStatusCode();

            using var anonymous = fixture.CreateClient();

            var zh = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/news/{slug}?lang=zh");
            Assert.Equal(500, zh.GetProperty("coverWidth").GetInt32());
            Assert.Equal(400, zh.GetProperty("coverHeight").GetInt32());
            Assert.Equal("中文替代文字", zh.GetProperty("coverAlt").GetString());
            // 沒有專屬 OG 圖、全站預設圖也沒有時回退到封面，OG alt 改用封面 Alt（S0-7h）
            if (zh.GetProperty("ogImageUrl").GetString() == zh.GetProperty("coverUrl").GetString())
            {
                Assert.Equal("中文替代文字", zh.GetProperty("ogImageAlt").GetString());
                Assert.Equal(500, zh.GetProperty("ogImageWidth").GetInt32());
            }

            // 英文沒填 Alt → 依慣例回退到中文
            var en = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/news/{slug}?lang=en");
            Assert.Equal("中文替代文字", en.GetProperty("coverAlt").GetString());

            var list = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/news?pageSize=100");
            var item = list.GetProperty("items").EnumerateArray().FirstOrDefault(i => i.GetProperty("slug").GetString() == slug);
            if (item.ValueKind == JsonValueKind.Object) // 列表只含最新 100 筆；找得到才驗，找不到不算失敗（詳情已驗過）
            {
                Assert.Equal(500, item.GetProperty("coverWidth").GetInt32());
                Assert.Equal("中文替代文字", item.GetProperty("coverAlt").GetString());
            }
        }
        finally
        {
            await DeleteAsync(client, created.Id);
        }
    }

    [AzuriteFact]
    public async Task 沒有封面的文章_公開API的封面寬高與Alt一律為null()
    {
        using var client = await EditorClientAsync();
        var slug = UniqueSlug();
        var created = await ReadAsync(await client.PostAsync(
            "/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewRequest(slug, "沒有圖卻填了 Alt", null))));
        try
        {
            (await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt }, TestJson.WriteOptions)).EnsureSuccessStatusCode();

            using var anonymous = fixture.CreateClient();
            var detail = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/news/{slug}?lang=zh");
            Assert.True(NullOrMissing(detail, "coverUrl"));
            Assert.True(NullOrMissing(detail, "coverWidth"));
            Assert.True(NullOrMissing(detail, "coverAlt")); // 沒有圖就不輸出孤立的 Alt
        }
        finally
        {
            await DeleteAsync(client, created.Id);
        }
    }
}
