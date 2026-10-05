using System.Net;
using Azure.Storage.Blobs;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminComics;
using Tcrfc.Api.Features.AdminDraws;
using Tcrfc.Api.Features.AdminFanEvents;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>F1 漫畫管理、F2 球迷會活動（S3-1）。含圖片的端點對真實 Azurite 驗證。</summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminCultureTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static async Task<int> CountAsync(BlobContainerClient container, string prefix)
    {
        var count = 0;
        await foreach (var _ in container.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    private static object EpisodePayload(int no, string status, string title = "【測試】集數") =>
        new { episodeNo = no, status, content = new { zh = new { title = $"{title}{no}" }, en = new { title = $"Test episode {no}" } } };

    private static readonly (string, byte[], string, string) Png = ("image", TestImages.SmallPng(), "a.png", "image/png");

    // ═════════════ F1 ═════════════

    [AzuriteFact]
    public async Task 漫畫_權限與藍鯨不設漫畫()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/comic/episodes")).StatusCode);

        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/comic/episodes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(EpisodePayload(9001, "draft")))).StatusCode);

        // 客服／行政沒有內容欄位的權限
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/tcrfc/comic/episodes")).StatusCode);

        // 系統管理員對藍鯨：授權通過，但藍鯨不設漫畫 → 403，訊息是日常中文
        using var sa = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        foreach (var path in new[] { "about", "characters", "episodes" })
        {
            var response = await sa.GetAsync($"/api/v1/admin/bw/comic/{path}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("台中藍鯨不設漫畫", await C1Test.BodyAsync(response));
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await sa.PostAsync("/api/v1/admin/bw/comic/episodes", BizTest.Multipart(EpisodePayload(1, "draft")))).StatusCode);
        // 合作球隊管理（藍鯨）連權限都沒有
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/comic/episodes")).StatusCode);
    }

    [AzuriteFact]
    public async Task 漫畫企劃設定_雙語儲存與清除()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "comic.%"); // 種子有企劃設定：改前拍照、finally 還原
        try
        {
            var saved = await C1Test.ReadAsync<AdminComicAboutDto>(await C1Test.PutJsonAsync(client, "/api/v1/admin/tcrfc/comic/about", new
            {
                zh = new { title = "【測試】世界觀", body = "這是測試世界觀說明。" }, en = new { title = "Test world", body = "Test body." },
            }));
            Assert.Equal("【測試】世界觀", saved.Zh.Title);
            Assert.Equal("Test body.", saved.En?.Body);
            var read = await C1Test.ReadAsync<AdminComicAboutDto>(await client.GetAsync("/api/v1/admin/tcrfc/comic/about"));
            Assert.Equal("這是測試世界觀說明。", read.Zh.Body);

            // 省略英文＝移除英文版
            var noEn = await C1Test.ReadAsync<AdminComicAboutDto>(await C1Test.PutJsonAsync(client, "/api/v1/admin/tcrfc/comic/about", new { zh = new { title = "只有中文" } }));
            Assert.Null(noEn.En);
            Assert.Null(noEn.Zh.Body);
        }
        finally
        {
            await restore();
        }
    }

    [AzuriteFact]
    public async Task 漫畫角色_圖片換圖刪除_關聯球員_排序()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var playerId = await C1Test.PlayerIdAsync("tcrfc");
        var ids = new List<Guid>();
        try
        {
            var first = await C1Test.ReadAsync<AdminComicCharacterDto>(await client.PostAsync("/api/v1/admin/tcrfc/comic/characters", BizTest.Multipart(
                new { playerId, content = new { zh = new { name = "【測試】角色甲", description = "設定" }, en = new { name = "Test A" } } }, Png)));
            ids.Add(first.Id);
            Assert.NotNull(first.ImageUrl);
            Assert.Equal(playerId, first.PlayerId);
            var prefix = $"tcrfc/comic/characters/{first.Id}/";
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));

            var second = await C1Test.ReadAsync<AdminComicCharacterDto>(await client.PostAsync("/api/v1/admin/tcrfc/comic/characters", BizTest.Multipart(
                new { content = new { zh = new { name = "【測試】角色乙" } } })));
            ids.Add(second.Id);
            Assert.Null(second.ImageKey);
            Assert.Equal(first.SortOrder + 1, second.SortOrder);

            // 換圖：舊物件刪除、仍是 5 個；同時給新檔與移除 → 400
            var replaced = await C1Test.ReadAsync<AdminComicCharacterDto>(await client.PutAsync($"/api/v1/admin/tcrfc/comic/characters/{first.Id}", BizTest.Multipart(
                new { playerId, content = new { zh = new { name = "【測試】角色甲" } } }, ("image", TestImages.SmallWebp(), "b.webp", "image/webp"))));
            Assert.NotEqual(first.ImageKey, replaced.ImageKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/v1/admin/tcrfc/comic/characters/{first.Id}", BizTest.Multipart(
                new { removeImage = true, content = new { zh = new { name = "x" } } }, Png))).StatusCode);

            // 關聯到別的俱樂部的球員 → 400
            var bwPlayer = await BizTest.ScalarGuidAsync("SELECT TOP 1 p.id FROM players p JOIN clubs c ON c.id = p.club_id WHERE c.code = 'bw'");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/v1/admin/tcrfc/comic/characters/{second.Id}", BizTest.Multipart(
                new { playerId = bwPlayer, content = new { zh = new { name = "x" } } }))).StatusCode);

            // 排序
            Assert.Equal(HttpStatusCode.NoContent, (await C1Test.PutJsonAsync(client, "/api/v1/admin/tcrfc/comic/characters/order", new { ids = new[] { second.Id, first.Id } })).StatusCode);
            var list = await C1Test.ReadAsync<List<AdminComicCharacterDto>>(await client.GetAsync("/api/v1/admin/tcrfc/comic/characters"));
            Assert.True(list.FindIndex(c => c.Id == second.Id) < list.FindIndex(c => c.Id == first.Id));
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(client, "/api/v1/admin/tcrfc/comic/characters/order", new { ids = new[] { Guid.NewGuid() } })).StatusCode);

            // 移除圖片、刪除角色一併刪物件；跨俱樂部 404
            var removed = await C1Test.ReadAsync<AdminComicCharacterDto>(await client.PutAsync($"/api/v1/admin/tcrfc/comic/characters/{first.Id}", BizTest.Multipart(
                new { removeImage = true, content = new { zh = new { name = "【測試】角色甲" } } })));
            Assert.Null(removed.ImageKey);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/characters/{second.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/characters/{second.Id}")).StatusCode);
            ids.Remove(second.Id);
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/characters/{id}");
            }
        }
    }

    [AzuriteFact]
    public async Task 漫畫集數_內頁批次上傳排序_最新集數自動判定_發布規則()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var ids = new List<Guid>();
        var no1 = 9101;
        var no2 = 9102;
        try
        {
            // 新集數不能直接發布（還沒有內頁）
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(EpisodePayload(no1, "published")))).StatusCode);
            var ep1 = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(EpisodePayload(no1, "draft"), ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            ids.Add(ep1.Id);
            Assert.NotNull(ep1.CoverUrl);
            Assert.False(ep1.IsLatest);
            Assert.Equal("草稿", ep1.StatusLabel);

            // 集數編號重複 → 409
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(EpisodePayload(no1, "draft")))).StatusCode);
            // 沒有內頁不能發布
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}", BizTest.Multipart(EpisodePayload(no1, "published")))).StatusCode);

            // 批次上傳三張內頁（一次請求），順序依上傳
            var pages = BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "1.png", "image/png"), ("files", TestImages.SmallWebp(), "2.webp", "image/webp"), ("files", TestImages.SmallPng(), "3.png", "image/png"));
            var withPages = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}/pages", pages));
            Assert.Equal(3, withPages.Pages.Count);
            Assert.Equal([0, 1, 2], withPages.Pages.Select(p => p.SortOrder).ToArray());
            Assert.All(withPages.Pages, p => Assert.NotNull(p.ImageUrl));
            var pagePrefix = $"tcrfc/comic/episodes/{ep1.Id}/pages/";
            Assert.Equal(15, await CountAsync(fixture.InspectorContainer, pagePrefix));
            // 假圖片 → 400，整批不留物件
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}/pages",
                BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "ok.png", "image/png"), ("files", TestImages.FakeImageBytes(), "bad.png", "image/png")))).StatusCode);
            Assert.Equal(15, await CountAsync(fixture.InspectorContainer, pagePrefix));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}/pages", BizTest.Multipart(new { }))).StatusCode);

            // 重排內頁
            var reversed = withPages.Pages.Select(p => p.Id).Reverse().ToArray();
            var reordered = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await C1Test.PutJsonAsync(client, $"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}/pages/order", new { ids = reversed }));
            Assert.Equal(reversed, reordered.Pages.Select(p => p.Id).ToArray());

            // 發布 → 自動成為最新集數（未給發布日則帶今天）
            var published = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}", BizTest.Multipart(EpisodePayload(no1, "published"))));
            Assert.True(published.IsLatest);
            Assert.NotNull(published.PublishedOn);
            Assert.Equal("已發布", published.StatusLabel);

            // 第二集發布 → 第一集不再是最新
            var ep2 = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(EpisodePayload(no2, "draft"))));
            ids.Add(ep2.Id);
            await client.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep2.Id}/pages", BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "p.png", "image/png")));
            var ep2Published = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep2.Id}", BizTest.Multipart(EpisodePayload(no2, "published"))));
            Assert.True(ep2Published.IsLatest);
            var afterList = await C1Test.ReadAsync<List<AdminComicEpisodeListItemDto>>(await client.GetAsync("/api/v1/admin/tcrfc/comic/episodes"));
            Assert.False(afterList.Single(e => e.Id == ep1.Id).IsLatest);
            Assert.Equal(3, afterList.Single(e => e.Id == ep1.Id).PageCount);
            Assert.Single(afterList, e => e.IsLatest && e.EpisodeNo is 9101 or 9102);

            // 未來的發布日：不算最新
            var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
            var scheduled = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep2.Id}",
                BizTest.Multipart(new { episodeNo = no2, status = "published", publishedOn = future.ToString("yyyy-MM-dd"), content = new { zh = new { title = "【測試】集數" } } })));
            Assert.False(scheduled.IsLatest);
            Assert.True((await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}"))).IsLatest);

            // 已發布的集數不能刪光內頁
            var cur = await C1Test.ReadAsync<AdminComicEpisodeDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep2.Id}"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep2.Id}/pages/{cur.Pages.Single().Id}")).StatusCode);

            // 刪除單張內頁一併刪物件；刪除集數一併刪封面與全部內頁
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}/pages/{reversed[0]}")).StatusCode);
            Assert.Equal(10, await CountAsync(fixture.InspectorContainer, pagePrefix));
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}")).StatusCode);
            ids.Remove(ep1.Id);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/comic/episodes/{ep1.Id}/"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/comic/episodes/{ep1.Id}")).StatusCode);
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{id}");
            }
        }
    }

    // ═════════════ F2 ═════════════

    private static object EventPayload(string status, object? extra = null, string name = "【測試】球迷活動") => new
    {
        status,
        startsAt = DateTime.UtcNow.AddDays(10).ToString("o"),
        registrationDeadlineAt = DateTime.UtcNow.AddDays(5).ToString("o"),
        capacity = 2,
        content = new { zh = new { name, description = "測試說明", location = "測試地點" }, en = new { name = "Test fan event" } },
    };

    [AzuriteFact]
    public async Task 球迷活動_CRUD_封面_回顧圖集_關聯文章_網址名稱重複()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var ids = new List<Guid>();
        try
        {
            var article1 = await C1Test.ArticleIdAsync("tcrfc", 0);
            var article2 = await C1Test.ArticleIdAsync("tcrfc", 1);
            var bwArticle = await C1Test.ArticleIdAsync("bw", 0);
            // 發布必須有開始時間
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { status = "published", content = new { zh = new { name = "【測試】無時間" } } }))).StatusCode);
            // 名額至少 1、結束不可早於開始、截止不可晚於開始
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { status = "draft", capacity = 0, content = new { zh = new { name = "x" } } }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { status = "draft", startsAt = "2030-01-02T00:00:00Z", endsAt = "2030-01-01T00:00:00Z", content = new { zh = new { name = "x" } } }))).StatusCode);

            var slug = BizTest.Unique("evt");
            var created = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new
                {
                    slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), capacity = 5, isPaidMembersOnly = true,
                    articleIds = new[] { article1, article2 }, content = new { zh = new { name = "【測試】球迷活動", description = "說明", location = "地點" }, en = new { name = "Test event" } },
                }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            ids.Add(created.Id);
            Assert.Equal(slug, created.Slug);
            Assert.NotNull(created.CoverUrl);
            Assert.Equal("已發布", created.StatusLabel);
            Assert.True(created.IsRegistrationOpen);
            Assert.Equal(2, created.Articles.Count);
            Assert.Equal("Test event", created.En?.Name);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, $"tcrfc/fan-events/{created.Id}/cover/"));

            // 網址名稱重複 → 409
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
                new { slug, status = "draft", content = new { zh = new { name = "重複" } } }))).StatusCode);
            // 關聯別的俱樂部的文章 → 400；省略 articleIds＝維持不變
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}", BizTest.Multipart(
                new { status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), articleIds = new[] { bwArticle }, content = new { zh = new { name = "x" } } }))).StatusCode);
            var keep = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}", BizTest.Multipart(
                new { slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), capacity = 5, isPaidMembersOnly = true, content = new { zh = new { name = "【測試】球迷活動改名" } } })));
            Assert.Equal(2, keep.Articles.Count);
            Assert.Null(keep.En);
            var cleared = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}", BizTest.Multipart(
                new { slug, status = "draft", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), articleIds = Array.Empty<Guid>(), content = new { zh = new { name = "【測試】球迷活動改名" } } })));
            Assert.Empty(cleared.Articles);
            Assert.Equal("草稿", cleared.StatusLabel);

            // 回顧圖集：批次上傳、排序、刪除
            var gallery = await C1Test.ReadAsync<AdminFanEventDetailDto>(await client.PostAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}/images",
                BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "1.png", "image/png"), ("files", TestImages.SmallWebp(), "2.webp", "image/webp"))));
            Assert.Equal(2, gallery.Images.Count);
            Assert.Equal(10, await CountAsync(fixture.InspectorContainer, $"tcrfc/fan-events/{created.Id}/gallery/"));
            var reversed = gallery.Images.Select(i => i.Id).Reverse().ToArray();
            var reordered = await C1Test.ReadAsync<AdminFanEventDetailDto>(await C1Test.PutJsonAsync(client, $"/api/v1/admin/tcrfc/fan-events/{created.Id}/images/order", new { ids = reversed }));
            Assert.Equal(reversed, reordered.Images.Select(i => i.Id).ToArray());
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}/images/{reversed[0]}")).StatusCode);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, $"tcrfc/fan-events/{created.Id}/gallery/"));

            // 列表與篩選
            var list = await C1Test.ReadAsync<List<AdminFanEventListItemDto>>(await client.GetAsync("/api/v1/admin/tcrfc/fan-events?status=draft&keyword=" + Uri.EscapeDataString("球迷活動改名")));
            Assert.Contains(list, e => e.Id == created.Id);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/admin/tcrfc/fan-events?status=oops")).StatusCode);

            // 刪除：一併刪封面與圖集物件；跨俱樂部 404
            using var bwClient = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            Assert.Equal(HttpStatusCode.NotFound, (await bwClient.GetAsync($"/api/v1/admin/bw/fan-events/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{created.Id}")).StatusCode);
            ids.Remove(created.Id);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, $"tcrfc/fan-events/{created.Id}/"));
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{id}");
            }
        }
    }

    [AzuriteFact]
    public async Task 球迷活動報名_名額候補_限付費會員_個資遮罩_已有報名不能刪除()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var sa = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var paid = await BizTest.ScalarGuidAsync(
            """
            SELECT TOP 1 m.id FROM members m JOIN memberships ms ON ms.member_id = m.id JOIN clubs c ON c.id = ms.club_id
            WHERE c.code = 'tcrfc' AND ms.tier = 'fan_club' AND ms.status = 'active' AND (ms.membership_end_on IS NULL OR ms.membership_end_on >= CAST(SYSUTCDATETIME() AS date))
              AND m.status = 'active' ORDER BY m.member_no
            """);
        var free = await BizTest.ScalarGuidAsync(
            """
            SELECT TOP 1 m.id FROM members m WHERE m.status = 'active' AND NOT EXISTS
              (SELECT 1 FROM memberships ms JOIN clubs c ON c.id = ms.club_id WHERE ms.member_id = m.id AND c.code = 'tcrfc' AND ms.tier = 'fan_club' AND ms.status = 'active') ORDER BY m.member_no
            """);
        var ev = await C1Test.ReadAsync<AdminFanEventDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(
            new { status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), capacity = 1, isPaidMembersOnly = true, content = new { zh = new { name = "【測試】限量活動" } } })));
        var eventId = ev.Id;
        try
        {
            var url = $"/api/v1/admin/tcrfc/fan-events/{eventId}/registrations";
            // 限付費會員：非會員、免費會員都不行
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(editor, url, new { applicantName = "路人", phone = "0900-000-111" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(editor, url, new { memberId = free })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(editor, url, new { memberId = Guid.NewGuid() })).StatusCode);

            var first = await C1Test.ReadAsync<AdminFanEventRegistrationDto>(await C1Test.PostJsonAsync(editor, url, new { memberId = paid, note = "測試備註" }));
            Assert.Equal("registered", first.Status);
            Assert.True(first.IsMember);
            Assert.True(first.IsMasked); // 內容編輯沒有解除遮罩權限
            Assert.Contains('○', first.ApplicantName!);
            // 同一位會員不能重複報名
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(editor, url, new { memberId = paid })).StatusCode);

            // 名額 1 已滿：改成開放給所有人後，第二人自動進候補
            var open = await editor.PutAsync($"/api/v1/admin/tcrfc/fan-events/{eventId}", BizTest.Multipart(
                new { slug = ev.Slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"), capacity = 1, isPaidMembersOnly = false, content = new { zh = new { name = "【測試】限量活動" } } }));
            Assert.Equal(HttpStatusCode.OK, open.StatusCode);
            var waiting = await C1Test.ReadAsync<AdminFanEventRegistrationDto>(await C1Test.PostJsonAsync(editor, url, new { applicantName = "【測試】路人", phone = "0900-000-222", email = "guest@example.com" }));
            Assert.Equal("waitlist", waiting.Status);
            Assert.False(waiting.IsMember);
            Assert.Equal("09******22", waiting.Phone);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(editor, url, new { applicantName = "無聯絡方式" })).StatusCode);

            // 候補遞補：名額滿時 409；取消第一位後才可以
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(editor, $"{url}/{waiting.Id}", new { status = "registered" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await C1Test.PutJsonAsync(editor, $"{url}/{first.Id}", new { status = "cancelled" })).StatusCode);
            var promoted = await C1Test.ReadAsync<AdminFanEventRegistrationDto>(await C1Test.PutJsonAsync(editor, $"{url}/{waiting.Id}", new { status = "registered" }));
            Assert.Equal("已報名", promoted.StatusLabel);
            var attended = await C1Test.ReadAsync<AdminFanEventRegistrationDto>(await C1Test.PutJsonAsync(editor, $"{url}/{waiting.Id}", new { status = "attended", note = "" }));
            Assert.Equal("已到場", attended.StatusLabel);
            Assert.Null(attended.Note);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(editor, $"{url}/{waiting.Id}", new { status = "oops" })).StatusCode);
            // 已取消的人不能直接標到場
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(editor, $"{url}/{first.Id}", new { status = "attended" })).StatusCode);

            // 遮罩：系統管理員看得到完整姓名；搜尋在沒有權限時只比對會員編號
            var revealed = await C1Test.ReadAsync<List<AdminFanEventRegistrationDto>>(await sa.GetAsync(url));
            Assert.All(revealed, r => Assert.False(r.IsMasked));
            Assert.Contains(revealed, r => r.ApplicantName == "【測試】路人");
            var maskedSearch = await C1Test.ReadAsync<List<AdminFanEventRegistrationDto>>(await editor.GetAsync(url + "?keyword=" + Uri.EscapeDataString("測試")));
            Assert.Empty(maskedSearch);
            var listed = await C1Test.ReadAsync<List<AdminFanEventRegistrationDto>>(await editor.GetAsync(url + "?status=cancelled"));
            Assert.Single(listed);

            // 已有報名紀錄不能刪除活動；列表的報名人數
            Assert.Equal(HttpStatusCode.Conflict, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{eventId}")).StatusCode);
            var detail = await C1Test.ReadAsync<AdminFanEventDetailDto>(await editor.GetAsync($"/api/v1/admin/tcrfc/fan-events/{eventId}"));
            Assert.Equal(1, detail.RegisteredCount); // 已到場也佔名額；已取消不算
            Assert.Equal(0, detail.WaitlistCount);
            // 跨俱樂部 404
            Assert.Equal(HttpStatusCode.NotFound, (await sa.GetAsync($"/api/v1/admin/bw/fan-events/{eventId}/registrations")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM fan_event_registrations WHERE fan_event_id = @E", ("@E", eventId));
            await editor.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{eventId}");
        }
    }

    // ═════════════ S1 商品圖集、K5 抽獎封面（圖片對真實 Azurite）═════════════

    [AzuriteFact]
    public async Task 商品圖集_批次上傳_排序_刪除單張_刪除商品一併刪物件_假圖片整批不留物件()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "img", ("M", 100, 1));
        var url = $"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/images";
        var prefix = $"tcrfc/shop/products/{made.ProductId}/";
        try
        {
            var uploaded = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.PostAsync(url, BizTest.Multipart(new { },
                ("files", TestImages.SmallPng(), "1.png", "image/png"), ("files", TestImages.SmallWebp(), "2.webp", "image/webp"))));
            Assert.Equal(2, uploaded.Images.Count);
            Assert.All(uploaded.Images, i => Assert.NotNull(i.ImageThumbUrl));
            Assert.Equal(10, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(url, BizTest.Multipart(new { },
                ("files", TestImages.SmallPng(), "ok.png", "image/png"), ("files", TestImages.FakeImageBytes(), "bad.png", "image/png")))).StatusCode);
            Assert.Equal(10, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(url, BizTest.Multipart(new { }))).StatusCode);

            var reversed = uploaded.Images.Select(i => i.Id).Reverse().ToArray();
            var reordered = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PutJsonAsync(admin, url + "/order", new { ids = reversed }));
            Assert.Equal(reversed, reordered.Images.Select(i => i.Id).ToArray());
            var list = await BizTest.ReadAsync<PagedResult<AdminProductListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/products?keyword=" + made.Slug));
            Assert.NotNull(list.Items.Single().CoverThumbUrl); // 列表封面＝排序第一張

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{url}/{reversed[0]}")).StatusCode);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{made.Variants[0].Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}")).StatusCode);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [AzuriteFact]
    public async Task 抽獎封面_上傳換圖移除_刪除草稿一併刪物件()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var code = "T-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Guid id = Guid.Empty;
        try
        {
            var created = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/draws", BizTest.Multipart(
                new { drawCode = code, content = new { zh = new { name = "【測試】封面" } } }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            id = created.Id;
            var prefix = $"tcrfc/member-draws/{id}/";
            Assert.NotNull(created.CoverUrl);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));
            var replaced = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/draws/{id}", BizTest.Multipart(
                new { drawCode = code, content = new { zh = new { name = "【測試】封面" } } }, ("cover", TestImages.SmallWebp(), "d.webp", "image/webp"))));
            Assert.NotEqual(created.CoverKey, replaced.CoverKey);
            Assert.Equal(5, await CountAsync(fixture.InspectorContainer, prefix));
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/draws/{id}", BizTest.Multipart(
                new { drawCode = code, removeCover = true, content = new { zh = new { name = "x" } } }, ("cover", TestImages.SmallPng(), "e.png", "image/png")))).StatusCode);
            var removed = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/draws/{id}", BizTest.Multipart(
                new { drawCode = code, removeCover = true, content = new { zh = new { name = "【測試】封面" } } })));
            Assert.Null(removed.CoverKey);
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
            await service.PutAsync($"/api/v1/admin/tcrfc/draws/{id}", BizTest.Multipart(
                new { drawCode = code, content = new { zh = new { name = "【測試】封面" } } }, ("cover", TestImages.SmallPng(), "f.png", "image/png")));
            Assert.Equal(HttpStatusCode.NoContent, (await service.DeleteAsync($"/api/v1/admin/tcrfc/draws/{id}")).StatusCode);
            id = Guid.Empty;
            Assert.Equal(0, await CountAsync(fixture.InspectorContainer, prefix));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM member_draws_i18n WHERE member_draw_id IN (SELECT id FROM member_draws WHERE draw_code = @C); DELETE FROM member_draws WHERE draw_code = @C", ("@C", code));
        }
    }
}
