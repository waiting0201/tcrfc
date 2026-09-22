using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 網址名稱（slug）保留字與格式驗證的回歸測試（見 <see cref="Tcrfc.Api.Features.AdminNews.SlugPolicy"/>）。
/// 涵蓋：09 個保留字逐一擋下、大小寫變形、建立與更新兩條路徑、以及合法網址名稱不受影響。
/// 打真正的 HTTP 管線、真正的 <c>tcrfc_club_dev</c>，跟這個測試專案既有的紀律一致。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminNewsSlugPolicyTests(AdminWriteApiFixture fixture)
{
    private const string CategoryCode = "club"; // 種子資料確認存在的分類代碼（見 apps/api/README.md）

    /// <summary>對應 <see cref="SlugPolicy"/> 裡 07 新聞單元的 9 個分類路由片段，
    /// 逐一列出而不是引用私有欄位——這樣測試本身也是一份「目前保留字有哪些」的可讀清單，
    /// 跟 SlugPolicy.cs 上的來源註解互相對照。</summary>
    public static TheoryData<string> ReservedWords => new()
    {
        "club", "match", "academy", "player-stories", "international",
        "camps-events", "community", "media", "article",
    };

    // 不用 CallerMemberName 帶入方法名稱——本測試檔的方法名稱是中文，套進網址名稱會直接違反
    // 這裡正要測的格式規則（只准小寫英文字母、數字、連字號），純 ASCII 亂數字串就好。
    private static string UniqueSlug()
        => $"slug-policy-test-{Guid.NewGuid():N}";

    private static CreateArticleRequest NewDraftRequest(string slug, string title = "測試文章標題")
        => new()
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = title } },
        };

    private async Task<AdminArticleDetailDto> CreateDraftAsync(HttpClient client, string slug)
    {
        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(slug)));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        return created!;
    }

    private static async Task DeleteBestEffortAsync(HttpClient client, Guid id, DateTime expectedUpdatedAt)
    {
        var url = $"/api/v1/admin/tcrfc/news/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}";
        await client.DeleteAsync(url);
    }

    // ───────────────────────────── 建立路徑：保留字 ─────────────────────────────

    [Theory]
    [MemberData(nameof(ReservedWords))]
    public async Task 建立文章_網址名稱是保留字_回400且訊息可讀(string reservedWord)
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(reservedWord)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("slug", body, StringComparison.OrdinalIgnoreCase); // ⛔ 訊息不得出現英文技術詞（docs/06 §1）
        Assert.Contains("網址名稱", body);
        Assert.Contains(reservedWord, body); // 訊息要點出實際送出的值，不是只講規則
    }

    [Theory]
    [InlineData("Club")]
    [InlineData("CLUB")]
    [InlineData("Match")]
    [InlineData("Player-Stories")]
    public async Task 建立文章_保留字大小寫變形_同樣回400(string variant)
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(variant)));

        // 大寫變形會先被「只能小寫」的格式規則擋下（400），不需要先通過格式檢查才走到保留字比對，
        // 但無論被哪一條規則擋下，結果都必須是 400——這裡驗證的是「不會意外通過」，不指定是哪條規則。
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── 建立路徑：其他危險形狀 ─────────────────────────────

    [Theory]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("has.dot")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    [InlineData("UpperCase")]
    [InlineData("12345")] // 純數字
    [InlineData("")]
    [InlineData("   ")]
    public async Task 建立文章_危險或不合格式的網址名稱_回400(string badSlug)
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(badSlug)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── 更新路徑：保留字與危險形狀同樣受擋 ─────────────────────────────

    [Fact]
    public async Task 更新文章_把網址名稱改成保留字_回400且原資料不受影響()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftAsync(client, UniqueSlug());

        try
        {
            var updateRequest = new UpdateArticleRequest
            {
                Slug = "media", // 保留字
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "想改成保留字" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };

            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // 確認沒有半套：原本的網址名稱與標題都沒被改到。
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal(created.Slug, current!.Slug);
            Assert.Equal("測試文章標題", current.Zh.Title);
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    [Theory]
    [InlineData("Academy")] // 保留字大小寫變形
    [InlineData("has space")]
    public async Task 更新文章_網址名稱改成危險形狀_回400(string badSlug)
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftAsync(client, UniqueSlug());

        try
        {
            var updateRequest = new UpdateArticleRequest
            {
                Slug = badSlug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "測試" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };

            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
        }
    }

    // ───────────────────────────── 合法網址名稱不受影響 ─────────────────────────────

    [Theory]
    [InlineData("2024-12-18-club-079")] // 現有 83 篇實際慣用的形狀（分類詞出現在中段，不是整段等於保留字）
    [InlineData("club-and-community-day")] // 以保留字開頭的詞組，但整段不等於保留字本身
    [InlineData("2026-camps-events-recap")]
    public async Task 建立文章_合法網址名稱_成功建立且不受保留字規則誤擋(string legalSlug)
    {
        using var client = fixture.CreateClient();
        var slug = $"{legalSlug}-{Guid.NewGuid():N}"; // 加隨機片段避免撞到既有 83 筆或跨測試重跑（articles.slug 是 nvarchar(160)，長度足夠）

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(slug)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Equal(slug, created!.Slug);

        await DeleteBestEffortAsync(client, created.Id, created.UpdatedAt);
    }

    [Fact]
    public async Task 更新文章_合法網址名稱不變更_不受新規則影響()
    {
        using var client = fixture.CreateClient();
        var created = await CreateDraftAsync(client, UniqueSlug());

        try
        {
            var updateRequest = new UpdateArticleRequest
            {
                Slug = created.Slug, // 沒有改變網址名稱
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "合法更新，網址名稱不變" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };

            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, created.Id, probe.UpdatedAt);
            }
        }
    }
}
