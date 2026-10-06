using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 後台驗證錯誤的欄位歸屬（<c>errors</c>：欄位鍵 → 訊息陣列）。試點兩個模組（球隊、新聞文章）打真正的 HTTP 管線，
/// 驗證處理器輸出形狀、鍵格式、沒有欄位歸屬時不帶 <c>errors</c>、<c>detail</c> 不變。
/// 欄位鍵是給前端對應用的，不得出現在訊息文字裡（所以訊息斷言不含鍵名）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminFieldErrorsTests(AdminWriteApiFixture fixture)
{
    private async Task<HttpClient> ClientAsync(string account)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(account));
        return client;
    }

    /// <summary>讀出 errors 為「鍵 → 第一則訊息」；沒有 errors 回 null。同時斷言每個鍵都符合欄位鍵格式、值是單元素陣列。</summary>
    private static async Task<(JsonElement Body, Dictionary<string, string>? Errors)> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!body.TryGetProperty("errors", out var errors))
        {
            return (body, null);
        }

        var map = new Dictionary<string, string>();
        foreach (var property in errors.EnumerateObject())
        {
            Assert.Matches(FieldKey.Pattern(), property.Name);
            Assert.Equal(JsonValueKind.Array, property.Value.ValueKind);
            Assert.Equal(1, property.Value.GetArrayLength());
            var message = property.Value[0].GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.DoesNotContain(property.Name, message, StringComparison.Ordinal); // 鍵不得出現在訊息文字裡
            map[property.Name] = message;
        }

        return (body, map);
    }

    private static MultipartFormDataContent TeamForm(string code, string type = "academy", string gender = "men", string zhName = "測試梯隊")
        => AdminArticleMultipart.Build(new CreateAdminTeamRequest
        {
            Code = code, Type = type, Gender = gender,
            Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = zhName } },
        });

    private static CreateArticleRequest Article(string slug, string title = "欄位錯誤測試", string category = "club",
        IReadOnlyList<AdminArticleTagInput>? tags = null, IReadOnlyList<string>? coreValues = null)
        => new()
        {
            Slug = slug, CategoryCode = category, Tags = tags, CoreValueTags = coreValues,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = title } },
        };

    private static string UniqueSlug() => $"field-errors-{Guid.NewGuid():N}";

    // ───────────────────────────── 球隊 ─────────────────────────────

    [Fact]
    public async Task 球隊_代號超長_400帶code鍵_detail與errors訊息一致()
    {
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var response = await client.PostAsync("/api/v1/admin/tcrfc/teams", TeamForm("TOOLONGCODE1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (body, errors) = await ReadAsync(response);
        Assert.NotNull(errors);
        Assert.Equal(["code"], errors!.Keys);
        Assert.Equal(body.GetProperty("detail").GetString(), errors["code"]); // detail 不變，errors 是同一句
        Assert.DoesNotContain("資料庫", errors["code"]);
    }

    [Fact]
    public async Task 球隊_中文名稱空白與值域錯誤_各自標到對應欄位()
    {
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var code = $"F{Guid.NewGuid():N}"[..4].ToUpperInvariant();

        var noName = await client.PostAsync("/api/v1/admin/tcrfc/teams", TeamForm(code, zhName: " "));
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(["nameZh"], (await ReadAsync(noName)).Errors!.Keys);

        var badType = await client.PostAsync("/api/v1/admin/tcrfc/teams", TeamForm(code, type: "women"));
        Assert.Equal(["type"], (await ReadAsync(badType)).Errors!.Keys);

        var badGender = await client.PostAsync("/api/v1/admin/tcrfc/teams", TeamForm(code, gender: "invalid"));
        var (_, genderErrors) = await ReadAsync(badGender);
        Assert.Equal(["gender"], genderErrors!.Keys);
        Assert.DoesNotContain("mixed", genderErrors["gender"]); // 不顯示英文技術值
    }

    [Fact]
    public async Task 球隊_隊別代號重複_409帶code鍵()
    {
        // D1 是種子裡既有的隊別代號（全站唯一），驗證在寫入之前就擋下，不會建立任何資料。
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var response = await client.PostAsync("/api/v1/admin/tcrfc/teams", TeamForm("D1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var (_, errors) = await ReadAsync(response);
        Assert.Equal(["code"], errors!.Keys);
    }

    [Fact]
    public async Task 球隊_沒有欄位歸屬的驗證錯誤_不帶errors_detail照舊()
    {
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/teams/writable?module=nope"); // 查詢參數錯誤，不是表單欄位

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (body, errors) = await ReadAsync(response);
        Assert.Null(errors);
        Assert.Contains("球隊用途", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task 球隊_請求格式錯誤_不帶欄位鍵()
    {
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var form = new MultipartFormDataContent { { new StringContent("x"), "other" } }; // 有 multipart 本文但缺 payload
        var response = await client.PostAsync("/api/v1/admin/tcrfc/teams", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await ReadAsync(response)).Errors);
    }

    // ───────────────────────────── 新聞文章 ─────────────────────────────

    [Fact]
    public async Task 文章_中文標題空白與網址名稱格式錯誤_各標到欄位()
    {
        using var client = await ClientAsync("content.editor@tcrfc.test");

        var noTitle = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug(), title: " ")));
        Assert.Equal(HttpStatusCode.BadRequest, noTitle.StatusCode);
        Assert.Equal(["titleZh"], (await ReadAsync(noTitle)).Errors!.Keys);

        var badSlug = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article("Bad Slug")));
        var (body, slugErrors) = await ReadAsync(badSlug);
        Assert.Equal(["slug"], slugErrors!.Keys);
        Assert.Equal(body.GetProperty("detail").GetString(), slugErrors["slug"]);

        var reserved = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article("media")));
        Assert.Equal(["slug"], (await ReadAsync(reserved)).Errors!.Keys);
    }

    [Fact]
    public async Task 文章_分類_標籤_核心價值_關聯錯誤_鍵帶陣列索引()
    {
        using var client = await ClientAsync("content.editor@tcrfc.test");

        var badCategory = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug(), category: "no-such-category")));
        Assert.Equal(["category"], (await ReadAsync(badCategory)).Errors!.Keys);

        var badTag = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug(), tags:
        [
            new AdminArticleTagInput { Slug = "ok-tag", NameZh = "好" },
            new AdminArticleTagInput { Slug = "Bad Tag" },
        ])));
        Assert.Equal(["tags[1].slug"], (await ReadAsync(badTag)).Errors!.Keys);

        var badValue = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug(), coreValues: ["integrity", "not-a-value"])));
        var (_, valueErrors) = await ReadAsync(badValue);
        Assert.Equal(["coreValueTags[1]"], valueErrors!.Keys);
        Assert.DoesNotContain("not-a-value", valueErrors["coreValueTags[1]"]); // 不回顯內部代碼

        var badRelation = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug()) with
        {
            Relations = [new AdminArticleRelationInput { TargetType = "player", TargetId = Guid.NewGuid() }],
        }));
        Assert.Equal(["relations[0].targetId"], (await ReadAsync(badRelation)).Errors!.Keys);
    }

    [Fact]
    public async Task 文章_網址名稱重複_409帶slug鍵()
    {
        using var client = await ClientAsync("content.editor@tcrfc.test");
        var slug = UniqueSlug();
        var created = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(slug)));
        created.EnsureSuccessStatusCode();
        var article = await created.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        try
        {
            var duplicate = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(slug)));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            var (body, errors) = await ReadAsync(duplicate);
            Assert.Equal(["slug"], errors!.Keys);
            Assert.Equal(body.GetProperty("detail").GetString(), errors["slug"]);
        }
        finally
        {
            var url = $"/api/v1/admin/tcrfc/news/{article!.Id}?expectedUpdatedAt={Uri.EscapeDataString(article.UpdatedAt.ToString("o"))}";
            var deleted = await client.DeleteAsync(url);
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); // 斷言清理成功
        }
    }

    [Fact]
    public async Task 文章_排程時間不在未來_400帶publishAt鍵_並行衝突409不帶errors()
    {
        using var client = await ClientAsync("content.editor@tcrfc.test");
        var created = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(Article(UniqueSlug())));
        created.EnsureSuccessStatusCode();
        var article = await created.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        try
        {
            var past = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/news/{article!.Id}/schedule",
                new ScheduleArticleRequest { PublishAt = DateTime.UtcNow.AddDays(-1), ExpectedUpdatedAt = article.UpdatedAt }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
            Assert.Equal(["publishAt"], (await ReadAsync(past)).Errors!.Keys);

            // 過期的 expectedUpdatedAt → 並行衝突（409），這類錯誤整筆資料層級，沒有欄位歸屬。
            var stale = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/news/{article.Id}/schedule",
                new ScheduleArticleRequest { PublishAt = DateTime.UtcNow.AddDays(1), ExpectedUpdatedAt = article.UpdatedAt.AddMinutes(-5) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Null((await ReadAsync(stale)).Errors);
        }
        finally
        {
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{article!.Id}", TestJson.Options);
            var url = $"/api/v1/admin/tcrfc/news/{article.Id}?expectedUpdatedAt={Uri.EscapeDataString(current!.UpdatedAt.ToString("o"))}";
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url)).StatusCode);
        }
    }

    // ───────────────────────────── 欄位鍵工具 ─────────────────────────────

    [Theory]
    [InlineData("slug", true)]
    [InlineData("nameZh", true)]
    [InlineData("blocks[2].bodyEn", true)]
    [InlineData("tags[1].slug", true)]
    [InlineData("a.b[0].c", true)]
    [InlineData("", false)]
    [InlineData("Name", false)]
    [InlineData("name_zh", false)]
    [InlineData("blocks[].x", false)]
    [InlineData("blocks[2]bodyEn", false)]
    [InlineData("name zh", false)]
    [InlineData("a..b", false)]
    public void 欄位鍵格式(string key, bool valid) => Assert.Equal(valid, FieldKey.IsValid(key));

    [Fact]
    public void 欄位鍵工具_組出的鍵都符合格式()
    {
        Assert.Equal("nameZh", FieldKey.Bi("name", "zh"));
        Assert.Equal("bodyEn", FieldKey.Bi("body", "EN"));
        Assert.Equal("blocks[2].bodyEn", FieldKey.Item("blocks", 2, FieldKey.Bi("body", "en")));
        Assert.True(FieldKey.IsValid(FieldKey.Item("tags", 0)));
    }

    [Fact]
    public void 沒指定欄位時FieldErrors為空_有指定時為單一鍵()
    {
        Assert.Empty(new AdminValidationException("x").FieldErrors);
        Assert.Empty(new AdminConflictException("t", "x").FieldErrors);
        Assert.Equal("訊息", new AdminValidationException("訊息", "slug").FieldErrors["slug"]);
        Assert.Equal("訊息", new AdminConflictException("t", "訊息", "slug").FieldErrors["slug"]);
        Assert.Equal("slug", Assert.Single(new ArticleSlugConflictException("a-b").FieldErrors).Key);
    }

    [Fact]
    public void AdminInput_帶field時錯誤指向該欄位()
    {
        var ex = Assert.Throws<AdminValidationException>(() => AdminInput.RequireText("", "名稱", 10, "nameZh"));
        Assert.Equal(["nameZh"], ex.FieldErrors.Keys);
        var tooLong = Assert.Throws<AdminValidationException>(() => AdminInput.OptionalText(new string('a', 11), "備註", 10, "note"));
        Assert.Equal(["note"], tooLong.FieldErrors.Keys);
        var bare = Assert.Throws<AdminValidationException>(() => AdminInput.RequireText("", "名稱", 10));
        Assert.Empty(bare.FieldErrors);
    }
    // ───────────────────────────── 第 3 階段：其餘模組的補鍵（代表性端點） ─────────────────────────────

    [Fact]
    public async Task 球隊_完全空的multipart_回400不是500()
    {
        using var client = await ClientAsync("team.manager@tcrfc.test");
        var content = new MultipartFormDataContent("emptyboundary");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=emptyboundary");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tcrfc/teams") { Content = new ByteArrayContent([]) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=emptyboundary");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await ReadAsync(response)).Errors);
    }

    [Fact]
    public async Task 角色_代碼格式錯誤與範圍錯誤_各自標到欄位()
    {
        using var client = await ClientAsync("super.admin@tcrfc.test");
        var badCode = await client.PostAsJsonAsync("/api/v1/admin/roles",
            new { code = "Bad Code", nameZh = "測試", scopeMode = "own_clubs" });
        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);
        Assert.Equal(["code"], (await ReadAsync(badCode)).Errors!.Keys);

        var badScope = await client.PostAsJsonAsync("/api/v1/admin/roles",
            new { code = $"fe_{Guid.NewGuid():N}"[..20], nameZh = "測試", scopeMode = "nope" });
        var (_, errors) = await ReadAsync(badScope);
        Assert.Equal(["scopeMode"], errors!.Keys);
        Assert.DoesNotContain("all_clubs", errors["scopeMode"]); // 不顯示英文列舉值
    }

    [Fact]
    public async Task 帳號_密碼過短_標到initialPassword()
    {
        using var client = await ClientAsync("super.admin@tcrfc.test");
        var response = await client.PostAsJsonAsync("/api/v1/admin/accounts",
            new { username = $"fe-{Guid.NewGuid():N}"[..12], displayName = "測試", initialPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["initialPassword"], (await ReadAsync(response)).Errors!.Keys);
    }

    [Fact]
    public async Task 俱樂部_代碼空白與重複網域_標到對應欄位()
    {
        using var client = await ClientAsync("super.admin@tcrfc.test");
        var blank = await client.PostAsJsonAsync("/api/v1/admin/clubs",
            new { code = "", domain = "x.example", content = new { zh = new { name = "測試" } } });
        Assert.Equal(["code"], (await ReadAsync(blank)).Errors!.Keys);

        var noName = await client.PostAsJsonAsync("/api/v1/admin/clubs",
            new { code = "feabc", domain = "x.example", content = new { zh = new { name = " " } } });
        Assert.Equal(["nameZh"], (await ReadAsync(noName)).Errors!.Keys);
    }

    [Fact]
    public async Task 常見問題分類_網址名稱格式錯誤_標到slug()
    {
        using var client = await ClientAsync("super.admin@tcrfc.test");
        var response = await client.PostAsJsonAsync("/api/v1/admin/faq-categories",
            new { slug = "Bad Slug", sortOrder = 0, content = new { zh = new { name = "測試" } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["slug"], (await ReadAsync(response)).Errors!.Keys);
    }

    [Fact]
    public void 欄位鍵_區塊內路徑符合格式()
    {
        Assert.True(FieldKey.IsValid(FieldKey.Item("blocks", 2, "items[0].questionZh")));
        Assert.True(FieldKey.IsValid(FieldKey.Item("winners", 0, "prizeName")));
        Assert.True(FieldKey.IsValid("items[1].children[0].labelZh"));
    }
}
