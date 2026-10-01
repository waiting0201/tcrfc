using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S3-2：08 文化的公開端點——8.1 漫畫（全免費、藍鯨不設）、8.2 球迷會活動（列表／詳情／報名／取消報名）。測試資料用 <c>mtest-</c> 前綴／9000 以上集數，測完清掉。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class CulturePublicTests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await BizTest.ExecuteSqlAsync(
            """
            DELETE FROM fan_event_registrations WHERE fan_event_id IN (SELECT id FROM fan_events WHERE slug LIKE 'mtest-%');
            DELETE FROM fan_event_images WHERE fan_event_id IN (SELECT id FROM fan_events WHERE slug LIKE 'mtest-%');
            DELETE FROM fan_event_articles WHERE fan_event_id IN (SELECT id FROM fan_events WHERE slug LIKE 'mtest-%');
            DELETE FROM fan_events_i18n WHERE fan_event_id IN (SELECT id FROM fan_events WHERE slug LIKE 'mtest-%');
            DELETE FROM fan_events WHERE slug LIKE 'mtest-%';
            DELETE FROM comic_pages WHERE comic_episode_id IN (SELECT id FROM comic_episodes WHERE episode_no >= 9000);
            DELETE FROM comic_episodes_i18n WHERE comic_episode_id IN (SELECT id FROM comic_episodes WHERE episode_no >= 9000);
            DELETE FROM comic_episodes WHERE episode_no >= 9000;
            """);
        await _scope.DisposeAsync();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static string? CodeOf(JsonElement problem) => problem.TryGetProperty("code", out var c) ? c.GetString() : null;

    // ═════════════ 8.1 漫畫 ═════════════

    private static async Task<Guid> InsertEpisodeAsync(int no, string status, string? publishedOnSql, int pages, string? titleEn = null)
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            $"""
            INSERT INTO comic_episodes (id, club_id, episode_no, status, published_on, is_latest)
            VALUES (@Id, (SELECT id FROM clubs WHERE code = 'tcrfc'), @No, @Status, {publishedOnSql ?? "NULL"}, 0);
            INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (@Id, N'zh-Hant', @Title);
            """, ("@Id", id), ("@No", no), ("@Status", status), ("@Title", $"【M測試】第 {no} 集"));
        if (titleEn is not null)
        {
            await BizTest.ExecuteSqlAsync("INSERT INTO comic_episodes_i18n (comic_episode_id, locale, title) VALUES (@Id, N'en', @T)", ("@Id", id), ("@T", titleEn));
        }

        for (var i = 1; i <= pages; i++)
        {
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO comic_pages (id, comic_episode_id, image_key, sort_order, image_width, image_height) VALUES (NEWID(), @Id, @K, @S, 800, 1200)",
                ("@Id", id), ("@K", $"mtest/comic/{no}/{i}.webp"), ("@S", i));
        }

        return id;
    }

    [Fact]
    public async Task 漫畫_藍鯨一律403_不是404()
    {
        using var client = fixture.CreateClient();
        foreach (var path in new[] { "about", "characters", "episodes", "episodes/latest", "episodes/1" })
        {
            var response = await client.GetAsync($"/api/v1/bw/comic/{path}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("藍鯨", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/bw/comic/episodes/1/views", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/nope/comic/episodes")).StatusCode);
    }

    [Fact]
    public async Task 漫畫_列表只含已發布且已到發布日的集數_最新集數即時判定_閱讀器資料含全部頁面與上下集導覽()
    {
        using var client = fixture.CreateClient();
        // 種子的 3 集都是草稿 → 公開列表本來是空的
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/comic/episodes/latest")).StatusCode);

        await InsertEpisodeAsync(9001, "published", "DATEADD(DAY, -10, CAST(SYSUTCDATETIME() AS date))", 3, "Episode 9001");
        await InsertEpisodeAsync(9002, "published", "DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date))", 2);
        await InsertEpisodeAsync(9003, "published", "DATEADD(DAY, 7, CAST(SYSUTCDATETIME() AS date))", 2); // 未來發布日：還沒到
        await InsertEpisodeAsync(9004, "draft", null, 2); // 草稿

        var list = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes"))).EnumerateArray().ToList();
        Assert.Equal(new[] { 9002, 9001 }, list.Select(e => e.GetProperty("episodeNo").GetInt32()).ToArray()); // 新→舊
        Assert.True(list[0].GetProperty("isLatest").GetBoolean());
        Assert.False(list[1].GetProperty("isLatest").GetBoolean());
        Assert.Equal(3, list[1].GetProperty("pageCount").GetInt32());

        var latest = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/latest"));
        Assert.Equal(9002, latest.GetProperty("episodeNo").GetInt32());

        // 詳情：頁面依序、上一集下一集；英文標題缺漏回退繁中
        var detail = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/9001"));
        Assert.Equal(3, detail.GetProperty("pages").GetArrayLength());
        Assert.Equal(new[] { 1, 2, 3 }, detail.GetProperty("pages").EnumerateArray().Select(p => p.GetProperty("pageNo").GetInt32()).ToArray());
        Assert.Equal(800, detail.GetProperty("pages")[0].GetProperty("width").GetInt32());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("previousEpisodeNo").ValueKind);
        Assert.Equal(9002, detail.GetProperty("nextEpisodeNo").GetInt32());
        Assert.False(detail.GetProperty("isLatest").GetBoolean());
        Assert.Equal("Episode 9001", (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/9001?lang=en"))).GetProperty("title").GetString());
        Assert.Equal("【M測試】第 9002 集", (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/9002?lang=en"))).GetProperty("title").GetString());
        var last = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/9002"));
        Assert.Equal(9001, last.GetProperty("previousEpisodeNo").GetInt32());
        Assert.Equal(JsonValueKind.Null, last.GetProperty("nextEpisodeNo").ValueKind);
        Assert.True(last.GetProperty("isLatest").GetBoolean());

        // 草稿與未到發布日：404（不洩漏存在）
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/comic/episodes/9003")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/comic/episodes/9004")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/comic/episodes/424242")).StatusCode);
        // 全免費：沒有任何付費牆欄位、不需登入（上面全部是匿名請求）
        Assert.DoesNotContain("paid", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // 到了發布日就變成最新集數（不必等後台重算 is_latest）
        await BizTest.ExecuteSqlAsync("UPDATE comic_episodes SET published_on = CAST(SYSUTCDATETIME() AS date) WHERE episode_no = 9003");
        Assert.Equal(9003, (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/episodes/latest"))).GetProperty("episodeNo").GetInt32());
    }

    [Fact]
    public async Task 漫畫_閱讀數_資料庫端遞增_草稿集數不計()
    {
        using var client = fixture.CreateClient();
        await InsertEpisodeAsync(9001, "published", null, 1);
        await InsertEpisodeAsync(9002, "draft", null, 1);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/tcrfc/comic/episodes/9001/views", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/tcrfc/comic/episodes/9001/views", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/tcrfc/comic/episodes/9002/views", null)).StatusCode);
        Assert.Equal(2, Convert.ToInt32(await ScalarAsync("SELECT view_count FROM comic_episodes WHERE episode_no = 9001")));
        Assert.Equal(0, Convert.ToInt32(await ScalarAsync("SELECT view_count FROM comic_episodes WHERE episode_no = 9002")));
    }

    [Fact]
    public async Task 漫畫_企劃說明與角色_讀到種子資料_英文回退繁中()
    {
        using var client = fixture.CreateClient();
        var about = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/about"));
        Assert.True(about.TryGetProperty("title", out _) && about.TryGetProperty("body", out _));
        var characters = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/characters"))).EnumerateArray().ToList();
        Assert.NotEmpty(characters);
        Assert.All(characters, c => Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("name").GetString())));
        var en = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/comic/characters?lang=en"))).EnumerateArray().ToList();
        Assert.Equal(characters.Count, en.Count);
        Assert.All(en, c => Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("name").GetString()))); // 缺英文時回退繁中，不是空白
    }

    private static async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    // ═════════════ 8.2 球迷會活動 ═════════════

    private static async Task<Guid> InsertEventAsync(
        string slug, int? capacity, bool paidOnly, string startsSql = "DATEADD(DAY, 10, SYSUTCDATETIME())", string? deadlineSql = null, string status = "published", string? endsSql = null)
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            $"""
            INSERT INTO fan_events (id, club_id, slug, starts_at, ends_at, capacity, is_paid_members_only, registration_deadline_at, status)
            VALUES (@Id, (SELECT id FROM clubs WHERE code = 'tcrfc'), @Slug, {startsSql}, {endsSql ?? "NULL"}, @Cap, @Paid, {deadlineSql ?? "NULL"}, @Status);
            INSERT INTO fan_events_i18n (fan_event_id, locale, name, description, location) VALUES (@Id, N'zh-Hant', @Name, N'活動說明', N'台中市測試場地');
            """, ("@Id", id), ("@Slug", slug), ("@Cap", capacity), ("@Paid", paidOnly), ("@Status", status), ("@Name", "【M測試】" + slug));
        return id;
    }

    private static object Guest(string tag) => new
    {
        applicantName = "【M測試】訪客" + tag, phone = "09" + Random.Shared.Next(10_000_000, 99_999_999), email = $"guest-{tag}-{Guid.NewGuid():N}@example.test",
    };

    [Fact]
    public async Task 活動列表與詳情_只列已發布_額度與剩餘名額_尚未結束與已結束分開_英文回退繁中()
    {
        using var client = fixture.CreateClient();
        var slug = "mtest-list";
        var eventId = await InsertEventAsync(slug, 2, false);
        await InsertEventAsync("mtest-draft", 5, false, status: "draft");
        await InsertEventAsync("mtest-past", null, false, startsSql: "DATEADD(DAY, -30, SYSUTCDATETIME())", endsSql: "DATEADD(DAY, -29, SYSUTCDATETIME())");
        await BizTest.ExecuteSqlAsync("INSERT INTO fan_events_i18n (fan_event_id, locale, name) VALUES (@Id, N'en', N'M Test Event')", ("@Id", eventId));
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO fan_event_images (id, fan_event_id, image_key, sort_order, image_width, image_height) VALUES (NEWID(), @Id, N'mtest/fan/1.webp', 1, 640, 480)", ("@Id", eventId));

        var upcoming = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/fan-events?phase=upcoming"))).EnumerateArray().ToList();
        var slugs = upcoming.Select(e => e.GetProperty("slug").GetString()).ToArray();
        Assert.Contains(slug, slugs);
        Assert.DoesNotContain("mtest-draft", slugs);
        Assert.DoesNotContain("mtest-past", slugs);
        var item = upcoming.First(e => e.GetProperty("slug").GetString() == slug);
        Assert.Equal(2, item.GetProperty("capacity").GetInt32());
        Assert.Equal(2, item.GetProperty("spotsLeft").GetInt32());
        Assert.True(item.GetProperty("isRegistrationOpen").GetBoolean());
        Assert.False(item.GetProperty("isFull").GetBoolean());
        Assert.Equal("upcoming", item.GetProperty("phase").GetString());

        var past = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/fan-events?phase=past"))).EnumerateArray().Select(e => e.GetProperty("slug").GetString()).ToArray();
        Assert.Contains("mtest-past", past);
        Assert.DoesNotContain(slug, past);

        var detail = await ReadJsonAsync(await client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"));
        Assert.Equal("活動說明", detail.GetProperty("description").GetString());
        Assert.Equal(1, detail.GetProperty("images").GetArrayLength());
        Assert.Equal(640, detail.GetProperty("images")[0].GetProperty("width").GetInt32());
        Assert.Equal("M Test Event", (await ReadJsonAsync(await client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=en"))).GetProperty("event").GetProperty("name").GetString());
        Assert.Equal("活動說明", (await ReadJsonAsync(await client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=en"))).GetProperty("description").GetString()); // 英文缺漏回退繁中
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("myRegistration").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/fan-events/mtest-draft")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/bw/fan-events/" + slug)).StatusCode); // 別隊的活動不能跨站讀
    }

    [Fact]
    public async Task 非會員報名一般活動_名額滿了自動進候補_重複報名與缺資料被擋_不回傳他人個資()
    {
        using var client = fixture.CreateClient();
        var slug = "mtest-guest";
        await InsertEventAsync(slug, 1, false);
        var url = $"/api/v1/tcrfc/fan-events/{slug}/registrations";

        var first = await client.PostAsJsonAsync(url, Guest("a"), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await ReadJsonAsync(first);
        Assert.Equal("registered", firstBody.GetProperty("status").GetString());
        Assert.False(firstBody.GetProperty("isWaitlisted").GetBoolean());
        Assert.DoesNotContain("0900", firstBody.GetRawText()); // 回應不含任何個資

        var second = await ReadJsonAsync(await client.PostAsJsonAsync(url, Guest("b"), TestJson.WriteOptions));
        Assert.Equal("waitlist", second.GetProperty("status").GetString()); // capacity=1 已滿 → 候補
        Assert.True(second.GetProperty("isWaitlisted").GetBoolean());

        var detail = (await ReadJsonAsync(await client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("event");
        Assert.True(detail.GetProperty("isFull").GetBoolean());
        Assert.Equal(0, detail.GetProperty("spotsLeft").GetInt32());
        Assert.True(detail.GetProperty("isRegistrationOpen").GetBoolean()); // 額滿仍可報名（進候補）

        // 重複（同一 Email）、缺姓名、缺聯絡方式、Email 格式錯、備註過長
        var email = "dup-" + Guid.NewGuid().ToString("N") + "@example.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(url, new { applicantName = "甲", email }, TestJson.WriteOptions)).StatusCode);
        var dup = await client.PostAsJsonAsync(url, new { applicantName = "乙", email = email.ToUpperInvariant() }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("already_registered", CodeOf(await ReadJsonAsync(dup)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { phone = "0900000000" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { applicantName = "丙" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { applicantName = "丙", email = "bad" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { applicantName = "丙", phone = "0900000001", note = new string('x', 501) }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/tcrfc/fan-events/nope/registrations", Guest("c"), TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 並行報名不會超收名額_多出來的進候補()
    {
        using var client = fixture.CreateClient();
        var slug = "mtest-race";
        var eventId = await InsertEventAsync(slug, 3, false);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            client.PostAsJsonAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations", Guest("r" + i), TestJson.WriteOptions)));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(3, Convert.ToInt32(await ScalarAsync($"SELECT COUNT(*) FROM fan_event_registrations WHERE fan_event_id = '{eventId}' AND status = 'registered'")));
        Assert.Equal(5, Convert.ToInt32(await ScalarAsync($"SELECT COUNT(*) FROM fan_event_registrations WHERE fan_event_id = '{eventId}' AND status = 'waitlist'")));
    }

    [Fact]
    public async Task 截止與已開始的活動不能報名()
    {
        using var client = fixture.CreateClient();
        await InsertEventAsync("mtest-deadline", null, false, deadlineSql: "DATEADD(HOUR, -1, SYSUTCDATETIME())");
        await InsertEventAsync("mtest-started", null, false, startsSql: "DATEADD(HOUR, -1, SYSUTCDATETIME())");
        foreach (var slug in new[] { "mtest-deadline", "mtest-started" })
        {
            var response = await client.PostAsJsonAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations", Guest("x"), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("registration_closed", CodeOf(await ReadJsonAsync(response)));
        }

        var item = (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/fan-events/mtest-deadline"))).GetProperty("event");
        Assert.False(item.GetProperty("isRegistrationOpen").GetBoolean());
    }

    [Fact]
    public async Task 限付費會員的活動_要登入且有效球迷會員會籍_會員報名記到會員_可看到自己的狀態與取消()
    {
        using var anonymous = fixture.CreateClient();
        var slug = "mtest-paid";
        await InsertEventAsync(slug, null, true);
        var url = $"/api/v1/tcrfc/fan-events/{slug}/registrations";

        // 沒登入 401（帶 login_required）；非會員資料也不行
        var guest = await anonymous.PostAsJsonAsync(url, Guest("p"), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal("login_required", CodeOf(await ReadJsonAsync(guest)));

        // 免費會員 403
        var free = await _scope.CreateVerifiedMemberAsync("culture-free");
        var forbidden = await free.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("fan_club_required", CodeOf(await ReadJsonAsync(forbidden)));

        // 球迷會員（走完付款流程）→ 報名成功
        var fan = await _scope.CreateVerifiedMemberAsync("culture-fan");
        await MemberTestScope.BuyFanClubAsync(fan, "single");
        var ok = await fan.Client.PostAsJsonAsync(url, new { note = "我會到" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("registered", (await ReadJsonAsync(ok)).GetProperty("status").GetString());
        Assert.Equal(1, Convert.ToInt32(await ScalarAsync($"SELECT COUNT(*) FROM fan_event_registrations r JOIN fan_events e ON e.id = r.fan_event_id WHERE e.slug = '{slug}' AND r.member_id = '{fan.MemberId}' AND r.applicant_name IS NULL"))); // 會員報名不另存個資

        var dup = await fan.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // 詳情帶會員權杖 → 看得到自己的狀態；別人與匿名看不到
        var mine = (await ReadJsonAsync(await fan.Client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("myRegistration");
        Assert.Equal("registered", mine.GetProperty("status").GetString());
        Assert.Equal("已報名", mine.GetProperty("statusLabel").GetString());
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(await free.Client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("myRegistration").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("myRegistration").ValueKind);

        // 取消：本人才能取消；取消後可以再報名
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await free.Client.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fan.Client.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fan.Client.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await fan.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions)).StatusCode);

        // 會籍過期後就不能報名限定活動
        await BizTest.ExecuteSqlAsync("DELETE FROM fan_event_registrations WHERE member_id = @M", ("@M", fan.MemberId));
        await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", fan.MemberId));
        Assert.Equal(HttpStatusCode.Forbidden, (await fan.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 一般活動_會員報名也算名額_同一會員不能重複報名_候補也可由會員取消()
    {
        var m = await _scope.CreateVerifiedMemberAsync("culture-member");
        var other = await _scope.CreateVerifiedMemberAsync("culture-member2");
        var slug = "mtest-member";
        await InsertEventAsync(slug, 1, false);
        var url = $"/api/v1/tcrfc/fan-events/{slug}/registrations";
        Assert.Equal("registered", (await ReadJsonAsync(await m.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions))).GetProperty("status").GetString());
        var waitlist = await ReadJsonAsync(await other.Client.PostAsJsonAsync(url, new { }, TestJson.WriteOptions));
        Assert.Equal("waitlist", waitlist.GetProperty("status").GetString());
        Assert.Equal("候補", (await ReadJsonAsync(await other.Client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("myRegistration").GetProperty("statusLabel").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await other.Client.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        // 取消已報名者不會自動遞補候補者（同後台 F2：由客服人工處理）
        Assert.Equal(HttpStatusCode.NoContent, (await m.Client.DeleteAsync($"/api/v1/tcrfc/fan-events/{slug}/registrations/me")).StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(await m.Client.GetAsync($"/api/v1/tcrfc/fan-events/{slug}"))).GetProperty("event").GetProperty("spotsLeft").GetInt32());
    }
}
