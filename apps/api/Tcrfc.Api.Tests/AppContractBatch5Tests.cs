using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 第五批 App 契約補強（2026-10-05）：我的報名課程摘要與穩定狀態代碼（C1／C2）、廣告裝置識別標頭與衍生檔網址與當日預載目錄（C3／C4／C5）、
/// 會籍方案與權益未翻譯標示、後台俱樂部簡稱讀寫、肖像同意布林（D1）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppContractBatch5Tests(AdminWriteApiFixture fixture)
{
    private const string App = "/api/v1/app";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> SeedMemberClientAsync(string email)
    {
        using var anonymous = fixture.CreateClient();
        var login = await MemberTestScope.LoginAsync(anonymous, email, "ContentEditor@123");
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    // ═════════════ C1／C2：我的報名與穩定狀態代碼 ═════════════

    [Fact]
    public async Task 我的報名_帶課程與試訓摘要_狀態有穩定代碼與雙語標籤_舊欄位保留()
    {
        var memberId = await BizTest.ScalarGuidAsync("SELECT id FROM members WHERE member_no = N'M900001'");
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = 'tcrfc'");
        var sessionId = await BizTest.ScalarGuidAsync("SELECT TOP 1 s.id FROM sessions s WHERE s.club_id = @C ORDER BY s.row_seq", ("@C", clubId));
        var trialId = await BizTest.ScalarGuidAsync("SELECT TOP 1 t.id FROM trials t WHERE t.club_id = @C ORDER BY t.row_seq", ("@C", clubId));
        var statuses = new[] { "待確認", "已確認", "已繳費", "完成", "取消", "候補" };
        var nos = new List<string>();
        try
        {
            for (var i = 0; i < statuses.Length; i++)
            {
                var no = $"ZZ5-{Guid.NewGuid():N}"[..20];
                nos.Add(no);
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO registrations (id, registration_no, club_id, session_id, trial_id, member_id, applicant_name, status) VALUES (NEWID(), @N, @C, @S, @T, @M, N'【測試】學員', @St)",
                    ("@N", no), ("@C", clubId), ("@S", i == 5 ? DBNull.Value : sessionId), ("@T", i == 5 ? trialId : DBNull.Value), ("@M", memberId), ("@St", statuses[i]));
            }

            using var client = await SeedMemberClientAsync("member-a@example.com");
            var zh = (await JsonAsync(await client.GetAsync("/api/v1/member/registrations?lang=zh"))).EnumerateArray()
                .Where(r => nos.Contains(r.GetProperty("registrationNo").GetString()!)).ToDictionary(r => r.GetProperty("registrationNo").GetString()!);
            Assert.Equal(6, zh.Count);

            var expected = new (string Zh, string Code, string LabelEn)[]
            {
                ("待確認", "pending", "Pending confirmation"), ("已確認", "confirmed", "Confirmed"), ("已繳費", "paid", "Paid"),
                ("完成", "completed", "Completed"), ("取消", "cancelled", "Cancelled"), ("候補", "waitlisted", "Waitlisted"),
            };
            for (var i = 0; i < expected.Length; i++)
            {
                var r = zh[nos[i]];
                Assert.Equal(expected[i].Zh, r.GetProperty("status").GetString());         // 既有欄位保留（中文字面值）
                Assert.Equal(expected[i].Code, r.GetProperty("statusCode").GetString());
                Assert.Equal(expected[i].Zh, r.GetProperty("statusLabelZh").GetString());
                Assert.Equal(expected[i].LabelEn, r.GetProperty("statusLabelEn").GetString());
            }

            // 課程摘要（前 5 筆是梯次報名）
            var course = zh[nos[0]];
            Assert.Equal("session", course.GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, course.GetProperty("trial").ValueKind);
            var c = course.GetProperty("course");
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("programSlug").GetString()));
            Assert.Contains(c.GetProperty("sessionStatusCode").GetString(), new[] { "open", "full", "waitlist", "ended", "unknown" });
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("sessionStatusLabelEn").GetString()));

            // 試訓摘要（第 6 筆）
            var trial = zh[nos[5]];
            Assert.Equal("trial", trial.GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, trial.GetProperty("course").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, trial.GetProperty("trial").GetProperty("trialOn").ValueKind);

            // 英文：課程名稱沒有英文版時標示未翻譯
            var en = (await JsonAsync(await client.GetAsync("/api/v1/member/registrations?lang=en"))).EnumerateArray().First(r => r.GetProperty("registrationNo").GetString() == nos[0]);
            Assert.Contains(en.GetProperty("isFallbackLocale").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
        }
        finally
        {
            foreach (var no in nos)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE registration_no = @N", ("@N", no));
            }
        }
    }

    [Fact]
    public async Task 公開課程梯次與試訓場次_帶穩定狀態代碼_每個課程詳情都能讀()
    {
        // 逐一讀兩個俱樂部的「每一個」課程詳情：先前有掛夥伴或有日期的梯次會讓 Dapper 具現化失敗（500）而沒有測試發現（E-171）。
        using var client = fixture.CreateClient();
        var total = 0;
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var programs = (await JsonAsync(await client.GetAsync($"/api/v1/{club}/programs?pageSize=100"))).GetProperty("items").EnumerateArray().ToList();
            foreach (var program in programs)
            {
                var detail = await JsonAsync(await client.GetAsync($"/api/v1/{club}/programs/{program.GetProperty("slug").GetString()}"));
                foreach (var s in detail.GetProperty("sessions").EnumerateArray())
                {
                    total++;
                    Assert.Contains(s.GetProperty("statusCode").GetString(), new[] { "open", "full", "waitlist", "ended" });
                    Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("statusLabelEn").GetString()));
                    Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("status").GetString())); // 既有欄位保留
                }

                Assert.Contains(detail.GetProperty("isFallbackLocale").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
            }
        }

        Assert.True(total > 0, "預期至少讀到一個梯次。");
    }

    [Fact]
    public void EnrollmentStatus_代碼表與DDL值域一致_未知值不丟例外()
    {
        Assert.Equal("open", Tcrfc.Api.Common.EnrollmentStatus.OfSlot("開放").Code);
        Assert.Equal("paid", Tcrfc.Api.Common.EnrollmentStatus.OfRegistration("已繳費").Code);
        var unknown = Tcrfc.Api.Common.EnrollmentStatus.OfRegistration("不存在");
        Assert.Equal("unknown", unknown.Code);
        Assert.Equal("unknown", Tcrfc.Api.Common.EnrollmentStatus.OfSlot(null).Code);
    }

    // ═════════════ C3／C4／C5：廣告 ═════════════

    [Fact]
    public async Task 廣告_裝置識別走標頭_頻次依標頭計算_素材帶衍生檔網址_當日預載目錄()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = fixture.CreateClient();
        try
        {
            var slot = await AdminAdsTests.CreateSlotAsync(biz, "zztest_b5", rotationCap: 3);
            var adv = await AdminAdsTests.CreateAdvertiserAsync(biz);
            var (campaign, creative) = await AdminAdsTests.StartRunningAsync(biz, adv.Id, slot.Id, new { perDeviceDailyCap = 1 }, "ZZTEST 每人上限B5");
            await BizTest.ExecuteSqlAsync("UPDATE ad_creatives SET image_key = N'zztest/ads/b5/x.webp', image_width = 1280, image_height = 720 WHERE id = @I", ("@I", creative));

            // 沒帶識別：不套用每人上限，回該素材（本 fixture 沒設定物件儲存，圖片網址為 null；衍生檔網址的實際內容見 AdminAdCreativeUploadTests 的 Azurite 驗證）
            var first = await JsonAsync(await anonymous.GetAsync($"{App}/ads/zztest_b5"));
            Assert.False(first.GetProperty("isFallback").GetBoolean());
            var stem = "x";

            // 同一台裝置曝光 1 次後：用「標頭」帶識別 → 每人頻次上限生效（回備援）；用查詢參數結果相同；沒帶識別仍回素材
            var device = AppTest.NewDeviceId();
            var events = new[] { new { type = "impression", creativeId = creative, occurredAt = DateTimeOffset.UtcNow.ToString("O"), presentationId = Guid.NewGuid().ToString() } };
            Assert.Equal(HttpStatusCode.OK, (await AppTest.PostJsonAsync(anonymous, $"{App}/ads/events", new { deviceInstallId = device, platform = "ios", locale = "zh", events })).StatusCode);

            using var withHeader = new HttpRequestMessage(HttpMethod.Get, $"{App}/ads/zztest_b5");
            withHeader.Headers.Add(Tcrfc.Api.Features.AppPublic.AppInput.DeviceHeaderName, device);
            var capped = await anonymous.SendAsync(withHeader);
            Assert.Contains("no-store", capped.Headers.CacheControl?.ToString() ?? "");
            Assert.True((await JsonAsync(capped)).GetProperty("isFallback").GetBoolean());
            Assert.True((await JsonAsync(await anonymous.GetAsync($"{App}/ads/zztest_b5?deviceInstallId={device}"))).GetProperty("isFallback").GetBoolean());
            Assert.False((await JsonAsync(await anonymous.GetAsync($"{App}/ads/zztest_b5"))).GetProperty("isFallback").GetBoolean());

            // 別台裝置（標頭）不受影響
            using var other = new HttpRequestMessage(HttpMethod.Get, $"{App}/ads/zztest_b5");
            other.Headers.Add(Tcrfc.Api.Features.AppPublic.AppInput.DeviceHeaderName, AppTest.NewDeviceId());
            Assert.False((await JsonAsync(await anonymous.SendAsync(other))).GetProperty("isFallback").GetBoolean());

            // 當日預載目錄：列出今天有效的檔期素材（含檔期起迄與權重，供「檔期結束即清除」），不套用每人頻次
            var prefetch = await JsonAsync(await anonymous.GetAsync($"{App}/ads/prefetch?lang=zh"));
            Assert.True(prefetch.GetProperty("validUntil").GetDateTime() > prefetch.GetProperty("generatedAt").GetDateTime());
            var slotEntry = prefetch.GetProperty("slots").EnumerateArray().First(s => s.GetProperty("slotCode").GetString() == "zztest_b5");
            var entry = Assert.Single(slotEntry.GetProperty("items").EnumerateArray());
            Assert.Equal(creative, entry.GetProperty("item").GetProperty("creativeId").GetGuid());
            Assert.True(entry.GetProperty("endsAt").GetDateTime() > DateTime.UtcNow);
            Assert.Equal(campaign.Weight, entry.GetProperty("weight").GetInt32());
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    // ═════════════ 會籍方案與權益未翻譯標示 ═════════════

    [Fact]
    public async Task 會籍方案與權益_繁中恆false_英文缺漏時true()
    {
        using var client = fixture.CreateClient();
        var zhPlans = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans?lang=zh"))).EnumerateArray().ToList();
        Assert.NotEmpty(zhPlans);
        Assert.All(zhPlans, p => Assert.False(p.GetProperty("isFallbackLocale").GetBoolean()));
        var enPlans = (await JsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans?lang=en"))).EnumerateArray().ToList();
        Assert.All(enPlans, p => Assert.Contains(p.GetProperty("isFallbackLocale").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False }));

        var benefits = await JsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?lang=zh"));
        Assert.False(benefits.GetProperty("isFallbackLocale").GetBoolean());
        foreach (var g in benefits.GetProperty("groups").EnumerateArray())
        {
            Assert.All(g.GetProperty("items").EnumerateArray(), i => Assert.False(i.GetProperty("isFallbackLocale").GetBoolean()));
        }

        var enBenefits = await JsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?lang=en"));
        Assert.Contains(enBenefits.GetProperty("isFallbackLocale").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
    }

    // ═════════════ 後台 J4 簡稱 ═════════════

    [Fact]
    public async Task 後台俱樂部_讀寫簡稱_公開端點立即反映_藍鯨英文可由後台人員填寫後還原()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var clubs = await JsonAsync(await admin.GetAsync("/api/v1/admin/clubs"));
        var bw = clubs.EnumerateArray().First(c => c.GetProperty("code").GetString() == "bw");
        var id = bw.GetProperty("id").GetGuid();
        var detail = await JsonAsync(await admin.GetAsync($"/api/v1/admin/clubs/{id}"));
        Assert.Equal("台中藍鯨", detail.GetProperty("zh").GetProperty("shortName").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("en").ValueKind); // 藍鯨沒有英文內容（B-5）

        // 準備一份「改寫簡稱」的更新請求（其餘欄位原樣帶回），驗證完成後還原
        object Body(string zhShort, object? en) => new
        {
            domain = detail.GetProperty("domain").GetString(), brandColor = ReadOrNull(detail, "brandColor"), brandSecondaryColor = ReadOrNull(detail, "brandSecondaryColor"),
            invoiceTitle = ReadOrNull(detail, "invoiceTitle"), taxId = ReadOrNull(detail, "taxId"), isCollectingSubject = detail.GetProperty("isCollectingSubject").GetBoolean(),
            defaultLocale = detail.GetProperty("defaultLocale").GetString(), sortOrder = detail.GetProperty("sortOrder").GetInt32(), status = detail.GetProperty("status").GetString(),
            content = new { zh = new { name = detail.GetProperty("zh").GetProperty("name").GetString(), shortName = zhShort, description = ReadOrNull(detail.GetProperty("zh"), "description") }, en },
        };

        var tooLong = await admin.PutAsJsonAsync($"/api/v1/admin/clubs/{id}", Body(new string('x', 33), null), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        try
        {
            var changed = await admin.PutAsJsonAsync($"/api/v1/admin/clubs/{id}", Body("台中藍鯨（測試簡稱）", null), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            using var anonymous = fixture.CreateClient();
            Assert.Equal("台中藍鯨（測試簡稱）", (await JsonAsync(await anonymous.GetAsync("/api/v1/clubs/bw?lang=zh"))).GetProperty("shortName").GetString());
            var list = (await JsonAsync(await anonymous.GetAsync("/api/v1/clubs?lang=zh"))).EnumerateArray().First(c => c.GetProperty("code").GetString() == "bw");
            Assert.Equal("台中藍鯨（測試簡稱）", list.GetProperty("shortName").GetString()); // 清單快取也已失效
        }
        finally
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/admin/clubs/{id}", Body("台中藍鯨", null), TestJson.WriteOptions)).StatusCode);
        }

        Assert.Equal("台中藍鯨", await C1Test.ScalarAsync<string>("SELECT short_name FROM clubs_i18n WHERE club_id = @C AND locale = N'zh-Hant'", ("@C", id)));
    }

    private static string? ReadOrNull(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    // ═════════════ D1：肖像同意布林 ═════════════

    [Fact]
    public async Task 肖像同意_球員與教練帶布林_照片非null必定為true_不洩漏是否未成年()
    {
        using var client = fixture.CreateClient();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var players = (await JsonAsync(await client.GetAsync($"/api/v1/{club}/players?pageSize=200"))).GetProperty("items").EnumerateArray().ToList();
            Assert.All(players, p =>
            {
                Assert.Contains(p.GetProperty("portraitConsented").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
                if (p.GetProperty("photoUrl").ValueKind != JsonValueKind.Null)
                {
                    Assert.True(p.GetProperty("portraitConsented").GetBoolean());
                }
            });
            var staffJson = await JsonAsync(await client.GetAsync($"/api/v1/{club}/staff"));
            var staff = (staffJson.ValueKind == JsonValueKind.Array ? staffJson.EnumerateArray() : staffJson.GetProperty("items").EnumerateArray()).ToList();
            Assert.All(staff, s => Assert.Contains(s.GetProperty("portraitConsented").ValueKind, new[] { JsonValueKind.True, JsonValueKind.False }));
        }

        var raw = await client.GetStringAsync("/api/v1/tcrfc/players?pageSize=200");
        Assert.DoesNotContain("consented_by_guardian", raw);
        Assert.DoesNotContain("portraitConsentStatus", raw, StringComparison.OrdinalIgnoreCase);
    }
}
