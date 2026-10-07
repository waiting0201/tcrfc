using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Images;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 後台讀取 DTO 的圖片網址（2026-10-07）：後台編輯頁的上傳元件要預覽已上傳的圖，API 不能只回物件鍵。
/// 契約：每個 <c>{X}Key</c> 旁一律有 <c>{X}Url</c>（主檔）與 <c>{X}ThumbUrl</c>（<see cref="ImageObjectKey.ForThumbnail"/>），
/// 沒有鍵時兩者為 <c>null</c>。這裡用確定性的假解析器（不依賴 Azurite），直接把鍵寫進資料列再從 HTTP 讀回，
/// 代表模組取新聞封面（原本完全沒有網址）與球員照片。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminImageUrlInReadDtosTests(AdminWriteApiFixture fixture)
{
    private sealed class FakeResolver : IImagePublicUrlResolver
    {
        public string? Resolve(string? objectKey) => string.IsNullOrEmpty(objectKey) ? null : $"https://img.test/{objectKey}";
    }

    private HttpClient CreateClient(out Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        factory = fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IImagePublicUrlResolver>();
            s.AddSingleton<IImagePublicUrlResolver>(new FakeResolver());
        }));
        return factory.CreateClient();
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task 新聞後台_有封面時列表與詳情都帶封面網址與縮圖網址_無封面時為null()
    {
        using var client = CreateClient(out var factory);
        using var _ = factory;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var slug = $"admin-image-url-{Guid.NewGuid():N}";
        Guid? id = null;
        try
        {
            var create = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(new CreateArticleRequest
            {
                Slug = slug,
                CategoryCode = "club",
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "封面網址測試" } },
            }));
            create.EnsureSuccessStatusCode();
            var created = (await create.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            id = created.Id;

            // 無封面：鍵與兩個網址都是 null。
            Assert.Null(created.CoverKey);
            Assert.Null(created.CoverUrl);
            Assert.Null(created.CoverThumbUrl);

            const string key = "tcrfc/articles/test-cover.webp";
            await ExecuteAsync("UPDATE articles SET cover_key = @K WHERE id = @Id;", ("@K", key), ("@Id", id));

            var detail = (await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{id}", TestJson.Options))!;
            Assert.Equal(key, detail.CoverKey);
            Assert.Equal($"https://img.test/{key}", detail.CoverUrl);
            Assert.Equal($"https://img.test/{ImageObjectKey.ForThumbnail(key)}", detail.CoverThumbUrl);

            var list = (await client.GetFromJsonAsync<PagedResult<AdminArticleListItemDto>>(
                $"/api/v1/admin/tcrfc/news?keyword={Uri.EscapeDataString(slug)}", TestJson.Options))!;
            var item = Assert.Single(list.Items);
            Assert.Equal($"https://img.test/{key}", item.CoverUrl);
            Assert.Equal($"https://img.test/{ImageObjectKey.ForThumbnail(key)}", item.CoverThumbUrl);
        }
        finally
        {
            if (id is Guid articleId)
            {
                await ExecuteAsync("DELETE FROM articles_i18n WHERE article_id = @Id; DELETE FROM articles WHERE id = @Id;", ("@Id", articleId));
            }
        }
    }

    [Fact]
    public async Task 球員後台_有照片時列表與詳情都帶照片網址與縮圖網址_無照片時為null()
    {
        using var client = CreateClient(out var factory);
        using var _ = factory;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("team.manager@tcrfc.test"));

        Guid teamId;
        await using (var connection = new SqlConnection(RequireConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id WHERE c.code = 'tcrfc' AND t.code = 'D1';";
            teamId = (Guid)(await command.ExecuteScalarAsync())!;
        }

        Guid? id = null;
        try
        {
            var create = await client.PostAsync("/api/v1/admin/tcrfc/players", AdminArticleMultipart.Build(new CreateAdminPlayerRequest
            {
                TeamId = teamId,
                ShirtNo = 88,
                Status = "active",
                Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "照片網址測試球員" } },
            }));
            Assert.Equal(System.Net.HttpStatusCode.Created, create.StatusCode);
            var created = (await create.Content.ReadFromJsonAsync<AdminPlayerDetailDto>(TestJson.Options))!;
            id = created.Id;
            Assert.Null(created.PhotoUrl);
            Assert.Null(created.PhotoThumbUrl);

            const string key = "tcrfc/players/test-photo.webp";
            await ExecuteAsync("UPDATE players SET photo_key = @K WHERE id = @Id;", ("@K", key), ("@Id", id));

            var detail = (await client.GetFromJsonAsync<AdminPlayerDetailDto>($"/api/v1/admin/tcrfc/players/{id}", TestJson.Options))!;
            Assert.Equal($"https://img.test/{key}", detail.PhotoUrl);
            Assert.Equal($"https://img.test/{ImageObjectKey.ForThumbnail(key)}", detail.PhotoThumbUrl);

            var list = (await client.GetFromJsonAsync<List<AdminPlayerListItemDto>>("/api/v1/admin/tcrfc/players", TestJson.Options))!;
            var item = Assert.Single(list, p => p.Id == id);
            Assert.Equal($"https://img.test/{key}", item.PhotoUrl);
            Assert.Equal($"https://img.test/{ImageObjectKey.ForThumbnail(key)}", item.PhotoThumbUrl);
        }
        finally
        {
            if (id is Guid playerId)
            {
                await ExecuteAsync("DELETE FROM players_i18n WHERE player_id = @Id; DELETE FROM players WHERE id = @Id;", ("@Id", playerId));
            }
        }
    }

    [Fact]
    public void 沒設Blob時的替身解析器_縮圖網址擴充方法回傳null不丟例外()
    {
        IImagePublicUrlResolver unavailable = new UnavailableImagePublicUrlResolver();
        Assert.Null(unavailable.ResolveThumbnail("tcrfc/articles/x.webp"));
        Assert.Null(unavailable.ResolveThumbnail(null));
        Assert.Null(new FakeResolver().ResolveThumbnail(""));
    }

    // ───────────────────────── 頁面區塊內容的圖片網址（不連資料庫）─────────────────────────

    [Fact]
    public void 頁面區塊_圖文左右與圖片藝廊的圖片物件補上url與thumbUrl_無鍵時為null_其他型別不動()
    {
        var resolver = new FakeResolver();

        var textImage = JsonNode.Parse("""{"imagePosition":"left","image":{"key":"tcrfc/pages/a.webp","altZh":"x"}}""");
        Attach(PageBlockTypes.TextImage, textImage, resolver.Resolve, resolver.ResolveThumbnail);
        Assert.Equal("https://img.test/tcrfc/pages/a.webp", textImage!["image"]!["url"]!.GetValue<string>());
        Assert.Equal($"https://img.test/{ImageObjectKey.ForThumbnail("tcrfc/pages/a.webp")}", textImage["image"]!["thumbUrl"]!.GetValue<string>());

        var gallery = JsonNode.Parse("""{"images":[{"key":"tcrfc/pages/b.webp"},{"key":null}]}""");
        Attach(PageBlockTypes.Gallery, gallery, resolver.Resolve, resolver.ResolveThumbnail);
        var images = gallery!["images"]!.AsArray();
        Assert.Equal("https://img.test/tcrfc/pages/b.webp", images[0]!["url"]!.GetValue<string>());
        Assert.Null(images[1]!["url"]);
        Assert.Null(images[1]!["thumbUrl"]);

        var text = JsonNode.Parse("""{"body":{"zh":"hi","en":null}}""");
        Attach(PageBlockTypes.Text, text, resolver.Resolve, resolver.ResolveThumbnail);
        Assert.Null(text!["url"]);
        Assert.Null(text["image"]);
    }

    [Fact]
    public async Task 頁面區塊_寫入時前端回傳的url與thumbUrl會被丟掉_不污染資料庫內容()
    {
        var content = JsonNode.Parse(
            """{"body":{"zh":"內文","en":null},"imagePosition":"right","image":{"key":"tcrfc/pages/a.webp","width":10,"height":10,"altZh":"圖","url":"https://stale/a","thumbUrl":"https://stale/t"}}""");

        var validate = ProcessorType.GetMethod("ValidateAndResolveAsync")!;
        var noUpload = Delegate.CreateDelegate(validate.GetParameters()[3].ParameterType, typeof(AdminImageUrlInReadDtosTests).GetMethod(nameof(NoUpload))!);
        await (Task)validate.Invoke(null, [0, PageBlockTypes.TextImage, content, noUpload, CancellationToken.None])!;

        var image = content!["image"]!.AsObject();
        Assert.False(image.ContainsKey("url"));
        Assert.False(image.ContainsKey("thumbUrl"));
        Assert.Equal("tcrfc/pages/a.webp", image["key"]!.GetValue<string>());
    }

    // PageBlockContentProcessor 是 internal（既有測試一律走 HTTP），這兩項純函式測試用反射呼叫，不為此新增 InternalsVisibleTo。
    private static readonly Type ProcessorType = typeof(AdminPageValidationException).Assembly.GetType("Tcrfc.Api.Features.AdminPages.PageBlockContentProcessor")!;

    public static Task<UploadedImageInfo?> NoUpload(string path, CancellationToken cancellationToken) => Task.FromResult<UploadedImageInfo?>(null);

    private static void Attach(string blockType, JsonNode? content, Func<string?, string?> url, Func<string?, string?> thumb)
        => ProcessorType.GetMethod("AttachImageUrls")!.Invoke(null, [blockType, content, url, thumb]);
}
