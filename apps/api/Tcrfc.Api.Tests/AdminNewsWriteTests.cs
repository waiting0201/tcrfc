using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 後台新聞（B2）寫入垂直切片的自動化回歸測試——涵蓋俱樂部範圍、共用內容唯讀、slug 重複、
/// 並行衝突、三態轉換、雙語側表。全部打真正的 HTTP 管線、真正的 <c>tcrfc_club_dev</c>，
/// 不 mock，跟這個測試專案既有的紀律一致。每個測試自己建立、自己清乾淨（DELETE 或直接 SQL），
/// 不依賴特定執行順序、也不依賴種子資料當下的確切筆數。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminNewsWriteTests(AdminWriteApiFixture fixture)
{
    private const string CategoryCode = "club"; // 種子資料確認存在的分類代碼（見 apps/api/README.md）

    // 🔴 本輪（slug 保留字驗證）改動：原本用 CallerMemberName 把測試方法名稱（中文，含底線）
    // 直接嵌進網址名稱，這在新增 SlugPolicy 格式驗證（只准小寫英文字母、數字、連字號）之後
    // 一律會被擋成 400——不是這裡的規則錯了，是舊版產生器本來就沒有遵守「合法網址名稱」的形狀，
    // 只是在格式驗證出現之前沒有任何東西會發現。改成純 ASCII、不帶呼叫端方法名稱的亂數字串。
    private static string UniqueSlug()
        => $"admin-write-test-{Guid.NewGuid():N}";

    private static CreateArticleRequest NewDraftRequest(string slug, string title = "測試文章標題")
        => new()
        {
            Slug = slug,
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = title } },
        };

    private async Task<AdminArticleDetailDto> CreateDraftAsync(HttpClient client, string club, string slug)
    {
        var response = await client.PostAsync($"/api/v1/admin/{club}/news", AdminArticleMultipart.Build(NewDraftRequest(slug)));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        return created!;
    }

    private static async Task DeleteBestEffortAsync(HttpClient client, string club, Guid id, DateTime expectedUpdatedAt)
    {
        var url = $"/api/v1/admin/{club}/news/{id}?expectedUpdatedAt={Uri.EscapeDataString(expectedUpdatedAt.ToString("o"))}";
        await client.DeleteAsync(url);
        // best-effort：測試清理不因為刪除失敗（例如測試本身已經把它刪過）而讓整支測試炸掉。
    }

    [Fact]
    public async Task 完整生命週期_建立草稿到刪除()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var slug = UniqueSlug();

        // 1. 建立草稿
        var created = await CreateDraftAsync(client, "tcrfc", slug);
        Assert.Equal("draft", created.Status);
        Assert.Equal(slug, created.Slug);
        Assert.False(created.IsShared);
        Assert.Null(created.PublishedAt);

        try
        {
            // 2. 修改內容（PUT，不改狀態）。🔴 S0-8 修正後封面圖片只能透過真的上傳檔案或
            // RemoveCover 改變（見 CoverKeyUpdate），不能再塞任意字串——這個生命週期測試用的是
            // 沒有接 Azurite 的 AdminWriteApiFixture，不夾檔案（維持 CoverKey 不變＝Keep），
            // 封面圖片上傳的實際行為（真的寫進物件儲存、換圖清舊物件）改在
            // AdminNewsCoverBlobCleanupTests（Azurite-enabled fixture）驗證，覆蓋範圍沒有縮小，
            // 只是搬到更適合的測試檔。
            var updateRequest = new UpdateArticleRequest
            {
                Slug = slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput
                {
                    Zh = new AdminArticleLocaleContent { Title = "改過的標題", Summary = "改過的摘要" },
                },
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(updated);
            Assert.Equal("改過的標題", updated!.Zh.Title);
            Assert.Null(updated.CoverKey); // 沒有夾檔案、RemoveCover 預設 false → 維持不變（仍是 null）
            Assert.True(updated.UpdatedAt > created.UpdatedAt, "更新後 updated_at 應該往前推進");

            // 3. 排程發布
            var publishAt = DateTime.UtcNow.AddMinutes(30);
            var scheduleResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/schedule",
                new ScheduleArticleRequest { ExpectedUpdatedAt = updated.UpdatedAt, PublishAt = publishAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
            var scheduled = await scheduleResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(scheduled);
            Assert.Equal("scheduled", scheduled!.Status);
            Assert.NotNull(scheduled.PublishedAt);

            // 4. 發布（立即生效，published_at 改成現在）
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = scheduled.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(published);
            Assert.Equal("published", published!.Status);
            Assert.NotNull(published.PublishedAt);
            Assert.True(published.PublishedAt <= DateTime.UtcNow.AddSeconds(1));

            // 5. 刪除
            var deleteUrl = $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(published.UpdatedAt.ToString("o"))}";
            var deleteResponse = await client.DeleteAsync(deleteUrl);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var afterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/news/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
        finally
        {
            // 保險清理：上面任何一步斷言失敗中途跳出時，仍嘗試把測試資料清掉（用當下能拿到的最新版本）。
            var probe = await client.GetAsync($"/api/v1/admin/tcrfc/news/{created.Id}");
            if (probe.StatusCode == HttpStatusCode.OK)
            {
                var current = await probe.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
                if (current is not null)
                {
                    await DeleteBestEffortAsync(client, "tcrfc", created.Id, current.UpdatedAt);
                }
            }
        }
    }

    [Fact]
    public async Task 建立文章_分類代碼不存在_回400()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var request = NewDraftRequest(UniqueSlug()) with { CategoryCode = "not-a-real-category" };

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 建立文章_中文標題空白_回400()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var request = new CreateArticleRequest
        {
            Slug = UniqueSlug(),
            CategoryCode = CategoryCode,
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "   " } },
        };

        var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 建立文章_網址名稱重複_回409()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var slug = UniqueSlug();
        var first = await CreateDraftAsync(client, "tcrfc", slug);

        try
        {
            var second = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(NewDraftRequest(slug)));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", first.Id, first.UpdatedAt);
        }
    }

    [Fact]
    public async Task 俱樂部範圍_用另一俱樂部路由更新_回404()
    {
        using var client = fixture.CreateClient();
        // 🔴 本輪（S1）起改走真實授權：content.editor@tcrfc.test 只被授權 tcrfc，換成 bw 路由會先在
        // IAdminClubAuthorizer 那一關被擋下（403），根本到不了 repository 的 club_id 過濾邏輯——
        // 這條測試原本要驗的是「repository 層的 WHERE club_id 過濾」本身，改用略過範圍檢查的
        // super.admin@tcrfc.test（is_super_admin=true）才能讓請求真的走到 repository，
        // 驗證找不到（404）而不是被授權層擋下（403）。授權層本身的擋下行為另有專門測試
        // （見 AdminClubAuthorizerTests 的「own_clubs 角色打別的俱樂部」情境）。
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var updateRequest = new UpdateArticleRequest
            {
                Slug = created.Slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "從藍鯨路由偷改" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };

            // 這篇文章屬於 tcrfc，改用 bw 的路由更新——WHERE club_id = @ClubId 應該找不到而回 404，
            // 不是「找到了但沒權限」的 403（不透露這個 id 存在於別的俱樂部）。
            var response = await client.PutAsync($"/api/v1/admin/bw/news/{created.Id}", AdminArticleMultipart.Build(updateRequest));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // 用 bw 路由讀單篇也一樣是 404。
            var getResponse = await client.GetAsync($"/api/v1/admin/bw/news/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            // 確認本尊沒有真的被改到。
            var stillTcrfc = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal("測試文章標題", stillTcrfc!.Zh.Title);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 俱樂部範圍_用另一俱樂部路由刪除_回404且本尊仍在()
    {
        using var client = fixture.CreateClient();
        // 同上一個測試的理由：用 super.admin@tcrfc.test 略過授權層的俱樂部範圍檢查，
        // 才能驗到 repository 層本身的 club_id 過濾。
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var deleteUrl = $"/api/v1/admin/bw/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}";
            var response = await client.DeleteAsync(deleteUrl);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var stillThere = await client.GetAsync($"/api/v1/admin/tcrfc/news/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 共用內容_更新回403()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var sharedId = await InsertSharedArticleDirectlyAsync();

        try
        {
            var updateRequest = new UpdateArticleRequest
            {
                Slug = $"shared-{sharedId:N}",
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "想改共用內容" } },
                ExpectedUpdatedAt = DateTime.UtcNow,
            };

            var response = await client.PutAsync($"/api/v1/admin/tcrfc/news/{sharedId}", AdminArticleMultipart.Build(updateRequest));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await DeleteSharedArticleDirectlyAsync(sharedId);
        }
    }

    [Fact]
    public async Task 共用內容_刪除回403()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var sharedId = await InsertSharedArticleDirectlyAsync();

        try
        {
            var deleteUrl = $"/api/v1/admin/tcrfc/news/{sharedId}?expectedUpdatedAt={Uri.EscapeDataString(DateTime.UtcNow.ToString("o"))}";
            var response = await client.DeleteAsync(deleteUrl);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await DeleteSharedArticleDirectlyAsync(sharedId);
        }
    }

    [Fact]
    public async Task 共用內容_後台可以讀到但標記為IsShared()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var sharedId = await InsertSharedArticleDirectlyAsync();

        try
        {
            var detail = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{sharedId}", TestJson.Options);
            Assert.NotNull(detail);
            Assert.True(detail!.IsShared);
        }
        finally
        {
            await DeleteSharedArticleDirectlyAsync(sharedId);
        }
    }

    [Fact]
    public async Task 樂觀並行控制_用過期的updatedAt更新_回409()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            // 先成功寫入一次，讓資料庫的 updated_at 往前推進。
            var firstUpdate = new UpdateArticleRequest
            {
                Slug = created.Slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "第一次修改" } },
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var firstResponse = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(firstUpdate));
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            var afterFirst = await firstResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(afterFirst);
            Assert.True(afterFirst!.UpdatedAt > created.UpdatedAt);

            // 再用「建立時那個已經過期的 updatedAt」寫第二次——模擬兩個編輯者同時打開同一篇。
            var staleUpdate = firstUpdate with { ExpectedUpdatedAt = created.UpdatedAt, Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "過期的第二次修改" } } };
            var staleResponse = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(staleUpdate));

            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

            // ⛔ 不是「後寫的贏」：資料庫裡應該仍是第一次修改的內容，不是過期請求的內容。
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal("第一次修改", current!.Zh.Title);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, "tcrfc", created.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 三態轉換_已發布的文章不能再排程_回409()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var publishResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
                new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
                TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            var published = await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(published);

            var scheduleResponse = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/schedule",
                new ScheduleArticleRequest { ExpectedUpdatedAt = published!.UpdatedAt, PublishAt = DateTime.UtcNow.AddHours(1) },
                TestJson.WriteOptions);

            Assert.Equal(HttpStatusCode.Conflict, scheduleResponse.StatusCode);

            // 狀態應該還是 published，不是被改成 scheduled。
            var current = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            Assert.Equal("published", current!.Status);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, "tcrfc", created.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 排程時間不在未來_回400()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());

        try
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}/schedule",
                new ScheduleArticleRequest { ExpectedUpdatedAt = created.UpdatedAt, PublishAt = DateTime.UtcNow.AddMinutes(-5) },
                TestJson.WriteOptions);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteBestEffortAsync(client, "tcrfc", created.Id, created.UpdatedAt);
        }
    }

    [Fact]
    public async Task 雙語側表_只給中文_更新加上英文_再更新省略英文會清空()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var created = await CreateDraftAsync(client, "tcrfc", UniqueSlug());
        Assert.Null(created.En);

        try
        {
            // 加上英文
            var withEnglish = new UpdateArticleRequest
            {
                Slug = created.Slug,
                CategoryCode = CategoryCode,
                IsFeatured = false,
                Content = new AdminArticleContentInput
                {
                    Zh = new AdminArticleLocaleContent { Title = "中文標題" },
                    En = new AdminArticleLocaleContent { Title = "English Title" },
                },
                ExpectedUpdatedAt = created.UpdatedAt,
            };
            var response1 = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(withEnglish));
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            var afterAddEnglish = await response1.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(afterAddEnglish);
            Assert.Equal("English Title", afterAddEnglish!.En?.Title);

            // 省略英文＝清空（PUT 是整份取代語意，見 DTO 上的註解）
            var withoutEnglish = withEnglish with { Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "中文標題" } }, ExpectedUpdatedAt = afterAddEnglish.UpdatedAt };
            var response2 = await client.PutAsync($"/api/v1/admin/tcrfc/news/{created.Id}", AdminArticleMultipart.Build(withoutEnglish));
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            var afterRemoveEnglish = await response2.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
            Assert.NotNull(afterRemoveEnglish);
            Assert.Null(afterRemoveEnglish!.En);
        }
        finally
        {
            var probe = await client.GetFromJsonAsync<AdminArticleDetailDto>($"/api/v1/admin/tcrfc/news/{created.Id}", TestJson.Options);
            if (probe is not null)
            {
                await DeleteBestEffortAsync(client, "tcrfc", created.Id, probe.UpdatedAt);
            }
        }
    }

    [Fact]
    public async Task 置頂精選_超過3篇回409()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Tcrfc.Api.Tests.Fixtures.TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        var alreadyFeatured = await CountFeaturedAsync("tcrfc");
        var createdIds = new List<(Guid Id, DateTime UpdatedAt)>();

        try
        {
            // 補到剛好 3 篇置頂（不管目前已經有幾篇，補到上限，行為不依賴其他測試留下的狀態）。
            for (var i = alreadyFeatured; i < 3; i++)
            {
                var request = new CreateArticleRequest
                {
                    Slug = UniqueSlug() + $"-{i}",
                    CategoryCode = CategoryCode,
                    IsFeatured = true,
                    Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"置頂測試 {i}" } },
                };
                var response = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(request));
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                var created = await response.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options);
                Assert.NotNull(created);
                createdIds.Add((created!.Id, created.UpdatedAt));
            }

            // 第 4 篇（超過上限）應該被擋下。
            var overLimitRequest = new CreateArticleRequest
            {
                Slug = UniqueSlug() + "-over-limit",
                CategoryCode = CategoryCode,
                IsFeatured = true,
                Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = "超過上限" } },
            };
            var overLimitResponse = await client.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(overLimitRequest));
            Assert.Equal(HttpStatusCode.Conflict, overLimitResponse.StatusCode);
        }
        finally
        {
            foreach (var (id, updatedAt) in createdIds)
            {
                await DeleteBestEffortAsync(client, "tcrfc", id, updatedAt);
            }
        }
    }

    // ───────────────────────────── 測試專用的直接 SQL 工具 ─────────────────────────────
    // 共用內容（club_id IS NULL）目前的種子資料裡一筆都沒有（見 apps/api/README.md），
    // 這組寫入端點本身也「刻意不給任何管道建立共用內容」（README／repository 上都有說明），
    // 所以要驗證「共用內容唯讀」只能繞過 API 直接插一筆測試用資料，測完立刻刪乾淨。

    private static async Task<Guid> InsertSharedArticleDirectlyAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        var articleId = Guid.NewGuid();
        var categoryId = await ScalarAsync<Guid>(connectionString, "SELECT TOP 1 id FROM article_categories WHERE code = @Code", ("@Code", CategoryCode));

        await ExecuteAsync(connectionString, """
            INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
            VALUES (@Id, NULL, @Slug, @CategoryId, 'published', SYSUTCDATETIME());
            """,
            ("@Id", articleId), ("@Slug", $"shared-test-{articleId:N}"), ("@CategoryId", categoryId));

        await ExecuteAsync(connectionString, """
            INSERT INTO articles_i18n (article_id, locale, title)
            VALUES (@Id, N'zh-Hant', N'測試用共用文章');
            """,
            ("@Id", articleId));

        return articleId;
    }

    private static async Task DeleteSharedArticleDirectlyAsync(Guid articleId)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await ExecuteAsync(connectionString, "DELETE FROM articles_i18n WHERE article_id = @Id;", ("@Id", articleId));
        await ExecuteAsync(connectionString, "DELETE FROM articles WHERE id = @Id;", ("@Id", articleId));
    }

    private static async Task<int> CountFeaturedAsync(string clubCode)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        return await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM articles a JOIN clubs c ON c.id = a.club_id
            WHERE c.code = @Code AND a.is_featured = 1;
            """,
            ("@Code", clubCode));
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private static async Task ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
