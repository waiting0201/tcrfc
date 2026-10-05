using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>會員中心：我的會籍、電子會員卡（含重產 QR 與公開驗證頁）、球衣登記、我的報名。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class MemberCenterTests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static string? CodeOf(JsonElement problem) => problem.TryGetProperty("code", out var c) ? c.GetString() : null;

    private static async Task<int> CountAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    // ═════════════ 我的會籍 ═════════════

    [Fact]
    public async Task 我的會籍_逐俱樂部列出_每份會籍帶卡片與俱樂部品牌_需要登入()
    {
        var m = await _scope.CreateVerifiedMemberAsync("center-list");
        using var anonymous = _scope.Client();
        foreach (var path in new[] { "/api/v1/member/memberships", "/api/v1/member/cards", "/api/v1/member/jerseys", "/api/v1/member/registrations" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        }

        var json = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"));
        var list = json.GetProperty("memberships");
        Assert.Equal(1, list.GetArrayLength());
        var ms = list[0];
        Assert.Equal("tcrfc", ms.GetProperty("club").GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(ms.GetProperty("club").GetProperty("name").GetString()));
        Assert.False(string.IsNullOrEmpty(ms.GetProperty("club").GetProperty("brandColor").GetString())); // 卡面品牌色
        Assert.Equal("一般會員", ms.GetProperty("tierLabel").GetString());
        Assert.Equal("有效", ms.GetProperty("statusLabel").GetString());
        Assert.Equal("2026-27", ms.GetProperty("seasonCode").GetString());
        Assert.True(ms.GetProperty("isCurrentSeason").GetBoolean());
        var card = ms.GetProperty("cards")[0];
        Assert.Equal(m.MemberNo, card.GetProperty("memberNo").GetString());
        Assert.True(card.GetProperty("isValid").GetBoolean());
        Assert.Equal(43, card.GetProperty("token").GetString()!.Length); // 不可由會員編號推導的隨機憑證

        // 藍鯨目前沒有開放的球季 → 不在「可加入」清單，直接加入回 409
        Assert.Equal(0, json.GetProperty("joinableClubs").GetArrayLength());
        var joinBw = await m.Client.PostAsync("/api/v1/bw/member/memberships/join", null);
        Assert.Equal(HttpStatusCode.Conflict, joinBw.StatusCode);
        Assert.Equal("season_not_available", CodeOf(await ReadJsonAsync(joinBw)));

        // 重複加入磐石：冪等，回同一份會籍
        var again = await ReadJsonAsync(await m.Client.PostAsync("/api/v1/tcrfc/member/memberships/join", null));
        Assert.Equal(ms.GetProperty("id").GetString(), again.GetProperty("id").GetString());
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M", ("@M", m.MemberId)));
        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.PostAsync("/api/v1/nope/member/memberships/join", null)).StatusCode);

        // 英文標籤
        var en = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships?lang=en"));
        Assert.Equal("Member", en.GetProperty("memberships")[0].GetProperty("tierLabel").GetString());
    }

    [Fact]
    public async Task 加入俱樂部_高強度並行_16並行乘10輪_全部200同一份會籍_只有一份會籍與一張卡()
    {
        for (var round = 0; round < 10; round++)
        {
            var m = await _scope.CreateVerifiedMemberAsync($"center-join-race-{round}");
            // 註冊時已自動建立磐石會籍；刪掉它，讓並行的 join 真的走「首次建立」路徑。
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM member_cards WHERE membership_id IN (SELECT id FROM memberships WHERE member_id = @M); DELETE FROM memberships WHERE member_id = @M;", ("@M", m.MemberId));
            var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => m.Client.PostAsync("/api/v1/tcrfc/member/memberships/join", null))));
            var codes = string.Join(",", results.Select(r => (int)r.StatusCode));
            Assert.True(results.All(r => r.StatusCode == HttpStatusCode.OK), $"第 {round} 輪：{codes}");
            var ids = new HashSet<string?>();
            foreach (var r in results)
            {
                ids.Add((await ReadJsonAsync(r)).GetProperty("id").GetString());
            }

            Assert.Single(ids);
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M", ("@M", m.MemberId)));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM member_cards WHERE membership_id IN (SELECT id FROM memberships WHERE member_id = @M)", ("@M", m.MemberId)));
        }
    }

    [Fact]
    public async Task 到期前三十天顯示續會提示_已過期的會籍顯示已到期()
    {
        var m = await _scope.CreateVerifiedMemberAsync("center-renew");
        await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, 10, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", m.MemberId));
        var soon = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0];
        Assert.True(soon.GetProperty("renewalDue").GetBoolean());

        await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", m.MemberId));
        var expired = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0];
        Assert.Equal("expired", expired.GetProperty("status").GetString()); // 以到期日為準，狀態欄仍是 active 也算已到期
        Assert.Equal("已到期", expired.GetProperty("statusLabel").GetString());
        Assert.False(expired.GetProperty("cards")[0].GetProperty("isValid").GetBoolean());
        Assert.False(expired.GetProperty("renewalDue").GetBoolean());
    }

    // ═════════════ 電子會員卡 ═════════════

    [Fact]
    public async Task 公開驗證頁_只回姓名首字_會員編號_層級_有效或已過期_不顯示任何其他個資與俱樂部()
    {
        var m = await _scope.CreateVerifiedMemberAsync("center-verify");
        using var anonymous = _scope.Client();
        var cards = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/cards"));
        var token = cards[0].GetProperty("token").GetString()!;

        var response = await anonymous.GetAsync($"/api/v1/m/{token}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", string.Join(",", response.Headers.CacheControl?.ToString() ?? string.Empty)); // 會員卡驗證不得被快取
        var json = await ReadJsonAsync(response);
        var names = json.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "memberNo", "nameInitial", "status", "statusLabel", "tier", "tierLabel" }, names);
        Assert.Equal("【", json.GetProperty("nameInitial").GetString()); // 姓名「【M測試】…」的第一個字
        Assert.Equal(m.MemberNo, json.GetProperty("memberNo").GetString());
        Assert.Equal("valid", json.GetProperty("status").GetString());
        Assert.Equal("有效", json.GetProperty("statusLabel").GetString());
        var raw = json.GetRawText();
        foreach (var leaked in new[] { m.Email, "0900-000-111", "club", "tcrfc", "birth", "phone" })
        {
            Assert.DoesNotContain(leaked, raw, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("Valid", (await ReadJsonAsync(await anonymous.GetAsync($"/api/v1/m/{token}?lang=en"))).GetProperty("statusLabel").GetString());

        // 到期 → 已過期（不是 404）
        await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", m.MemberId));
        var expired = await ReadJsonAsync(await anonymous.GetAsync($"/api/v1/m/{token}"));
        Assert.Equal("expired", expired.GetProperty("status").GetString());
        Assert.Equal("已過期", expired.GetProperty("statusLabel").GetString());

        // 查無、過長、撤銷、帳號停用 → 一律 404（不分原因）
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/m/not-a-real-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/m/" + new string('a', 80))).StatusCode);
        await BizTest.ExecuteSqlAsync("UPDATE members SET status = 'suspended' WHERE id = @M", ("@M", m.MemberId));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/m/{token}")).StatusCode);
        await BizTest.ExecuteSqlAsync("UPDATE members SET status = 'active' WHERE id = @M", ("@M", m.MemberId));
        await BizTest.ExecuteSqlAsync("UPDATE member_cards SET status = 'revoked' WHERE token = @T", ("@T", token));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/m/{token}")).StatusCode);
    }

    [Fact]
    public async Task 重新產生QR_舊token立即失效_補發次數加一_別人的卡404()
    {
        var a = await _scope.CreateVerifiedMemberAsync("center-regen-a");
        var b = await _scope.CreateVerifiedMemberAsync("center-regen-b");
        using var anonymous = _scope.Client();
        var card = (await ReadJsonAsync(await a.Client.GetAsync("/api/v1/member/cards")))[0];
        var cardId = card.GetProperty("id").GetString();
        var oldToken = card.GetProperty("token").GetString()!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/v1/member/cards/{cardId}/regenerate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.PostAsync($"/api/v1/member/cards/{cardId}/regenerate", null)).StatusCode); // 別人的卡

        var regenerated = await ReadJsonAsync(await a.Client.PostAsync($"/api/v1/member/cards/{cardId}/regenerate", null));
        var newToken = regenerated.GetProperty("token").GetString()!;
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(1, regenerated.GetProperty("reissueCount").GetInt32());
        Assert.Equal(cardId, regenerated.GetProperty("id").GetString()); // 同一張卡、換 token（一卡一 token）
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/m/{oldToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/m/{newToken}")).StatusCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM member_cards c JOIN memberships m ON m.id = c.membership_id WHERE m.member_id = @M", ("@M", a.MemberId))); // 沒有多發一張

        // 已停用的卡不能重產
        await BizTest.ExecuteSqlAsync("UPDATE member_cards SET status = 'revoked' WHERE id = @C", ("@C", Guid.Parse(cardId!)));
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.PostAsync($"/api/v1/member/cards/{cardId}/regenerate", null)).StatusCode);
    }

    // ═════════════ 球衣 ═════════════

    private static object Jersey(Guid membershipId, string size = "l", string delivery = "pickup") => new
    {
        membershipId, recipientName = "【M測試】領用人", size, deliveryMethod = delivery, phone = "0900-000-222", address = delivery == "ship" ? "【測試】台中市西屯區測試路 1 號" : null,
    };

    [Fact]
    public async Task 球衣登記_只有有效的球迷會員可以登記_件數受方案限制_寄出後不能再改_別人的會籍404()
    {
        var m = await _scope.CreateVerifiedMemberAsync("center-jersey");
        var other = await _scope.CreateVerifiedMemberAsync("center-jersey-other");
        var freeMembership = Guid.Parse((await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0].GetProperty("id").GetString()!);

        // 免費會員：列表沒有可登記的會籍；登記 409
        Assert.Equal(0, (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/jerseys"))).GetArrayLength());
        var notFan = await m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, notFan.StatusCode);
        Assert.Equal("not_fan_club", CodeOf(await ReadJsonAsync(notFan)));

        await MemberTestScope.BuyFanClubAsync(m, "single"); // single 含 1 件球衣
        var group = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/jerseys")))[0];
        Assert.Equal(1, group.GetProperty("quota").GetInt32());
        Assert.Equal(0, group.GetProperty("used").GetInt32());
        Assert.True(group.GetProperty("canRegister").GetBoolean());

        // 驗證：寄送要電話與地址、領取方式只有兩種、尺寸必填
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", new { membershipId = freeMembership, recipientName = "x", size = "L", deliveryMethod = "ship" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership, delivery: "courier"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership, size: " "), TestJson.WriteOptions)).StatusCode);

        // 別人不能登記到我的會籍；藍鯨路由不能碰磐石的會籍
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.PostAsJsonAsync("/api/v1/bw/member/jerseys", Jersey(freeMembership), TestJson.WriteOptions)).StatusCode);

        var created = await m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership, "xl", "ship"), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var jersey = await ReadJsonAsync(created);
        Assert.Equal("XL", jersey.GetProperty("size").GetString());
        Assert.Equal("待處理", jersey.GetProperty("statusLabel").GetString());
        Assert.True(jersey.GetProperty("editable").GetBoolean());
        var jerseyId = jersey.GetProperty("id").GetString();

        // single 只含 1 件 → 第二件 409（並行也一樣：用鎖保證不超收）
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => m.Client.PostAsJsonAsync("/api/v1/tcrfc/member/jerseys", Jersey(freeMembership), TestJson.WriteOptions)));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM jersey_issues WHERE member_id = @M", ("@M", m.MemberId)));

        // 待處理時可以改；別人改不了；已寄出後不能再改
        var update = new { recipientName = "【M測試】改名", size = "m", deliveryMethod = "pickup", phone = "0900-000-333" };
        var updated = await m.Client.PutAsJsonAsync($"/api/v1/tcrfc/member/jerseys/{jerseyId}", update, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("M", (await ReadJsonAsync(updated)).GetProperty("size").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PutAsJsonAsync($"/api/v1/tcrfc/member/jerseys/{jerseyId}", update, TestJson.WriteOptions)).StatusCode);
        await BizTest.ExecuteSqlAsync("UPDATE jersey_issues SET status = 'shipped', shipped_on = CAST(SYSUTCDATETIME() AS date) WHERE id = @J", ("@J", Guid.Parse(jerseyId!)));
        var locked = await m.Client.PutAsJsonAsync($"/api/v1/tcrfc/member/jerseys/{jerseyId}", update, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal("jersey_locked", CodeOf(await ReadJsonAsync(locked)));
        var after = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/jerseys")))[0];
        Assert.Equal("已寄出", after.GetProperty("items")[0].GetProperty("statusLabel").GetString());
        Assert.False(after.GetProperty("canRegister").GetBoolean());
    }

    // ═════════════ 我的報名 ═════════════

    [Fact]
    public async Task 我的報名_帶會員權杖報名才記到會員_匿名報名不歸戶_只看得到自己的()
    {
        var m = await _scope.CreateVerifiedMemberAsync("center-reg");
        var other = await _scope.CreateVerifiedMemberAsync("center-reg-other");
        using var anonymous = _scope.Client();
        var sessionId = await B1Test.ScalarAsync("SELECT TOP 1 s.id FROM sessions s JOIN clubs c ON c.id = s.club_id WHERE c.code = 'tcrfc' AND s.status = N'開放' AND s.signup_opens_at IS NULL AND s.signup_closes_at IS NULL AND s.capacity > s.enrolled_count + 5");
        var memberNos = new List<string>();
        try
        {
            var body = new { applicantName = "【M測試】報名者", phone = "0900-000-444", note = "member-reg-test" };
            var memberReg = await m.Client.PostAsJsonAsync($"/api/v1/tcrfc/programs/sessions/{sessionId}/registrations", body, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, memberReg.StatusCode);
            memberNos.Add((await ReadJsonAsync(memberReg)).GetProperty("registrationNo").GetString()!);
            var anonReg = await anonymous.PostAsJsonAsync($"/api/v1/tcrfc/programs/sessions/{sessionId}/registrations", body, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, anonReg.StatusCode); // 匿名報名行為與先前完全相同
            memberNos.Add((await ReadJsonAsync(anonReg)).GetProperty("registrationNo").GetString()!);
            // 帶壞掉的權杖：視為訪客報名，不報錯
            using var broken = _scope.Client("bad.token.value");
            var brokenReg = await broken.PostAsJsonAsync($"/api/v1/tcrfc/programs/sessions/{sessionId}/registrations", body, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, brokenReg.StatusCode);
            memberNos.Add((await ReadJsonAsync(brokenReg)).GetProperty("registrationNo").GetString()!);

            var mine = await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/registrations"));
            Assert.Equal(1, mine.GetArrayLength());
            Assert.Equal(memberNos[0], mine[0].GetProperty("registrationNo").GetString());
            Assert.Equal("tcrfc", mine[0].GetProperty("clubCode").GetString());
            Assert.Equal(0, (await ReadJsonAsync(await other.Client.GetAsync("/api/v1/member/registrations"))).GetArrayLength());
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM registrations WHERE registration_no = @N AND member_id IS NULL", ("@N", memberNos[1])));
        }
        finally
        {
            foreach (var no in memberNos)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE registration_no = @N", ("@N", no));
            }

            // 報名時名額已加一，測完還原（只還原本測試加上去的筆數）
            await BizTest.ExecuteSqlAsync("UPDATE sessions SET enrolled_count = enrolled_count - @N WHERE id = @S", ("@N", memberNos.Count), ("@S", sessionId));
        }
    }
}
