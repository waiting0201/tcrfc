using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 寫入 json 欄位的守門測試（docs/18 E-111）。正式環境的 json 是原生型別，只收物件或陣列；
/// 本機 SQL Server 2022 的 nvarchar(max) 什麼都收，所以「純量被擋成 400」必須在應用層驗證，
/// 這組測試在 2022 與 2025（apps/api/scripts/native-json-test.sh）上都能跑、行為一致。
/// </summary>
public sealed class JsonColumnTests
{
    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"a\":1}", true)]
    [InlineData("[]", true)]
    [InlineData("[1,2]", true)]
    [InlineData("  {\"a\":1}  ", true)]
    [InlineData("\"abc\"", false)]
    [InlineData("123", false)]
    [InlineData("true", false)]
    [InlineData("null", false)]
    [InlineData("週一至週五 11:00", false)]
    [InlineData("{not json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsObjectOrArray_只有物件或陣列為真(string? content, bool expected)
        => Assert.Equal(expected, JsonColumn.IsObjectOrArray(content));

    [Fact]
    public void WrapText_包成物件_空白視為沒有值_讀回相同文字()
    {
        var stored = JsonColumn.WrapText("週一至週五 11:00–21:00");
        Assert.True(JsonColumn.IsObjectOrArray(stored));
        Assert.Equal("週一至週五 11:00–21:00", JsonColumn.UnwrapText(stored));
        Assert.Null(JsonColumn.WrapText("   "));
        Assert.Null(JsonColumn.WrapText(null));
    }

    [Theory]
    [InlineData("\"週一至週五\"", "週一至週五")]        // 舊格式：JSON 字串純量（2022／nvarchar 時代寫入的資料）
    [InlineData("週一至週五", "週一至週五")]            // 非 JSON 的純文字
    [InlineData("{\"text\":\"全年無休\"}", "全年無休")]
    [InlineData("{\"mon\":\"11-21\"}", "{\"mon\":\"11-21\"}")] // 其他物件形狀原樣回傳，不丟例外
    public void UnwrapText_相容舊格式(string stored, string expected)
        => Assert.Equal(expected, JsonColumn.UnwrapText(stored));

    [Theory]
    [InlineData("{\"ok\":true}", "{\"ok\":true}")]
    [InlineData("[1]", "[1]")]
    [InlineData("OK", "{\"raw\":\"OK\"}")]
    [InlineData("123", "{\"raw\":\"123\"}")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void CoerceToObject_外部回應一律變成物件或陣列(string? raw, string? expected)
        => Assert.Equal(expected, JsonColumn.CoerceToObject(raw));

    [Fact]
    public void OptionalJson_純量擋成中文驗證錯誤_物件陣列與空值通過()
    {
        Assert.Null(AdminInput.OptionalJson(null, "內容"));
        Assert.Null(AdminInput.OptionalJson("  ", "內容"));
        Assert.Equal("{\"a\":1}", AdminInput.OptionalJson("{\"a\":1}", "內容"));
        Assert.Equal("[]", AdminInput.OptionalJson("[]", "內容"));
        foreach (var scalar in new[] { "\"abc\"", "123", "true", "null", "{not json" })
        {
            var ex = Assert.Throws<AdminValidationException>(() => AdminInput.OptionalJson(scalar, "中文緣起與內容"));
            Assert.Contains("中文緣起與內容", ex.Message);
            Assert.DoesNotContain("JSON", ex.Message); // docs/06：介面文字不出現英文技術詞
        }
    }

    [Fact]
    public void ValidateContentJson_課程內容與週期時段表_純量擋掉()
    {
        AdminProgramsRepository.ValidateContentJson(null);
        AdminProgramsRepository.ValidateContentJson("{\"slots\":[]}");
        AdminProgramsRepository.ValidateContentJson("[]");
        foreach (var scalar in new[] { "\"abc\"", "42", "false", "null", "{not json" })
        {
            var ex = Assert.Throws<AdminProgramValidationException>(() => AdminProgramsRepository.ValidateContentJson(scalar));
            Assert.DoesNotContain("JSON", ex.Message);
        }
    }
}

/// <summary>端點層：新聞內文是原生 json 欄位，純文字存成 <c>{"text":"…"}</c>、對外還原成原文字；物件原樣保存（E-111）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ArticleBodyJsonColumnTests(AdminWriteApiFixture fixture)
{
    private static async Task<string?> StoredBodyAsync(Guid id, string locale)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(body AS nvarchar(max)) FROM articles_i18n WHERE article_id = @I AND locale = @L";
        command.Parameters.AddWithValue("@I", id);
        command.Parameters.AddWithValue("@L", locale);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? null : (string)value;
    }

    [Fact]
    public async Task 純文字內文存成物件_後台與公開端點讀回原文字_物件輸入原樣保存()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
            await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var slug = $"json-body-{Guid.NewGuid():N}";
        const string plain = "第一段 <b>& 'quote'</b> + https://example.com/a?x=1&y=2\n\n第二段";
        const string blockObject = "{\"blocks\":[{\"type\":\"p\",\"text\":\"區塊\"}]}";
        var create = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = "測試標題", Body = plain },
                En = new AdminArticleLocaleContent { Title = "Test", Body = blockObject },
            },
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(create));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
        AdminArticleDetailDto current = created;
        try
        {
            // 資料庫裡：純文字是物件 {"text":…}，物件輸入原樣
            var storedZh = await StoredBodyAsync(created.Id, "zh-Hant");
            Assert.True(JsonColumn.IsObjectOrArray(storedZh));
            Assert.StartsWith("{\"text\":", storedZh);
            Assert.Contains("第一段", storedZh); // 中文不被轉成 \uXXXX
            Assert.Equal(blockObject, await StoredBodyAsync(created.Id, "en"));

            // 後台詳情：原文字／原物件
            var admin = (await (await client.GetAsync($"/api/v1/admin/tcrfc/news/{created.Id}")).Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            current = admin;
            Assert.Equal(plain, admin.Zh.Body);
            Assert.Equal(blockObject, admin.En!.Body);

            // 發布後的公開端點
            var published = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = admin.UpdatedAt }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
            current = (await published.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            using var anonymous = fixture.CreateClient();
            var pubZh = await anonymous.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/tcrfc/news/{slug}?lang=zh");
            Assert.Equal(plain, pubZh.GetProperty("bodyJson").GetString());
            var pubEn = await anonymous.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/tcrfc/news/{slug}?lang=en");
            Assert.Equal(blockObject, pubEn.GetProperty("bodyJson").GetString());

            // 清空內文 → NULL（不是空字串，原生 json 不收）
            var cleared = new UpdateArticleRequest
            {
                Slug = slug, CategoryCode = "club", IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "測試標題", Body = "   " } },
                ExpectedUpdatedAt = current.UpdatedAt,
            };
            var put = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(cleared));
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            current = (await put.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
            Assert.Null(await StoredBodyAsync(created.Id, "zh-Hant"));
        }
        finally
        {
            var probe = await client.GetAsync($"/api/v1/admin/tcrfc/news/{created.Id}");
            if (probe.StatusCode == HttpStatusCode.OK)
            {
                var latest = (await probe.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
                await client.DeleteAsync($"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(latest.UpdatedAt.ToString("o"))}");
            }
        }
    }
}
