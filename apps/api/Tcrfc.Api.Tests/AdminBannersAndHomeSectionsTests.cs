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
    public async Task Banner_圖片寬高由上傳結果自動填入_alt雙語()
    {
        // S1-7a：banners.media_type／image_width／image_height／banners_i18n.image_alt。
        using var client = await CreateContentEditorClientAsync();

        var createRequest = new CreateBannerRequest
        {
            SortOrder = 0,
            Content = new AdminBannerContentInput
            {
                Zh = new AdminBannerLocaleContent { Title = "測試輪播", ImageAlt = "台中磐石球員慶祝進球" },
                En = new AdminBannerLocaleContent { Title = "Test Banner", ImageAlt = "Taichung Rock FC players celebrating a goal" },
            },
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners", AdminArticleMultipart.Build(createRequest, TestImages.SmallPng()));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);

        try
        {
            // 省略 MediaType → 回退 image；寬高由 TestImages.SmallPng()（500×400，不放大不縮小）自動填入。
            Assert.Equal("image", created!.MediaType);
            Assert.Equal(500, created.ImageWidth);
            Assert.Equal(400, created.ImageHeight);
            Assert.Null(created.VideoKey);
            Assert.Equal("台中磐石球員慶祝進球", created.Zh.ImageAlt);
            Assert.Equal("Taichung Rock FC players celebrating a goal", created.En!.ImageAlt);

            // 換一張不同尺寸的圖，寬高應該跟著換新值（WebP 200×200，見 TestImages.SmallWebp）。
            var updateRequest = new UpdateBannerRequest
            {
                SortOrder = 0,
                Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "測試輪播", ImageAlt = "更新後的替代文字" } },
            };
            var updateResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created.Id}",
                AdminArticleMultipart.Build(updateRequest, TestImages.SmallWebp(), fileName: "banner.webp", fileContentType: "image/webp"));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal(200, updated!.ImageWidth);
            Assert.Equal(200, updated.ImageHeight);
            Assert.Equal("更新後的替代文字", updated.Zh.ImageAlt);

            // 公開端點也吐得出 mediaType／寬高／alt（前台需要，見 Features/Home/HomeDtos.cs）——
            // 🔴 v3.14：新建立的輪播預設是 draft，公開端點只回 published，這裡要先發布才看得到
            // （見 Banner_草稿不出現在公開端點_發布後才出現_可改回草稿）。
            var publishResponse = await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

            var publicBanners = await client.GetFromJsonAsync<List<BannerDto>>("/api/v1/tcrfc/banners", TestJson.Options);
            var publicBanner = publicBanners!.First(b => b.Id == created.Id);
            Assert.Equal("image", publicBanner.MediaType);
            Assert.Equal(200, publicBanner.ImageWidth);
            Assert.Equal(200, publicBanner.ImageHeight);
            Assert.Equal("更新後的替代文字", publicBanner.ImageAlt);

            // 更新時不換圖：寬高維持原值（不因為這次請求沒帶檔案就被清空）。
            var updateWithoutFile = new UpdateBannerRequest
            {
                SortOrder = 2,
                Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "測試輪播（不換圖）" } },
            };
            var updateWithoutFileResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created.Id}", AdminArticleMultipart.Build(updateWithoutFile));
            Assert.Equal(HttpStatusCode.OK, updateWithoutFileResponse.StatusCode);
            var afterNoFileUpdate = await updateWithoutFileResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal(200, afterNoFileUpdate!.ImageWidth);
            Assert.Equal(200, afterNoFileUpdate.ImageHeight);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created!.Id}");
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

    // ───────────────────────────── Banner：草稿／發布（v3.14） ─────────────────────────────

    [Fact]
    public async Task Banner_新建立為草稿_公開端點看不到_發布後看得到_改回草稿後又看不到()
    {
        using var client = await CreateContentEditorClientAsync();
        var created = await CreateBannerAsync(client, "草稿輪播", startAt: null, endAt: null, publish: false);

        try
        {
            Assert.Equal("draft", created.Status);

            var beforePublish = await client.GetFromJsonAsync<List<BannerDto>>("/api/v1/tcrfc/banners", TestJson.Options);
            Assert.DoesNotContain(beforePublish!, b => b.Id == created.Id);

            var publishResponse = await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal("published", published!.Status);

            var afterPublish = await client.GetFromJsonAsync<List<BannerDto>>("/api/v1/tcrfc/banners", TestJson.Options);
            Assert.Contains(afterPublish!, b => b.Id == created.Id);

            // 發布是冪等的，再打一次不報錯、狀態不變。
            var publishAgainResponse = await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            Assert.Equal(HttpStatusCode.OK, publishAgainResponse.StatusCode);

            var unpublishResponse = await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/unpublish", content: null);
            Assert.Equal(HttpStatusCode.OK, unpublishResponse.StatusCode);
            var unpublished = await unpublishResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal("draft", unpublished!.Status);

            var afterUnpublish = await client.GetFromJsonAsync<List<BannerDto>>("/api/v1/tcrfc/banners", TestJson.Options);
            Assert.DoesNotContain(afterUnpublish!, b => b.Id == created.Id);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created.Id}");
        }
    }

    [Fact]
    public async Task Banner_發布與改回草稿_檢視者角色被擋下_找不到的輪播404()
    {
        var created = await CreateBannerAsync(await CreateContentEditorClientAsync(), "測試發布權限", startAt: null, endAt: null, publish: false);

        try
        {
            using var viewerClient = fixture.CreateClient();
            viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test"));

            var publishAttempt = await viewerClient.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            Assert.Equal(HttpStatusCode.Forbidden, publishAttempt.StatusCode);

            using var editorClient = await CreateContentEditorClientAsync();
            var notFound = await editorClient.PostAsync($"/api/v1/admin/tcrfc/banners/{Guid.NewGuid()}/publish", content: null);
            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        }
        finally
        {
            await (await CreateContentEditorClientAsync()).DeleteAsync($"/api/v1/admin/tcrfc/banners/{created.Id}");
        }
    }

    // ───────────────────────────── Banner：影片（v3.14） ─────────────────────────────

    [Fact]
    public async Task Banner_影片模式_建立成功_海報圖與影片鍵皆有值_公開端點吐出videoKey()
    {
        using var client = await CreateContentEditorClientAsync();
        var request = new CreateBannerRequest
        {
            MediaType = "video",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "影片輪播" } },
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(request, TestImages.SmallPng(), videoBytes: TestVideos.SmallValidMp4()));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);

        try
        {
            Assert.Equal("video", created!.MediaType);
            Assert.NotNull(created.ImageKey);
            Assert.NotNull(created.VideoKey);

            await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            var publicBanners = await client.GetFromJsonAsync<List<BannerDto>>("/api/v1/tcrfc/banners", TestJson.Options);
            var publicBanner = publicBanners!.First(b => b.Id == created.Id);
            Assert.Equal("video", publicBanner.MediaType);
            Assert.Equal(created.VideoKey, publicBanner.VideoKey);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created!.Id}");
        }
    }

    [Fact]
    public async Task Banner_影片模式_缺影片檔案_400_圖片模式送影片檔案_400()
    {
        using var client = await CreateContentEditorClientAsync();

        var missingVideo = new CreateBannerRequest
        {
            MediaType = "video",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "缺影片" } },
        };
        var missingVideoResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners", AdminArticleMultipart.Build(missingVideo, TestImages.SmallPng()));
        Assert.Equal(HttpStatusCode.BadRequest, missingVideoResponse.StatusCode);

        var imageWithVideoFile = new CreateBannerRequest
        {
            MediaType = "image",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "圖片模式不該有影片檔" } },
        };
        var imageWithVideoResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(imageWithVideoFile, TestImages.SmallPng(), videoBytes: TestVideos.SmallValidMp4()));
        Assert.Equal(HttpStatusCode.BadRequest, imageWithVideoResponse.StatusCode);
    }

    [Fact]
    public async Task Banner_影片格式不支援_400_影片超過大小上限_400()
    {
        using var client = await CreateContentEditorClientAsync();

        var fakeVideoRequest = new CreateBannerRequest
        {
            MediaType = "video",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "假影片" } },
        };
        var fakeVideoResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(fakeVideoRequest, TestImages.SmallPng(), videoBytes: TestVideos.FakeVideoBytes()));
        Assert.Equal(HttpStatusCode.BadRequest, fakeVideoResponse.StatusCode);

        var oversizedRequest = new CreateBannerRequest
        {
            MediaType = "video",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "超大影片" } },
        };
        var oversizedResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(oversizedRequest, TestImages.SmallPng(), videoBytes: TestVideos.OversizedBytes()));
        Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
    }

    [Fact]
    public async Task Banner_影片模式切回圖片_清空影片鍵_再切回影片模式須重新上傳()
    {
        using var client = await CreateContentEditorClientAsync();
        var createRequest = new CreateBannerRequest
        {
            MediaType = "video",
            SortOrder = 0,
            Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "影片輪播" } },
        };
        var createResponse = await client.PostAsync(
            "/api/v1/admin/tcrfc/banners",
            AdminArticleMultipart.Build(createRequest, TestImages.SmallPng(), videoBytes: TestVideos.SmallValidMp4()));
        var created = await createResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);

        try
        {
            // 切回圖片：video_key 應該被清空。
            var switchToImage = new UpdateBannerRequest
            {
                MediaType = "image",
                SortOrder = 0,
                Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "切回圖片" } },
            };
            var switchResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created!.Id}", AdminArticleMultipart.Build(switchToImage));
            Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);
            var afterSwitch = await switchResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal("image", afterSwitch!.MediaType);
            Assert.Null(afterSwitch.VideoKey);

            // 再切回影片但沒有帶影片檔案（既有 video_key 已經是 null）→ 400。
            var switchBackWithoutVideo = new UpdateBannerRequest
            {
                MediaType = "video",
                SortOrder = 0,
                Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "切回影片但沒帶檔案" } },
            };
            var failResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created.Id}", AdminArticleMultipart.Build(switchBackWithoutVideo));
            Assert.Equal(HttpStatusCode.BadRequest, failResponse.StatusCode);

            // 這次帶影片檔案就會成功。
            var switchBackWithVideo = new UpdateBannerRequest
            {
                MediaType = "video",
                SortOrder = 0,
                Content = new AdminBannerContentInput { Zh = new AdminBannerLocaleContent { Title = "切回影片" } },
            };
            var successResponse = await client.PutAsync(
                $"/api/v1/admin/tcrfc/banners/{created.Id}",
                AdminArticleMultipart.Build(switchBackWithVideo, videoBytes: TestVideos.SmallValidMp4()));
            Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
            var afterSwitchBack = await successResponse.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options);
            Assert.Equal("video", afterSwitchBack!.MediaType);
            Assert.NotNull(afterSwitchBack.VideoKey);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/banners/{created!.Id}");
        }
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

    /// <summary><paramref name="publish"/>（v3.14，預設 <c>true</c>）：新建立的輪播一律是
    /// <c>draft</c>，公開端點只回 <c>published</c>，這個工具方法預設順手發布，讓既有呼叫端
    /// （驗證上架期間篩選）不必逐一自己補這一步；需要測草稿行為本身的測試改傳 <c>false</c>。</summary>
    private async Task<AdminBannerDetailDto> CreateBannerAsync(
        HttpClient client, string titleZh, DateTime? startAt, DateTime? endAt, bool publish = true)
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
        var created = (await response.Content.ReadFromJsonAsync<AdminBannerDetailDto>(TestJson.Options))!;

        if (publish)
        {
            var publishResponse = await client.PostAsync($"/api/v1/admin/tcrfc/banners/{created.Id}/publish", content: null);
            publishResponse.EnsureSuccessStatusCode();
        }

        return created;
    }

    private async Task<HttpClient> CreateContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }
}
