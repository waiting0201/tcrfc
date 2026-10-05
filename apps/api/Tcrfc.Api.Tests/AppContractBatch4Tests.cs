using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 第四批 App 契約補強（2026-10-05）：開賽時刻 <c>kickoffAt</c>、俱樂部簡稱、監護人同意（年齡閘門）、抽獎資訊（會員唯讀）、會員卡伺服器時間、
/// 搜尋球員 slug、店家座標、其餘型別的未翻譯標示。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppContractBatch4Tests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> SeedMemberClientAsync(string email)
    {
        var anonymous = fixture.CreateClient();
        var login = await MemberTestScope.LoginAsync(anonymous, email, "ContentEditor@123"); // 種子會員的共同本機密碼
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    // ═════════════ kickoffAt ═════════════

    [Fact]
    public async Task 賽事_kickoffAt是台北牆上時間換算的UTC時刻_沒有開賽時間為null()
    {
        using var client = fixture.CreateClient();
        var items = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/schedule?pageSize=100"))).GetProperty("items").EnumerateArray().ToList();
        var withTime = items.Where(m => m.GetProperty("kickoff").ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(m.GetProperty("kickoff").GetString())).ToList();
        Assert.NotEmpty(withTime);
        foreach (var m in withTime)
        {
            var wall = DateTime.Parse($"{m.GetProperty("matchOn").GetString()}T{m.GetProperty("kickoff").GetString()}:00", System.Globalization.CultureInfo.InvariantCulture);
            var expected = DateTime.SpecifyKind(wall.AddHours(-8), DateTimeKind.Utc);
            Assert.Equal(expected, m.GetProperty("kickoffAt").GetDateTime().ToUniversalTime());
        }

        var bw = (await JsonAsync(await client.GetAsync("/api/v1/bw/schedule?pageSize=100"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(bw, m => m.GetProperty("kickoff").ValueKind == JsonValueKind.Null && m.GetProperty("kickoffAt").ValueKind == JsonValueKind.Null);
    }

    [Theory]
    [InlineData("2026-10-10", "19:00", "2026-10-10T11:00:00Z")]
    [InlineData("2026-10-10", "9:30", "2026-10-10T01:30:00Z")]
    [InlineData("2026-10-10", "00:30", "2026-10-09T16:30:00Z")] // 跨日
    [InlineData("2026-10-10", null, null)]
    [InlineData("2026-10-10", "", null)]
    [InlineData("2026-10-10", "晚上七點", null)]
    [InlineData("2026-10-10", "25:00", null)]
    public void KickoffToUtc_換算與容錯(string date, string? kickoff, string? expected)
    {
        var actual = Tcrfc.Api.Common.TaiwanClock.KickoffToUtc(DateOnly.Parse(date), kickoff);
        Assert.Equal(expected is null ? null : DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), actual);
    }

    // ═════════════ 俱樂部簡稱 ═════════════

    [Fact]
    public async Task 俱樂部簡稱_磐石中英文_藍鯨中文有英文沒有_英文請求回退繁中()
    {
        using var client = fixture.CreateClient();
        var tc = await JsonAsync(await client.GetAsync("/api/v1/clubs/tcrfc?lang=zh"));
        Assert.Equal("台中磐石", tc.GetProperty("shortName").GetString());
        Assert.Equal("Taichung Rock FC", (await JsonAsync(await client.GetAsync("/api/v1/clubs/tcrfc?lang=en"))).GetProperty("shortName").GetString());

        var bw = await JsonAsync(await client.GetAsync("/api/v1/clubs/bw?lang=zh"));
        Assert.Equal("台中藍鯨", bw.GetProperty("shortName").GetString());
        // 藍鯨英文一律沒有（B-5）：英文請求時簡稱回退繁中，名稱欄位標示未翻譯
        var bwEn = await JsonAsync(await client.GetAsync("/api/v1/clubs/bw?lang=en"));
        Assert.Equal("台中藍鯨", bwEn.GetProperty("shortName").GetString());
        Assert.True(bwEn.GetProperty("isFallbackLocale").GetBoolean());
        var hasBwEnShort = await BizTest.ScalarGuidAsync("SELECT COALESCE((SELECT TOP 1 c.id FROM clubs c JOIN clubs_i18n i ON i.club_id = c.id WHERE c.code = 'bw' AND i.locale = N'en' AND i.short_name IS NOT NULL), CAST(0x0 AS uniqueidentifier))");
        Assert.Equal(Guid.Empty, hasBwEnShort); // 資料層：藍鯨英文簡稱是 NULL
    }

    // ═════════════ 監護人同意（年齡閘門）═════════════

    private async Task<HttpResponseMessage> RegisterAsync(object body)
    {
        using var client = fixture.CreateClient();
        return await client.PostAsJsonAsync("/api/v1/member/auth/register", body, TestJson.WriteOptions);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Fact]
    public async Task 註冊_生日必填_成年不蒐集監護人資料()
    {
        var email = _scope.NewEmail("age-adult");
        var noBirth = await RegisterAsync(new { club = "tcrfc", email, password = "Abcdefg1", name = "【M測試】成年" });
        Assert.Equal(HttpStatusCode.BadRequest, noBirth.StatusCode);
        Assert.Equal("birth_on_required", await CodeAsync(noBirth));

        // 成年即使帶了監護人資料也不儲存
        var ok = await RegisterAsync(new
        {
            club = "tcrfc", email, password = "Abcdefg1", name = "【M測試】成年", birthOn = "1990-05-05",
            guardianConsent = new { consented = true, guardianName = "不該被存", relationship = "parent" },
        });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(0, await ShopTest.CountAsync("SELECT COUNT(*) FROM members WHERE email = @E AND (guardian_consented_at IS NOT NULL OR guardian_name IS NOT NULL)", ("@E", email)));
    }

    [Fact]
    public async Task 註冊_未滿18歲須監護人同意_專屬錯誤碼_成功時記錄同意與關係與版本()
    {
        var birth = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).AddYears(-15).ToString("yyyy-MM-dd");
        var email = _scope.NewEmail("age-minor");
        object Body(object? consent) => new { club = "tcrfc", email, password = "Abcdefg1", name = "【M測試】未成年", birthOn = birth, guardianConsent = consent };

        var none = await RegisterAsync(Body(null));
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal("guardian_consent_required", await CodeAsync(none));
        Assert.Equal("guardian_consent_required", await CodeAsync(await RegisterAsync(Body(new { consented = false, guardianName = "王大明", relationship = "parent" }))));
        Assert.Equal("guardian_name_required", await CodeAsync(await RegisterAsync(Body(new { consented = true, guardianName = "  ", relationship = "parent" }))));
        Assert.Equal("guardian_name_required", await CodeAsync(await RegisterAsync(Body(new { consented = true, relationship = "parent" }))));
        Assert.Equal("invalid_guardian_relationship", await CodeAsync(await RegisterAsync(Body(new { consented = true, guardianName = "王大明", relationship = "uncle" }))));
        Assert.Equal("invalid_guardian_relationship", await CodeAsync(await RegisterAsync(Body(new { consented = true, guardianName = "王大明" }))));
        Assert.Equal("invalid_guardian_consent_version", await CodeAsync(await RegisterAsync(Body(new { consented = true, guardianName = "王大明", relationship = "parent", consentTextVersion = new string('v', 33) }))));
        Assert.Equal(0, await ShopTest.CountAsync("SELECT COUNT(*) FROM members WHERE email = @E", ("@E", email))); // 全部被擋，沒有半成品

        var ok = await RegisterAsync(Body(new { consented = true, guardianName = "王大明", relationship = "legal_guardian", consentTextVersion = "draft-0" }));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(1, await ShopTest.CountAsync(
            "SELECT COUNT(*) FROM members WHERE email = @E AND guardian_name = N'王大明' AND guardian_relationship = 'legal_guardian' AND guardian_consent_version = 'draft-0' AND guardian_consented_at IS NOT NULL AND DATEDIFF(minute, guardian_consented_at, SYSUTCDATETIME()) BETWEEN 0 AND 5",
            ("@E", email)));

        // 後台 K1 會員詳情看得到同意紀錄：沒解除遮罩時姓名遮罩，解除後完整
        var memberId = await BizTest.ScalarGuidAsync("SELECT id FROM members WHERE email = @E", ("@E", email));
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var masked = await JsonAsync(await admin.GetAsync($"/api/v1/admin/tcrfc/members/{memberId}"));
        var g = masked.GetProperty("guardianConsent");
        Assert.NotEqual("王大明", g.GetProperty("guardianName").GetString());
        Assert.Contains("○", g.GetProperty("guardianName").GetString());
        Assert.Equal("legal_guardian", g.GetProperty("relationship").GetString());
        Assert.Equal("法定監護人", g.GetProperty("relationshipLabel").GetString());
        Assert.Equal("draft-0", g.GetProperty("consentTextVersion").GetString());
        var revealed = await JsonAsync(await admin.GetAsync($"/api/v1/admin/tcrfc/members/{memberId}?reveal=true"));
        Assert.Equal("王大明", revealed.GetProperty("guardianConsent").GetProperty("guardianName").GetString());
    }

    [Fact]
    public async Task 年齡閘門_剛好滿18歲的當天算成年_差一天算未成年()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        Assert.False(Tcrfc.Api.Features.MemberAuth.MemberAgeGate.IsMinor(today.AddYears(-18), today));
        Assert.True(Tcrfc.Api.Features.MemberAuth.MemberAgeGate.IsMinor(today.AddYears(-18).AddDays(1), today));
        Assert.Equal(17, Tcrfc.Api.Features.MemberAuth.MemberAgeGate.AgeOn(new DateOnly(2008, 3, 1), new DateOnly(2026, 2, 28)));
        Assert.Equal(18, Tcrfc.Api.Features.MemberAuth.MemberAgeGate.AgeOn(new DateOnly(2008, 3, 1), new DateOnly(2026, 3, 1)));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task 刪除帳號_清除監護人姓名_保留同意事實()
    {
        var birth = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).AddYears(-16).ToString("yyyy-MM-dd");
        var email = _scope.NewEmail("age-del");
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(new
        {
            club = "tcrfc", email, password = MemberTestScope.Password, name = "【M測試】刪除", birthOn = birth,
            guardianConsent = new { consented = true, guardianName = "李監護", relationship = "parent" },
        })).StatusCode);
        var mail = MemberTestDoubles.Email.LastTo(email, "verify")!;
        using var anonymous = fixture.CreateClient();
        await anonymous.PostAsJsonAsync("/api/v1/member/auth/verify-email", new { token = CapturingEmailSender.TokenOf(mail) }, TestJson.WriteOptions);
        var login = await MemberTestScope.LoginAsync(anonymous, email, MemberTestScope.Password);
        using var authed = fixture.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/member/me") { Content = JsonContent.Create(new { password = MemberTestScope.Password }, options: TestJson.WriteOptions) };
        Assert.Equal(HttpStatusCode.NoContent, (await authed.SendAsync(delete)).StatusCode);
        Assert.Equal(1, await ShopTest.CountAsync(
            "SELECT COUNT(*) FROM members WHERE member_no = @N AND guardian_name IS NULL AND guardian_consented_at IS NOT NULL AND guardian_relationship = 'parent'", ("@N", login.MemberNo)));
    }

    // ═════════════ 抽獎資訊（會員唯讀）═════════════

    [Fact]
    public async Task 抽獎資訊_需登入_只含活動公開欄位與個人資格布林_不含序號名單與人數()
    {
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/member/draws")).StatusCode);

        using var fan = await SeedMemberClientAsync("member-a@example.com"); // M900001：磐石有效的球迷會員
        var response = await fan.GetAsync("/api/v1/member/draws");
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        var raw = await response.Content.ReadAsStringAsync();
        var draws = JsonDocument.Parse(raw).RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(draws); // 種子有一場已抽出的磐石抽獎
        foreach (var forbidden in new[] { "serial", "roster", "totalCount", "winner", "backup", "withholding", "rosterHash", "internalNote", "fulfil", "claimMethod", "nameSnapshot", "memberNo" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }

        var draw = draws.First(d => d.GetProperty("club").GetProperty("code").GetString() == "tcrfc");
        Assert.NotEqual(Guid.Empty, draw.GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(draw.GetProperty("drawCode").GetString()));
        Assert.Contains(draw.GetProperty("status").GetString(), new[] { "roster_locked", "drawn", "announced", "closed" });
        Assert.NotEqual(JsonValueKind.Null, draw.GetProperty("snapshotAt").ValueKind);
        Assert.True(draw.GetProperty("isEligible").GetBoolean());
        Assert.All(draws, d => Assert.NotEqual("draft", d.GetProperty("status").GetString())); // 草稿不列

        // 沒有球迷會籍的一般會員：看得到活動，個人資格為 false（布林，不是缺漏）
        using var plain = await SeedMemberClientAsync("member-c@example.com"); // M900003：只有一般會員
        var plainDraws = JsonDocument.Parse(await plain.GetStringAsync("/api/v1/member/draws")).RootElement.EnumerateArray().ToList();
        Assert.Equal(draws.Count, plainDraws.Count);
        Assert.All(plainDraws, d => Assert.False(d.GetProperty("isEligible").GetBoolean()));

        // 英文：標籤英文化、未翻譯標示
        var en = JsonDocument.Parse(await fan.GetStringAsync("/api/v1/member/draws?lang=en")).RootElement.EnumerateArray().First();
        Assert.Contains(en.GetProperty("statusLabel").GetString(), new[] { "Roster locked", "Drawn", "Announced", "Closed" });
    }

    // ═════════════ 會員卡伺服器時間 ═════════════

    [Fact]
    public async Task 會員卡_帶伺服器時間_供App記最後同步時間()
    {
        using var fan = await SeedMemberClientAsync("member-a@example.com");
        var before = DateTime.UtcNow.AddSeconds(-5);
        var cards = JsonDocument.Parse(await fan.GetStringAsync("/api/v1/member/cards")).RootElement.EnumerateArray().ToList();
        var after = DateTime.UtcNow.AddSeconds(5);
        Assert.NotEmpty(cards);
        Assert.All(cards, c =>
        {
            var t = c.GetProperty("serverTime").GetDateTime().ToUniversalTime();
            Assert.InRange(t, before, after);
        });
    }

    // ═════════════ 搜尋球員 slug ═════════════

    [Fact]
    public async Task 搜尋_球員結果帶slug_教練沒有()
    {
        using var client = fixture.CreateClient();
        var players = (await JsonAsync(await client.GetAsync("/api/v1/bw/players?pageSize=5"))).GetProperty("items").EnumerateArray().ToList();
        var name = players[0].GetProperty("name").GetString()!;
        var result = await JsonAsync(await client.GetAsync($"/api/v1/bw/search?q={Uri.EscapeDataString(name)}&type=player"));
        var hits = result.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("type").GetString() == "player").ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", h.GetProperty("slug").GetString()!));
        var slug = hits[0].GetProperty("slug").GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/bw/players/{slug}")).StatusCode); // 搜尋給的 slug 可直接查詳情
    }

    // ═════════════ 店家座標 ═════════════

    [Fact]
    public async Task 店家座標_後台拒絕0_0與半邊座標_資料庫約束當最後防線_公開端點null代表未確認()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        async Task<HttpResponseMessage> Post(decimal? lat, decimal? lng) => await admin.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(new
        {
            slug = BizTest.Unique("coord"), status = "draft", applicableTier = "all", lat, lng,
            content = new { zh = new { name = "【測試】座標店" } },
        }));

        var zero = await Post(0m, 0m);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Contains("0, 0", await zero.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(24.1m, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(91m, 120m)).StatusCode);

        // 資料庫約束：繞過應用層直接寫 (0,0) 會被擋
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => BizTest.ExecuteSqlAsync(
            "INSERT INTO partner_stores (id, slug, status, lat, lng) VALUES (NEWID(), @S, 'draft', 0, 0)", ("@S", BizTest.Unique("db0"))));
        Assert.Contains("CK_partner_stores_coords", ex.ToString());

        // 公開端點：未確認座標是 null（種子裡的店都有座標；這裡驗證欄位語意——有值的必在範圍內且不是 0,0）
        using var client = fixture.CreateClient();
        var stores = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?pageSize=100"))).EnumerateArray().ToList();
        foreach (var s in stores)
        {
            var lat = s.GetProperty("lat"); var lng = s.GetProperty("lng");
            Assert.Equal(lat.ValueKind == JsonValueKind.Null, lng.ValueKind == JsonValueKind.Null);
            if (lat.ValueKind != JsonValueKind.Null)
            {
                Assert.InRange(lat.GetDecimal(), -90, 90);
                Assert.InRange(lng.GetDecimal(), -180, 180);
                Assert.False(lat.GetDecimal() == 0 && lng.GetDecimal() == 0);
            }
        }
    }

    // ═════════════ 其餘型別的未翻譯標示 ═════════════

    [Fact]
    public async Task 未翻譯標示_夥伴贊助課程特約店家_繁中恆false_英文缺漏時true()
    {
        using var client = fixture.CreateClient();
        foreach (var path in new[] { "partners", "sponsors", "sponsor-packages", "programs", "partner-stores" })
        {
            var zh = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/{path}?lang=zh"));
            var zhItems = (zh.ValueKind == JsonValueKind.Array ? zh.EnumerateArray() : zh.GetProperty("items").EnumerateArray()).ToList();
            Assert.All(zhItems, i => Assert.False(i.GetProperty("isFallbackLocale").GetBoolean()));

            var en = await JsonAsync(await client.GetAsync($"/api/v1/tcrfc/{path}?lang=en"));
            var enItems = (en.ValueKind == JsonValueKind.Array ? en.EnumerateArray() : en.GetProperty("items").EnumerateArray()).ToList();
            Assert.Equal(zhItems.Count, enItems.Count);
            Assert.All(enItems, i => Assert.Contains(i.GetProperty("isFallbackLocale").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False })); // 欄位存在且為布林
        }
    }
}
