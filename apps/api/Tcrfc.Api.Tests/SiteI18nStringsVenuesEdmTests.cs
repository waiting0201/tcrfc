using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.AdminVenues;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// I4 多語系管理與字串翻譯表、I5 場地管理、I6 EDM 平台設定。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）；
/// 翻譯人員權限依賴 migration <c>AlignSchemaI1</c> 把 <c>site.string.*</c> 指派給 translator 角色（合併後先套 migration 再跑）。
/// 場地照片上傳需要 Azurite，本檔不涵蓋。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class SiteI18nStringsVenuesEdmTests(AdminWriteApiFixture fixture)
{
    private const string I18n = "/api/v1/admin/tcrfc/i18n";
    private const string Strings = "/api/v1/admin/tcrfc/i18n/strings";

    private static string Key(string tag = "a") => $"zz.test.{tag}.{Guid.NewGuid():N}"[..40];

    private static Task CleanupStringsAsync() => BizTest.ExecuteSqlAsync("DELETE FROM ui_strings WHERE string_key LIKE 'zz.test.%'");

    // ── 語系與規則 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task 語系_清單與更新規則_預設語系不能停用_備援不能是自己或不存在_權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(I18n + "/locales")).StatusCode);

        var locales = await BizTest.ReadAsync<List<AdminLocaleDto>>(await admin.GetAsync(I18n + "/locales"));
        var zh = locales.Single(l => l.IsDefault);
        var en = locales.Single(l => l.Code == "en");

        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/{zh.Code}",
            new { name = zh.Name, isEnabled = false, sortOrder = zh.SortOrder })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/{zh.Code}",
            new { name = zh.Name, isEnabled = true, fallbackCode = "en", sortOrder = zh.SortOrder })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/en",
            new { name = en.Name, isEnabled = true, fallbackCode = "en", sortOrder = en.SortOrder })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/en",
            new { name = en.Name, isEnabled = true, fallbackCode = "fr", sortOrder = en.SortOrder })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/en",
            new { name = "", isEnabled = true, sortOrder = en.SortOrder })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PutJsonAsync(admin, $"{I18n}/locales/fr",
            new { name = "Français", isEnabled = true, sortOrder = 9 })).StatusCode);

        // 原值重送（備援回到繁中）成功，且資料不變
        var same = await BizTest.ReadAsync<AdminLocaleDto>(await AppTest.PutJsonAsync(admin, $"{I18n}/locales/en",
            new { name = en.Name, isEnabled = en.IsEnabled, fallbackCode = en.FallbackCode, sortOrder = en.SortOrder }));
        Assert.Equal(en.Code, same.Code);
        Assert.Equal(en.IsEnabled, same.IsEnabled);
    }

    [Fact]
    public async Task 多語系規則_備援模式與日期數字格式_儲存讀回_前台公開_驗證()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "i18n.%");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            var saved = await BizTest.ReadAsync<AdminI18nSettingsDto>(await AppTest.PutJsonAsync(admin, I18n + "/settings", new
            {
                fallbackMode = "hide", dateFormatZh = "YYYY年M月D日", dateFormatEn = "MMM D, YYYY", numberFormatZh = "1,234.56", numberFormatEn = "1.234,56",
            }));
            Assert.Equal("hide", saved.FallbackMode);
            Assert.Equal("MMM D, YYYY", saved.DateFormatEn);

            var pubEn = await BizTest.ReadAsync<PublicSiteSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/site-settings?lang=en"));
            Assert.Equal("hide", pubEn.FallbackMode);
            Assert.Equal("MMM D, YYYY", pubEn.Formats.DateFormat);
            Assert.Equal(".", pubEn.Formats.ThousandsSeparator);
            Assert.Equal(",", pubEn.Formats.DecimalSeparator);
            var pubZh = await BizTest.ReadAsync<PublicSiteSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/site-settings?lang=zh"));
            Assert.Equal(",", pubZh.Formats.ThousandsSeparator);
            Assert.Contains(pubZh.Languages, l => l.Code == "zh-Hant" && l.IsDefault);

            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, I18n + "/settings", new { fallbackMode = "delete" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, I18n + "/settings", new { dateFormatZh = "隨便寫" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, I18n + "/settings", new { dateFormatZh = "YYYY-MM" })).StatusCode); // 缺日
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, I18n + "/settings", new { numberFormatZh = "12,345.67" })).StatusCode);

            // 空白＝清除，備援模式回到預設
            var cleared = await BizTest.ReadAsync<AdminI18nSettingsDto>(await AppTest.PutJsonAsync(admin, I18n + "/settings", new { }));
            Assert.Equal("show_default", cleared.FallbackMode);
            Assert.Null(cleared.DateFormatZh);
        }
        finally
        {
            await restore();
        }
    }

    [Fact]
    public async Task 翻譯狀態總覽_缺英文篩選_類別摘要_關鍵字_翻譯人員可看()
    {
        var zhOnly = Guid.NewGuid();
        var both = Guid.NewGuid();
        var translator = await SiteSettingsTest.CreateTranslatorAsync();
        var marker = "ZZTRANS" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                DECLARE @cat uniqueidentifier = (SELECT TOP 1 id FROM article_categories ORDER BY row_seq);
                INSERT INTO articles (id, club_id, slug, article_category_id, status) VALUES (@A, @club, @SA, @cat, 'draft');
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@A, N'zh-Hant', @TA);
                INSERT INTO articles (id, club_id, slug, article_category_id, status) VALUES (@B, @club, @SB, @cat, 'draft');
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@B, N'zh-Hant', @TB);
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@B, N'en', N'English title');
                """,
                ("@A", zhOnly), ("@SA", "zz-trans-" + zhOnly.ToString("N")[..10]), ("@TA", marker + " 只有繁中"),
                ("@B", both), ("@SB", "zz-trans-" + both.ToString("N")[..10]), ("@TB", marker + " 兩種語言"));

            using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            var overview = await BizTest.ReadAsync<AdminTranslationOverviewDto>(
                await admin.GetAsync($"{I18n}/overview?type=article&keyword={marker}"));
            Assert.Equal(2, overview.TotalCount);
            Assert.Contains("en", overview.Locales);
            var a = overview.Items.Single(i => i.Id == zhOnly);
            Assert.True(a.Done["zh-Hant"]);
            Assert.False(a.Done["en"]);
            Assert.True(overview.Items.Single(i => i.Id == both).Done["en"]);

            var missing = await BizTest.ReadAsync<AdminTranslationOverviewDto>(
                await admin.GetAsync($"{I18n}/overview?type=article&missing=en&keyword={marker}"));
            Assert.Equal([zhOnly], missing.Items.Select(i => i.Id).ToList());
            Assert.True(overview.Summary.Single(s => s.Type == "article").Missing["en"] >= 1);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(I18n + "/overview?type=nonsense")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(I18n + "/overview?missing=fr")).StatusCode);

            // 翻譯人員（只有字串翻譯表權限）也能看總覽
            using var translatorClient = fixture.CreateClient();
            translatorClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(translator));
            Assert.Equal(HttpStatusCode.OK, (await translatorClient.GetAsync(I18n + "/overview")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await translatorClient.PutAsync(I18n + "/settings", BizTest.Json(new { }))).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM articles WHERE slug LIKE 'zz-trans-%'");
            await SiteSettingsTest.DeleteAccountAsync(translator);
        }
    }

    // ── 字串翻譯表 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task 字串翻譯表_新增_重複409_格式400_缺繁中400_前台依語系回退_清除英文_刪除()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var key = Key("a");
        try
        {
            var created = await BizTest.ReadAsync<AdminUiStringDto>(await AppTest.PostJsonAsync(admin, Strings, new
            {
                key, group = "zztest", values = new Dictionary<string, string?> { ["zh-Hant"] = "送出" },
            }));
            Assert.Equal("zztest", created.Group);
            Assert.Equal("送出", created.Values["zh-Hant"]);

            Assert.Equal(HttpStatusCode.Conflict, (await AppTest.PostJsonAsync(admin, Strings, new { key, values = new Dictionary<string, string?> { ["zh-Hant"] = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Strings, new { key = "BAD KEY", values = new Dictionary<string, string?> { ["zh-Hant"] = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Strings, new { key = Key("b"), values = new Dictionary<string, string?> { ["en"] = "only en" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Strings, new { key = Key("c"), values = new Dictionary<string, string?> { ["zh-Hant"] = "x", ["fr"] = "y" } })).StatusCode);

            // 前台：沒有英文先回退繁中；補英文後回英文
            var zhFirst = await BizTest.ReadAsync<PublicUiStringsDto>(await anonymous.GetAsync("/api/v1/ui-strings?lang=en&group=zztest"));
            Assert.Equal("送出", zhFirst.Strings[key]);
            var updated = await BizTest.ReadAsync<AdminUiStringDto>(await AppTest.PutJsonAsync(admin, $"{Strings}/{created.Id}",
                new { values = new Dictionary<string, string?> { ["en"] = "Submit" } }));
            Assert.Equal("Submit", updated.Values["en"]);
            var enNow = await BizTest.ReadAsync<PublicUiStringsDto>(await anonymous.GetAsync("/api/v1/ui-strings?lang=en&group=zztest"));
            Assert.Equal("Submit", enNow.Strings[key]);
            var zhNow = await BizTest.ReadAsync<PublicUiStringsDto>(await anonymous.GetAsync("/api/v1/ui-strings?lang=zh&group=zztest"));
            Assert.Equal("送出", zhNow.Strings[key]);

            // 清除英文（空白）→ 回退；繁中不能清空
            var clearedEn = await BizTest.ReadAsync<AdminUiStringDto>(await AppTest.PutJsonAsync(admin, $"{Strings}/{created.Id}",
                new { values = new Dictionary<string, string?> { ["en"] = "" } }));
            Assert.False(clearedEn.Values.ContainsKey("en"));
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Strings}/{created.Id}",
                new { values = new Dictionary<string, string?> { ["zh-Hant"] = "" } })).StatusCode);

            // 清單篩選
            var missingEn = await BizTest.ReadAsync<PagedResult<AdminUiStringDto>>(await admin.GetAsync($"{Strings}?group=zztest&missing=en"));
            Assert.Contains(missingEn.Items, i => i.Id == created.Id);
            var byKeyword = await BizTest.ReadAsync<PagedResult<AdminUiStringDto>>(await admin.GetAsync($"{Strings}?keyword=送出&group=zztest"));
            Assert.Contains(byKeyword.Items, i => i.Id == created.Id);
            var groups = await BizTest.ReadAsync<List<string>>(await admin.GetAsync(Strings + "/groups"));
            Assert.Contains("zztest", groups);

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Strings}/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Strings}/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PutJsonAsync(admin, $"{Strings}/{Guid.NewGuid()}", new { values = new Dictionary<string, string?>() })).StatusCode);
        }
        finally
        {
            await CleanupStringsAsync();
        }
    }

    [Fact]
    public async Task 字串翻譯表_翻譯人員只能改非繁中語系_改繁中或分組或新增刪除一律403_檢視者403()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        var translator = await SiteSettingsTest.CreateTranslatorAsync();
        var key = Key("t");
        try
        {
            var created = await BizTest.ReadAsync<AdminUiStringDto>(await AppTest.PostJsonAsync(admin, Strings, new
            {
                key, group = "zztest", values = new Dictionary<string, string?> { ["zh-Hant"] = "原文" },
            }));

            using var tr = fixture.CreateClient();
            tr.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(translator));

            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Strings)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await tr.GetAsync(Strings + "?group=zztest")).StatusCode);

            // 翻成英文可以；同時把繁中原文原樣重送（沒改）也可以
            var ok = await BizTest.ReadAsync<AdminUiStringDto>(await AppTest.PutJsonAsync(tr, $"{Strings}/{created.Id}",
                new { values = new Dictionary<string, string?> { ["zh-Hant"] = "原文", ["en"] = "Source" } }));
            Assert.Equal("Source", ok.Values["en"]);

            // 改繁中原文、改分組、新增、刪除：全部 403，且資料不變
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(tr, $"{Strings}/{created.Id}",
                new { values = new Dictionary<string, string?> { ["zh-Hant"] = "被竄改", ["en"] = "Hacked" } })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PutJsonAsync(tr, $"{Strings}/{created.Id}", new { group = "other" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(tr, Strings,
                new { key = Key("n"), values = new Dictionary<string, string?> { ["zh-Hant"] = "x" } })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await tr.DeleteAsync($"{Strings}/{created.Id}")).StatusCode);

            var after = await BizTest.ReadAsync<PagedResult<AdminUiStringDto>>(await admin.GetAsync($"{Strings}?group=zztest"));
            var row = after.Items.Single(i => i.Id == created.Id);
            Assert.Equal("原文", row.Values["zh-Hant"]);
            Assert.Equal("Source", row.Values["en"]);
            Assert.Equal("zztest", row.Group);
        }
        finally
        {
            await CleanupStringsAsync();
            await SiteSettingsTest.DeleteAccountAsync(translator);
        }
    }

    // ── 場地 ───────────────────────────────────────────────────────────────────────
    private static async Task CleanupVenuesAsync()
    {
        await BizTest.ExecuteSqlAsync("DELETE FROM trials WHERE venue_id IN (SELECT venue_id FROM venues_i18n WHERE name LIKE 'ZZTEST%')");
        await BizTest.ExecuteSqlAsync("DELETE FROM venues WHERE id IN (SELECT venue_id FROM venues_i18n WHERE name LIKE 'ZZTEST%')");
    }

    private static object VenuePayload(string name, decimal? lat = 24.1815m, decimal? lng = 120.6064m)
        => new
        {
            zh = new { name, address = "台中市西屯區", directions = "搭乘公車 100 路", photoAlt = "球場全景" },
            en = new { name = name + " EN", address = "Xitun, Taichung" },
            lat, lng, sortOrder = 0,
        };

    [Fact]
    public async Task 場地_新增_讀取_修改_經緯度驗證_前台依語系與引用顯示_刪除前檢查引用()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var name = "ZZTEST 場地 " + Guid.NewGuid().ToString("N")[..6];
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(VenuePayload(name)))).StatusCode);

            var created = await BizTest.ReadAsync<AdminVenueDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(VenuePayload(name))));
            Assert.Equal(name, created.Zh.Name);
            Assert.Equal(24.1815m, created.Lat);
            Assert.Equal("Xitun, Taichung", created.En!.Address);
            Assert.Equal(0, created.UsageCount);
            Assert.Null(created.PhotoUrl);

            var list = await BizTest.ReadAsync<List<AdminVenueListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/venues"));
            Assert.Contains(list, v => v.Id == created.Id && v.Lat == 24.1815m);

            // 驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(VenuePayload(name, lat: 24.1m, lng: null)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(VenuePayload(name, lat: 91m, lng: 120m)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(VenuePayload(name, lat: 24m, lng: 181m)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(new { zh = new { name = "" }, sortOrder = 0 }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/venues", BizTest.Multipart(new { zh = new { name = "ZZTEST x" }, removePhoto = true }))).StatusCode);

            // 修改（英文名稱留空＝刪除英文版）
            var updated = await BizTest.ReadAsync<AdminVenueDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/venues/{created.Id}", BizTest.Multipart(new
            {
                zh = new { name, address = "新地址", directions = "新的交通說明" }, en = new { name = "" }, lat = 25.0m, lng = 121.5m, sortOrder = 3,
            })));
            Assert.Equal("新地址", updated.Zh.Address);
            Assert.Null(updated.En);
            Assert.Equal(3, updated.SortOrder);

            // 前台：沒有被任何資料引用且不是主場 → 不出現；被試訓引用後出現
            Assert.DoesNotContain(await BizTest.ReadAsync<List<PublicVenueDto>>(await anonymous.GetAsync("/api/v1/tcrfc/venues")), v => v.Id == created.Id);
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                INSERT INTO trials (id, club_id, venue_id, trial_on) VALUES (NEWID(), @club, @V, DATEADD(day, 20, CAST(SYSUTCDATETIME() AS date)));
                """,
                ("@V", created.Id));
            var pub = await BizTest.ReadAsync<List<PublicVenueDto>>(await anonymous.GetAsync("/api/v1/tcrfc/venues?lang=en"));
            var shown = pub.Single(v => v.Id == created.Id);
            Assert.Equal(name, shown.Name); // 英文版已刪，回退繁中
            Assert.Equal(25.0m, shown.Lat);
            Assert.False(shown.IsHome);
            Assert.DoesNotContain(await BizTest.ReadAsync<List<PublicVenueDto>>(await anonymous.GetAsync("/api/v1/bw/venues")), v => v.Id == created.Id);

            // 被引用不能刪
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/venues/{created.Id}")).StatusCode);
            Assert.Equal(1, (await BizTest.ReadAsync<AdminVenueDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/venues/{created.Id}"))).UsageCount);
            await BizTest.ExecuteSqlAsync("DELETE FROM trials WHERE venue_id = @V", ("@V", created.Id));
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/venues/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/venues/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/admin/tcrfc/venues/{Guid.NewGuid()}", BizTest.Multipart(VenuePayload(name)))).StatusCode);
        }
        finally
        {
            await CleanupVenuesAsync();
        }
    }

    [Fact]
    public async Task 場地_由地址定位預覽_查無地址404_權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(viewer, "/api/v1/admin/tcrfc/venues/locate", new { address = "台中市西屯區" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, "/api/v1/admin/tcrfc/venues/locate", new { address = " " })).StatusCode);

        // 測試主機是 Development：預設用本機假定位（不碰外部服務）；若環境刻意設為未啟用則回 503，兩者都接受
        var response = await AppTest.PostJsonAsync(admin, "/api/v1/admin/tcrfc/venues/locate", new { address = "台中市西屯區文心路" });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("lat", body);
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PostJsonAsync(admin, "/api/v1/admin/tcrfc/venues/locate", new { address = "查無此地" })).StatusCode);
        }
    }

    // ── EDM 設定 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task EDM設定_金鑰只寫不讀_加密存放_啟用條件_清除_權限()
    {
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "edm.%");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        const string Url = "/api/v1/admin/tcrfc/edm-settings";
        const string secret = "zz-secret-api-key-0123456789";
        try
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM settings WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND setting_key LIKE 'edm.%'");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Url)).StatusCode);
            var initial = await BizTest.ReadAsync<AdminEdmSettingsDto>(await admin.GetAsync(Url));
            Assert.False(initial.Enabled);
            Assert.False(initial.ApiKeyConfigured);
            Assert.False(initial.IntegrationAvailable);

            // 沒有平台名稱或金鑰不能啟用
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { enabled = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { enabled = true, provider = "某平台" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { apiKey = secret, clearApiKey = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { apiKey = "short" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { senderEmail = "not-an-email" })).StatusCode);

            var put = await AppTest.PutJsonAsync(admin, Url, new { enabled = true, provider = "某平台", listId = "L-1", senderEmail = "News@Example.test", apiKey = secret });
            var body = await put.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            Assert.DoesNotContain(secret, body);
            var saved = await BizTest.ReadAsync<AdminEdmSettingsDto>(put);
            Assert.True(saved.ApiKeyConfigured);
            Assert.Equal("news@example.test", saved.SenderEmail);

            // 資料庫裡不是明文
            var stored = await C1Test.ScalarAsync<string>(
                "SELECT s.setting_value FROM settings s JOIN clubs c ON c.id = s.club_id WHERE c.code = N'tcrfc' AND s.setting_key = N'edm.api_key_encrypted'");
            Assert.NotNull(stored);
            Assert.DoesNotContain(secret, stored);

            // 讀取永遠不含金鑰；不帶新金鑰＝維持原金鑰
            Assert.DoesNotContain(secret, await (await admin.GetAsync(Url)).Content.ReadAsStringAsync());
            var kept = await BizTest.ReadAsync<AdminEdmSettingsDto>(await AppTest.PutJsonAsync(admin, Url, new { enabled = true, provider = "某平台" }));
            Assert.True(kept.ApiKeyConfigured);

            // 清除金鑰後不能維持啟用
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, Url, new { enabled = true, provider = "某平台", clearApiKey = true })).StatusCode);
            var cleared = await BizTest.ReadAsync<AdminEdmSettingsDto>(await AppTest.PutJsonAsync(admin, Url, new { enabled = false, provider = "某平台", clearApiKey = true }));
            Assert.False(cleared.ApiKeyConfigured);
            Assert.False(cleared.Enabled);
        }
        finally
        {
            await restore();
        }
    }
}
