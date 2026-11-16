using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminApp;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>稽核 docs/23 M-3／M-5：後台「給 App 使用者點的連結」不得是樣板；解析器與 shared/deeplinks.json 一致。純單元，不連資料庫。</summary>
public sealed class AppLinkContractTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }

    [Fact]
    public void 內嵌契約與_shared_deeplinks_逐字相同()
    {
        var shared = File.ReadAllText(RepoFile("shared/deeplinks.json")).Replace("\r\n", "\n");
        using var stream = typeof(AppLinkContract).Assembly.GetManifestResourceStream("deeplinks.contract.json")!;
        var embedded = new StreamReader(stream).ReadToEnd().Replace("\r\n", "\n");
        Assert.True(shared == embedded, "apps/api/Features/AdminApp/deeplinks.contract.json 與 shared/deeplinks.json 不同；執行 shared/scripts/gen-all.sh 或 cp shared/deeplinks.json apps/api/Features/AdminApp/deeplinks.contract.json。");
    }

    [Fact]
    public void 契約檔的_parseCases_自訂_scheme_逐案相同()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("shared/deeplinks.json")));
        var checkedCases = 0;
        foreach (var c in doc.RootElement.GetProperty("parseCases").EnumerateArray())
        {
            var input = c.GetProperty("input").GetString()!;
            if (!input.StartsWith("tcrfc://", StringComparison.Ordinal))
            {
                continue; // 官網網址的解析不在後台驗證範圍
            }

            var parsed = AppLinkContract.Instance.TryParse(input);
            Assert.NotNull(parsed);
            var expect = c.GetProperty("expect");
            Assert.Equal(expect.GetProperty("route").GetString(), parsed!.RouteId);
            var expected = expect.GetProperty("params").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            Assert.Equal(expected, parsed.Params.ToDictionary(p => p.Key, p => p.Value));
            checkedCases++;
        }

        Assert.True(checkedCases >= 9);
    }

    [Theory]
    [InlineData("tcrfc://news/{slug}")]
    [InlineData("tcrfc://match/{id}")]
    [InlineData("tcrfc://schedule/{team}")]
    [InlineData("tcrfc://news/abc}")]
    [InlineData("tcrfc://unknown/x")]
    [InlineData("tcrfc://schedule/zzz")]
    [InlineData("tcrfc://news")]
    [InlineData("tcrfc://news/a?x=1")]
    public void 解析器拒收樣板與無法解析的連結(string link) => Assert.Null(AppLinkContract.Instance.TryParse(link));

    [Theory]
    [InlineData("tcrfc://news/{slug}", "最新消息")]
    [InlineData("tcrfc://match/{id}", "比賽")]
    [InlineData("tcrfc://store/{id}", "特約店家")]
    [InlineData("tcrfc://program/{slug}", "課程")]
    [InlineData("tcrfc://player/{slug}", "球員")]
    public void 樣板的錯誤訊息指出缺哪種對象且不露技術詞(string link, string noun)
    {
        var message = AppLinkValidator.CheckSyntax(link, "點擊後前往");
        Assert.NotNull(message);
        Assert.Contains("還缺少", message);
        Assert.Contains(noun, message);
        Assert.DoesNotContain("tcrfc", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{", message);
        Assert.DoesNotContain("slug", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("tcrfc://news/{slug}")]
    [InlineData("tcrfc://news/abc}")]
    [InlineData("tcrfc://unknown/x")]
    [InlineData("tcrfc://schedule/zzz")]
    [InlineData("tcrfc://news")]
    [InlineData("tcrfc://news/a?x=1")]
    public void 語法層拒絕的連結(string link) => Assert.NotNull(AppLinkValidator.CheckSyntax(link, "連結"));

    [Theory]
    [InlineData("tcrfc://membercard")]
    [InlineData("tcrfc://upgrade")]
    [InlineData("tcrfc://schedule/d1")]
    [InlineData("tcrfc://schedule/BW1")]
    [InlineData("tcrfc://news/opening-day")]
    [InlineData("tcrfc://match/123")]
    public void 語法層接受的連結(string link) => Assert.Null(AppLinkValidator.CheckSyntax(link, "連結"));

    [Fact]
    public void 靜態驗證也拒絕樣板與含大括號的網址()
    {
        Assert.ThrowsAny<AdminValidationException>(() => AdminInput.OptionalHttpOrAppLink("tcrfc://news/{slug}", "點擊目的地", 500, "clickUrl"));
        Assert.ThrowsAny<AdminValidationException>(() => AdminInput.OptionalHttpOrAppLink("https://x.test/{a}", "點擊目的地", 500, "clickUrl"));
        Assert.Equal("tcrfc://membercard", AdminInput.OptionalHttpOrAppLink("tcrfc://membercard", "點擊目的地"));
    }

    [Fact]
    public void 契約每條路由都有選單名稱與對象說法_契約新增路由時這裡會紅燈()
    {
        var destinations = AppLinkValidator.Destinations();
        Assert.DoesNotContain(destinations, d => d.Label.Contains("其他"));
        foreach (var route in AppLinkContract.Instance.Routes)
        {
            Assert.Contains(destinations, d => d.Key == route.Id || d.Key.StartsWith(route.Id + ":", StringComparison.Ordinal));
            if (route.Params.Count > 0)
            {
                Assert.True(AppLinkValidator.TargetNouns.ContainsKey(route.Id), $"路由 {route.Id} 缺少對象說法");
            }
        }

        Assert.All(destinations.Where(d => d.Link is not null), d => Assert.NotNull(AppLinkContract.Instance.TryParse(d.Link!)));
        Assert.All(destinations, d => Assert.DoesNotContain("tcrfc", d.Label, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>連結寫入驗證與選擇器端點（整合）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppLinkWriteTests(AdminWriteApiFixture fixture)
{
    private const string Base = "/api/v1/admin/app";

    private static object Announcement(string? link) => new { message = new { zh = "ZZTEST 連結公告" }, linkUrl = link, audienceTier = "all", isEnabled = false };

    private static Task CleanupAsync() => BizTest.ExecuteSqlAsync("DELETE FROM app_announcements WHERE id IN (SELECT app_announcement_id FROM app_announcements_i18n WHERE message LIKE 'ZZTEST%')");

    [Fact]
    public async Task 公告連結_樣板_缺對象_對象不存在或未公開都被拒_合法連結可存()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var slug = await C1Test.ScalarAsync<string>("SELECT TOP 1 slug FROM articles WHERE status = 'published' AND (published_at IS NULL OR published_at <= SYSUTCDATETIME()) ORDER BY row_seq");
        var matchId = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM matches ORDER BY row_seq");
        try
        {
            foreach (var bad in new[] { "tcrfc://news/{slug}", "tcrfc://match/{id}", "tcrfc://program/{slug}", "tcrfc://news/zz-no-such-article", "tcrfc://match/" + Guid.NewGuid(),
                                        "tcrfc://schedule/zz", "tcrfc://unknown/1", "https://x.test/{a}", "javascript:alert(1)" })
            {
                var r = await AppTest.PostJsonAsync(admin, Base + "/layout/announcements", Announcement(bad));
                var body = await r.Content.ReadAsStringAsync();
                Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{bad} 應被拒：{(int)r.StatusCode} {body}");
                Assert.Contains("linkUrl", body);
                Assert.DoesNotContain("{slug}", body);
            }

            var missing = await AppTest.PostJsonAsync(admin, Base + "/layout/announcements", Announcement("tcrfc://news/{slug}"));
            Assert.Contains("還缺少要前往的對象", await missing.Content.ReadAsStringAsync());

            foreach (var good in new[] { null, "tcrfc://membercard", "tcrfc://upgrade", "tcrfc://schedule/d1", $"tcrfc://news/{slug}", $"tcrfc://match/{matchId}", "https://x.test/a" })
            {
                var r = await AppTest.PostJsonAsync(admin, Base + "/layout/announcements", Announcement(good));
                Assert.True(r.StatusCode == HttpStatusCode.Created, $"{good} 應可存：{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            }
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 公告_連結沒改時舊樣板不擋其他欄位的修改_改成另一個樣板才擋()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var id = Guid.NewGuid();
            await BizTest.ExecuteSqlAsync("INSERT INTO app_announcements (id, link_url, audience_tier, is_enabled) VALUES (@I, 'tcrfc://news/{slug}', 'all', 0)", ("@I", id));
            await BizTest.ExecuteSqlAsync("INSERT INTO app_announcements_i18n (app_announcement_id, locale, message) VALUES (@I, 'zh-Hant', N'ZZTEST 舊樣板')", ("@I", id));
            var same = await AppTest.PutJsonAsync(admin, $"{Base}/layout/announcements/{id}", new { message = new { zh = "ZZTEST 舊樣板改字" }, linkUrl = "tcrfc://news/{slug}", audienceTier = "all", isEnabled = false });
            Assert.Equal(HttpStatusCode.OK, same.StatusCode);
            var other = await AppTest.PutJsonAsync(admin, $"{Base}/layout/announcements/{id}", new { message = new { zh = "ZZTEST 舊樣板改字" }, linkUrl = "tcrfc://player/{slug}", audienceTier = "all", isEnabled = false });
            Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 推播點擊目標_樣板與不存在的對象被拒_合法可存()
    {
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        var slug = await C1Test.ScalarAsync<string>("SELECT TOP 1 slug FROM articles WHERE status = 'published' AND (published_at IS NULL OR published_at <= SYSUTCDATETIME()) ORDER BY row_seq");
        try
        {
            HttpContent Payload(string? link) => BizTest.Multipart(new { content = new { zh = new { title = "ZZTEST 連結推播", body = "ZZTEST 內文" } }, deepLink = link });
            foreach (var bad in new[] { "tcrfc://news/{slug}", "tcrfc://store/{id}", "tcrfc://news/zz-no-such-article", "tcrfc://program/zz-none" })
            {
                var r = await pr.PostAsync(Base + "/push/messages", Payload(bad));
                Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{bad}：{(int)r.StatusCode}");
                Assert.Contains("deepLink", await r.Content.ReadAsStringAsync());
            }

            foreach (var good in new[] { null, "tcrfc://membercard", $"tcrfc://news/{slug}", "https://x.test/a" })
            {
                var r = await pr.PostAsync(Base + "/push/messages", Payload(good));
                Assert.True(r.StatusCode == HttpStatusCode.Created, $"{good}：{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            }
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM push_messages WHERE id IN (SELECT push_message_id FROM push_messages_i18n WHERE title LIKE 'ZZTEST%')");
        }
    }

    [Fact]
    public async Task 版面項目_不能選樣板那一列_沒換選時不被舊樣板擋_深連結對照表仍可存樣板()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var tpl = await AppTest.ReadAsync<AdminAppDeepLinkDto>(await AppTest.PostJsonAsync(admin, Base + "/layout/deep-links", new { code = "zztest_tpl", appLink = "tcrfc://news/{slug}", webUrl = "/zh/news/{slug}", label = new { zh = "ZZTEST 樣板列" } }));
            var ok = await AppTest.ReadAsync<AdminAppDeepLinkDto>(await AppTest.PostJsonAsync(admin, Base + "/layout/deep-links", new { code = "zztest_ok", appLink = "tcrfc://upgrade", label = new { zh = "ZZTEST 完整列" } }));
            var bad = await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "quick_entry", itemKey = "zztest_bad", deepLinkId = tpl.Id, label = new { zh = "ZZTEST 入口" } });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            var text = await bad.Content.ReadAsStringAsync();
            Assert.Contains("deepLinkId", text);
            Assert.Contains("還缺少要前往的對象", text);

            var item = await AppTest.ReadAsync<AdminAppLayoutItemDto>(await AppTest.PostJsonAsync(admin, Base + "/layout/items", new { kind = "quick_entry", itemKey = "zztest_good", deepLinkId = ok.Id, label = new { zh = "ZZTEST 入口" } }));
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Base}/layout/items/{item.Id}", new { deepLinkId = tpl.Id, isEnabled = true, label = new { zh = "ZZTEST 入口" } })).StatusCode);

            // 舊資料：項目已經綁著樣板列，只切開關／改名稱不被擋；換選別的完整列可以
            await BizTest.ExecuteSqlAsync("UPDATE app_layout_items SET deep_link_id = @L WHERE id = @I", ("@L", tpl.Id), ("@I", item.Id));
            Assert.Equal(HttpStatusCode.OK, (await AppTest.PutJsonAsync(admin, $"{Base}/layout/items/{item.Id}", new { deepLinkId = tpl.Id, isEnabled = false, label = new { zh = "ZZTEST 入口改名" } })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AppTest.PutJsonAsync(admin, $"{Base}/layout/items/{item.Id}", new { deepLinkId = ok.Id, isEnabled = false, label = new { zh = "ZZTEST 入口改名" } })).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM app_layout_items WHERE item_key LIKE 'zztest\\_%' ESCAPE '\\'");
            await BizTest.ExecuteSqlAsync("DELETE FROM app_deep_links WHERE code LIKE 'zztest\\_%' ESCAPE '\\'");
        }
    }

    [Fact]
    public async Task 選擇器端點_目的地清單_對象搜尋只回已公開且連結完整_還原四種狀態_沒有權限被拒()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(Base + "/links/destinations")).StatusCode);

        var destinations = await AppTest.ReadAsync<List<AppLinkDestinationDto>>(await admin.GetAsync(Base + "/links/destinations"));
        Assert.Contains(destinations, d => d.TargetKind == "news");
        Assert.Contains(destinations, d => d.Link == "tcrfc://membercard");
        Assert.All(destinations.Where(d => d.Link is not null), d => Assert.DoesNotContain("{", d.Link!));

        foreach (var kind in new[] { "news", "match", "program", "player", "store" })
        {
            var targets = await AppTest.ReadAsync<List<AppLinkTargetDto>>(await admin.GetAsync($"{Base}/links/targets?kind={kind}"));
            Assert.All(targets, t =>
            {
                Assert.DoesNotContain("{", t.Link);
                Assert.StartsWith($"tcrfc://{kind}/", t.Link);
            });
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Base + "/links/targets?kind=zzz")).StatusCode);

        // 每個搜到的對象，選出來的連結都要能通過寫入驗證
        var news = await AppTest.ReadAsync<List<AppLinkTargetDto>>(await admin.GetAsync(Base + "/links/targets?kind=news"));
        if (news.Count > 0)
        {
            var d = await AppTest.ReadAsync<AppLinkDescriptionDto>(await AppTest.PostJsonAsync(admin, Base + "/links/describe", new { link = news[0].Link }));
            Assert.Equal("app", d.Status);
            Assert.Equal("news", d.DestinationKey);
            Assert.Equal(news[0].Label, d.TargetLabel);
        }

        async Task<AppLinkDescriptionDto> Describe(string? link) => await AppTest.ReadAsync<AppLinkDescriptionDto>(await AppTest.PostJsonAsync(admin, Base + "/links/describe", new { link }));
        Assert.Equal("empty", (await Describe(null)).Status);
        Assert.Equal("web", (await Describe("https://x.test/a")).Status);
        Assert.Equal("template", (await Describe("tcrfc://news/{slug}")).Status);
        Assert.Equal("unrecognized", (await Describe("tcrfc://nope/1")).Status);
        var missing = await Describe("tcrfc://news/zz-no-such-article");
        Assert.Equal("missing", missing.Status);
        Assert.Equal("news", missing.DestinationKey);
        Assert.Equal("schedule:d1", (await Describe("tcrfc://schedule/d1")).DestinationKey);
        Assert.All(new[] { missing.Message }, m => Assert.DoesNotContain("tcrfc", m!, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>遷移 20261116_app-link-template-cleanup.sql：只動值含大括號的使用者連結、冪等、不動深連結對照表。包在外層交易裡跑完回滾，不留痕跡。</summary>
    [Fact]
    public async Task 遷移_只清樣板值_冪等_不動對照表與完整連結()
    {
        var path = Path.Combine(AppContext.BaseDirectory);
        var dir = new DirectoryInfo(path);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "db/migrations/20261116_app-link-template-cleanup.sql")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var script = File.ReadAllText(Path.Combine(dir!.FullName, "db/migrations/20261116_app-link-template-cleanup.sql"));
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        var cs = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")!;
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        async Task Exec(string sql, params (string, object)[] p)
        {
            await using var cmd = new SqlCommand(sql, conn);
            foreach (var (n, v) in p)
            {
                cmd.Parameters.AddWithValue(n, v);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        async Task<T> Scalar<T>(string sql, params (string, object)[] p)
        {
            await using var cmd = new SqlCommand(sql, conn);
            foreach (var (n, v) in p)
            {
                cmd.Parameters.AddWithValue(n, v);
            }

            return (T)(await cmd.ExecuteScalarAsync())!;
        }

        await Exec("BEGIN TRAN");
        try
        {
            var tpl = Guid.NewGuid();
            var good = Guid.NewGuid();
            var item1 = Guid.NewGuid();
            var item2 = Guid.NewGuid();
            var a1 = Guid.NewGuid();
            var a2 = Guid.NewGuid();
            await Exec("INSERT INTO app_deep_links (id, code, app_link, requires_login, is_active, sort_order) VALUES (@I, 'zztest_mig_tpl', 'tcrfc://news/{slug}', 0, 1, 900)", ("@I", tpl));
            await Exec("INSERT INTO app_deep_links (id, code, app_link, requires_login, is_active, sort_order) VALUES (@I, 'zztest_mig_ok', 'tcrfc://upgrade', 0, 1, 901)", ("@I", good));
            await Exec("INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled) VALUES (@I, 'quick_entry', 'zztest_mig_1', @L, 900, 1)", ("@I", item1), ("@L", tpl));
            await Exec("INSERT INTO app_layout_items (id, kind, item_key, deep_link_id, sort_order, is_enabled) VALUES (@I, 'quick_entry', 'zztest_mig_2', @L, 901, 1)", ("@I", item2), ("@L", good));
            await Exec("INSERT INTO app_announcements (id, link_url, audience_tier, is_enabled) VALUES (@I, 'tcrfc://news/{slug}', 'all', 0)", ("@I", a1));
            await Exec("INSERT INTO app_announcements (id, link_url, audience_tier, is_enabled) VALUES (@I, 'tcrfc://membercard', 'all', 0)", ("@I", a2));

            foreach (var round in new[] { 1, 2 }) // 第二次跑＝冪等
            {
                foreach (var b in batches)
                {
                    await Exec(b);
                }
            }

            Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM app_layout_items WHERE id = @I AND deep_link_id IS NOT NULL", ("@I", item1)));
            Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM app_layout_items WHERE id = @I AND deep_link_id = @L", ("@I", item2), ("@L", good)));
            Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM app_announcements WHERE id = @I AND link_url IS NOT NULL", ("@I", a1)));
            Assert.Equal("tcrfc://membercard", await Scalar<string>("SELECT link_url FROM app_announcements WHERE id = @I", ("@I", a2)));
            Assert.Equal("tcrfc://news/{slug}", await Scalar<string>("SELECT app_link FROM app_deep_links WHERE id = @I", ("@I", tpl))); // 對照表不動
            Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM app_layout_items i JOIN app_deep_links l ON l.id = i.deep_link_id WHERE l.app_link LIKE '%{%'"));
        }
        finally
        {
            await Exec("IF @@TRANCOUNT > 0 ROLLBACK");
        }
    }
}
