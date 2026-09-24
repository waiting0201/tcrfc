using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminBanners;
using Tcrfc.Api.Features.AdminHomeSections;
using Tcrfc.Api.Features.Home;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-6：B3 首頁編排（<c>banners</c>／<c>home_sections</c>）後台讀寫＋公開讀取。
/// 真的打 HTTP 管線、真的啟動 Azurite（輪播圖片上傳），不 mock。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminBannersAndHomeSectionsTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    // ───────────────────────────── Banner：授權 ─────────────────────────────

    [Fact]
    public async Task Banner_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/banners");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Banner_檢視者角色_可讀不可寫()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/banners");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(NewCreateRequest(), TestImages.SmallPng()));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Banner_跨俱樂部_擋下_授權範圍內_成功()
    {
        // partner.club@tcrfc.test（合作球隊管理，own_clubs）只被授權 bw。
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test"));

        var tcrfcResponse = await client.GetAsync("/api/v1/admin/tcrfc/banners");
        Assert.Equal(HttpStatusCode.Forbidden, tcrfcResponse.StatusCode);

        var bwResponse = await client.GetAsync("/api/v1/admin/bw/banners");
        Assert.Equal(HttpStatusCode.OK, bwResponse.StatusCode);
    }

    // ───────────────────────────── Banner：行為 ─────────────────────────────

    [Fact]
    public async Task Banner_建立更新刪除完整生命週期_含換圖與刪圖()
    {
        using var client = await CreateContentEditorClientAsync();

        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(NewCreateRequest(), TestImages.SmallPng()));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Equal("測試輪播", created!.Zh.Title);
        var firstImageKey = created.ImageKey;

        try
        {
            // 換圖：更新請求帶新檔案，舊物件應該被刪除。
            var updateRequest = new UpdateBannerRequest
            {
                SortOrder = 1,
                Content = new AdminBannerContentInput
                {
                    Zh = new AdminBannerLocaleContent { Title = "測試輪播（已更新）" },
                    En = new AdminBannerLocaleContent { Title = "Test Banner (Updated)" },
                },
            };
            var updateResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created.Id}",
                AdminArticleMultipart.Build(updateRequest, TestImages.SmallWebp(), fileName: "banner.webp", fileContentType: "image/webp"));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.NotNull(updated);
            Assert.Equal("測試輪播（已更新）", updated!.Zh.Title);
            Assert.Equal("Test Banner (Updated)", updated.En!.Title);
            Assert.NotEqual(firstImageKey, updated.ImageKey);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getAfterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/banners/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
        finally
        {
            // 保險起見再刪一次（若上面斷言中途失敗），並確認沒有殘留物件。
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created.Id}");
        }
    }

    [Fact]
    public async Task Banner_建立時未帶圖片_400()
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners", AdminArticleMultipart.Build(NewCreateRequest()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Banner_上架時間不早於下架時間_400()
    {
        using var client = await CreateContentEditorClientAsync();
        var request = new CreateBannerRequest
        {
            StartAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            EndAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "不合法的期間" } },
        };
        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners", AdminArticleMultipart.Build(request, TestImages.SmallPng()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ───────────────────────────── HomeSection：授權與行為 ─────────────────────────────

    [Fact]
    public async Task HomeSection_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/home-sections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HomeSection_列表回傳九個固定區塊()
    {
        using var client = await CreateContentEditorClientAsync();
        var sections = await client.GetFromJsonAsync<List<AdminHomeSectionDto>>(
            "/api/v1/admin/tcrfc/home-sections", TestJson.Options);
        Assert.NotNull(sections);
        Assert.Equal(9, sections!.Count);
        Assert.Contains(sections, s => s.SectionCode == "hero" && s.NameZh == "Hero 輪播");
        Assert.Contains(sections, s => s.SectionCode == "bottom_cta");
    }

    [Fact]
    public async Task HomeSection_更新開關與排序成功_檢視者角色被擋下()
    {
        using var viewerClient = fixture.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));
        var viewerAttempt = await viewerClient.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/home-sections/core_values",
            new UpdateHomeSectionRequest { IsEnabled = false, SortOrder = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, viewerAttempt.StatusCode);

        using var client = await CreateContentEditorClientAsync();
        try
        {
            var updateResponse = await client.PutAsJsonAsync(
                "/api/v1/admin/tcrfc/home-sections/core_values",
                new UpdateHomeSectionRequest { IsEnabled = false, SortOrder = 5 });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminHomeSectionDto>(TestJson.Options);
            Assert.False(updated!.IsEnabled);
            Assert.Equal(5, updated.SortOrder);
        }
        finally
        {
            // 還原成種子初始狀態，不影響其他測試。
            await client.PutAsJsonAsync(
                "/api/v1/admin/tcrfc/home-sections/core_values",
                new UpdateHomeSectionRequest { IsEnabled = true, SortOrder = 1 });
        }
    }

    [Fact]
    public async Task HomeSection_非Hero區塊指定精選輪播_400()
    {
        using var client = await CreateContentEditorClientAsync();
        var response = await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/home-sections/latest_news",
            new UpdateHomeSectionRequest { IsEnabled = true, SortOrder = 5, FeaturedBannerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HomeSection_Hero指定不存在的輪播_400_不存在的區塊代碼_404()
    {
        using var client = await CreateContentEditorClientAsync();

        var invalidBanner = await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/home-sections/hero",
            new UpdateHomeSectionRequest { IsEnabled = true, SortOrder = 0, FeaturedBannerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, invalidBanner.StatusCode);

        var unknownSection = await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/home-sections/no-such-section",
            new UpdateHomeSectionRequest { IsEnabled = true, SortOrder = 0 });
        Assert.Equal(HttpStatusCode.NotFound, unknownSection.StatusCode);
    }

    // ───────────────────────────── 公開讀取 ─────────────────────────────

    [Fact]
    public async Task Public_Banners_只回上架期間內的輪播()
    {
        using var client = await CreateContentEditorClientAsync();

        var active = await CreateBannerAsync(client, "上架中", startAt: null, endAt: null);
        var future = await CreateBannerAsync(client, "尚未上架", startAt: DateTime.UtcNow.AddDays(30), endAt: null);
        var past = await CreateBannerAsync(client, "已下架", startAt: null, endAt: DateTime.UtcNow.AddDays(-30));

        try
        {
            var publicResponse = await client.GetAsync("/api/v1/tcrfc/banners");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            var banners = await publicResponse.Content.ReadFromJsonAsync<List<BannerDto>>(TestJson.Options);
            Assert.NotNull(banners);
            Assert.Contains(banners!, b => b.Id == active.Id);
            Assert.DoesNotContain(banners!, b => b.Id == future.Id);
            Assert.DoesNotContain(banners!, b => b.Id == past.Id);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{active.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{future.Id}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{past.Id}");
        }
    }

    [Fact]
    public async Task Public_HomeSections_回傳九筆含停用區塊()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/tcrfc/home-sections");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sections = await response.Content.ReadFromJsonAsync<List<HomeSectionDto>>(TestJson.Options);
        Assert.NotNull(sections);
        Assert.Equal(9, sections!.Count);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static CreateBannerRequest NewCreateRequest() => new()
    {
        SortOrder = 0,
        Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "測試輪播" } },
    };

    private async Task<AdminBannerDetailDto> CreateBannerAsync(HttpClient client, string titleZh, DateTime? startAt, DateTime? endAt)
    {
        var request = new CreateBannerRequest
        {
            StartAt = startAt,
            EndAt = endAt,
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = titleZh } },
        };
        var response = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners", AdminArticleMultipart.Build(request, TestImages.SmallPng()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options))!;
    }

    private async Task<HttpClient> CreateContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }
}
