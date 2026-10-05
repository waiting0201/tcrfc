using System.Net;
using System.Text;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.AdminMemberships;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// K1 會員名單與檢視（S2-5）。打真正的 HTTP 管線與 <c>tcrfc_club</c>；種子會員 M900001–M900007（見 <c>backoffice_seed.py</c> §44）
/// 只讀不寫，測試自己建的會員在 finally 清掉。角色：<c>customer.service</c>＝客服／行政（檢視／處理／解除遮罩／匯出，僅 tcrfc）、
/// <c>partner.club</c>＝合作球隊管理（僅 bw，只有檢視、沒有解除遮罩）、<c>super.admin</c>＝系統管理員、<c>content.editor</c>＝無會員權限。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminMembersTests(AdminWriteApiFixture fixture)
{
    private async Task<PagedResult<AdminMemberListItemDto>> ListAsync(HttpClient client, string club, string query = "")
        => await BizTest.ReadAsync<PagedResult<AdminMemberListItemDto>>(await client.GetAsync($"/api/v1/admin/{club}/members{query}"));

    [Fact]
    public async Task 名單_未登入401_沒有會員權限的角色403()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);

        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);

        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await service.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);
        // 客服只授權 tcrfc，看不到 bw。
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/bw/members")).StatusCode);
    }

    [Fact]
    public async Task 名單_一律遮罩個資_回應本文不含真實Email與姓名()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var response = await service.GetAsync("/api/v1/admin/tcrfc/members?keyword=M900001");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("member-a@example.com", body);
        Assert.DoesNotContain("0900-000-001", body);
        Assert.DoesNotContain("lineUserId", body, StringComparison.OrdinalIgnoreCase);

        var page = JsonSerializer.Deserialize<PagedResult<AdminMemberListItemDto>>(body, TestJson.Options)!;
        var item = Assert.Single(page.Items);
        Assert.True(item.IsMasked);
        Assert.Equal("M900001", item.MemberNo);
        Assert.Contains("○", item.Name);
        Assert.Contains("***@example.com", item.Email);
        Assert.False(item.LineBound);
        // 預設只列目前俱樂部（tcrfc）的會籍列。
        Assert.All(item.Memberships, m => Assert.Equal("tcrfc", m.ClubCode));
    }

    [Fact]
    public async Task 名單_合作球隊管理只看得到自家會籍_看不到對方俱樂部的任何會籍列()
    {
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var page = await ListAsync(partner, "bw", "?pageSize=100");
        var numbers = page.Items.Select(i => i.MemberNo).ToList();
        Assert.Contains("M900001", numbers); // 雙會籍：在 bw 有會籍
        Assert.Contains("M900006", numbers);
        Assert.DoesNotContain("M900002", numbers); // 只有 tcrfc 會籍
        var dual = page.Items.First(i => i.MemberNo == "M900001");
        Assert.All(dual.Memberships, m => Assert.Equal("bw", m.ClubCode));

        // crossClub 也不能繞過：範圍是「你有授權的俱樂部」。
        var cross = await ListAsync(partner, "bw", "?crossClub=true&pageSize=100");
        Assert.DoesNotContain(cross.Items, i => i.MemberNo == "M900002");
        Assert.All(cross.Items.SelectMany(i => i.Memberships), m => Assert.Equal("bw", m.ClubCode));

        // 直接打詳情：對方俱樂部的會員是 404，不洩漏存在與否。
        var tcrfcOnly = await B1Test.MemberIdAsync("M900002");
        Assert.Equal(HttpStatusCode.NotFound, (await partner.GetAsync($"/api/v1/admin/bw/members/{tcrfcOnly}")).StatusCode);
        // 也不能拿 tcrfc 路徑（沒有授權）。
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);
    }

    [Fact]
    public async Task 名單_沒有解除遮罩權限時關鍵字只比對會員編號_不能拿來探測個資()
    {
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Empty((await ListAsync(partner, "bw", "?keyword=member-a%40example.com")).Items);
        Assert.Single((await ListAsync(partner, "bw", "?keyword=M900001")).Items);

        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var byEmail = await ListAsync(service, "tcrfc", "?keyword=member-b%40example.com");
        Assert.Equal("M900002", Assert.Single(byEmail.Items).MemberNo);
    }

    [Fact]
    public async Task 名單_系統管理員跨俱樂部看得到雙會籍_單一俱樂部模式只有目前俱樂部()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var cross = await ListAsync(admin, "tcrfc", "?crossClub=true&keyword=M900001");
        Assert.Equal(new[] { "bw", "tcrfc" }, Assert.Single(cross.Items).Memberships.Select(m => m.ClubCode).OrderBy(c => c).ToArray());

        var single = await ListAsync(admin, "tcrfc", "?keyword=M900001");
        Assert.Equal("tcrfc", Assert.Single(Assert.Single(single.Items).Memberships).ClubCode);
    }

    [Fact]
    public async Task 名單_includeNoMembership_納入尚無任何會籍的會員_不洩漏只在他隊有會籍的會員()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "no-membership");
        try
        {
            // K1 現場建立、尚未開通：預設（只列本俱樂部有會籍者）看不到，這就是 K2 選擇器搜不到的原因。
            Assert.Empty((await ListAsync(service, "tcrfc", $"?keyword={member.MemberNo}")).Items);
            // 帶 includeNoMembership=true 才找得到（本俱樂部有會籍者 ＋ 任何俱樂部都沒有會籍者）。
            var found = await ListAsync(service, "tcrfc", $"?keyword={member.MemberNo}&includeNoMembership=true");
            var item = Assert.Single(found.Items);
            Assert.True(item.IsMasked);
            Assert.Empty(item.Memberships);

            // 單一俱樂部資料範圍的角色（只授權 bw）同樣可用，且不會因此看到只在 tcrfc 有會籍的會員。
            using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
            Assert.Single((await ListAsync(partner, "bw", $"?keyword={member.MemberNo}&includeNoMembership=true")).Items);
            var wide = await ListAsync(partner, "bw", "?includeNoMembership=true&pageSize=100");
            Assert.DoesNotContain(wide.Items, i => i.MemberNo == "M900002"); // 只有 tcrfc 會籍
            Assert.Contains(wide.Items, i => i.MemberNo == "M900001"); // 雙會籍：在 bw 有會籍
            Assert.All(wide.Items.SelectMany(i => i.Memberships), m => Assert.Equal("bw", m.ClubCode));

            // 與會籍篩選同時使用時不生效（層級、狀態等條件本來就只針對有會籍者）。
            var withTier = await ListAsync(service, "tcrfc", $"?keyword={member.MemberNo}&includeNoMembership=true&tier=fan_club");
            Assert.Empty(withTier.Items);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 名單_篩選_註冊來源_帳號狀態_層級_未知代碼400()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var line = await ListAsync(service, "tcrfc", "?signupSource=line&pageSize=100");
        Assert.NotEmpty(line.Items);
        Assert.All(line.Items, i => Assert.Equal("line", i.SignupSource));

        var unverified = await ListAsync(service, "tcrfc", "?status=unverified&pageSize=100");
        Assert.Contains(unverified.Items, i => i.MemberNo == "M900004");
        Assert.All(unverified.Items, i => Assert.Equal("unverified", i.DisplayStatus));

        var suspended = await ListAsync(service, "tcrfc", "?status=suspended&pageSize=100");
        Assert.Contains(suspended.Items, i => i.MemberNo == "M900005");

        var fan = await ListAsync(service, "tcrfc", "?tier=fan_club&pageSize=100");
        Assert.All(fan.Items, i => Assert.Contains(i.Memberships, m => m.Tier == "fan_club"));

        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/members?tier=gold")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/members?expiringWithinDays=0")).StatusCode);
    }

    [Fact]
    public async Task 名單_即將到期篩選_只含指定天數內到期的有效會籍()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "expiring");
        try
        {
            var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            var reg = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships",
                BizTest.Json(new { memberId = member.Id, seasonId })));
            var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd");
            var adjust = await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{reg.Membership.MembershipId}/adjust",
                BizTest.Json(new { endOn = end, reason = "測試：縮短到期日" }));
            Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);

            var within10 = await ListAsync(service, "tcrfc", "?expiringWithinDays=10&pageSize=100");
            Assert.Contains(within10.Items, i => i.Id == member.Id);
            var within3 = await ListAsync(service, "tcrfc", "?expiringWithinDays=3&pageSize=100");
            Assert.DoesNotContain(within3.Items, i => i.Id == member.Id);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 詳情_預設遮罩_解除遮罩要權限_客服可以_合作球隊管理不行()
    {
        var id = await B1Test.MemberIdAsync("M900001");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var masked = await BizTest.ReadAsync<AdminMemberDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/members/{id}"));
        Assert.True(masked.IsMasked);
        Assert.True(masked.CanReveal);
        Assert.Equal("****-**-**", masked.BirthOn);
        Assert.DoesNotContain("member-a@example.com", masked.Email);

        var full = await BizTest.ReadAsync<AdminMemberDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/members/{id}?reveal=true"));
        Assert.False(full.IsMasked);
        Assert.Equal("member-a@example.com", full.Email);
        Assert.Equal("1990-01-15", full.BirthOn);
        Assert.Equal("【測試】會員甲", full.Name);
        // 客服只看得到自己授權的 tcrfc 會籍（雙會籍會員的 bw 會籍列看不到）。
        Assert.All(full.Memberships, m => Assert.Equal("tcrfc", m.Membership.ClubCode));
        Assert.NotEmpty(full.Memberships.SelectMany(m => m.Payments));
        Assert.NotEmpty(full.Memberships.SelectMany(m => m.Cards));

        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var denied = await partner.GetAsync($"/api/v1/admin/bw/members/{id}?reveal=true");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var partnerMasked = await BizTest.ReadAsync<AdminMemberDetailDto>(await partner.GetAsync($"/api/v1/admin/bw/members/{id}"));
        Assert.False(partnerMasked.CanReveal);
        Assert.All(partnerMasked.Memberships, m => Assert.Equal("bw", m.Membership.ClubCode));
    }

    [Fact]
    public async Task 詳情_回應不含QR憑證字串與LINE識別碼()
    {
        var id = await B1Test.MemberIdAsync("M900001");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var body = await (await service.GetAsync($"/api/v1/admin/tcrfc/members/{id}?reveal=true")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lineUserId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 建立會員_重複Email409_格式錯誤400_編號依規則產生()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "create");
        try
        {
            Assert.Matches("^M[0-9]{6}$", member.MemberNo);
            Assert.Equal("admin", member.SignupSource);
            Assert.Equal("unverified", member.DisplayStatus);
            Assert.False(member.IsMasked); // 客服有解除遮罩權限，建立者拿到剛輸入的完整值

            var email = member.Email!;
            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync("/api/v1/admin/tcrfc/members", BizTest.Json(new { name = "重複", email }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/members", BizTest.Json(new { name = "格式", email = "not-an-email" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/members",
                BizTest.Json(new { name = "生日", email = $"future-{Guid.NewGuid():N}@example.com", birthOn = "2999-01-01" }))).StatusCode);

            // 沒有建立權限的角色（partner 只有檢視）
            using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsync("/api/v1/admin/bw/members", BizTest.Json(new { name = "x", email = "x@example.com" }))).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 停用啟用與內部備註()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "status");
        try
        {
            // 沒有會籍的帳號，受限帳號（客服只授權 tcrfc）不是「系統管理員」→ 找不到（範圍：要在自家有會籍）
            Assert.Equal(HttpStatusCode.NotFound, (await service.PutAsync($"/api/v1/admin/tcrfc/members/{member.Id}/note", BizTest.Json(new { internalNote = "x" }))).StatusCode);

            using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            var suspended = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.PutAsync(
                $"/api/v1/admin/tcrfc/members/{member.Id}/status", BizTest.Json(new { status = "suspended", reason = "測試停用" })));
            Assert.Equal("suspended", suspended.DisplayStatus);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/members/{member.Id}/status", BizTest.Json(new { status = "deleted" }))).StatusCode);

            var noted = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.PutAsync(
                $"/api/v1/admin/tcrfc/members/{member.Id}/note", BizTest.Json(new { internalNote = "【測試】內部備註" })));
            Assert.Equal("【測試】內部備註", noted.InternalNote);

            var active = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.PutAsync(
                $"/api/v1/admin/tcrfc/members/{member.Id}/status", BizTest.Json(new { status = "active" })));
            Assert.Equal("unverified", active.DisplayStatus); // Email 尚未驗證，啟用後仍顯示未驗證
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 重新產生QR_舊憑證失效_次數加一_不回傳憑證字串()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "reissue");
        try
        {
            var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            var membership = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships",
                BizTest.Json(new { memberId = member.Id, seasonId })));
            var card = Assert.Single(membership.Cards);
            var before = await ReadTokenAsync(card.Id);

            var response = await service.PostAsync($"/api/v1/admin/tcrfc/members/{member.Id}/cards/{card.Id}/reissue", null);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(before, body);
            var reissued = JsonSerializer.Deserialize<AdminMemberCardDto>(body, TestJson.Options)!;
            Assert.Equal(1, reissued.ReissueCount);
            Assert.NotEqual(before, await ReadTokenAsync(card.Id));

            // 別的會員的卡 → 404
            Assert.Equal(HttpStatusCode.NotFound, (await service.PostAsync($"/api/v1/admin/tcrfc/members/{Guid.NewGuid()}/cards/{card.Id}/reissue", null)).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    private static async Task<string> ReadTokenAsync(Guid cardId)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")!;
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT token FROM member_cards WHERE id = @Id";
        command.Parameters.AddWithValue("@Id", cardId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task 重複帳號比對_同一支電話會被列出_Email大小寫與Gmail點號視為同一個()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var groups = await BizTest.ReadAsync<List<AdminMemberDuplicateGroupDto>>(await service.GetAsync("/api/v1/admin/tcrfc/members/duplicates"));
        var phone = groups.FirstOrDefault(g => g.MatchKind == "phone" && g.Members.Any(m => m.MemberNo == "M900001"));
        Assert.NotNull(phone);
        Assert.Contains(phone!.Members, m => m.MemberNo == "M900007");
        Assert.All(phone.Members, m => Assert.Contains("*", m.Phone)); // 遮罩

        // 兩個 Gmail 寫法不同（點號）其實是同一個信箱
        var a = await B1Test.CreateMemberAsync(service, "tcrfc", "dupA", phone: "0900-000-778");
        Guid b = Guid.Empty;
        try
        {
            using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            var localA = "b1.dup" + Guid.NewGuid().ToString("N")[..6];
            await BizTest.ExecuteSqlAsync("UPDATE members SET email = @E WHERE id = @Id", ("@E", $"{localA}@gmail.com"), ("@Id", a.Id));
            var second = await B1Test.CreateMemberAsync(service, "tcrfc", "dupB", phone: "0900-000-779");
            b = second.Id;
            await BizTest.ExecuteSqlAsync("UPDATE members SET email = @E WHERE id = @Id", ("@E", $"{localA.Replace(".", "").ToUpperInvariant()}+x@GMAIL.com"), ("@Id", b));
            var found = await BizTest.ReadAsync<List<AdminMemberDuplicateGroupDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/members/duplicates?crossClub=true"));
            Assert.Contains(found, g => g.MatchKind == "email" && g.Members.Any(m => m.Id == a.Id) && g.Members.Any(m => m.Id == b));
        }
        finally
        {
            await B1Test.DeleteMembersAsync(a.Id, b);
        }
    }

    [Fact]
    public async Task 合併帳號_只有系統管理員_會籍轉移_被合併帳號標為已刪除且個資清除()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var target = await B1Test.CreateMemberAsync(admin, "tcrfc", "mergeT");
        var source = await B1Test.CreateMemberAsync(admin, "tcrfc", "mergeS");
        try
        {
            var tcrfcSeason = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            var bwSeason = await B1Test.SeasonIdAsync("bw", "2025");
            await BizTest.ReadAsync<AdminMembershipDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/memberships", BizTest.Json(new { memberId = target.Id, seasonId = tcrfcSeason })));
            await BizTest.ReadAsync<AdminMembershipDetailDto>(await admin.PostAsync("/api/v1/admin/bw/memberships", BizTest.Json(new { memberId = source.Id, seasonId = bwSeason })));

            // 客服沒有合併權限（sysadmin_only）
            Assert.Equal(HttpStatusCode.Forbidden, (await service.PostAsync("/api/v1/admin/tcrfc/members/merge",
                BizTest.Json(new { targetMemberId = target.Id, sourceMemberId = source.Id }))).StatusCode);
            // 自己合併自己 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/members/merge",
                BizTest.Json(new { targetMemberId = target.Id, sourceMemberId = target.Id }))).StatusCode);

            var merged = await BizTest.ReadAsync<MergeAdminMembersResultDto>(await admin.PostAsync("/api/v1/admin/tcrfc/members/merge",
                BizTest.Json(new { targetMemberId = target.Id, sourceMemberId = source.Id })));
            Assert.Equal(1, merged.MovedMemberships);

            var after = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/members/{target.Id}?reveal=true"));
            Assert.Equal(new[] { "bw", "tcrfc" }, after.Memberships.Select(m => m.Membership.ClubCode).OrderBy(c => c).ToArray());
            // 系統管理員跨俱樂部：被合併的帳號是已刪除，個資清除，指向保留帳號
            var gone = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/members/{source.Id}?reveal=true"));
            Assert.Equal("deleted", gone.Status);
            Assert.Equal(target.MemberNo, gone.MergedIntoMemberNo);
            Assert.EndsWith("@merged.invalid", gone.Email);
            Assert.Null(gone.Phone);
            Assert.Empty(gone.Memberships);
            // 再合併一次 → 400（已合併帳號不能再參與）
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/members/merge",
                BizTest.Json(new { targetMemberId = target.Id, sourceMemberId = source.Id }))).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(source.Id, target.Id);
        }
    }

    [Fact]
    public async Task 合併帳號_同一俱樂部同一球季都有會籍時整批拒絕409()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var a = await B1Test.CreateMemberAsync(admin, "tcrfc", "conflictA");
        var b = await B1Test.CreateMemberAsync(admin, "tcrfc", "conflictB");
        try
        {
            var season = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            foreach (var m in new[] { a, b })
            {
                await BizTest.ReadAsync<AdminMembershipDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/memberships", BizTest.Json(new { memberId = m.Id, seasonId = season })));
            }

            var response = await admin.PostAsync("/api/v1/admin/tcrfc/members/merge", BizTest.Json(new { targetMemberId = a.Id, sourceMemberId = b.Id }));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            // 沒有寫入：b 仍是啟用（未驗證）帳號
            var stillThere = await BizTest.ReadAsync<AdminMemberDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/members/{b.Id}"));
            Assert.NotEqual("deleted", stillThere.Status);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(a.Id, b.Id);
        }
    }

    [Fact]
    public async Task 匯出_需要用途_需要匯出權限_內容含個資但不含生日與LINE()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/members/export")).StatusCode);

        var response = await service.GetAsync("/api/v1/admin/tcrfc/members/export?purpose=%E6%B8%AC%E8%A9%A6%E5%8C%AF%E5%87%BA&keyword=M900001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })); // UTF-8 BOM
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("會員編號", csv);
        Assert.Contains("member-a@example.com", csv);
        Assert.DoesNotContain("1990-01-15", csv);
        Assert.DoesNotContain("生日", csv);
        Assert.DoesNotContain("LINE", csv.Split('\n')[0]);

        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/members/export?purpose=x")).StatusCode);
    }
}
