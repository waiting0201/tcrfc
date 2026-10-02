using System.Net;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// I2 選單管理與 I3 全域設定（後台）＋前台讀取。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）。
/// 改動共用庫的選單與設定前後一律快照還原（E-81／E-119）。Logo／Favicon 上傳需要 Azurite（<c>AdminWriteAzuriteEnabledCollection</c>），本檔不涵蓋。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class SiteMenusAndGlobalSettingsTests(AdminWriteApiFixture fixture)
{
    private const string Menus = "/api/v1/admin/tcrfc/menus";
    private const string Global = "/api/v1/admin/tcrfc/global-settings";

    private static object Leaf(string zh, string? en, string url, bool external = false, Guid? id = null)
        => new { id, labelZh = zh, labelEn = en, url, isExternal = external };

    private static async Task<AdminMenuLocationDto> PutMenuAsync(HttpClient admin, string location, object items)
    {
        var response = await AppTest.PutJsonAsync(admin, $"{Menus}/{location}", new { items });
        return await BizTest.ReadAsync<AdminMenuLocationDto>(response);
    }

    [Fact]
    public async Task 選單_權限_檢視者不可_系統管理員可_三個位置都在()
    {
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Menus)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(viewer, Menus + "/main", new { items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Menus)).StatusCode);

        var menus = await BizTest.ReadAsync<AdminMenusDto>(await admin.GetAsync(Menus));
        Assert.Equal(["main", "mega", "footer"], menus.Locations.Select(l => l.Location).ToList());
    }

    [Fact]
    public async Task 選單_多層級儲存_前台依語系讀取_缺英文回退繁中_兩個俱樂部各自獨立()
    {
        var restoreTcrfc = await SiteSettingsTest.SnapshotMenusAsync("tcrfc");
        var restoreBw = await SiteSettingsTest.SnapshotMenusAsync("bw");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            await PutMenuAsync(admin, "main", Array.Empty<object>());
            await PutMenuAsync(admin, "footer", Array.Empty<object>());
            await AppTest.PutJsonAsync(admin, "/api/v1/admin/bw/menus/main", new { items = Array.Empty<object>() });

            var saved = await PutMenuAsync(admin, "main", new object[]
            {
                new
                {
                    labelZh = "關於", labelEn = "About", url = (string?)null, isExternal = false,
                    children = new object[] { Leaf("俱樂部", null, "/about/club/"), Leaf("歷史", "History", "/about/history/") },
                },
                Leaf("新聞", "News", "/news/"),
                Leaf("粉絲專頁", "Facebook", "https://www.facebook.com/TCRFC2024", external: true),
            });
            Assert.Equal(["關於", "新聞", "粉絲專頁"], saved.Items.Select(i => i.LabelZh).ToList());
            Assert.Equal(["俱樂部", "歷史"], saved.Items[0].Children.Select(i => i.LabelZh).ToList());
            Assert.True(saved.Items[2].IsExternal);

            var zh = await BizTest.ReadAsync<PublicMenusDto>(await anonymous.GetAsync("/api/v1/tcrfc/menus?lang=zh"));
            Assert.Equal("關於", zh.Main[0].Label);
            Assert.Null(zh.Main[0].Url);
            Assert.Equal("/about/club/", zh.Main[0].Children[0].Url);

            var en = await BizTest.ReadAsync<PublicMenusDto>(await anonymous.GetAsync("/api/v1/tcrfc/menus?lang=en"));
            Assert.Equal("About", en.Main[0].Label);
            Assert.Equal("俱樂部", en.Main[0].Children[0].Label); // 缺英文回退繁中
            Assert.Equal("History", en.Main[0].Children[1].Label);

            var bw = await BizTest.ReadAsync<PublicMenusDto>(await anonymous.GetAsync("/api/v1/bw/menus"));
            Assert.Empty(bw.Main);
        }
        finally
        {
            await restoreTcrfc();
            await restoreBw();
        }
    }

    [Fact]
    public async Task 選單_再次儲存_沿用id_重排_刪除_新增()
    {
        var restore = await SiteSettingsTest.SnapshotMenusAsync("tcrfc");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            await PutMenuAsync(admin, "footer", Array.Empty<object>());
            var first = await PutMenuAsync(admin, "footer", new object[] { Leaf("甲", null, "/a/"), Leaf("乙", null, "/b/"), Leaf("丙", null, "/c/") });
            var a = first.Items[0].Id;
            var b = first.Items[1].Id;
            var c = first.Items[2].Id;

            // 乙刪除、丙移到最前面並改名、甲變成丙的子項目、新增丁
            var second = await PutMenuAsync(admin, "footer", new object[]
            {
                new
                {
                    id = c, labelZh = "丙改", url = "/c/", isExternal = false,
                    children = new object[] { Leaf("甲", null, "/a/", id: a) },
                },
                Leaf("丁", null, "/d/"),
            });
            Assert.Equal(c, second.Items[0].Id);
            Assert.Equal("丙改", second.Items[0].LabelZh);
            Assert.Equal(a, second.Items[0].Children.Single().Id);
            Assert.Equal(2, second.Items.Count);
            Assert.DoesNotContain(second.Items, i => i.Id == b);
            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM menu_items WHERE id = @I", ("@I", b)));
            Assert.Equal(3, await C1Test.ScalarAsync<int>(
                "SELECT COUNT(*) FROM menu_items m JOIN clubs c ON c.id = m.club_id WHERE c.code = N'tcrfc' AND m.menu_location = N'footer'"));
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task 選單_驗證失敗_400_且不改動既有資料()
    {
        var restore = await SiteSettingsTest.SnapshotMenusAsync("tcrfc");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var baseline = await PutMenuAsync(admin, "main", new object[] { Leaf("基準", null, "/base/") });
            var mainId = baseline.Items.Single().Id;

            async Task AssertBadAsync(string location, object items)
                => Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Menus}/{location}", new { items })).StatusCode);

            // 超過 3 層
            await AssertBadAsync("main", new object[]
            {
                new
                {
                    labelZh = "1", url = "/1/", children = new object[]
                    {
                        new { labelZh = "2", url = "/2/", children = new object[] { new { labelZh = "3", url = "/3/", children = new object[] { Leaf("4", null, "/4/") } } } },
                    },
                },
            });
            await AssertBadAsync("main", new object[] { new { labelZh = "無連結的葉節點" } });                       // 葉節點沒有連結
            await AssertBadAsync("main", new object[] { Leaf("危險", null, "javascript:alert(1)") });                  // 內部連結必須以 / 開頭
            await AssertBadAsync("main", new object[] { Leaf("協定相對", null, "//evil.example/") });
            await AssertBadAsync("main", new object[] { Leaf("含空白", null, "/a b/") });
            await AssertBadAsync("main", new object[] { Leaf("外部非http", null, "ftp://example.test/", external: true) });
            await AssertBadAsync("main", new object[] { Leaf("外部沒網域", null, "/relative/", external: true) });
            await AssertBadAsync("main", new object[] { Leaf("", null, "/x/") });                                      // 繁中名稱必填
            await AssertBadAsync("main", new object[] { Leaf(new string('字', 65), null, "/x/") });                    // 超過 64 字
            await AssertBadAsync("main", new object[] { Leaf("找不到的id", null, "/x/", id: Guid.NewGuid()) });
            await AssertBadAsync("main", new object[] { Leaf("重複a", null, "/x/", id: mainId), Leaf("重複b", null, "/y/", id: mainId) });
            await AssertBadAsync("main", Enumerable.Range(0, 101).Select(i => (object)Leaf($"項{i}", null, $"/p{i}/")).ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Menus}/sidebar", new { items = Array.Empty<object>() })).StatusCode);

            // 別的位置的 id 不能拿來用
            await PutMenuAsync(admin, "footer", new object[] { Leaf("頁尾", null, "/f/") });
            await AssertBadAsync("main", new object[] { Leaf("借用頁尾id", null, "/x/", id: (await BizTest.ReadAsync<AdminMenusDto>(await admin.GetAsync(Menus))).Locations[2].Items[0].Id) });

            var after = await BizTest.ReadAsync<AdminMenusDto>(await admin.GetAsync(Menus));
            Assert.Equal(mainId, after.Locations[0].Items.Single().Id); // 全部失敗後原選單不變
        }
        finally
        {
            await restore();
        }
    }

    // ── 全域設定 ───────────────────────────────────────────────────────────────────
    private static async Task<Func<Task>> SnapshotGlobalAsync()
    {
        var policy = await C1Test.SnapshotSettingsAsync("tcrfc", "policy.%");
        var maintenance = await C1Test.SnapshotSettingsAsync("tcrfc", "maintenance.%");
        var colors = (
            await C1Test.ScalarAsync<string>("SELECT brand_color FROM clubs WHERE code = N'tcrfc'"),
            await C1Test.ScalarAsync<string>("SELECT brand_secondary_color FROM clubs WHERE code = N'tcrfc'"));
        return async () =>
        {
            await policy();
            await maintenance();
            await BizTest.ExecuteSqlAsync("UPDATE clubs SET brand_color = @A, brand_secondary_color = @B WHERE code = N'tcrfc'", ("@A", colors.Item1), ("@B", colors.Item2));
        };
    }

    private static Task<HttpResponseMessage> PutGlobalAsync(HttpClient admin, object payload) => admin.PutAsync(Global, BizTest.Multipart(payload));

    [Fact]
    public async Task 全域設定_權限與儲存讀回_政策頁_維護模式_品牌色()
    {
        var restore = await SnapshotGlobalAsync();
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Global)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await PutGlobalAsync(viewer, new { })).StatusCode);

            var response = await PutGlobalAsync(admin, new
            {
                brandColor = "#1a5f3a", brandSecondaryColor = "#C8A24A",
                cookiePolicy = new { bodyZh = "Cookie 政策繁中內文\n\n第二段", bodyEn = "Cookie policy" },
                privacyPolicy = new { bodyZh = "隱私權政策繁中內文" },
                memberTerms = new { bodyZh = (string?)null },
                maintenanceEnabled = true, maintenanceMessageZh = "系統維護中，預計 10 分鐘", maintenanceMessageEn = (string?)null,
            });
            var saved = await BizTest.ReadAsync<AdminGlobalSettingsDto>(response);
            Assert.Equal("#1A5F3A", saved.Brand.BrandColor); // 色碼正規化為大寫
            Assert.True(saved.Maintenance.Enabled);
            Assert.Equal("Cookie policy", saved.Policies.Single(p => p.Code == "cookie").BodyEn);
            Assert.Null(saved.Policies.Single(p => p.Code == "member-terms").BodyZh);

            var read = await BizTest.ReadAsync<AdminGlobalSettingsDto>(await admin.GetAsync(Global));
            Assert.Equal("隱私權政策繁中內文", read.Policies.Single(p => p.Code == "privacy").BodyZh);

            // 前台：維護模式立即反映、缺英文回退繁中、政策索引只標有內容者
            var pub = await BizTest.ReadAsync<PublicSiteSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/site-settings?lang=en"));
            Assert.True(pub.Maintenance.Enabled);
            Assert.Equal("系統維護中，預計 10 分鐘", pub.Maintenance.Message);
            Assert.Equal("#1A5F3A", pub.Brand.BrandColor);
            Assert.True(pub.Policies.Single(p => p.Code == "cookie").HasContent);
            Assert.False(pub.Policies.Single(p => p.Code == "member-terms").HasContent);

            var policyEn = await BizTest.ReadAsync<PublicPolicyDto>(await anonymous.GetAsync("/api/v1/tcrfc/policies/privacy?lang=en"));
            Assert.Equal("隱私權政策繁中內文", policyEn.Body);
            Assert.True(policyEn.IsFallbackLocale);
            Assert.Equal("Privacy Policy", policyEn.Title);
            var cookieEn = await BizTest.ReadAsync<PublicPolicyDto>(await anonymous.GetAsync("/api/v1/tcrfc/policies/cookie?lang=en"));
            Assert.False(cookieEn.IsFallbackLocale);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tcrfc/policies/member-terms")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tcrfc/policies/unknown")).StatusCode);

            // 關閉維護模式後前台馬上恢復
            await PutGlobalAsync(admin, new { maintenanceEnabled = false });
            var off = await BizTest.ReadAsync<PublicSiteSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/site-settings"));
            Assert.False(off.Maintenance.Enabled);
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task 全域設定_驗證_色碼格式_政策過長_維護訊息過長_缺payload()
    {
        var restore = await SnapshotGlobalAsync();
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await PutGlobalAsync(admin, new { brandColor = "green" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutGlobalAsync(admin, new { brandSecondaryColor = "#12345" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutGlobalAsync(admin, new { privacyPolicy = new { bodyZh = new string('字', 50_001) } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutGlobalAsync(admin, new { maintenanceMessageZh = new string('字', 501) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync(Global, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        }
        finally
        {
            await restore();
        }
    }
}
