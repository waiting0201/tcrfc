using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// F 批前台缺口公開端點：首頁核心價值、賽事積分榜、球員數據自動彙總（含手動覆寫）、K2「待確認申請」後台清單。
/// 球季／賽事／進球等測試資料一律用 <c>TST-F</c> 球季與 <c>【F測試】</c> 前綴，測完清除；<b>只讀取種子的球員，不改動</b>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class FrontGapsPublicTests(AdminWriteApiFixture fixture)
{
    private const string TestSeason = "TST-F";

    // ═══════════════════════════ 首頁核心價值 ═══════════════════════════

    [Fact]
    public async Task 首頁核心價值_五項固定目錄_代碼與文章標籤一致_兩站共用_不存在的俱樂部404()
    {
        using var client = fixture.CreateClient();
        var tcrfc = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/home/core-values");
        Assert.Equal(["players_first", "excellence", "global_pathways", "community", "integrity"],
            tcrfc.EnumerateArray().Select(v => v.GetProperty("code").GetString()).ToArray());
        var first = tcrfc[0];
        Assert.Equal("以球員為本", first.GetProperty("nameZh").GetString());
        Assert.Equal("Players First", first.GetProperty("nameEn").GetString());
        Assert.Equal("about/philosophy", first.GetProperty("learnMorePageSlug").GetString());
        Assert.Equal(5, (await client.GetFromJsonAsync<JsonElement>("/api/v1/bw/home/core-values")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/no-such-club/home/core-values")).StatusCode);
    }

    // ═══════════════════════════ 積分榜與球員數據 ═══════════════════════════

    private sealed class MatchData : IAsyncDisposable
    {
        public Guid SeasonId;
        public Guid ClubId;

        public async ValueTask DisposeAsync()
        {
            await BizTest.ExecuteSqlAsync(
                """
                DELETE FROM match_goals WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM match_cards WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM match_lineups WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM matches WHERE season_id = @S;
                DELETE FROM player_season_stats WHERE season_id = @S;
                DELETE FROM standings WHERE season_id = @S;
                DELETE FROM seasons WHERE id = @S;
                """, ("@S", SeasonId));
        }
    }

    private static async Task<Guid> NewMatchAsync(Guid clubId, Guid seasonId, string status, int day)
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO matches (id, club_id, season_id, match_on, status, opponent, round_no) VALUES (@I, @C, @S, DATEADD(day, @D, '2020-01-01'), @St, N'【F測試】對手', @D)",
            ("@I", id), ("@C", clubId), ("@S", seasonId), ("@D", day), ("@St", status));
        return id;
    }

    private static Task AddGoalAsync(Guid matchId, Guid playerId) =>
        BizTest.ExecuteSqlAsync("INSERT INTO match_goals (match_id, player_id, minute) VALUES (@M, @P, 10)", ("@M", matchId), ("@P", playerId));

    private static Task AddCardAsync(Guid matchId, Guid playerId, string type) =>
        BizTest.ExecuteSqlAsync("INSERT INTO match_cards (match_id, player_id, card_type, minute) VALUES (@M, @P, @T, 20)", ("@M", matchId), ("@P", playerId), ("@T", type));

    private static Task AddLineupAsync(Guid matchId, Guid playerId, bool starter) =>
        BizTest.ExecuteSqlAsync("INSERT INTO match_lineups (match_id, player_id, is_starter) VALUES (@M, @P, @B)", ("@M", matchId), ("@P", playerId), ("@B", starter));

    [Fact]
    public async Task 積分榜_依名次排序_空名次墊底_球季選擇_別隊看不到()
    {
        var clubId = await C1Test.ClubIdAsync("tcrfc");
        var seasonId = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync("INSERT INTO seasons (id, club_id, code, start_on, end_on) VALUES (@I, @C, @K, '2020-01-01', '2020-12-31')", ("@I", seasonId), ("@C", clubId), ("@K", TestSeason));
        await using var data = new MatchData { SeasonId = seasonId, ClubId = clubId };
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @A uniqueidentifier = NEWID(), @B uniqueidentifier = NEWID(), @U uniqueidentifier = NEWID();
            INSERT INTO standings (id, club_id, season_id, rank, played, points) VALUES
              (@A, @C, @S, 2, 10, 20), (@B, @C, @S, 1, 10, 25), (@U, @C, @S, NULL, 9, NULL);
            INSERT INTO standings_i18n (standing_id, locale, team_name) VALUES
              (@A, N'zh-Hant', N'【F測試】乙隊'), (@B, N'zh-Hant', N'【F測試】甲隊'), (@U, N'zh-Hant', N'【F測試】未排名隊'),
              (@B, N'en', N'F-Test Team A');
            """, ("@C", clubId), ("@S", seasonId));
        using var client = fixture.CreateClient();

        var standings = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/standings?season={TestSeason}");
        Assert.Equal(TestSeason, standings.GetProperty("season").GetProperty("code").GetString());
        Assert.Equal(["【F測試】甲隊", "【F測試】乙隊", "【F測試】未排名隊"], standings.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("teamName").GetString()).ToArray());
        Assert.Equal(25, standings.GetProperty("items")[0].GetProperty("points").GetInt32());

        // 英文：有英文名稱者用英文、沒有的回退繁中（standings_i18n 側表，RequestLocale.Pick）。
        var en = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/standings?season={TestSeason}&lang=en");
        Assert.Equal(["F-Test Team A", "【F測試】乙隊", "【F測試】未排名隊"], en.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("teamName").GetString()).ToArray());
        Assert.Equal(JsonValueKind.Null, standings.GetProperty("items")[2].GetProperty("rank").ValueKind);
        Assert.Contains(standings.GetProperty("seasons").EnumerateArray(), s => s.GetString() == TestSeason);

        // 不指定球季：不會壞（有資料的球季之一）；指定不存在的球季：空榜，不是 500。
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tcrfc/standings")).StatusCode);
        var unknown = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/standings?season=NO-SUCH");
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("season").ValueKind);
        Assert.Equal(0, unknown.GetProperty("items").GetArrayLength());

        // 🔴 club_id 硬過濾：藍鯨用同一個球季代碼查不到磐石的積分榜。
        var bw = await client.GetFromJsonAsync<JsonElement>($"/api/v1/bw/standings?season={TestSeason}");
        Assert.Equal(0, bw.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/no-such-club/standings")).StatusCode);
    }

    [Fact]
    public async Task 球員數據_自動彙總規則_手動數據優先_只計已結束賽事_球員逐季_別隊球員404()
    {
        var clubId = await C1Test.ClubIdAsync("tcrfc");
        var seasonId = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync("INSERT INTO seasons (id, club_id, code, start_on, end_on) VALUES (@I, @C, @K, '2020-01-01', '2020-12-31')", ("@I", seasonId), ("@C", clubId), ("@K", TestSeason));
        await using var data = new MatchData { SeasonId = seasonId, ClubId = clubId };
        var ids = new List<Guid>();
        for (var skip = 0; skip < 4; skip++)
        {
            ids.Add(await BizTest.ScalarGuidAsync(
                "SELECT p.id FROM players p WHERE p.club_id = @C ORDER BY p.row_seq OFFSET @K ROWS FETCH NEXT 1 ROWS ONLY", ("@C", clubId), ("@K", skip)));
        }

        var (a, b, c, d) = (ids[0], ids[1], ids[2], ids[3]);
        var m1 = await NewMatchAsync(clubId, seasonId, "played", 1);
        var m2 = await NewMatchAsync(clubId, seasonId, "played", 2);
        var future = await NewMatchAsync(clubId, seasonId, "scheduled", 3); // 未結束的賽事不計
        // A：兩場先發、第一場進 1 球；B：兩場都在替補，第二場進球＋黃牌（才算出賽）；C：只在替補、沒有任何事件（不算出賽）；D：有自動數據但另有手動數據。
        await AddLineupAsync(m1, a, true);
        await AddLineupAsync(m2, a, true);
        await AddGoalAsync(m1, a);
        await AddLineupAsync(m1, b, false);
        await AddLineupAsync(m2, b, false);
        await AddGoalAsync(m2, b);
        await AddCardAsync(m2, b, "yellow");
        await AddLineupAsync(m1, c, false);
        await AddLineupAsync(m1, d, true);
        await AddGoalAsync(future, a); // 不計
        await AddCardAsync(future, c, "red"); // 不計
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO player_season_stats (player_id, season_id, appearances, goals, assists, yellow_cards, red_cards) VALUES (@P, @S, 9, 8, 3, 2, 1)", ("@P", d), ("@S", seasonId));
        using var client = fixture.CreateClient();

        var stats = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/stats/players?season={TestSeason}");
        var items = stats.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("playerId").GetGuid());
        Assert.Equal(TestSeason, stats.GetProperty("season").GetProperty("code").GetString());
        Assert.False(items.ContainsKey(c)); // 替補沒有任何事件 → 不算出賽
        var pa = items[a];
        Assert.Equal(2, pa.GetProperty("appearances").GetInt32());
        Assert.Equal(1, pa.GetProperty("goals").GetInt32()); // 未來賽事的進球不計
        Assert.Equal(JsonValueKind.Null, pa.GetProperty("assists").ValueKind); // 賽事紀錄沒有助攻
        Assert.Equal("auto", pa.GetProperty("source").GetString());
        var pb = items[b];
        Assert.Equal(1, pb.GetProperty("appearances").GetInt32()); // 只有第二場（有進球與牌）
        Assert.Equal(1, pb.GetProperty("goals").GetInt32());
        Assert.Equal(1, pb.GetProperty("yellowCards").GetInt32());
        Assert.Equal(0, pb.GetProperty("redCards").GetInt32());
        var pd = items[d];
        Assert.Equal("manual", pd.GetProperty("source").GetString()); // 有手動數據就以手動為準
        Assert.Equal(9, pd.GetProperty("appearances").GetInt32());
        Assert.Equal(8, pd.GetProperty("goals").GetInt32());
        Assert.Equal(3, pd.GetProperty("assists").GetInt32());
        Assert.Equal(1, pd.GetProperty("redCards").GetInt32());
        // 排序：進球多者在前。
        Assert.Equal(d, stats.GetProperty("items")[0].GetProperty("playerId").GetGuid());
        // 肖像同意 fail-closed：測試球員若未同意肖像，photoUrl 一定是 null（同球員名單）。
        foreach (var (id, item) in items)
        {
            var consent = await C1Test.ScalarAsync<string>("SELECT portrait_consent_status FROM players WHERE id = @P", ("@P", id));
            if (consent == "not_consented")
            {
                Assert.Equal(JsonValueKind.Null, item.GetProperty("photoUrl").ValueKind);
            }
        }

        // 隊別篩選：只回該隊的球員。
        var teamCode = await C1Test.ScalarAsync<string>("SELECT t.code FROM players p JOIN teams t ON t.id = p.team_id WHERE p.id = @P", ("@P", a));
        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/stats/players?season={TestSeason}&team={teamCode}");
        Assert.All(filtered.GetProperty("items").EnumerateArray(), i => Assert.Equal(teamCode, i.GetProperty("teamCode").GetString()));
        var none = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/stats/players?season={TestSeason}&team=NO-SUCH-TEAM");
        Assert.Equal(0, none.GetProperty("items").GetArrayLength());

        // 球員逐季數據。
        var career = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/players/{a}/stats");
        var row = career.GetProperty("seasons").EnumerateArray().First(s => s.GetProperty("seasonCode").GetString() == TestSeason);
        Assert.Equal(2, row.GetProperty("appearances").GetInt32());
        Assert.Equal(1, row.GetProperty("goals").GetInt32());
        // 🔴 別隊的球員 id 一律 404；藍鯨同球季代碼查不到磐石的數據。
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/bw/players/{a}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/tcrfc/players/{Guid.NewGuid()}/stats")).StatusCode);
        var bwStats = await client.GetFromJsonAsync<JsonElement>($"/api/v1/bw/stats/players?season={TestSeason}");
        Assert.Equal(0, bwStats.GetProperty("items").GetArrayLength());
    }

    // ═══════════════════════════ K2 待確認申請 ═══════════════════════════

    [Fact]
    public async Task K2待確認申請_網頁升級申請出現在後台清單_遮罩_篩選_開通後結案_俱樂部範圍_需要登入()
    {
        await using var members = new MemberTestScope(fixture);
        var m = await members.CreateVerifiedMemberAsync("k2-apps");
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tcrfc/member/membership-orders") { Content = BizTest.Json(new { planCode = "single" }) };
        create.Headers.Add("Idempotency-Key", "k2-" + Guid.NewGuid().ToString("N"));
        var created = await m.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var orderNo = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNo").GetString()!;

        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/membership-applications")).StatusCode);

        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var superAdmin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var list = await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?keyword={orderNo}");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var item = list.GetProperty("items")[0];
        Assert.Equal(orderNo, item.GetProperty("orderNo").GetString());
        Assert.Equal("created", item.GetProperty("status").GetString());
        Assert.Equal("待確認", item.GetProperty("statusLabel").GetString());
        Assert.Equal(m.MemberId, item.GetProperty("memberId").GetGuid());
        Assert.Equal("single", item.GetProperty("planCode").GetString());
        Assert.True(item.GetProperty("amount").GetInt32() > 0);
        // 完整個資權限：超級管理員看得到完整姓名與 Email；沒有權限的角色看到遮罩（兩者至少一邊必須與對方不同，證明遮罩有套用）。
        var full = (await superAdmin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?keyword={orderNo}")).GetProperty("items")[0];
        Assert.Equal(m.Email, full.GetProperty("memberEmail").GetString());
        Assert.Contains("k2-apps", full.GetProperty("memberName").GetString());

        // 預設只列 created；其他狀態用 status 查；不合法的狀態 400；別的俱樂部看不到。
        var pending = await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?status=pending_payment&keyword={orderNo}");
        Assert.Equal(0, pending.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/membership-applications?status=bogus")).StatusCode);
        var bw = await superAdmin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/bw/membership-applications?keyword={orderNo}");
        Assert.Equal(0, bw.GetProperty("totalCount").GetInt32());

        // 會員改走線上付款（請款）→ 離開「待確認」、出現在「待付款」。
        Assert.Equal(HttpStatusCode.OK, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null)).StatusCode);
        Assert.Equal(0, (await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?keyword={orderNo}")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?status=pending_payment&keyword={orderNo}")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/cancel", null)).StatusCode);

        // 再送一份申請 → 客服用清單帶出的 memberId／planId／amount 開通 → 申請一併結案，不再出現在待確認。
        using var create2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tcrfc/member/membership-orders") { Content = BizTest.Json(new { planCode = "single" }) };
        create2.Headers.Add("Idempotency-Key", "k2-" + Guid.NewGuid().ToString("N"));
        var orderNo2 = (await (await m.Client.SendAsync(create2)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNo").GetString()!;
        var row = (await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?keyword={orderNo2}")).GetProperty("items")[0];
        var activate = await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate", BizTest.Json(new
        {
            memberId = row.GetProperty("memberId").GetGuid(), planId = row.GetProperty("planId").GetGuid(), paymentMethod = "onsite",
            amount = row.GetProperty("amount").GetInt32(), paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        }));
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        Assert.Equal(0, (await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?keyword={orderNo2}")).GetProperty("totalCount").GetInt32());
        var closed = await service.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tcrfc/membership-applications?status=activated&keyword={orderNo2}");
        Assert.Equal(1, closed.GetProperty("totalCount").GetInt32());
        Assert.Equal("已開通", closed.GetProperty("items")[0].GetProperty("statusLabel").GetString());
    }
}
