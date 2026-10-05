using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminApp;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>行動 App 後台 M1 版本與維護、M2 內容編排與深連結、M3 推播、M4 推播裝置、M5 設定與連線檢查（App 規劃書 §8）。
/// 推播傳輸尚未串接：預設的 NotConfigured 行為與「串接後」（測試用記錄傳輸）的行為各驗證一次。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminAppTests(AdminWriteApiFixture fixture)
{
    private const string Base = "/api/v1/admin/app";

    private static string Iso(TimeSpan fromNow) => DateTimeOffset.UtcNow.Add(fromNow).ToString("O");

    // ═════════════ M1 ═════════════

    [Fact]
    public async Task M1_版本管理僅系統管理員可寫_檢視者可看_版本格式與重複_只有已上架版本能設為更新門檻_最低支援版本須二次確認()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Base + "/releases")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync(Base + "/releases")).StatusCode);
            var payload = new { platform = "android", version = "9.0.0", status = "live", content = new { zh = new { whatsNew = "ZZTEST" } } };
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(viewer, Base + "/releases", payload)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/releases", new { platform = "android", version = "v1", content = new { zh = new { } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/releases", new { platform = "web", version = "9.0.1", content = new { zh = new { } } })).StatusCode);

            var live = await AppTest.ReadAsync<AdminAppReleaseDto>(await AppTest.PostJsonAsync(admin, Base + "/releases", payload));
            Assert.Equal("已上架", live.StatusLabel);
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Base + "/releases", payload)).StatusCode);
            var testing = await AppTest.ReadAsync<AdminAppReleaseDto>(await AppTest.PostJsonAsync(admin, Base + "/releases", new { platform = "android", version = "9.5.0", status = "testing", content = new { zh = new { } } }));

            // 測試中的版本不能設為門檻；最低支援版本沒勾二次確認 409
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PutJsonAsync(admin, $"{Base}/releases/{testing.Id}/flags", new { isRecommended = true })).StatusCode);
            var unconfirmed = await AppTest.PutJsonAsync(admin, $"{Base}/releases/{live.Id}/flags", new { isMinSupported = true });
            Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
            Assert.Contains("強制", await unconfirmed.Content.ReadAsStringAsync());

            var set = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppReleaseDto>>(await AppTest.PutJsonAsync(admin, $"{Base}/releases/{live.Id}/flags", new { isMinSupported = true, confirmForceUpdate = true }));
            Assert.True(set.Value.IsMinSupported);
            Assert.False(set.EdgePublish.Published); // Cloudflare 靜態設定尚未串接：存檔成功，但如實說明
            Assert.Contains("尚未串接", set.EdgePublish.Message);

            // 每個平台至多一筆最低支援版本：設新的自動取消舊的
            var newer = await AppTest.ReadAsync<AdminAppReleaseDto>(await AppTest.PostJsonAsync(admin, Base + "/releases", new { platform = "android", version = "9.1.0", status = "live", content = new { zh = new { } } }));
            await AppTest.PutJsonAsync(admin, $"{Base}/releases/{newer.Id}/flags", new { isMinSupported = true, confirmForceUpdate = true });
            var list = await AppTest.ReadAsync<List<AdminAppReleaseDto>>(await admin.GetAsync(Base + "/releases?platform=android"));
            Assert.Equal(1, list.Count(r => r.IsMinSupported));
            Assert.True(list.Single(r => r.Version == "9.1.0").IsMinSupported);

            // 被設為門檻的版本不能下架或刪除
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PutJsonAsync(admin, $"{Base}/releases/{newer.Id}", new { platform = "android", version = "9.1.0", status = "withdrawn", content = new { zh = new { } } })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{Base}/releases/{newer.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Base}/releases/{newer.Id}", new { platform = "android", version = "9.9.9", content = new { zh = new { } } })).StatusCode); // 版本號不能改

            // 測試中的版本可以刪
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Base}/releases/{testing.Id}")).StatusCode);
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task M1_維護模式_開啟須填繁中訊息_可全域或分平台_僅系統管理員()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(viewer, Base + "/maintenance/ios", new { enabled = true, messageZh = "維護" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Base + "/maintenance/ios", new { enabled = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Base + "/maintenance/web", new { enabled = false })).StatusCode);
            var on = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppMaintenanceDto>>(await AppTest.PutJsonAsync(admin, Base + "/maintenance/ios", new { enabled = true, messageZh = "ZZTEST 維護中", messageEn = "Down" }));
            Assert.True(on.Value.Enabled);
            Assert.Equal("iOS", on.Value.ScopeLabel);
            var list = await AppTest.ReadAsync<List<AdminAppMaintenanceDto>>(await viewer.GetAsync(Base + "/maintenance"));
            Assert.Equal(3, list.Count);
            Assert.True(list.Single(m => m.Scope == "ios").Enabled);
            Assert.False(list.Single(m => m.Scope == "android").Enabled);
            var off = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppMaintenanceDto>>(await AppTest.PutJsonAsync(admin, Base + "/maintenance/ios", new { enabled = false }));
            Assert.False(off.Value.Enabled);
        }
        finally
        {
            await restore();
        }
    }

    // ═════════════ M2 ═════════════

    [Fact]
    public async Task M2_內容編排_首頁區塊固定不能刪_快捷入口新增排序_深連結格式與使用中保護_內容編輯可寫_合作球隊沒有權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        var quickOrder = await AppTest.ReadAsync<List<AdminAppLayoutItemDto>>(await admin.GetAsync(Base + "/layout/items?kind=quick_entry"));
        var originalOrder = quickOrder.OrderBy(i => i.SortOrder).Select(i => i.Id).ToList();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(Base + "/layout/items")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync(Base + "/layout/items")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync(Base + "/layout/items")).StatusCode);

            var home = await AppTest.ReadAsync<List<AdminAppLayoutItemDto>>(await admin.GetAsync(Base + "/layout/items?kind=home_section"));
            Assert.Equal(9, home.Count);
            Assert.All(home, h => Assert.True(h.IsFixed));
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{Base}/layout/items/{home[0].Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "home_section", itemKey = "zztest_x", label = new { zh = "x" } })).StatusCode);

            // 深連結：必須 tcrfc://、代碼格式與重複、使用中不能刪
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/deep-links", new { code = "zztest_a", appLink = "https://x.test", label = new { zh = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/deep-links", new { code = "Bad Code", appLink = "tcrfc://x", label = new { zh = "x" } })).StatusCode);
            var link = await AppTest.ReadAsync<AdminAppDeepLinkDto>(await AppTest.PostJsonAsync(editor, Base + "/layout/deep-links", new { code = "zztest_link", appLink = "tcrfc://zztest/{id}", webUrl = "/zh/zztest/{id}", label = new { zh = "ZZTEST 連結", en = "ZZTEST link" } }));
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Base + "/layout/deep-links", new { code = "zztest_link", appLink = "tcrfc://y", label = new { zh = "x" } })).StatusCode);

            var item = await AppTest.ReadAsync<AdminAppLayoutItemDto>(await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "quick_entry", itemKey = "zztest_entry", deepLinkId = link.Id, iconKey = "star", label = new { zh = "ZZTEST 入口", en = "ZZTEST entry" } }));
            Assert.Equal("zztest_link", item.DeepLinkCode);
            Assert.False(item.IsFixed);
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "quick_entry", itemKey = "zztest_entry", label = new { zh = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "quick_entry", itemKey = "Bad-Key", label = new { zh = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{Base}/layout/deep-links/{link.Id}")).StatusCode);

            // 排序：把新項目排到最前面，其餘維持原本相對順序
            var reordered = await AppTest.ReadAsync<List<AdminAppLayoutItemDto>>(await AppTest.PostJsonAsync(admin, Base + "/layout/items/reorder", new { kind = "quick_entry", ids = new[] { item.Id } }));
            Assert.Equal(item.Id, reordered[0].Id);
            Assert.Equal(originalOrder, reordered.Skip(1).Select(r => r.Id).ToList());
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/items/reorder", new { kind = "quick_entry", ids = new[] { home[0].Id } })).StatusCode); // 不屬於這個類型
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/layout/items/reorder", new { kind = "quick_entry", ids = new[] { item.Id, item.Id } })).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Base}/layout/items/{item.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Base}/layout/deep-links/{link.Id}")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM app_layout_items WHERE item_key LIKE 'zztest\\_%' ESCAPE '\\'");
            await BizTest.ExecuteSqlAsync("DELETE FROM app_deep_links WHERE code LIKE 'zztest\\_%' ESCAPE '\\'");
            // 排序還原（測試動過快捷入口的排序）
            for (var i = 0; i < originalOrder.Count; i++)
            {
                await BizTest.ExecuteSqlAsync("UPDATE app_layout_items SET sort_order = @S WHERE id = @I", ("@S", i), ("@I", originalOrder[i]));
            }
        }
    }

    // ═════════════ M3 ═════════════

    private static HttpContent PushPayload(string zhTitle = "ZZTEST 公告", string zhBody = "ZZTEST 內文", string? enTitle = "ZZTEST Notice", string? enBody = "ZZTEST body", object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["content"] = new { zh = new { title = zhTitle, body = zhBody }, en = enTitle is null ? null : new { title = enTitle, body = enBody } },
        };
        if (extra is not null)
        {
            foreach (var p in extra.GetType().GetProperties())
            {
                body[p.Name] = p.GetValue(extra);
            }
        }

        return BizTest.Multipart(body);
    }

    private static async Task<AdminPushMessageDto> CreatePushAsync(HttpClient client, HttpContent payload)
    {
        var response = await client.PostAsync(Base + "/push/messages", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AppTest.ReadAsync<AdminPushMessageDto>(response);
    }

    private static Task<HttpResponseMessage> PushAction(HttpClient client, Guid id, string action, object? body = null)
        => AppTest.PostJsonAsync(client, $"{Base}/push/messages/{id}/{action}", body ?? new { });

    private static Task CleanupPushAsync() => BizTest.ExecuteSqlAsync("DELETE FROM push_messages WHERE id IN (SELECT push_message_id FROM push_messages_i18n WHERE title LIKE 'ZZTEST%')");

    [Fact]
    public async Task M3_系統層阻擋_中獎通知與抽獎公布文章的推播一律擋下_正常公告不受影響()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        var article = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM articles ORDER BY row_seq");
        var slug = await C1Test.ScalarAsync<string>("SELECT slug FROM articles WHERE id = @I", ("@I", article));
        var tagId = Guid.NewGuid();
        var createdTag = false;
        try
        {
            foreach (var text in new[] { "恭喜你中獎了", "本週得獎名單", "You are the winner" })
            {
                var blocked = await pr.PostAsync(Base + "/push/messages", PushPayload(zhTitle: "ZZTEST " + text));
                Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
                Assert.Contains("最新消息", await blocked.Content.ReadAsStringAsync());
            }

            // 英文內文含中獎字樣一樣擋
            Assert.Equal(HttpStatusCode.Conflict, (await pr.PostAsync(Base + "/push/messages", PushPayload(enTitle: "ZZTEST Winner announced"))).StatusCode);

            // 深連結指向「球迷會員抽獎」標籤文章：擋（不管文案怎麼寫）
            if (await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM tags WHERE slug = @S", ("@S", "member-draw")) == 0)
            {
                await BizTest.ExecuteSqlAsync("INSERT INTO tags (id, slug) VALUES (@I, @S)", ("@I", tagId), ("@S", "member-draw"));
                createdTag = true;
            }

            var realTag = createdTag ? tagId : await BizTest.ScalarGuidAsync("SELECT id FROM tags WHERE slug = 'member-draw'");
            var alreadyTagged = await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM article_tags WHERE article_id = @A AND tag_id = @T", ("@A", article), ("@T", realTag)) > 0;
            if (!alreadyTagged)
            {
                await BizTest.ExecuteSqlAsync("INSERT INTO article_tags (article_id, tag_id) VALUES (@A, @T)", ("@A", article), ("@T", realTag));
            }

            try
            {
                var drawLink = await pr.PostAsync(Base + "/push/messages", PushPayload(zhTitle: "ZZTEST 新文章上線", extra: new { deepLink = $"tcrfc://news/{slug}" }));
                Assert.Equal(HttpStatusCode.Conflict, drawLink.StatusCode);
                var webLink = await pr.PostAsync(Base + "/push/messages", PushPayload(zhTitle: "ZZTEST 新文章上線", extra: new { deepLink = $"https://example.test/zh/news/{slug}?ref=x" }));
                Assert.Equal(HttpStatusCode.Conflict, webLink.StatusCode);

                // 自動推播（新聞發布）必須先問這個檢查
                await using var scope = fixture.Services.CreateAsyncScope();
                Assert.True(await scope.ServiceProvider.GetRequiredService<PushContentGuard>().IsMemberDrawArticleAsync(article, CancellationToken.None));
            }
            finally
            {
                if (!alreadyTagged)
                {
                    await BizTest.ExecuteSqlAsync("DELETE FROM article_tags WHERE article_id = @A AND tag_id = @T", ("@A", article), ("@T", realTag));
                }
            }

            // 沒有抽獎標籤的文章與一般公告正常
            var ok = await CreatePushAsync(pr, PushPayload(extra: new { deepLink = $"tcrfc://news/{slug}" }));
            Assert.Equal("draft", ok.Status);
        }
        finally
        {
            if (createdTag)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM tags WHERE id = @I", ("@I", tagId));
            }

            await CleanupPushAsync();
        }
    }

    [Fact]
    public async Task M3_雙人覆核_公關建立系統管理員核可_不能自己核可自己_預估人數二次確認_尚未串接時批次停在失敗並保留_串接後重送()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        RecordingPushTransport.Reset();
        try
        {
            var d1 = await AppTest.RegisterDeviceAsync(anonymous, "ios", "zh", "tok-a1");
            await AppTest.RegisterDeviceAsync(anonymous, "android", "en", "tok-a2");
            await AppTest.RegisterDeviceAsync(anonymous, "android", "zh", "tok-denied", "denied"); // 沒有推播權限，不算觸及

            var draft = await CreatePushAsync(pr, PushPayload());
            Assert.Equal("draft", draft.Status);
            Assert.Contains("submit", draft.AvailableActions);
            Assert.DoesNotContain("approve", draft.AvailableActions);

            // 預估人數只回人數（依平台與語系）
            var estimate = await AppTest.ReadAsync<AdminPushAudienceEstimateDto>(await AppTest.PostJsonAsync(pr, Base + "/push/estimate", new { audienceTier = "all" }));
            Assert.Equal(2, estimate.Total);
            Assert.Contains(estimate.Breakdown, r => r.Platform == "ios" && r.Locale == "zh-Hant" && r.Sent == 1);
            Assert.Contains(estimate.Breakdown, r => r.Platform == "android" && r.Locale == "en" && r.Sent == 1);

            // 公關可以建立與送審，不能核可（沒有 app.push.approve）
            var submitted = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(pr, draft.Id, "submit"));
            Assert.Equal("pending_review", submitted.Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await PushAction(pr, draft.Id, "approve", new { expectedAudience = 2 })).StatusCode);
            // 待覆核狀態不能修改，須先退回
            var edit = await pr.PutAsync($"{Base}/push/messages/{draft.Id}", PushPayload(zhTitle: "ZZTEST 改標題"));
            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PushAction(admin, draft.Id, "return")).StatusCode); // 退回原因必填
            var back = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(admin, draft.Id, "return", new { note = "標題請再精簡" }));
            Assert.Equal("draft", back.Status);
            Assert.Equal("標題請再精簡", back.RejectNote);
            await PushAction(pr, draft.Id, "submit");

            // 二次確認：預估人數不符 409；符合才核可
            var wrong = await PushAction(admin, draft.Id, "approve", new { expectedAudience = 99 });
            Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
            Assert.Contains("預估", await wrong.Content.ReadAsStringAsync());
            var approved = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(admin, draft.Id, "approve", new { expectedAudience = 2 }));

            // 🔴 傳輸尚未串接：核可後立刻嘗試發送 → 停在「失敗」並保留，沒有任何裝置被動到、統計是 0
            Assert.Equal("failed", approved.Status);
            Assert.Contains("尚未串接", approved.FailureMessage);
            Assert.Equal(0, approved.SentCount);
            Assert.Equal(2, approved.AudienceEstimate);
            Assert.Equal(await C1Test.ScalarAsync<Guid>("SELECT id FROM admin_users WHERE username = N'super.admin@tcrfc.test'"), approved.ReviewedBy);
            Assert.Contains("retry", approved.AvailableActions);
            Assert.Equal("valid", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", d1)));

            // 串接後重送：從游標續送，依裝置語系送出對應文案
            using var connected = fixture.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IPushTransport>();
                s.AddSingleton<IPushTransport, RecordingPushTransport>();
            }));
            using var adminConnected = await BizTest.ClientAsync(connected, "super.admin@tcrfc.test");
            var sent = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(adminConnected, draft.Id, "retry"));
            Assert.Equal("sent", sent.Status);
            Assert.Equal(2, sent.SentCount);
            Assert.Equal(2, sent.DeliveredCount);
            Assert.Equal(0, sent.FailedCount);
            Assert.NotNull(sent.SentAt);
            Assert.Contains(RecordingPushTransport.Sent, x => x.Token == "tok-a1" && x.Title == "ZZTEST 公告");
            Assert.Contains(RecordingPushTransport.Sent, x => x.Token == "tok-a2" && x.Title == "ZZTEST Notice"); // 英文裝置收到英文文案
            Assert.DoesNotContain(RecordingPushTransport.Sent, x => x.Token == "tok-denied");
            Assert.Equal(2, sent.Stats.Sum(s => s.Sent));
            Assert.Equal(2, sent.Stats.Sum(s => s.Delivered));
            Assert.Contains("不等於已經到達", sent.StatsNote);
            Assert.DoesNotContain("retry", sent.AvailableActions);
            Assert.Equal(HttpStatusCode.Conflict, (await PushAction(adminConnected, draft.Id, "retry")).StatusCode); // 已發送的批次不能重送
        }
        finally
        {
            RecordingPushTransport.Reset();
            await CleanupPushAsync();
            await AppTest.CleanupDevicesAsync();
        }
    }

    [Fact]
    public async Task M3_不能自己核可自己的批次_系統管理員建立也需要另一位覆核()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            await AppTest.RegisterDeviceAsync(anonymous, token: "tok-self");
            var draft = await CreatePushAsync(admin, PushPayload());
            await PushAction(admin, draft.Id, "submit");
            var own = await PushAction(admin, draft.Id, "approve", new { expectedAudience = 1 });
            Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
            Assert.Contains("另一位", await own.Content.ReadAsStringAsync());
            var detail = await AppTest.ReadAsync<AdminPushMessageDto>(await admin.GetAsync($"{Base}/push/messages/{draft.Id}"));
            Assert.Equal("pending_review", detail.Status);
            Assert.DoesNotContain("approve", detail.AvailableActions); // 建立者本人的畫面上沒有核可按鈕
            // B-11：建立者要有顯示名稱（不是只有 GUID）；尚未覆核者為 null。
            Assert.False(string.IsNullOrWhiteSpace(detail.CreatedByName));
            Assert.Null(detail.ReviewedByName);
            var listed = (await AppTest.ReadAsync<List<AdminPushMessageListItemDto>>(await admin.GetAsync($"{Base}/push/messages"))).Single(m => m.Id == draft.Id);
            Assert.Equal(detail.CreatedByName, listed.CreatedByName);
            Assert.Equal("cancelled", (await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(admin, draft.Id, "cancel"))).Status);
        }
        finally
        {
            await CleanupPushAsync();
            await AppTest.CleanupDevicesAsync();
        }
    }

    [Fact]
    public async Task M3_分眾_會籍層級_追蹤球隊只算推播開啟_俱樂部歸屬_發送時權杖失效標記並記為部分送出_排程到點才發送()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        using var connected = fixture.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IPushTransport>();
            s.AddSingleton<IPushTransport, RecordingPushTransport>();
        }));
        using var adminConnected = await BizTest.ClientAsync(connected, "super.admin@tcrfc.test");
        RecordingPushTransport.Reset();
        try
        {
            var follower = await AppTest.RegisterDeviceAsync(anonymous, "ios", "zh", "tok-follow");
            var muted = await AppTest.RegisterDeviceAsync(anonymous, "ios", "zh", "tok-muted");
            var member = await AppTest.RegisterDeviceAsync(anonymous, "android", "zh", "tok-member");
            var bwFollower = await AppTest.RegisterDeviceAsync(anonymous, "android", "en", "tok-bw");
            await AppTest.PutJsonAsync(anonymous, $"/api/v1/app/devices/{follower}/subscriptions", new { items = new[] { new { topicType = "team", topicValue = "D1", isFollowing = true, isPushEnabled = true } } });
            await AppTest.PutJsonAsync(anonymous, $"/api/v1/app/devices/{muted}/subscriptions", new { items = new[] { new { topicType = "team", topicValue = "D1", isFollowing = true, isPushEnabled = false } } });
            await AppTest.PutJsonAsync(anonymous, $"/api/v1/app/devices/{bwFollower}/subscriptions", new { items = new[] { new { topicType = "club", topicValue = "bw", isFollowing = true, isPushEnabled = true } } });
            await BizTest.ExecuteSqlAsync(
                "UPDATE app_devices SET member_id = (SELECT TOP 1 m.member_id FROM memberships m WHERE m.tier = 'fan_club' AND m.status = 'active' ORDER BY m.row_seq) WHERE device_install_id = @I", ("@I", member));

            async Task<int> Estimate(object audience)
                => (await AppTest.ReadAsync<AdminPushAudienceEstimateDto>(await AppTest.PostJsonAsync(pr, Base + "/push/estimate", audience))).Total;

            Assert.Equal(4, await Estimate(new { audienceTier = "all" }));
            Assert.Equal(1, await Estimate(new { audienceTeamCodes = new[] { "D1" } })); // 追蹤但關閉推播的不算
            Assert.Equal(3, await Estimate(new { audienceTier = "anonymous" }));
            Assert.Equal(1, await Estimate(new { audienceTier = "fan_club" }));
            Assert.Equal(1, await Estimate(new { audienceClubCode = "bw" })); // 追蹤藍鯨俱樂部
            Assert.Equal(0, await Estimate(new { audienceTier = "registered" })); // 唯一的會員裝置是球迷會員
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(pr, Base + "/push/estimate", new { audienceTier = "vip" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(pr, Base + "/push/estimate", new { audienceTeamCodes = new[] { "NOPE" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(pr, Base + "/push/estimate", new { audienceClubCode = "nope" })).StatusCode);

            // 發送：一個權杖被推播服務回報失效 → 該裝置權杖標記失效、批次記為部分送出
            RecordingPushTransport.Decide = token => token == "tok-member" ? PushSendOutcome.InvalidToken : PushSendOutcome.Accepted;
            var draft = await CreatePushAsync(pr, PushPayload(extra: new { audienceTier = "all" }));
            await PushAction(pr, draft.Id, "submit");
            var partial = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(adminConnected, draft.Id, "approve", new { expectedAudience = 4 }));
            Assert.Equal("partial", partial.Status);
            Assert.Equal(4, partial.SentCount);
            Assert.Equal(3, partial.DeliveredCount);
            Assert.Equal(1, partial.FailedCount);
            Assert.Equal("invalid", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", member)));
            Assert.Equal("valid", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", follower)));

            // 重送不會對已處理的裝置再送一次（游標）
            RecordingPushTransport.Reset();
            await PushAction(adminConnected, draft.Id, "retry");
            Assert.Empty(RecordingPushTransport.Sent);

            // 排程：核可時排程時間在未來 → 只是「已排程」，到點才發；取消後不會發
            var later = await CreatePushAsync(pr, PushPayload(zhTitle: "ZZTEST 排程", extra: new { audienceTeamCodes = new[] { "D1" }, scheduledAt = Iso(TimeSpan.FromHours(2)) }));
            await PushAction(pr, later.Id, "submit");
            var scheduled = await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(adminConnected, later.Id, "approve", new { expectedAudience = 1 }));
            Assert.Equal("scheduled", scheduled.Status);
            Assert.Empty(RecordingPushTransport.Sent);
            Assert.Equal(0, (await AppTest.ReadAsync<Dictionary<string, int>>(await AppTest.PostJsonAsync(adminConnected, Base + "/push/dispatch-due", new { })))["dispatched"]); // 還沒到點
            await BizTest.ExecuteSqlAsync("UPDATE push_messages SET scheduled_at = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE id = @I", ("@I", later.Id));
            Assert.Equal(1, (await AppTest.ReadAsync<Dictionary<string, int>>(await AppTest.PostJsonAsync(adminConnected, Base + "/push/dispatch-due", new { })))["dispatched"]);
            var done = await AppTest.ReadAsync<AdminPushMessageDto>(await adminConnected.GetAsync($"{Base}/push/messages/{later.Id}"));
            Assert.Equal("sent", done.Status);
            Assert.Single(RecordingPushTransport.Sent);
            Assert.Equal("ZZTEST 排程", RecordingPushTransport.Sent[0].Title);

            var cancelled = await CreatePushAsync(pr, PushPayload(zhTitle: "ZZTEST 取消", extra: new { scheduledAt = Iso(TimeSpan.FromHours(3)) }));
            await PushAction(pr, cancelled.Id, "submit");
            await PushAction(adminConnected, cancelled.Id, "approve", new { expectedAudience = 4 });
            Assert.Equal("cancelled", (await AppTest.ReadAsync<AdminPushMessageDto>(await PushAction(pr, cancelled.Id, "cancel"))).Status);
            Assert.Equal(HttpStatusCode.Conflict, (await PushAction(pr, later.Id, "cancel")).StatusCode); // 已發送的不能取消
        }
        finally
        {
            RecordingPushTransport.Reset();
            await CleanupPushAsync();
            await AppTest.CleanupDevicesAsync();
        }
    }

    [Fact]
    public async Task M3_預覽雙語_英文缺漏回退繁中_試送尚未串接如實回報_沒有符合條件的裝置不能核可_自動推播規則()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            var noEnglish = await CreatePushAsync(pr, PushPayload(enTitle: null));
            var preview = await AppTest.ReadAsync<AdminPushPreviewDto>(await pr.GetAsync($"{Base}/push/messages/{noEnglish.Id}/preview"));
            Assert.Equal("ZZTEST 公告", preview.Zh.Title);
            Assert.Null(preview.En);
            Assert.Equal("ZZTEST 公告", preview.EnEffective.Title); // 英文語系裝置實際看到的是繁中
            Assert.Contains("全部裝置", preview.AudienceSummary);

            var device = await AppTest.RegisterDeviceAsync(anonymous, token: "tok-test-send");
            var test = await AppTest.ReadAsync<AdminPushTestSendResultDto>(await AppTest.PostJsonAsync(pr, $"{Base}/push/messages/{noEnglish.Id}/test-send", new { deviceInstallIds = new[] { device } }));
            Assert.False(test.Configured);
            Assert.Contains("尚未串接", test.Message);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(pr, $"{Base}/push/messages/{noEnglish.Id}/test-send", new { deviceInstallIds = Array.Empty<string>() })).StatusCode);

            // 沒有任何符合條件的裝置：不能核可
            await AppTest.CleanupDevicesAsync();
            await PushAction(pr, noEnglish.Id, "submit");
            var none = await PushAction(admin, noEnglish.Id, "approve", new { expectedAudience = 0 });
            Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
            Assert.Contains("沒有符合條件", await none.Content.ReadAsStringAsync());

            // 草稿與已取消才能刪除
            Assert.Equal(HttpStatusCode.Conflict, (await pr.DeleteAsync($"{Base}/push/messages/{noEnglish.Id}")).StatusCode);
            await PushAction(pr, noEnglish.Id, "cancel");
            Assert.Equal(HttpStatusCode.NoContent, (await pr.DeleteAsync($"{Base}/push/messages/{noEnglish.Id}")).StatusCode);

            // 自動推播規則：預設值照規劃書（提前 2 小時、到期前 30 與 7 天、新聞預設關閉）
            var rules = await AppTest.ReadAsync<AdminPushRulesDto>(await viewer.GetAsync(Base + "/push/rules"));
            Assert.Equal(2, rules.MatchReminderHours);
            Assert.Equal([30, 7], rules.MembershipExpiryDays.ToArray());
            Assert.False(rules.Toggles.Single(t => t.Key == "news_published").Enabled);
            Assert.True(rules.Toggles.Single(t => t.Key == "match_reminder").Enabled);
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(pr, Base + "/push/rules", new { matchReminderHours = 3 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Base + "/push/rules", new { matchReminderHours = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Base + "/push/rules", new { membershipExpiryDays = new[] { 7, 7 } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Base + "/push/rules", new { toggles = new Dictionary<string, bool> { ["not_a_rule"] = true } })).StatusCode);
            var updated = await AppTest.ReadAsync<AdminPushRulesDto>(await AppTest.PutJsonAsync(admin, Base + "/push/rules", new { matchReminderHours = 4, membershipExpiryDays = new[] { 7, 30, 14 }, toggles = new Dictionary<string, bool> { ["news_published"] = true } }));
            Assert.Equal(4, updated.MatchReminderHours);
            Assert.Equal([30, 14, 7], updated.MembershipExpiryDays.ToArray());
            Assert.True(updated.Toggles.Single(t => t.Key == "news_published").Enabled);
        }
        finally
        {
            await restore();
            await CleanupPushAsync();
            await AppTest.CleanupDevicesAsync();
        }
    }

    // ═════════════ M4 ═════════════

    [Fact]
    public async Task M4_裝置清單遮罩_完整值僅系統管理員_統計含低於某版本的裝置數_失效權杖清理僅系統管理員()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await AppTest.RegisterDeviceAsync(anonymous, "ios", "zh", "secret-token-value");
            await BizTest.ExecuteSqlAsync("UPDATE app_devices SET app_version = N'6.6.6' WHERE device_install_id = @I", ("@I", id));
            var deviceRowId = await BizTest.ScalarGuidAsync("SELECT id FROM app_devices WHERE device_install_id = @I", ("@I", id));

            // 客服／行政可看（遮罩）：識別碼只留前 4 後 2、沒有任何權杖欄位
            var page = await AppTest.ReadAsync<PagedResult<AdminAppDeviceListItemDto>>(await service.GetAsync(Base + "/devices?appVersion=6.6.6"));
            var item = Assert.Single(page.Items);
            Assert.Equal(id[..4] + "****" + id[^2..], item.DeviceInstallIdMasked);
            Assert.Equal("有效", item.PushTokenStatusLabel);
            var raw = await (await service.GetAsync(Base + "/devices?appVersion=6.6.6")).Content.ReadAsStringAsync();
            Assert.DoesNotContain(id, raw);
            Assert.DoesNotContain("secret-token-value", raw);
            var detail = await AppTest.ReadAsync<AdminAppDeviceDetailDto>(await service.GetAsync($"{Base}/devices/{deviceRowId}"));
            Assert.False(detail.Revealed);
            Assert.Null(detail.PushToken);
            Assert.Null(detail.DeviceInstallId);

            // 完整值：客服 403，系統管理員看得到
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync($"{Base}/devices/{deviceRowId}?reveal=true")).StatusCode);
            var revealed = await AppTest.ReadAsync<AdminAppDeviceDetailDto>(await admin.GetAsync($"{Base}/devices/{deviceRowId}?reveal=true"));
            Assert.Equal(id, revealed.DeviceInstallId);
            Assert.Equal("secret-token-value", revealed.PushToken);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Base}/devices/{Guid.NewGuid()}")).StatusCode);

            // 篩選
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync(Base + "/devices?permission=maybe")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync(Base + "/devices?platform=windows")).StatusCode);
            var denied = await AppTest.ReadAsync<PagedResult<AdminAppDeviceListItemDto>>(await service.GetAsync(Base + "/devices?permission=denied&pageSize=100"));
            Assert.DoesNotContain(denied.Items, d => d.Id == deviceRowId);

            // 統計：版本分佈與「低於某版本」的裝置數（供決定最低支援版本）
            var stats = await AppTest.ReadAsync<AdminAppDeviceStatsDto>(await service.GetAsync(Base + "/devices/stats?platform=ios&belowVersion=7.0.0"));
            Assert.True(stats.DevicesBelowVersion >= 1);
            Assert.Contains(stats.ByVersion, v => v.Platform == "ios" && v.AppVersion == "6.6.6" && v.Count == 1);
            Assert.True(stats.ActiveLast7Days >= 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync(Base + "/devices/stats?platform=ios&belowVersion=zzz")).StatusCode);

            // 失效權杖清理：客服 403、系統管理員清掉（種子有一台失效權杖的示範裝置，測完還原）
            await BizTest.ExecuteSqlAsync("UPDATE app_devices SET push_token_status = 'invalid' WHERE device_install_id = @I", ("@I", id));
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(service, Base + "/devices/cleanup-invalid-tokens", new { })).StatusCode);
            var cleaned = await AppTest.ReadAsync<AdminAppDeviceCleanupResultDto>(await AppTest.PostJsonAsync(admin, Base + "/devices/cleanup-invalid-tokens", new { }));
            Assert.True(cleaned.TokensCleared >= 1);
            Assert.Equal("none", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", id)));
            Assert.Null(await C1Test.ScalarAsync<string>("SELECT push_token_encrypted FROM app_devices WHERE device_install_id = @I", ("@I", id)));
        }
        finally
        {
            await AppTest.CleanupDevicesAsync();
            await BizTest.ExecuteSqlAsync("UPDATE app_devices SET push_token_status = 'invalid' WHERE device_install_id = 'test-device-seed-0002'");
        }
    }

    // ═════════════ M5 ═════════════

    [Fact]
    public async Task M5_功能開關_命名規則_平台範圍_付款模式只能降級不得反向開啟_僅系統管理員可寫_檢視者可看()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Base + "/config/flags")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(viewer, Base + "/config/flags", new { flagKey = "zztest_x", isEnabled = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "BadName", isEnabled = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "noscore", isEnabled = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "zztest_x", isEnabled = true, platform = "web" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "zztest_x", stringValue = "on" })).StatusCode); // 只有付款模式是三態

            var created = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppFeatureFlagDto>>(await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "zztest_flag", isEnabled = true, platform = "android", description = "測試" }));
            Assert.Equal("Android", created.Value.PlatformLabel);
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "zztest_flag", isEnabled = false, platform = "android" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Base}/config/flags/{created.Value.Id}", new { flagKey = "zztest_other", isEnabled = true, platform = "android" })).StatusCode);
            var off = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppFeatureFlagDto>>(await AppTest.PutJsonAsync(admin, $"{Base}/config/flags/{created.Value.Id}", new { flagKey = "zztest_flag", isEnabled = false, platform = "android" }));
            Assert.False(off.Value.IsEnabled);

            // 🔴 付款模式（off／external／inapp）：不得以 external 送審後再遠端開成 inapp
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "payment_mode", stringValue = "maybe", platform = "ios" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "payment_mode", stringValue = "inapp", platform = "ios" })).StatusCode);
            var external = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppFeatureFlagDto>>(await AppTest.PostJsonAsync(admin, Base + "/config/flags", new { flagKey = "payment_mode", stringValue = "external", platform = "ios" }));
            Assert.True(external.Value.IsEnabled);
            var reverse = await AppTest.PutJsonAsync(admin, $"{Base}/config/flags/{external.Value.Id}", new { flagKey = "payment_mode", stringValue = "inapp", platform = "ios" });
            Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
            Assert.Contains("降級", await reverse.Content.ReadAsStringAsync());
            var offMode = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppFeatureFlagDto>>(await AppTest.PutJsonAsync(admin, $"{Base}/config/flags/{external.Value.Id}", new { flagKey = "payment_mode", stringValue = "off", platform = "ios" }));
            Assert.False(offMode.Value.IsEnabled);
            // 已經是 inapp（審查通過並上線後）可以降級回 external
            await BizTest.ExecuteSqlAsync("UPDATE app_feature_flags SET string_value = 'inapp' WHERE id = @I", ("@I", external.Value.Id));
            var down = await AppTest.ReadAsync<AppConfigChangeDto<AdminAppFeatureFlagDto>>(await AppTest.PutJsonAsync(admin, $"{Base}/config/flags/{external.Value.Id}", new { flagKey = "payment_mode", stringValue = "external", platform = "ios" }));
            Assert.Equal("external", down.Value.StringValue);

            Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"{Base}/config/flags/{created.Value.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Base}/config/flags/{created.Value.Id}")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM app_feature_flags WHERE flag_key = 'payment_mode' AND platform = 'ios'");
            await restore();
        }
    }

    [Fact]
    public async Task M5_憑證列管_只存列管資訊_無屆期日以輪替週期為基準_屆期前六十天告警_輪替更新日期_僅系統管理員()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            var today = TaiwanClock.Today;
            string D(int days) => today.AddDays(days).ToString("yyyy-MM-dd");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Base + "/config/credentials")).StatusCode); // 憑證列管僅系統管理員
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/credentials", new { kind = "secret_sauce", label = "ZZTEST x", createdOn = D(-1) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/credentials", new { kind = "apns_key", label = "ZZTEST x", createdOn = D(-10), expiresOn = D(-20) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Base + "/config/credentials", new { kind = "apns_key", label = "ZZTEST x", createdOn = D(-10), rotationPeriodDays = 0 })).StatusCode);

            async Task<AdminAppCredentialDto> Create(string label, object extra)
            {
                var body = new Dictionary<string, object?> { ["kind"] = "apns_key", ["label"] = "ZZTEST " + label, ["createdOn"] = D(-400) };
                foreach (var p in extra.GetType().GetProperties()) { body[p.Name] = p.GetValue(extra); }
                return await AppTest.ReadAsync<AdminAppCredentialDto>(await AppTest.PostJsonAsync(admin, Base + "/config/credentials", body));
            }

            var ok = await Create("正常", new { expiresOn = D(200) });
            var soon = await Create("即將屆期", new { expiresOn = D(59) });
            var overdue = await Create("已屆期", new { expiresOn = D(-1) });
            var byPeriod = await Create("週期已過", new { rotationPeriodDays = 365, lastRotatedOn = D(-366) }); // 沒有屆期日，以上次輪替日＋週期為基準
            var periodOk = await Create("週期內", new { rotationPeriodDays = 365, lastRotatedOn = D(-30) });
            var untracked = await Create("未列管期限", new { });
            Assert.Equal("ok", ok.Health);
            Assert.Equal("due_soon", soon.Health);
            Assert.Equal(59, soon.DaysUntilDue);
            Assert.Equal("overdue", overdue.Health);
            Assert.Equal("overdue", byPeriod.Health);
            Assert.Equal(today.AddDays(-1), byPeriod.NextDueOn);
            Assert.Equal("ok", periodOk.Health);
            Assert.Equal("untracked", untracked.Health);

            // 清單依下次屆期日排序（最急的在前），沒有期限的排最後
            var list = (await AppTest.ReadAsync<List<AdminAppCredentialDto>>(await admin.GetAsync(Base + "/config/credentials"))).Where(c => c.Label.StartsWith("ZZTEST")).ToList();
            Assert.Equal("ZZTEST 未列管期限", list[^1].Label);
            Assert.True(list.IndexOf(list.Single(c => c.Id == overdue.Id)) < list.IndexOf(list.Single(c => c.Id == soon.Id)));

            // 輪替：上次輪替日＝今天；屆期日不早於今天
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, $"{Base}/config/credentials/{overdue.Id}/rotate?newExpiresOn={D(-5)}", new { })).StatusCode);
            var rotated = await AppTest.ReadAsync<AdminAppCredentialDto>(await AppTest.PostJsonAsync(admin, $"{Base}/config/credentials/{byPeriod.Id}/rotate", new { }));
            Assert.Equal(today, rotated.LastRotatedOn);
            Assert.Equal("ok", rotated.Health);
            Assert.Equal(today.AddDays(365), rotated.NextDueOn);
            var withNewExpiry = await AppTest.ReadAsync<AdminAppCredentialDto>(await AppTest.PostJsonAsync(admin, $"{Base}/config/credentials/{overdue.Id}/rotate?newExpiresOn={D(365)}", new { }));
            Assert.Equal("ok", withNewExpiry.Health);
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PostJsonAsync(admin, $"{Base}/config/credentials/{Guid.NewGuid()}/rotate", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Base}/config/credentials/{untracked.Id}")).StatusCode);
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task M5_連線檢查_尚未串接與異常分開顯示_串接後推播傳輸轉為正常_憑證屆期納入檢查()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(Base + "/config/connection-check")).StatusCode);
            var check = await AppTest.ReadAsync<AdminAppConnectionCheckDto>(await viewer.GetAsync(Base + "/config/connection-check"));
            AdminAppConnectionCheckItemDto Item(string key) => check.Items.Single(i => i.Key == key);
            Assert.Equal("ok", Item("database").Status);
            Assert.Equal("not_configured", Item("push_transport").Status);
            Assert.Equal("尚未串接", Item("push_transport").StatusLabel);
            Assert.Equal("not_configured", Item("edge_config").Status);

            await AppTest.PostJsonAsync(admin, Base + "/config/credentials", new { kind = "fcm_credential", label = "ZZTEST 已屆期", createdOn = TaiwanClock.Today.AddDays(-400).ToString("yyyy-MM-dd"), expiresOn = TaiwanClock.Today.AddDays(-1).ToString("yyyy-MM-dd") });
            Assert.Equal("error", (await AppTest.ReadAsync<AdminAppConnectionCheckDto>(await viewer.GetAsync(Base + "/config/connection-check"))).Items.Single(i => i.Key == "credentials").Status);

            using var connected = fixture.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<IPushTransport>();
                s.AddSingleton<IPushTransport, RecordingPushTransport>();
            }));
            using var viewerConnected = await BizTest.ClientAsync(connected, "viewer@tcrfc.test");
            var ok = await AppTest.ReadAsync<AdminAppConnectionCheckDto>(await viewerConnected.GetAsync(Base + "/config/connection-check"));
            Assert.Equal("ok", ok.Items.Single(i => i.Key == "push_transport").Status);
            Assert.Empty(RecordingPushTransport.Sent); // 連線檢查不會真的送出任何推播
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task M5_診斷回報_清單篩選_詳情_處理狀態_權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var id = Guid.NewGuid();
        try
        {
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO app_diagnostic_reports (id, platform, app_version, occurred_at, report_type, summary, detail, status) VALUES (@I, N'android', N'6.1.1', SYSUTCDATETIME(), N'crash', N'ZZTEST 崩潰', N'堆疊細節', N'new')", ("@I", id));
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(Base + "/diagnostics")).StatusCode);
            var page = await AppTest.ReadAsync<PagedResult<AdminAppDiagnosticListItemDto>>(await viewer.GetAsync(Base + "/diagnostics?type=crash&status=new&platform=android&appVersion=6.1.1"));
            var item = Assert.Single(page.Items);
            Assert.Equal("崩潰", item.ReportTypeLabel);
            Assert.Equal("新回報", item.StatusLabel);
            Assert.DoesNotContain("堆疊細節", await (await viewer.GetAsync(Base + "/diagnostics?appVersion=6.1.1")).Content.ReadAsStringAsync()); // 清單不含技術細節
            Assert.Equal("堆疊細節", (await AppTest.ReadAsync<AdminAppDiagnosticDetailDto>(await viewer.GetAsync($"{Base}/diagnostics/{id}"))).Detail);
            Assert.Equal(HttpStatusCode.BadRequest, (await viewer.GetAsync(Base + "/diagnostics?type=weird")).StatusCode);

            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(viewer, $"{Base}/diagnostics/{id}/status", new { status = "resolved" })).StatusCode);
            var resolved = await AppTest.ReadAsync<AdminAppDiagnosticListItemDto>(await AppTest.PutJsonAsync(admin, $"{Base}/diagnostics/{id}/status", new { status = "resolved" }));
            Assert.Equal("已解決", resolved.StatusLabel);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Base}/diagnostics/{id}/status", new { status = "zzz" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PutJsonAsync(admin, $"{Base}/diagnostics/{Guid.NewGuid()}/status", new { status = "new" })).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM app_diagnostic_reports WHERE id = @I", ("@I", id));
        }
    }
}
