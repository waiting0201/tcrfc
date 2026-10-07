using System.Net;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// I3 全域設定（後台）＋前台讀取（選單管理已由規劃書 v3.22 取消）。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）。
/// 改動共用庫的設定前後一律快照還原（E-81／E-119）。Logo／Favicon 上傳需要 Azurite（<c>AdminWriteAzuriteEnabledCollection</c>），本檔不涵蓋。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class GlobalSettingsTests(AdminWriteApiFixture fixture)
{
    private const string Global = "/api/v1/admin/tcrfc/global-settings";

    // ── 全域設定 ───────────────────────────────────────────────────────────────────
    private static async Task<Func<Task>> SnapshotGlobalAsync()
    {
        var policy = await C1Test.SnapshotSettingsAsync("tcrfc", "policy.%");
        var maintenance = await C1Test.SnapshotSettingsAsync("tcrfc", "maintenance.%");
        return async () =>
        {
            await policy();
            await maintenance();
        };
    }

    private static Task<HttpResponseMessage> PutGlobalAsync(HttpClient admin, object payload) => admin.PutAsync(Global, BizTest.Multipart(payload));

    [Fact]
    public async Task 全域設定_權限與儲存讀回_政策頁_維護模式()
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
                cookiePolicy = new { bodyZh = "Cookie 政策繁中內文\n\n第二段", bodyEn = "Cookie policy" },
                privacyPolicy = new { bodyZh = "隱私權政策繁中內文" },
                memberTerms = new { bodyZh = (string?)null },
                maintenanceEnabled = true, maintenanceMessageZh = "系統維護中，預計 10 分鐘", maintenanceMessageEn = (string?)null,
            });
            var saved = await BizTest.ReadAsync<AdminGlobalSettingsDto>(response);
            Assert.True(saved.Maintenance.Enabled);
            Assert.Equal("Cookie policy", saved.Policies.Single(p => p.Code == "cookie").BodyEn);
            Assert.Null(saved.Policies.Single(p => p.Code == "member-terms").BodyZh);

            // 🔴 主站規劃書 v3.20：後台與公開設定都不再有標誌、Favicon、品牌色（由前台靜態資產與 CSS 定義）。
            foreach (var removed in new[] { "brand", "logoLightUrl", "logoDarkUrl", "faviconUrl", "brandColor", "brandSecondaryColor" })
            {
                Assert.DoesNotContain($"\"{removed}\"", await (await admin.GetAsync(Global)).Content.ReadAsStringAsync());
                Assert.DoesNotContain($"\"{removed}\"", await (await anonymous.GetAsync("/api/v1/tcrfc/site-settings")).Content.ReadAsStringAsync());
            }

            var read = await BizTest.ReadAsync<AdminGlobalSettingsDto>(await admin.GetAsync(Global));
            Assert.Equal("隱私權政策繁中內文", read.Policies.Single(p => p.Code == "privacy").BodyZh);

            // 前台：維護模式立即反映、缺英文回退繁中、政策索引只標有內容者
            var pub = await BizTest.ReadAsync<PublicSiteSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/site-settings?lang=en"));
            Assert.True(pub.Maintenance.Enabled);
            Assert.Equal("系統維護中，預計 10 分鐘", pub.Maintenance.Message);
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
    public async Task 全域設定_驗證_政策過長_維護訊息過長_缺payload_舊版品牌欄位被忽略()
    {
        var restore = await SnapshotGlobalAsync();
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            // 後台畫面已移除品牌欄位；萬一舊版客戶端仍夾帶（含不合法色碼），一律忽略、不報錯、也不寫入任何地方。
            Assert.Equal(HttpStatusCode.OK, (await PutGlobalAsync(admin, new { brandColor = "green", brandSecondaryColor = "#12345", removeLogoLight = true })).StatusCode);
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
