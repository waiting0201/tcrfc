using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Pages;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-12 驗收退回後補做：OG 圖片覆寫真的上傳、真的解析出網址、真的套用優先序。真的啟動
/// <c>azurite-blob</c>（見 <see cref="AdminWriteAzuriteEnabledApiFixture"/>），不 mock 儲存體——
/// 跟 <see cref="AdminNewsCoverUploadTests"/>／<see cref="AdminPagesImageTests"/> 同一套紀律。
/// 純文字的 SEO 設定／轉址／孤立頁面偵測測試見 <see cref="AdminSeoTests"/>（不需要真實儲存體，
/// 用比較快的 <see cref="AdminWriteApiFixture"/>）。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminSeoImageTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        return client;
    }

    private async Task<HttpClient> CreateContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static MultipartFormDataContent BuildSettingsMultipart(UpdateSeoSettingsRequest request, byte[]? ogImageBytes = null)
    {
        var form = new MultipartFormDataContent();
        var json = JsonSerializer.Serialize(request, TestJson.WriteOptions);
        var payloadContent = new StringContent(json, Encoding.UTF8);
        payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(payloadContent, "payload");

        if (ogImageBytes is not null)
        {
            var fileContent = new ByteArrayContent(ogImageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fileContent, "ogImage", "og.png");
        }

        return form;
    }

    // ───────────────────────────── 全站預設 OG 圖片（Club.OgImageKey） ─────────────────────────────

    /// <summary>🔴 跟 <see cref="AdminSeoTests"/> 同一個理由：這個測試會真的改動
    /// <c>tcrfc_club_dev</c> 的 <c>clubs.og_image_key</c>（本機開發環境唯一一份，會反映到公開
    /// 端點），測試前後都要還原，不能留下痕跡。</summary>
    [Fact]
    public async Task 全站預設OgImage_上傳後解析出網址_移除後清空()
    {
        using var client = await CreateSuperAdminClientAsync();
        var original = await CaptureClubOgImageAsync();

        try
        {
            var uploadResponse = await client.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BuildSettingsMultipart(
                    new UpdateSeoSettingsRequest { TitleTemplateZh = "{title}", DefaultDescriptionZh = "測試描述" },
                    TestImages.SmallPng()));
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
            var uploaded = await uploadResponse.Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options);
            Assert.NotNull(uploaded!.OgImageUrl);
            Assert.True(uploaded.OgImageWidth > 0);
            Assert.True(uploaded.OgImageHeight > 0);

            // 公開端點應該看得到同一個網址（沒有接快取的測試環境，立即反映）。
            using var publicClient = fixture.CreateClient();
            var publicSettings = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/settings"))
                .Content.ReadFromJsonAsync<PublicSeoSettingsDto>(TestJson.Options);
            Assert.Equal(uploaded.OgImageUrl, publicSettings!.OgImageUrl);

            // 移除：跟上傳新檔互斥，這次不夾檔案、勾選 RemoveOgImage。
            var removeResponse = await client.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BuildSettingsMultipart(new UpdateSeoSettingsRequest
                {
                    TitleTemplateZh = "{title}",
                    DefaultDescriptionZh = "測試描述",
                    RemoveOgImage = true,
                }));
            Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
            var removed = await removeResponse.Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options);
            Assert.Null(removed!.OgImageUrl);
        }
        finally
        {
            await RestoreClubOgImageAsync(original);
            // settings 文字欄位（title_template／default_description）也是這個測試寫入的，
            // 一併清乾淨，理由與 AdminSeoTests 的還原邏輯相同。
            await DeleteSeoTextSettingsAsync();
        }
    }

    // ───────────────────────────── 文章 OG 圖片（含優先序） ─────────────────────────────

    [Fact]
    public async Task 文章OgImage_專屬圖片優先於全站預設()
    {
        using var editorClient = await CreateContentEditorClientAsync();
        using var superAdmin = await CreateSuperAdminClientAsync();
        var slug = $"s1-12-og-{Guid.NewGuid():N}";

        var originalClubOg = await CaptureClubOgImageAsync();
        try
        {
            // 先設定全站預設 OG 圖片，確認「文章自己的圖片」蓋得過它。
            var settingsResponse = await superAdmin.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BuildSettingsMultipart(
                    new UpdateSeoSettingsRequest { TitleTemplateZh = "{title}", DefaultDescriptionZh = "測試描述" },
                    TestImages.SmallPng()));
            settingsResponse.EnsureSuccessStatusCode();

            var createRequest = new CreateArticleRequest
            {
                Slug = slug,
                CategoryCode = "club",
                Content = new AdminArticleContentInput
                {
                    Zh = new AdminArticleLocaleContent { Title = $"OG 圖片測試 {slug}", OgImageAlt = "測試替代文字" },
                },
            };

            var form = AdminArticleMultipart.Build(createRequest);
            var ogImageContent = new ByteArrayContent(TestImages.SmallPng());
            ogImageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(ogImageContent, "ogImage", "og.png");

            var createResponse = await editorClient.PostAsync("/api/v1/admin/tcrfc/news", form);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(created!.OgImageUrl);
            Assert.Equal("測試替代文字", created.Zh.OgImageAlt);

            // 🔴（docs/18 E-62）並行權杖要一路追蹤到最新值：DELETE 端點要求 expectedUpdatedAt
            // 對得起來，發布會改變 updated_at，用建立時的舊值刪除會 409 且被靜默吞掉，在
            // tcrfc_club_dev 留下孤兒測試文章——這是本輪實測抓到的既有寫法錯誤，本檔與
            // AdminSeoTests.cs 的類似清理呼叫已一併修正並都補上 Assert 確保清理真的成功。
            var currentUpdatedAt = created.UpdatedAt;
            try
            {
                var publishResponse = await editorClient.PostAsJsonAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                    new PublishArticleRequest { ExpectedUpdatedAt = currentUpdatedAt },
                    TestJson.WriteOptions);
                publishResponse.EnsureSuccessStatusCode();
                currentUpdatedAt = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!.UpdatedAt;

                using var publicClient = fixture.CreateClient();
                var publicArticle = await (await publicClient.GetAsync($"/api/v1/tcrfc/news/{slug}"))
                    .Content.ReadFromJsonAsync<ArticleDetailDto>(TestJson.Options);

                // 文章自己的 OG 圖片存在 → 優先序第一層生效，不是全站預設圖。
                Assert.Equal(created.OgImageUrl, publicArticle!.OgImageUrl);
                Assert.Equal("測試替代文字", publicArticle.OgImageAlt);
            }
            finally
            {
                var deleteResponse = await editorClient.DeleteAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(currentUpdatedAt.ToString("o"))}");
                Assert.True(deleteResponse.IsSuccessStatusCode, $"清理測試文章失敗：{deleteResponse.StatusCode}");
            }
        }
        finally
        {
            await RestoreClubOgImageAsync(originalClubOg);
            await DeleteSeoTextSettingsAsync();
        }
    }

    [Fact]
    public async Task 文章OgImage_沒有專屬圖片時回退全站預設()
    {
        using var editorClient = await CreateContentEditorClientAsync();
        using var superAdmin = await CreateSuperAdminClientAsync();
        var slug = $"s1-12-og-fallback-{Guid.NewGuid():N}";

        var originalClubOg = await CaptureClubOgImageAsync();
        try
        {
            var settingsResponse = await superAdmin.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BuildSettingsMultipart(
                    new UpdateSeoSettingsRequest { TitleTemplateZh = "{title}", DefaultDescriptionZh = "測試描述" },
                    TestImages.SmallPng()));
            settingsResponse.EnsureSuccessStatusCode();
            var clubOgSettings = await settingsResponse.Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options);

            var createRequest = new CreateArticleRequest
            {
                Slug = slug,
                CategoryCode = "club",
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"OG 回退測試 {slug}" } },
            };
            var createResponse = await editorClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
            createResponse.EnsureSuccessStatusCode();
            var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Null(created.OgImageUrl); // 這篇文章自己沒有設定 OG 圖片。

            // 🔴（docs/18 E-62）並行權杖要一路追蹤到最新值，理由見上一個測試同樣的註解。
            var currentUpdatedAt = created.UpdatedAt;
            try
            {
                var publishResponse = await editorClient.PostAsJsonAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                    new PublishArticleRequest { ExpectedUpdatedAt = currentUpdatedAt },
                    TestJson.WriteOptions);
                publishResponse.EnsureSuccessStatusCode();
                currentUpdatedAt = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!.UpdatedAt;

                using var publicClient = fixture.CreateClient();
                var publicArticle = await (await publicClient.GetAsync($"/api/v1/tcrfc/news/{slug}"))
                    .Content.ReadFromJsonAsync<ArticleDetailDto>(TestJson.Options);

                Assert.Equal(clubOgSettings!.OgImageUrl, publicArticle!.OgImageUrl);
                Assert.Null(publicArticle.OgImageAlt); // 回退到全站預設圖時不輸出 alt，見 ResolveOgImageAsync 判斷。
            }
            finally
            {
                var deleteResponse = await editorClient.DeleteAsync(
                    $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(currentUpdatedAt.ToString("o"))}");
                Assert.True(deleteResponse.IsSuccessStatusCode, $"清理測試文章失敗：{deleteResponse.StatusCode}");
            }
        }
        finally
        {
            await RestoreClubOgImageAsync(originalClubOg);
            await DeleteSeoTextSettingsAsync();
        }
    }

    // ───────────────────────────── 頁面 OG 圖片（僅後台，無公開前台路由可驗，見任務回報） ─────────────────────────────

    [Fact]
    public async Task 頁面OgImage_後台上傳後解析出網址()
    {
        using var editorClient = await CreateContentEditorClientAsync();
        var slug = $"s1-12-page-og-{Guid.NewGuid():N}";

        var createRequest = new CreatePageRequest
        {
            Slug = slug,
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "OG 圖片測試頁", OgImageAlt = "頁面替代文字" } },
            Blocks = [],
        };

        var form = new MultipartFormDataContent();
        var json = JsonSerializer.Serialize(createRequest, TestJson.WriteOptions);
        var payloadContent = new StringContent(json, Encoding.UTF8);
        payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(payloadContent, "payload");
        var ogImageContent = new ByteArrayContent(TestImages.SmallPng());
        ogImageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(ogImageContent, "ogImage", "og.png");

        var createResponse = await editorClient.PostAsync("/api/v1/admin/tcrfc/pages", form);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);

        try
        {
            Assert.NotNull(created!.OgImageUrl);
            Assert.Equal("頁面替代文字", created.Zh.OgImageAlt);
        }
        finally
        {
            await editorClient.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created!.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
        }
    }

    // ───────────────────────────── 直接 SQL 快照／還原（clubs.og_image_key 等） ─────────────────────────────

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private sealed record ClubOgImageSnapshot(string? Key, int? Width, int? Height);

    private static async Task<ClubOgImageSnapshot> CaptureClubOgImageAsync()
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT og_image_key, og_image_width, og_image_height FROM clubs WHERE code = 'tcrfc'";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new ClubOgImageSnapshot(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2));
    }

    private static async Task RestoreClubOgImageAsync(ClubOgImageSnapshot original)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE clubs SET og_image_key = @Key, og_image_width = @Width, og_image_height = @Height WHERE code = 'tcrfc'";
        command.Parameters.AddWithValue("@Key", (object?)original.Key ?? DBNull.Value);
        command.Parameters.AddWithValue("@Width", (object?)original.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("@Height", (object?)original.Height ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 🔴 <c>AdminSeoSettingsRepository.UpdateAsync</c> 每次 PUT 會把全部七個
    /// <c>seo.*</c>／<c>tracking.*</c> 鍵都建一列（即使請求沒有帶值，也會建一列
    /// <c>setting_value = NULL</c> 的列）——不是只建這個測試明確傳了值的
    /// <c>seo.title_template</c>／<c>seo.default_description</c> 兩個鍵。第一版這裡只刪那兩個鍵，
    /// 實測後發現另外五個鍵（<c>seo.robots_custom_rules</c>、四個 <c>tracking.*</c>）會留下空值列
    /// 殘留在 <c>tcrfc_club_dev</c>，已修正為整組刪除，跟 <see cref="AdminSeoTests"/> 的還原邏輯
    /// 一致範圍。本測試檔執行前這批鍵理論上都不存在，直接刪除即是還原。
    /// </summary>
    private static async Task DeleteSeoTextSettingsAsync()
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE si FROM settings_i18n si
            JOIN settings s ON s.id = si.setting_id
            JOIN clubs c ON c.id = s.club_id
            WHERE c.code = 'tcrfc' AND (s.setting_key LIKE 'seo.%' OR s.setting_key LIKE 'tracking.%');

            DELETE s FROM settings s
            JOIN clubs c ON c.id = s.club_id
            WHERE c.code = 'tcrfc' AND (s.setting_key LIKE 'seo.%' OR s.setting_key LIKE 'tracking.%');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
