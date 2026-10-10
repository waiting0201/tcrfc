using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminTrials;
using Tcrfc.Api.Features.Trials;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 3.3／4.7 試訓場次公開讀取與線上報名（P4）。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）；
/// 驗證失敗的規則（姓名／聯絡方式／家長聯絡／出生日期）另有不需資料庫的測試在 <c>SiteBackendOfflineTranslationTests</c>。
/// 測試資料以對象說明前綴 <c>ZZTEST</c> 辨識，finally 一律清掉。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class TrialsPublicTests(AdminWriteApiFixture fixture)
{
    private const string Marker = "ZZTEST";

    private static DateOnly Soon(int days = 10) => TaiwanClock.Today.AddDays(days);

    private static async Task CleanupAsync()
    {
        const string ids = "SELECT trial_id FROM trials_i18n WHERE audience LIKE 'ZZTEST%'";
        await BizTest.ExecuteSqlAsync($"DELETE FROM registrations WHERE trial_id IN ({ids})");
        await BizTest.ExecuteSqlAsync($"DELETE FROM trials WHERE id IN ({ids}) ; DELETE FROM trials_i18n WHERE audience LIKE 'ZZTEST%'");
    }

    private static async Task<Guid> InsertTrialAsync(
        string club, DateOnly on, int? capacity = null, DateOnly? deadline = null, string status = "開放",
        string audienceZh = Marker + " U15 男足", string? audienceEn = Marker + " U15 boys", string? teamCode = null)
    {
        var id = Guid.NewGuid();
        // 先寫側表再寫主表會違反外鍵，所以主表在前；清除時順序相反（見 CleanupAsync：主表依側表的 trial_id 找）。
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = @C);
            INSERT INTO trials (id, club_id, team_id, trial_on, capacity, deadline_on, status)
            VALUES (@I, @club, (SELECT TOP 1 id FROM teams WHERE code = @T AND club_id = @club), @On, @Cap, @Dl, @S);
            INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@I, N'zh-Hant', @Zh);
            IF @En IS NOT NULL INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@I, N'en', @En);
            """,
            ("@C", club), ("@I", id), ("@T", teamCode), ("@On", on.ToDateTime(TimeOnly.MinValue)), ("@Cap", capacity),
            ("@Dl", deadline?.ToDateTime(TimeOnly.MinValue)), ("@S", status), ("@Zh", audienceZh), ("@En", audienceEn));
        return id;
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string club, Guid trialId, object body)
        => AppTest.PostJsonAsync(client, $"/api/v1/{club}/trials/{trialId}/registrations", body);

    private static async Task<(int Enrolled, string Status)> TrialStateAsync(Guid id)
        => (await C1Test.ScalarAsync<int>("SELECT enrolled_count FROM trials WHERE id = @I", ("@I", id)),
            (await C1Test.ScalarAsync<string>("SELECT status FROM trials WHERE id = @I", ("@I", id)))!);

    [Fact]
    public async Task 公開清單_只列未結束且日期未過的場次_依語系回退_俱樂部隔離()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var open = await InsertTrialAsync("tcrfc", Soon(10), capacity: 20, audienceZh: Marker + " 開放場", audienceEn: Marker + " Open");
            var onlyZh = await InsertTrialAsync("tcrfc", Soon(11), audienceZh: Marker + " 僅繁中", audienceEn: null);
            var ended = await InsertTrialAsync("tcrfc", Soon(12), status: "已結束", audienceZh: Marker + " 已結束場");
            var past = await InsertTrialAsync("tcrfc", TaiwanClock.Today.AddDays(-1), audienceZh: Marker + " 過去場");
            var bwTrial = await InsertTrialAsync("bw", Soon(10), audienceZh: Marker + " 藍鯨場");

            var zh = await BizTest.ReadAsync<List<PublicTrialDto>>(await client.GetAsync("/api/v1/tcrfc/trials?lang=zh"));
            Assert.Contains(zh, t => t.Id == open && t.Audience == Marker + " 開放場" && t.IsSignupOpen && !t.AcceptsWaitlist);
            Assert.DoesNotContain(zh, t => t.Id == ended || t.Id == past || t.Id == bwTrial);

            var en = await BizTest.ReadAsync<List<PublicTrialDto>>(await client.GetAsync("/api/v1/tcrfc/trials?lang=en"));
            Assert.Equal(Marker + " Open", en.Single(t => t.Id == open).Audience);
            Assert.Equal(Marker + " 僅繁中", en.Single(t => t.Id == onlyZh).Audience); // 缺英文回退繁中

            var bw = await BizTest.ReadAsync<List<PublicTrialDto>>(await client.GetAsync("/api/v1/bw/trials"));
            Assert.Contains(bw, t => t.Id == bwTrial);
            Assert.DoesNotContain(bw, t => t.Id == open);

            // 依日期由近到遠
            var dates = zh.Where(t => t.Audience!.StartsWith(Marker)).Select(t => t.TrialOn).ToList();
            Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 報名成功_佔名額_待確認_留報名編號_後台P4名單看得到()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 10);
            var response = await RegisterAsync(client, "tcrfc", id, new
            {
                privacyConsent = true, applicantName = Marker + " 王小明", phone = "0912345678", email = "ZZTEST@Example.test",
                birthOn = "2000-05-05", healthDeclaration = "無特殊疾病", note = "希望踢前鋒",
            });
            var result = await BizTest.ReadAsync<TrialRegistrationSubmittedDto>(response);
            Assert.Equal("待確認", result.Status);
            Assert.StartsWith("TCRFC-", result.RegistrationNo);

            Assert.Equal((1, "開放"), await TrialStateAsync(id));
            Assert.Equal(1, await C1Test.ScalarAsync<int>(
                "SELECT COUNT(*) FROM registrations WHERE trial_id = @I AND session_id IS NULL AND status = N'待確認' AND email = N'zztest@example.test'", ("@I", id)));

            var list = await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}/registrations");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(Marker + " 王小明", await list.Content.ReadAsStringAsync());
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 名額滿了_場次自動轉額滿_後來者排候補_不再增加已報名數()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 1);
            var first = await BizTest.ReadAsync<TrialRegistrationSubmittedDto>(
                await RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = Marker + " 甲", phone = "0911000001" }));
            Assert.Equal("待確認", first.Status);
            Assert.Equal((1, "額滿"), await TrialStateAsync(id));

            var second = await BizTest.ReadAsync<TrialRegistrationSubmittedDto>(
                await RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = Marker + " 乙", phone = "0911000002" }));
            Assert.Equal("候補", second.Status);
            Assert.Equal((1, "額滿"), await TrialStateAsync(id));

            var listed = (await BizTest.ReadAsync<List<PublicTrialDto>>(await client.GetAsync("/api/v1/tcrfc/trials"))).Single(t => t.Id == id);
            Assert.False(listed.IsSignupOpen);
            Assert.True(listed.AcceptsWaitlist);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 試訓剩1名額_16個不同報名者並行_16並行乘10輪_恰好一個待確認_其餘候補_已報名數恰為一()
    {
        // 語意（規劃書 P4／試訓：額滿自動關閉、候補）：名額是硬上限，多出來的進候補，不是拒絕。
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            for (var round = 0; round < 10; round++)
            {
                var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 1);
                var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() =>
                    RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = $"{Marker} 搶{round}-{i}", phone = $"0944{round:00}{i:0000}" }))));
                var dtos = new List<TrialRegistrationSubmittedDto>();
                foreach (var r in results)
                {
                    Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"第 {round} 輪：{(int)r.StatusCode}");
                    dtos.Add(await BizTest.ReadAsync<TrialRegistrationSubmittedDto>(r));
                }

                Assert.Equal(1, dtos.Count(r => r.Status == "待確認"));
                Assert.Equal(15, dtos.Count(r => r.Status == "候補"));
                Assert.Equal((1, "額滿"), await TrialStateAsync(id));
            }
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 同一人並行重複報名_高強度_16並行乘10輪_恰好一筆成立_其餘409_已報名數只加一()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            for (var round = 0; round < 10; round++)
            {
                var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 50);
                var body = new { privacyConsent = true, applicantName = $"{Marker} 重複{round}", phone = $"0922000{round:000}" };
                var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => RegisterAsync(client, "tcrfc", id, body))));
                var codes = string.Join(",", results.Select(r => (int)r.StatusCode));
                Assert.True(results.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict), $"第 {round} 輪：{codes}");
                Assert.Equal(1, results.Count(r => r.StatusCode != HttpStatusCode.Conflict));
                Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM registrations WHERE trial_id = @I AND status <> N'取消'", ("@I", id)));
                Assert.Equal(1, (await TrialStateAsync(id)).Enrolled);
            }
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 並行搶最後一個名額_只有一人待確認_其餘候補_已報名數恰為一()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 1);
            var tasks = Enumerable.Range(1, 6).Select(i => RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = $"{Marker} 並行{i}", phone = $"09110001{i:00}" })).ToArray();
            var responses = await Task.WhenAll(tasks);
            var results = new List<TrialRegistrationSubmittedDto>();
            foreach (var r in responses)
            {
                results.Add(await BizTest.ReadAsync<TrialRegistrationSubmittedDto>(r));
            }

            Assert.Equal(1, results.Count(r => r.Status == "待確認"));
            Assert.Equal(5, results.Count(r => r.Status == "候補"));
            Assert.Equal(6, results.Select(r => r.RegistrationNo).Distinct().Count());
            Assert.Equal((1, "額滿"), await TrialStateAsync(id));
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 重複報名_同姓名同電話409_取消後可再報名()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 10);
            var body = new { privacyConsent = true, applicantName = Marker + " 重複", phone = "0922000001" };
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", id, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(client, "tcrfc", id, body)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = Marker + " 重複", phone = "0922000002" })).StatusCode); // 不同電話視為不同人

            await BizTest.ExecuteSqlAsync("UPDATE registrations SET status = N'取消' WHERE trial_id = @I AND phone = N'0922000001'", ("@I", id));
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", id, body)).StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 未成年_家長聯絡方式必填_有填就成立()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 10);
            var minor = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-13)).ToString("yyyy-MM-dd");
            Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(client, "tcrfc", id,
                new { privacyConsent = true, applicantName = Marker + " 小球員", phone = "0933000001", birthOn = minor })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", id,
                new { privacyConsent = true, applicantName = Marker + " 小球員", phone = "0933000001", birthOn = minor, guardianName = "王大明", guardianPhone = "0933000002" })).StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 無法報名的情況_已結束_已過截止日_日期已過_不存在_跨俱樂部_格式錯誤()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        try
        {
            var ended = await InsertTrialAsync("tcrfc", Soon(), status: "已結束");
            var deadlinePassed = await InsertTrialAsync("tcrfc", Soon(), deadline: TaiwanClock.Today.AddDays(-1));
            var past = await InsertTrialAsync("tcrfc", TaiwanClock.Today.AddDays(-2));
            var deadlineToday = await InsertTrialAsync("tcrfc", Soon(), deadline: TaiwanClock.Today); // 截止日當天仍可報名（含當日）
            var open = await InsertTrialAsync("tcrfc", Soon());
            var body = new { privacyConsent = true, applicantName = Marker + " 測試", phone = "0944000001" };

            Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(client, "tcrfc", ended, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(client, "tcrfc", deadlinePassed, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(client, "tcrfc", past, body)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", deadlineToday, body)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await RegisterAsync(client, "tcrfc", Guid.NewGuid(), body)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await RegisterAsync(client, "bw", open, body)).StatusCode); // 別的俱樂部看不到
            Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(client, "tcrfc", open, new { privacyConsent = true, applicantName = "  ", phone = "0944000001" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync(client, "tcrfc", open, new { privacyConsent = true, applicantName = Marker + " 無聯絡方式" })).StatusCode);
            Assert.Equal((0, "開放"), await TrialStateAsync(open)); // 驗證失敗不佔名額
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 後台P4_公開報名進來的資料可被確認_狀態與名額連動沿用既有規則()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var id = await InsertTrialAsync("tcrfc", Soon(), capacity: 5);
            Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "tcrfc", id, new { privacyConsent = true, applicantName = Marker + " 後台", phone = "0955000001" })).StatusCode);
            var detail = await BizTest.ReadAsync<AdminTrialDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/trials/{id}"));
            Assert.Equal(1, detail.EnrolledCount);
        }
        finally
        {
            await CleanupAsync();
        }
    }
}
