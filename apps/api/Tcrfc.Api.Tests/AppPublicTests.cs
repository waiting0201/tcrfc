using System.Net;
using System.Text.Json;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AdminApp;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>行動 App 公開端點（App 規劃書 §9.2）：裝置註冊、追蹤／推播訂閱、設定、內容編排、通知中心、廣告投放與事件、診斷回報。
/// 全部匿名呼叫；官網與 App 共用同一套 API（B-14）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppPublicTests(AdminWriteApiFixture fixture)
{
    private const string App = "/api/v1/app";
    private const string Ads = "/api/v1/admin/ads";

    private static string Iso(TimeSpan fromNow) => DateTimeOffset.UtcNow.Add(fromNow).ToString("O");

    // ═════════════ 裝置 ═════════════

    [Fact]
    public async Task 裝置註冊_格式驗證_更新不重複建立_權杖加密儲存_同一權杖換裝置時舊列失效()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, $"{App}/devices/short", new { platform = "ios" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{AppTest.NewDeviceId()}", new { platform = "windows" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{AppTest.NewDeviceId()}", new { platform = "ios", appVersion = "abc" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{AppTest.NewDeviceId()}", new { platform = "ios", pushPermission = "maybe" })).StatusCode);

            var id = AppTest.NewDeviceId();
            var created = await AppTest.ReadAsync<AppDeviceRegisteredDto>(await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{id}", new
            {
                platform = "android", osVersion = "14", appVersion = "1.2.3", locale = "en", pushToken = "plain-secret-token-abc", pushPermission = "granted",
            }));
            Assert.True(created.IsNew);
            Assert.Equal("valid", created.PushTokenStatus);

            // 🔴 推播權杖視同個資：資料庫裡只有密文與雜湊，沒有明文
            var stored = await C1Test.ScalarAsync<string>("SELECT push_token_encrypted FROM app_devices WHERE device_install_id = @I", ("@I", id));
            Assert.NotNull(stored);
            Assert.DoesNotContain("plain-secret-token-abc", stored);
            Assert.Equal(64, (await C1Test.ScalarAsync<string>("SELECT push_token_hash FROM app_devices WHERE device_install_id = @I", ("@I", id)))!.Length);
            Assert.Equal("en", await C1Test.ScalarAsync<string>("SELECT locale FROM app_devices WHERE device_install_id = @I", ("@I", id)));

            // 再註冊同一台：更新，不新增；沒帶權杖不動既有權杖
            var again = await AppTest.ReadAsync<AppDeviceRegisteredDto>(await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{id}", new { platform = "android", appVersion = "1.2.4", pushPermission = "denied" }));
            Assert.False(again.IsNew);
            Assert.Equal("valid", again.PushTokenStatus);
            Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM app_devices WHERE device_install_id = @I", ("@I", id)));
            Assert.Equal("1.2.4", await C1Test.ScalarAsync<string>("SELECT app_version FROM app_devices WHERE device_install_id = @I", ("@I", id)));
            Assert.Equal("denied", await C1Test.ScalarAsync<string>("SELECT push_permission FROM app_devices WHERE device_install_id = @I", ("@I", id)));

            // 換機／重裝：同一個權杖出現在新的裝置列，舊列的權杖失效
            var newId = AppTest.NewDeviceId();
            await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{newId}", new { platform = "android", pushToken = "plain-secret-token-abc", pushPermission = "granted" });
            Assert.Equal("invalid", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", id)));
            Assert.Null(await C1Test.ScalarAsync<string>("SELECT push_token_encrypted FROM app_devices WHERE device_install_id = @I", ("@I", id)));
            Assert.Equal("valid", await C1Test.ScalarAsync<string>("SELECT push_token_status FROM app_devices WHERE device_install_id = @I", ("@I", newId)));
        }
        finally
        {
            await AppTest.CleanupDevicesAsync();
        }
    }

    [Fact]
    public async Task 追蹤與推播訂閱_對象必須存在_追蹤與推播分離_完整取代_未註冊裝置404()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await AppTest.RegisterDeviceAsync(anonymous);
            var url = $"{App}/devices/{id}/subscriptions";
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PutJsonAsync(anonymous, $"{App}/devices/{AppTest.NewDeviceId()}/subscriptions", new { items = Array.Empty<object>() })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, url, new { items = new[] { new { topicType = "team", topicValue = "NOPE", isFollowing = true, isPushEnabled = true } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, url, new { items = new[] { new { topicType = "planet", topicValue = "x" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(anonymous, url, new { items = new[] { new { topicType = "team", topicValue = "D1" }, new { topicType = "team", topicValue = "D1" } } })).StatusCode);

            // 追蹤但不推播是合法組合
            var list = await AppTest.ReadAsync<List<AppSubscriptionDto>>(await AppTest.PutJsonAsync(anonymous, url, new
            {
                items = new object[]
                {
                    new { topicType = "team", topicValue = "D1", isFollowing = true, isPushEnabled = false },
                    new { topicType = "club", topicValue = "bw", isFollowing = true, isPushEnabled = true },
                    new { topicType = "news_category", topicValue = "club", isFollowing = true, isPushEnabled = true },
                },
            }));
            Assert.Equal(3, list.Count);
            Assert.False(list.Single(s => s.TopicType == "team").IsPushEnabled);
            Assert.True(list.Single(s => s.TopicType == "team").IsFollowing);

            // 只更新列出的項目；ReplaceAll 才會移除沒列出的
            var updated = await AppTest.ReadAsync<List<AppSubscriptionDto>>(await AppTest.PutJsonAsync(anonymous, url, new { items = new[] { new { topicType = "team", topicValue = "D1", isFollowing = true, isPushEnabled = true } } }));
            Assert.Equal(3, updated.Count);
            Assert.True(updated.Single(s => s.TopicType == "team").IsPushEnabled);
            var replaced = await AppTest.ReadAsync<List<AppSubscriptionDto>>(await AppTest.PutJsonAsync(anonymous, url, new { replaceAll = true, items = new[] { new { topicType = "club", topicValue = "tcrfc" } } }));
            var only = Assert.Single(replaced);
            Assert.Equal("tcrfc", only.TopicValue);
            var got = await anonymous.GetAsync(url);
            Assert.True(got.Headers.CacheControl!.NoStore && got.Headers.CacheControl.Private);
            Assert.Single(await AppTest.ReadAsync<List<AppSubscriptionDto>>(got));
        }
        finally
        {
            await AppTest.CleanupDevicesAsync();
        }
    }

    // ═════════════ 設定 ═════════════

    [Fact]
    public async Task 設定_最低支援版本與建議版本判斷_維護模式_平台開關優先於全部_可邊緣快取()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var restore = await AppTest.SnapshotAppStateAsync();
        try
        {
            async Task<AdminAppReleaseDto> Release(string platform, string version)
                => await AppTest.ReadAsync<AdminAppReleaseDto>(await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/releases", new
                {
                    platform, version, buildNumber = "1", status = "live", content = new { zh = new { whatsNew = "ZZTEST 更新", forceMessage = "請更新", recommendMessage = "有新版" } },
                }));

            var min = await Release("ios", "9.0.0");
            var rec = await Release("ios", "9.2.0");
            await AppTest.PutJsonAsync(admin, $"/api/v1/admin/app/releases/{min.Id}/flags", new { isMinSupported = true, confirmForceUpdate = true });
            await AppTest.PutJsonAsync(admin, $"/api/v1/admin/app/releases/{rec.Id}/flags", new { isRecommended = true });

            var response = await anonymous.GetAsync($"{App}/config?platform=ios&appVersion=8.9.9");
            Assert.Contains("max-age=60", response.Headers.CacheControl!.ToString());
            Assert.Contains("stale-if-error=86400", response.Headers.CacheControl.ToString());
            var config = await AppTest.ReadAsync<AppConfigResponse>(response);
            Assert.Equal("9.0.0", config.Ios.MinSupportedVersion);
            Assert.Equal("9.2.0", config.Ios.RecommendedVersion);
            Assert.Equal("請更新", config.Ios.ForceUpdateMessage!.Zh);
            Assert.True(config.Evaluation!.UpdateRequired);
            Assert.False(config.Evaluation.UpdateRecommended);

            // 建置號不參與比較；剛好等於最低版不強制；低於建議版只建議
            async Task<AppConfigEvaluation> Eval(string v) => (await AppTest.ReadAsync<AppConfigResponse>(await anonymous.GetAsync($"{App}/config?platform=ios&appVersion={v}"))).Evaluation!;
            var equal = await Eval("9.0.0.777");
            Assert.False(equal.UpdateRequired);
            Assert.True(equal.UpdateRecommended);
            Assert.False((await Eval("9.2.0")).UpdateRecommended);
            Assert.False((await Eval("10.0.0")).UpdateRequired); // 語意化比較，不是字串比較

            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync($"{App}/config?platform=windows")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync($"{App}/config?platform=ios&appVersion=zzz")).StatusCode);

            // 維護模式：全域與分平台
            await AppTest.PutJsonAsync(admin, "/api/v1/admin/app/maintenance/all", new { enabled = true, messageZh = "系統維護中", messageEn = "Maintenance" });
            var m = await AppTest.ReadAsync<AppConfigResponse>(await anonymous.GetAsync($"{App}/config?platform=android&appVersion=1.0.0"));
            Assert.True(m.Evaluation!.Maintenance);
            Assert.Equal("Maintenance", m.Android.Maintenance.Message!.En);
            await AppTest.PutJsonAsync(admin, "/api/v1/admin/app/maintenance/all", new { enabled = false });
            Assert.False((await AppTest.ReadAsync<AppConfigResponse>(await anonymous.GetAsync($"{App}/config?platform=android"))).Android.Maintenance.Enabled);

            // 功能開關：單一平台的值優先於「全部」
            await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/config/flags", new { flagKey = "zztest_feature", isEnabled = true, platform = "all" });
            await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/config/flags", new { flagKey = "zztest_feature", isEnabled = false, platform = "ios" });
            var flags = await AppTest.ReadAsync<AppConfigResponse>(await anonymous.GetAsync($"{App}/config"));
            Assert.False(flags.Ios.FeatureFlags["zztest_feature"]);
            Assert.True(flags.Android.FeatureFlags["zztest_feature"]);
            Assert.Null(flags.Evaluation);
        }
        finally
        {
            await restore();
        }
    }

    // ═════════════ 內容編排 ═════════════

    [Fact]
    public async Task 內容編排_首頁九個區塊依序_關閉的不出現_公告條依期間與對象篩選_深連結對照表()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var created = new List<Guid>();
        try
        {
            var layout = await AppTest.ReadAsync<AppLayoutResponse>(await anonymous.GetAsync($"{App}/layout?lang=zh"));
            Assert.Equal("public, max-age=60, stale-while-revalidate=60, stale-if-error=86400", (await anonymous.GetAsync($"{App}/layout")).Headers.CacheControl!.ToString());
            Assert.Equal(["next_match", "ad_home_top", "latest_news", "member_card", "recent_matches", "ad_home_mid", "nearby_stores", "quick_entries", "sponsor_wall"], layout.HomeSections.Select(s => s.Code).ToArray());
            Assert.Equal("下一場賽事", layout.HomeSections[0].Label);
            var en = await AppTest.ReadAsync<AppLayoutResponse>(await anonymous.GetAsync($"{App}/layout?lang=en"));
            Assert.Equal("Next Match", en.HomeSections[0].Label);
            Assert.Equal("tcrfc://membercard", layout.HomeSections.Single(s => s.Code == "member_card").DeepLink);
            Assert.Contains(layout.DeepLinks, l => l.Code == "membercard" && l.RequiresLogin);
            Assert.Contains(layout.DeepLinks, l => l.Code == "upgrade" && l.WebUrl == "/zh/member/upgrade/");

            // 關掉一個區塊、新增快捷入口
            var items = await AppTest.ReadAsync<List<AdminAppLayoutItemDto>>(await admin.GetAsync("/api/v1/admin/app/layout/items?kind=home_section"));
            var sponsor = items.Single(i => i.ItemKey == "sponsor_wall");
            try
            {
                await AppTest.PutJsonAsync(admin, $"/api/v1/admin/app/layout/items/{sponsor.Id}", new { isEnabled = false, label = new { zh = sponsor.LabelZh, en = sponsor.LabelEn } });
                var off = await AppTest.ReadAsync<AppLayoutResponse>(await anonymous.GetAsync($"{App}/layout"));
                Assert.DoesNotContain(off.HomeSections, s => s.Code == "sponsor_wall");
                Assert.Equal(8, off.HomeSections.Count);
            }
            finally
            {
                await AppTest.PutJsonAsync(admin, $"/api/v1/admin/app/layout/items/{sponsor.Id}", new { isEnabled = true, label = new { zh = sponsor.LabelZh, en = sponsor.LabelEn } });
            }

            // 公告條：期間外不顯示；對象＝球迷會員的公告，匿名不可見、綁定有效球迷會員的裝置可見
            var all = await AppTest.ReadAsync<AdminAppAnnouncementDto>(await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/layout/announcements", new { message = new { zh = "ZZTEST 全部對象", en = "ZZTEST all" }, audienceTier = "all" }));
            var fan = await AppTest.ReadAsync<AdminAppAnnouncementDto>(await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/layout/announcements", new { message = new { zh = "ZZTEST 球迷限定" }, audienceTier = "fan_club" }));
            var expired = await AppTest.ReadAsync<AdminAppAnnouncementDto>(await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/layout/announcements", new { message = new { zh = "ZZTEST 已過期" }, startsAt = Iso(-TimeSpan.FromDays(3)), endsAt = Iso(-TimeSpan.FromDays(1)) }));
            created.AddRange([all.Id, fan.Id, expired.Id]);
            Assert.False(expired.IsActiveNow);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, "/api/v1/admin/app/layout/announcements", new { message = new { zh = "x" }, startsAt = Iso(TimeSpan.FromDays(2)), endsAt = Iso(TimeSpan.FromDays(1)) })).StatusCode);

            var anon = await AppTest.ReadAsync<AppLayoutResponse>(await anonymous.GetAsync($"{App}/layout?lang=en"));
            Assert.Contains(anon.Announcements, a => a.Message == "ZZTEST all");
            Assert.DoesNotContain(anon.Announcements, a => a.Message == "ZZTEST 球迷限定");
            Assert.DoesNotContain(anon.Announcements, a => a.Message == "ZZTEST 已過期");

            var deviceId = await AppTest.RegisterDeviceAsync(anonymous);
            var asDevice = await anonymous.GetAsync($"{App}/layout?deviceInstallId={deviceId}");
            Assert.True(asDevice.Headers.CacheControl!.NoStore && asDevice.Headers.CacheControl.Private);
            Assert.DoesNotContain((await AppTest.ReadAsync<AppLayoutResponse>(asDevice)).Announcements, a => a.Message == "ZZTEST 球迷限定");
            await BizTest.ExecuteSqlAsync(
                "UPDATE app_devices SET member_id = (SELECT TOP 1 m.member_id FROM memberships m WHERE m.tier = 'fan_club' AND m.status = 'active' ORDER BY m.row_seq) WHERE device_install_id = @I", ("@I", deviceId));
            Assert.Contains((await AppTest.ReadAsync<AppLayoutResponse>(await anonymous.GetAsync($"{App}/layout?deviceInstallId={deviceId}"))).Announcements, a => a.Message == "ZZTEST 球迷限定");
        }
        finally
        {
            foreach (var id in created)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM app_announcements WHERE id = @I", ("@I", id));
            }

            await AppTest.CleanupDevicesAsync();
        }
    }

    [Fact]
    public async Task 通知中心_已送出的推播依裝置是否為對象篩選_開啟只累加彙總數字_保留九十天()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var everyone = Guid.NewGuid();
        var fanOnly = Guid.NewGuid();
        var old = Guid.NewGuid();
        try
        {
            async Task Message(Guid id, string tier, int daysAgo, string title)
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO push_messages (id, kind, audience_tier, status, sent_at, reviewed_at, sent_count, delivered_count) VALUES (@I, N'announcement', @T, N'sent', DATEADD(day, -@D, SYSUTCDATETIME()), SYSUTCDATETIME(), 1, 1)",
                    ("@I", id), ("@T", tier), ("@D", daysAgo));
                await BizTest.ExecuteSqlAsync("INSERT INTO push_messages_i18n (push_message_id, locale, title, body) VALUES (@I, N'zh-Hant', @X, N'ZZTEST 內文'), (@I, N'en', @Y, N'ZZTEST body')",
                    ("@I", id), ("@X", title), ("@Y", title + " EN"));
            }

            await Message(everyone, "all", 1, "ZZTEST 對所有人");
            await Message(fanOnly, "fan_club", 2, "ZZTEST 只給球迷會員");
            await Message(old, "all", 120, "ZZTEST 太舊");
            var device = await AppTest.RegisterDeviceAsync(anonymous, locale: "en");

            var noDevice = await AppTest.ReadAsync<List<AppNotificationDto>>(await anonymous.GetAsync($"{App}/notifications?lang=zh"));
            Assert.Contains(noDevice, n => n.Title == "ZZTEST 對所有人");
            Assert.DoesNotContain(noDevice, n => n.Title == "ZZTEST 只給球迷會員");
            Assert.DoesNotContain(noDevice, n => n.Title == "ZZTEST 太舊"); // 只保留 90 天
            var asDevice = await AppTest.ReadAsync<List<AppNotificationDto>>(await anonymous.GetAsync($"{App}/notifications?lang=en&deviceInstallId={device}"));
            Assert.Contains(asDevice, n => n.Title == "ZZTEST 對所有人 EN");
            Assert.DoesNotContain(asDevice, n => n.Title!.Contains("球迷"));

            // 開啟回報：只累加批次 × 平台 × 語系的彙總，不記錄是哪台裝置
            Assert.Equal(HttpStatusCode.NoContent, (await AppTest.PostJsonAsync(anonymous, $"{App}/push/{everyone}/opened", new { deviceInstallId = device })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await AppTest.PostJsonAsync(anonymous, $"{App}/push/{everyone}/opened", new { deviceInstallId = device })).StatusCode);
            Assert.Equal(2, await C1Test.ScalarAsync<int>("SELECT opened_count FROM push_messages WHERE id = @I", ("@I", everyone)));
            Assert.Equal(2, await C1Test.ScalarAsync<int>("SELECT opened FROM push_message_stats WHERE push_message_id = @I AND platform = N'ios' AND locale = N'en'", ("@I", everyone)));
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PostJsonAsync(anonymous, $"{App}/push/{Guid.NewGuid()}/opened", new { deviceInstallId = device })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PostJsonAsync(anonymous, $"{App}/push/{everyone}/opened", new { deviceInstallId = AppTest.NewDeviceId() })).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM push_messages WHERE id IN (@A, @B, @C)", ("@A", everyone), ("@B", fanOnly), ("@C", old));
            await AppTest.CleanupDevicesAsync();
        }
    }

    // ═════════════ 廣告投放 ═════════════

    [Fact]
    public async Task 廣告投放_只投投放中且已審核未暫停的素材_無素材回退繁中_暫停即停_快取標頭依是否帶裝置識別()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_serve", rotationCap: 3);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var (campaign, creative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, name: "ZZTEST 投放");

            var served = await anonymous.GetAsync($"{App}/ads/zztest_serve?lang=en");
            Assert.Equal("public, max-age=60", served.Headers.CacheControl!.ToString());
            var body = await AppTest.ReadAsync<AppAdResponse>(served);
            Assert.False(body.IsFallback);
            Assert.Equal("Ad", body.DisclosureLabel);
            var item = Assert.Single(body.Items); // 只有繁中素材，英文請求回退繁中
            Assert.Equal(creative, item.CreativeId);
            Assert.Equal(campaign.Id, item.CampaignId);
            Assert.Equal("ZZTEST 標題", item.Title);
            Assert.True((await anonymous.GetAsync($"{App}/ads/zztest_serve?deviceInstallId={AppTest.NewDeviceId()}")).Headers.CacheControl!.NoStore);
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync($"{App}/ads/zztest_serve?deviceInstallId=bad")).StatusCode);

            // 素材暫停 → 不再投放（回備援）；恢復 → 又投放
            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{creative}/pause", new { });
            var paused = await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_serve"));
            Assert.True(paused.IsFallback);
            await AppTest.PostJsonAsync(biz, $"{Ads}/creatives/{creative}/resume", new { });
            Assert.False((await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_serve"))).IsFallback);

            // 未審核的第二個素材不會上線
            await AppTest.InsertCreativeAsync(campaign.Id, "en", "pending");
            Assert.Single((await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_serve?lang=zh"))).Items);
            Assert.Equal("ZZTEST 標題", (await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_serve?lang=en"))).Items.Single().Title);

            // 檔期緊急暫停 → 立刻停止投放
            await AdminAdsTests.ActAsync(biz, campaign.Id, "pause", new { reason = "申訴" });
            Assert.True((await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_serve"))).IsFallback);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 廣告投放_輪播權重加權隨機_不重複抽取_每個版位最多輪播張數上限()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_weight", rotationCap: 1);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var (heavy, _) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, new { weight = 100 }, "ZZTEST 重");
            var (light, _) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, new { weight = 1 }, "ZZTEST 輕");

            var heavyCount = 0;
            for (var i = 0; i < 60; i++)
            {
                var r = await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_weight"));
                Assert.Single(r.Items); // 輪播上限 1
                if (r.Items[0].CampaignId == heavy.Id) { heavyCount++; }
            }

            Assert.InRange(heavyCount, 50, 60); // 權重 100:1，期望約 59 次

            // 上限放寬到 3：兩個檔期各出現一次，不重複
            await biz.PutAsync($"{Ads}/slots/{slot.Id}", BizTest.Multipart(new { slotCode = "zztest_weight", rotationCap = 3, isActive = true, content = new { zh = new { name = "ZZTEST 權重" } } }));
            var both = await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync($"{App}/ads/zztest_weight"));
            Assert.Equal(2, both.Items.Count);
            Assert.Equal(2, both.Items.Select(i => i.CampaignId).Distinct().Count());
            Assert.Contains(both.Items, i => i.CampaignId == light.Id);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 廣告投放_每日曝光上限_每人頻次上限_曝光保證pacing不在前幾天燒完_達標即停()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_caps", rotationCap: 5);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var (capped, cappedCreative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, new { dailyImpressionCap = 2 }, "ZZTEST 日上限");
            var (perDevice, perDeviceCreative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, new { perDeviceDailyCap = 1 }, "ZZTEST 每人上限");
            // 曝光保證 20 次、分 4 天：今天最多 ceil(20/4)=5 次
            var (guaranteed, guaranteedCreative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id,
                new { goalType = "guaranteed", goalImpressions = 20 }, "ZZTEST 保證", -TimeSpan.FromHours(1), TimeSpan.FromHours(95));

            async Task Impress(Guid creative, string device, int n, string? batch = null)
            {
                var events = Enumerable.Range(0, n).Select(_ => new { type = "impression", creativeId = creative, occurredAt = DateTimeOffset.UtcNow.ToString("O"), presentationId = Guid.NewGuid().ToString() }).ToArray();
                var r = await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", new { batchId = batch, deviceInstallId = device, platform = "ios", locale = "zh", events });
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                Assert.Equal(n, (await AppTest.ReadAsync<AppAdEventBatchResult>(r)).Accepted);
            }

            async Task<HashSet<Guid?>> Served(string? device = null)
            {
                var url = $"{App}/ads/zztest_caps" + (device is null ? string.Empty : $"?deviceInstallId={device}");
                return (await AppTest.ReadAsync<AppAdResponse>(await anonymous.GetAsync(url))).Items.Select(i => i.CampaignId).ToHashSet();
            }

            Assert.Equal(3, (await Served()).Count);
            var deviceA = AppTest.NewDeviceId();
            var deviceB = AppTest.NewDeviceId();

            // 每日曝光上限 2：送出 2 次曝光後不再投放
            await Impress(cappedCreative, deviceA, 2);
            Assert.DoesNotContain(capped.Id, await Served());

            // 每人頻次上限 1：同一裝置看過一次就不再看到；別的裝置與沒帶裝置識別的不受影響
            await Impress(perDeviceCreative, deviceA, 1);
            Assert.DoesNotContain(perDevice.Id, await Served(deviceA));
            Assert.Contains(perDevice.Id, await Served(deviceB));
            Assert.Contains(perDevice.Id, await Served());

            // pacing：今天的份額 5 次用完就先停（雖然整個檔期還沒交付到目標）
            Assert.Contains(guaranteed.Id, await Served());
            await Impress(guaranteedCreative, deviceA, 4);
            Assert.Contains(guaranteed.Id, await Served());
            await Impress(guaranteedCreative, deviceB, 1);
            Assert.DoesNotContain(guaranteed.Id, await Served());
            var detail = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await biz.GetAsync($"{Ads}/campaigns/{guaranteed.Id}"));
            Assert.Equal(5, detail.DeliveredToday);
            Assert.Equal(5, detail.DeliveredTotal);
            Assert.Equal(20, detail.Pacing!.GoalImpressions);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    // ═════════════ 廣告事件 ═════════════

    [Fact]
    public async Task 廣告事件_時間關卡_去重_檔期由素材推導_批次重送冪等_累計投遞量_不存個資欄位()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_events");
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var (campaign, creative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id);
            var draft = await AdminAdsTests.CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(9), TimeSpan.FromDays(10), name: "ZZTEST 草稿");
            var draftCreative = await AppTest.InsertCreativeAsync(draft.Id);
            var device = AppTest.NewDeviceId();
            var now = DateTimeOffset.UtcNow;
            var presentation = Guid.NewGuid().ToString();

            var request = new
            {
                batchId = "batch-1", deviceInstallId = device, platform = "android", appVersion = "1.0.0", locale = "en",
                events = new object[]
                {
                    new { type = "impression", creativeId = creative, occurredAt = now.AddSeconds(-30).ToString("O"), presentationId = presentation },
                    new { type = "impression", creativeId = creative, occurredAt = now.AddSeconds(-20).ToString("O"), presentationId = presentation }, // 同一個曝光識別碼只算一次
                    new { type = "click", creativeId = creative, occurredAt = now.AddSeconds(-10).ToString("O") },
                    new { type = "click", creativeId = creative, occurredAt = now.AddSeconds(-8).ToString("O") }, // 5 秒內重複點擊只計 1 次
                    new { type = "impression", creativeId = creative, occurredAt = now.AddHours(-25).ToString("O") }, // 超過 24 小時拒收
                    new { type = "impression", creativeId = creative, occurredAt = now.AddHours(1).ToString("O") }, // 明顯在未來
                    new { type = "impression", creativeId = Guid.NewGuid(), occurredAt = now.ToString("O") }, // 沒有這個素材
                    new { type = "impression", creativeId = draftCreative, occurredAt = now.ToString("O") }, // 草稿檔期不收
                    new { type = "tap", creativeId = creative, occurredAt = now.ToString("O") },
                },
            };
            var result = await AppTest.ReadAsync<AppAdEventBatchResult>(await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", request));
            Assert.Equal(2, result.Accepted); // 1 次曝光＋1 次點擊
            Assert.Equal(2, result.Duplicates);
            Assert.Equal(["invalid", "not_serving", "unknown_creative", "future", "too_old"], result.Rejected.OrderByDescending(r => r.Index).Select(r => r.Reason).ToArray());

            // 同一個批次整份重送：全部是重複，不會重複計數
            var resend = await AppTest.ReadAsync<AppAdEventBatchResult>(await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", request));
            Assert.Equal(0, resend.Accepted);

            // 檔期與版位由素材推導；累計投遞量只算曝光
            Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM ad_events WHERE campaign_id = @C AND slot_id = @S AND event_type = 'impression' AND device_install_id = @D", ("@C", campaign.Id), ("@S", slot.Id), ("@D", device)));
            var detail = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await biz.GetAsync($"{Ads}/campaigns/{campaign.Id}"));
            Assert.Equal(1, detail.DeliveredTotal);
            Assert.Equal(1, detail.DeliveredToday);

            // 事件以「發生時間」記錄、平台與語系來自請求
            Assert.Equal("en", await C1Test.ScalarAsync<string>("SELECT TOP 1 locale FROM ad_events WHERE device_install_id = @D", ("@D", device)));
            var occurred = await C1Test.ScalarAsync<DateTime>("SELECT occurred_at FROM ad_events WHERE device_install_id = @D AND event_type = 'click'", ("@D", device));
            Assert.InRange((now.UtcDateTime.AddSeconds(-10) - occurred).TotalSeconds, -1, 1);

            // 請求驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", new { deviceInstallId = "x", platform = "ios", events = new[] { new { type = "impression", creativeId = creative, occurredAt = now.ToString("O") } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", new { deviceInstallId = device, platform = "ios", events = Array.Empty<object>() })).StatusCode);
            var many = Enumerable.Range(0, 201).Select(_ => new { type = "impression", creativeId = creative, occurredAt = now.ToString("O") }).ToArray();
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", new { deviceInstallId = device, platform = "ios", events = many })).StatusCode);

            // 🔴 原始事件表沒有任何個資欄位：會員、IP、定位座標、廣告識別碼
            var columns = new List<string>();
            await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")))
            {
                await connection.OpenAsync();
                await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT column_name FROM information_schema.columns WHERE table_name = 'ad_events'", connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { columns.Add(reader.GetString(0)); }
            }

            Assert.DoesNotContain(columns, c => c.Contains("member", StringComparison.OrdinalIgnoreCase) || c.Contains("ip", StringComparison.OrdinalIgnoreCase) && c != "presentation_id"
                                                || c.Contains("lat", StringComparison.OrdinalIgnoreCase) && c != "platform" || c.Contains("lng") || c.Contains("idfa", StringComparison.OrdinalIgnoreCase) || c.Contains("aaid", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    // ═════════════ 診斷回報 ═════════════

    [Fact]
    public async Task 診斷回報_不存個資_電子郵件與長串數字入庫前遮蔽_時間與筆數限制_後台彙總無崩潰裝置比例()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var device = await AppTest.RegisterDeviceAsync(anonymous);
            await BizTest.ExecuteSqlAsync("UPDATE app_devices SET app_version = N'7.7.7' WHERE device_install_id = @I", ("@I", device));
            var accepted = await AppTest.PostJsonAsync(anonymous, $"{App}/diagnostics", new
            {
                reports = new object[]
                {
                    new { deviceInstallId = device, platform = "ios", appVersion = "7.7.7", buildNumber = "77", osVersion = "17", occurredAt = DateTimeOffset.UtcNow.ToString("O"), type = "crash", summary = "使用者 someone@example.com 電話 0912345678 閃退", detail = "堆疊 at Foo.Bar\n信箱 a.b@c.dev" },
                    new { platform = "ios", appVersion = "7.7.7", occurredAt = DateTimeOffset.UtcNow.ToString("O"), type = "startup_time", metricValue = 1200 },
                    new { platform = "ios", appVersion = "7.7.7", occurredAt = DateTimeOffset.UtcNow.ToString("O"), type = "startup_time", metricValue = 2400 },
                },
            });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            var stored = await C1Test.ScalarAsync<string>("SELECT summary FROM app_diagnostic_reports WHERE app_version = N'7.7.7' AND report_type = N'crash'");
            Assert.DoesNotContain("someone@example.com", stored);
            Assert.DoesNotContain("0912345678", stored);
            Assert.Contains("[已遮蔽]", stored);
            Assert.DoesNotContain("a.b@c.dev", await C1Test.ScalarAsync<string>("SELECT detail FROM app_diagnostic_reports WHERE app_version = N'7.7.7' AND report_type = N'crash'"));

            var old = new { platform = "ios", appVersion = "7.7.7", occurredAt = DateTimeOffset.UtcNow.AddDays(-30).ToString("O"), type = "crash" };
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/diagnostics", new { reports = new[] { old } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/diagnostics", new { reports = new[] { new { platform = "ios", appVersion = "7.7.7", occurredAt = DateTimeOffset.UtcNow.ToString("O"), type = "weird" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(anonymous, $"{App}/diagnostics", new { reports = Array.Empty<object>() })).StatusCode);

            var summary = await AppTest.ReadAsync<AdminAppDiagnosticSummaryDto>(await admin.GetAsync("/api/v1/admin/app/diagnostics/summary?days=7"));
            var row = summary.ByVersion.Single(v => v.AppVersion == "7.7.7");
            Assert.Equal(1, row.Crashes);
            Assert.Equal(1, row.DevicesWithCrash);
            Assert.Equal(1, row.ActiveDevices);
            Assert.Equal(0m, row.CrashFreeDevicePercent);
            Assert.InRange(summary.StartupMedianMs!.Value, 1200, 2400); // 種子也有一筆啟動耗時回報，只驗證落在範圍內
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM app_diagnostic_reports WHERE app_version = N'7.7.7'");
            await AppTest.CleanupDevicesAsync();
        }
    }
}
