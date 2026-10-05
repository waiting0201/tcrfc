using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// iOS／Android 回報的契約缺口（docs/19 §11c／§11d，2026-10-05）：賽事系列清單與單場賽事端點（A1）、MatchDto／TeamDto 的俱樂部與賽事系列代碼（A2）、
/// ETag／If-None-Match → 304（A4）、layout 外連網址與贊助洽詢網址（A6）、裝置識別標頭（A7）、未翻譯標示（A8）。全部唯讀。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppContractGapsTests(AdminWriteApiFixture fixture)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ═════════════ A1：賽事系列清單 ═════════════

    [Fact]
    public async Task 賽事系列清單_只回本俱樂部已發布的_帶代碼與球季_英文缺漏標示未翻譯()
    {
        using var client = fixture.CreateClient();
        var tcrfc = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/competitions"))).EnumerateArray().ToList();
        Assert.NotEmpty(tcrfc);
        Assert.All(tcrfc, c =>
        {
            Assert.Equal("tcrfc", c.GetProperty("clubCode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("seasonCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("name").GetString()));
            Assert.False(c.GetProperty("isFallbackLocale").GetBoolean()); // 請求繁中
        });
        Assert.Contains(tcrfc, c => c.GetProperty("code").GetString() == "enterprise-a");

        var bw = (await JsonAsync(await client.GetAsync("/api/v1/bw/competitions"))).EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToList();
        Assert.DoesNotContain("enterprise-a", bw); // 別的俱樂部的系列不外洩
        Assert.Contains("mulan", bw);

        // 英文缺漏的回退規則：不依賴種子（種子已補英文列），自建一個只有繁中名稱的系列與一個有英文名稱的系列。
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var seasonId = await BizTest.ScalarGuidAsync("SELECT id FROM seasons WHERE club_id = @C AND code = N'2026-27'", ("@C", clubId));
        var codeZhOnly = BizTest.Unique("cgz");
        var codeWithEn = BizTest.Unique("cge");
        var idZhOnly = Guid.NewGuid();
        var idWithEn = Guid.NewGuid();
        try
        {
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO competitions (id, club_id, season_id, code, status) VALUES (@A, @C, @S, @CA, N'published'), (@B, @C, @S, @CB, N'published')",
                ("@A", idZhOnly), ("@B", idWithEn), ("@C", clubId), ("@S", seasonId), ("@CA", codeZhOnly), ("@CB", codeWithEn));
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@A, N'zh-Hant', N'測試僅繁中系列'), (@B, N'zh-Hant', N'測試有英文系列'), (@B, N'en', N'Test Series With English')",
                ("@A", idZhOnly), ("@B", idWithEn));

            var en = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/competitions?lang=en"))).EnumerateArray().ToList();
            var enZhOnly = en.Single(c => c.GetProperty("code").GetString() == codeZhOnly);
            Assert.True(enZhOnly.GetProperty("isFallbackLocale").GetBoolean());
            Assert.Equal("測試僅繁中系列", enZhOnly.GetProperty("name").GetString()); // 回退繁中名稱
            var enWithEn = en.Single(c => c.GetProperty("code").GetString() == codeWithEn);
            Assert.False(enWithEn.GetProperty("isFallbackLocale").GetBoolean());
            Assert.Equal("Test Series With English", enWithEn.GetProperty("name").GetString());
        }
        finally
        {
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM competitions_i18n WHERE competition_id IN (@A, @B); DELETE FROM competitions WHERE id IN (@A, @B)",
                ("@A", idZhOnly), ("@B", idWithEn));
        }

        // 球季篩選
        var season = tcrfc[0].GetProperty("seasonCode").GetString();
        var bySeason = (await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/competitions?season={season}"))).EnumerateArray().ToList();
        Assert.All(bySeason, c => Assert.Equal(season, c.GetProperty("seasonCode").GetString()));
        Assert.Empty((await JsonAsync(await client.GetAsync("/api/v1/tcrfc/competitions?season=1999-00"))).EnumerateArray());
    }

    // ═════════════ A1／A2：賽事清單的新篩選、新欄位、單場端點 ═════════════

    [Fact]
    public async Task 賽程_帶俱樂部與賽事系列代碼_可依系列與期間篩選_單場端點只認本俱樂部()
    {
        using var client = fixture.CreateClient();
        var all = await JsonAsync(await client.GetAsync("/api/v1/tcrfc/schedule?pageSize=100"));
        var items = all.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, m =>
        {
            Assert.Equal("tcrfc", m.GetProperty("clubCode").GetString());
            Assert.Equal("enterprise-a", m.GetProperty("competitionCode").GetString());
        });

        // 依賽事系列：命中全部；不存在的代碼 → 空
        var byComp = await JsonAsync(await client.GetAsync("/api/v1/tcrfc/schedule?competition=enterprise-a&pageSize=100"));
        Assert.Equal(all.GetProperty("totalCount").GetInt32(), byComp.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/schedule?competition=mulan"))).GetProperty("totalCount").GetInt32()); // 別俱樂部的系列
        Assert.Equal(0, (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/schedule?competition=zz-none"))).GetProperty("totalCount").GetInt32());

        // 依期間（含頭尾）
        var dates = items.Select(m => m.GetProperty("matchOn").GetString()!).OrderBy(d => d, StringComparer.Ordinal).ToList();
        var mid = dates[dates.Count / 2];
        var from = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/schedule?from={mid}&pageSize=100"));
        Assert.All(from.GetProperty("items").EnumerateArray(), m => Assert.True(string.CompareOrdinal(m.GetProperty("matchOn").GetString(), mid) >= 0));
        var to = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/schedule?to={mid}&pageSize=100"));
        Assert.All(to.GetProperty("items").EnumerateArray(), m => Assert.True(string.CompareOrdinal(m.GetProperty("matchOn").GetString(), mid) <= 0));
        var exact = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/schedule?from={mid}&to={mid}&pageSize=100"));
        Assert.All(exact.GetProperty("items").EnumerateArray(), m => Assert.Equal(mid, m.GetProperty("matchOn").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tcrfc/schedule?from=2026-12-31&to=2026-01-01")).StatusCode);

        // 單場
        var first = items[0];
        var id = first.GetProperty("id").GetGuid();
        var single = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/schedule/{id}"));
        Assert.Equal(id, single.GetProperty("id").GetGuid());
        Assert.Equal("tcrfc", single.GetProperty("clubCode").GetString());
        Assert.Equal(first.GetProperty("matchOn").GetString(), single.GetProperty("matchOn").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/bw/schedule/{id}")).StatusCode); // 別的俱樂部看不到
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/tcrfc/schedule/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task 球隊與球員與賽事_英文缺漏時標示未翻譯_繁中恆為false()
    {
        using var client = fixture.CreateClient();
        foreach (var path in new[] { "teams", "players?pageSize=200", "staff" })
        {
            var sep = path.Contains('?') ? "&" : "?";
            var zh = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/{path}{sep}lang=zh"));
            var zhItems = zh.ValueKind == JsonValueKind.Array ? zh.EnumerateArray() : zh.GetProperty("items").EnumerateArray();
            Assert.All(zhItems, i => Assert.False(i.GetProperty("isFallbackLocale").GetBoolean()));
        }

        // 種子有些球員沒有英文姓名：英文請求 → 該筆 true、姓名是繁中；有英文姓名的 → false
        var en = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/players?pageSize=200&lang=en"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(en, p => p.GetProperty("isFallbackLocale").GetBoolean());
        Assert.Contains(en, p => !p.GetProperty("isFallbackLocale").GetBoolean());
        Assert.All(en, p => Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("name").GetString()))); // 回退後仍有可顯示的名字

        // 單筆球員也帶標示
        var slug = en.First(p => p.GetProperty("isFallbackLocale").GetBoolean()).GetProperty("slug").GetString();
        Assert.True((await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/players/{slug}?lang=en"))).GetProperty("isFallbackLocale").GetBoolean());
    }

    [Fact]
    public async Task 球隊_帶俱樂部代碼_類型與性別在固定值域內()
    {
        using var client = fixture.CreateClient();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var teams = (await JsonAsync(await client.GetAsync($"/api/v1/{club}/teams"))).EnumerateArray().ToList();
            Assert.NotEmpty(teams);
            Assert.All(teams, t =>
            {
                Assert.Equal(club, t.GetProperty("clubCode").GetString());
                Assert.Contains(t.GetProperty("type").GetString(), new[] { "first_team", "academy" }); // shared/enums.json
                Assert.Contains(t.GetProperty("gender").GetString(), new[] { "men", "women", "mixed" });
            });
        }
    }

    // ═════════════ A4：ETag／If-None-Match ═════════════

    [Fact]
    public async Task ETag_公開列表帶ETag_重送If_None_Match回304且無本文_內容不同ETag不同()
    {
        using var client = fixture.CreateClient();
        var first = await client.GetAsync("/api/v1/tcrfc/teams");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag?.ToString();
        Assert.False(string.IsNullOrEmpty(etag));
        var body = await first.Content.ReadAsStringAsync();

        // 命中：304、沒有本文、仍帶 ETag
        using var hit = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tcrfc/teams");
        hit.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var notModified = await client.SendAsync(hit);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(string.Empty, await notModified.Content.ReadAsStringAsync());
        Assert.Equal(etag, notModified.Headers.ETag?.ToString());

        // 清單內多個值、弱驗證前綴、* 都算命中
        foreach (var header in new[] { $"\"zzz\", {etag}", $"W/{etag}", "*" })
        {
            using var r = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tcrfc/teams");
            r.Headers.TryAddWithoutValidation("If-None-Match", header);
            Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(r)).StatusCode);
        }

        // 沒命中（舊的或亂寫的 ETag）→ 200 完整本文，與第一次相同
        using var miss = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tcrfc/teams");
        miss.Headers.TryAddWithoutValidation("If-None-Match", "\"stale\"");
        var full = await client.SendAsync(miss);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal(body, await full.Content.ReadAsStringAsync());

        // 本文不同（英文 vs 繁中）→ ETag 不同；用繁中的 ETag 請求英文不會被誤判為未變更
        using var en = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tcrfc/teams?lang=en");
        en.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var enResponse = await client.SendAsync(en);
        Assert.Equal(HttpStatusCode.OK, enResponse.StatusCode);
        Assert.NotEqual(etag, enResponse.Headers.ETag?.ToString());
    }

    [Theory]
    [InlineData("/api/v1/tcrfc/programs")]            // 課程含即時名額
    [InlineData("/api/v1/tcrfc/shop/products")]       // 商店含庫存
    [InlineData("/api/v1/tcrfc/shop/cart")]           // 購物車
    [InlineData("/api/v1/member/memberships")]        // 會員一族
    [InlineData("/api/v1/app/notifications")]         // 依裝置而異
    public async Task ETag_不得讀快取的五類與個人化端點_完全不帶ETag_也不會回304(string path)
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        var response = await client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public void ETag白名單_路徑判斷_只涵蓋公開內容列表()
    {
        foreach (var ok in new[] { "/api/v1/clubs", "/api/v1/clubs/tcrfc", "/api/v1/app/layout", "/api/v1/app/config", "/api/v1/bw/schedule", "/api/v1/tcrfc/schedule/0b7c1f0e-0000-0000-0000-000000000000",
            "/api/v1/tcrfc/competitions", "/api/v1/tcrfc/players/foo", "/api/v1/tcrfc/news", "/api/v1/tcrfc/news/some-slug", "/api/v1/tcrfc/faqs", "/api/v1/tcrfc/partner-stores" })
        {
            Assert.True(ConditionalGetMiddleware.IsEligiblePath(ok), ok);
        }

        foreach (var no in new[] { "/api/v1/tcrfc/programs", "/api/v1/tcrfc/shop/products", "/api/v1/tcrfc/shop/cart", "/api/v1/member/me", "/api/v1/m/abc", "/api/v1/app/ads/home_top",
            "/api/v1/app/notifications", "/api/v1/app/devices/x/subscriptions", "/api/v1/admin/tcrfc/teams", "/api/v1/tcrfc/membership/plans", "/api/v1/tcrfc/shop/my-orders" })
        {
            Assert.False(ConditionalGetMiddleware.IsEligiblePath(no), no);
        }
    }

    // ═════════════ A6：layout 外連網址；A7：裝置識別標頭 ═════════════

    [Fact]
    public async Task Layout_慈善與商店有外連網址_贊助洽詢網址_英文用en前綴()
    {
        using var client = fixture.CreateClient();
        var zh = await JsonAsync(await client.GetAsync("/api/v1/app/layout?lang=zh"));
        var more = zh.GetProperty("moreItems").EnumerateArray().ToDictionary(i => i.GetProperty("code").GetString()!);
        Assert.Equal("/zh/shop/", more["shop"].GetProperty("webUrl").GetString());
        Assert.False(more["shop"].GetProperty("isExternal").GetBoolean());
        var charity = more["charity"];
        Assert.StartsWith("https://", charity.GetProperty("webUrl").GetString());
        Assert.True(charity.GetProperty("isExternal").GetBoolean()); // 站外：用戶端須先顯示收受者說明
        Assert.Equal("/zh/partners/become-a-partner/", zh.GetProperty("sponsorshipInquiryWebUrl").GetString());

        var en = await JsonAsync(await client.GetAsync("/api/v1/app/layout?lang=en"));
        Assert.Equal("/en/shop/", en.GetProperty("moreItems").EnumerateArray().First(i => i.GetProperty("code").GetString() == "shop").GetProperty("webUrl").GetString());
        Assert.Equal("/en/partners/become-a-partner/", en.GetProperty("sponsorshipInquiryWebUrl").GetString());
    }

    [Fact]
    public async Task 裝置識別標頭_標頭與查詢參數等效_格式錯400_沒帶不拒絕且可邊緣快取()
    {
        using var client = fixture.CreateClient();

        // 沒帶：照舊（全體對象內容、可邊緣快取）
        var anonymous = await client.GetAsync("/api/v1/app/layout");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Contains("public", anonymous.Headers.CacheControl?.ToString() ?? "");

        // 標頭帶了合法值：依裝置而異 → private, no-store
        using var withHeader = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/layout");
        withHeader.Headers.Add(Tcrfc.Api.Features.AppPublic.AppInput.DeviceHeaderName, "test-device-0001");
        var ok = await client.SendAsync(withHeader);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("no-store", ok.Headers.CacheControl?.ToString() ?? "");

        // 查詢參數仍相容
        var query = await client.GetAsync("/api/v1/app/layout?deviceInstallId=test-device-0001");
        Assert.Contains("no-store", query.Headers.CacheControl?.ToString() ?? "");

        // 格式不合 → 400（日常中文，且有統一錯誤結構）
        using var bad = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/notifications");
        bad.Headers.Add(Tcrfc.Api.Features.AppPublic.AppInput.DeviceHeaderName, "bad id with spaces!");
        var rejected = await client.SendAsync(bad);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("validation_failed", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // 其他公開內容端點接受但忽略這個標頭（不報錯）
        using var ignored = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tcrfc/teams");
        ignored.Headers.Add(Tcrfc.Api.Features.AppPublic.AppInput.DeviceHeaderName, "test-device-0001");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(ignored)).StatusCode);
    }
}
