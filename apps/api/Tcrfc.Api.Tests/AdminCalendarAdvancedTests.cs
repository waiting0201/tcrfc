using System.Net;
using System.Text;
using System.Text.Json;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// L1 進階（分軌／衝突偵測／拖曳改期）、L3 分類與顯示設定、L4 訂閱與匯出（S2-6）。
/// 測試用的賽事與活動放在 2027-08（一線隊賽季之外、仍在訂閱 feed 的 400 天視窗內），一律在 finally 清掉；
/// 會改動的設定（預設檢視、試訓同步、隊別顯示）測完還原成讀到的原值。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminCalendarAdvancedTests(AdminWriteApiFixture fixture)
{
    private const string Base = "/api/v1/admin/tcrfc/calendar";
    private static readonly DateTime Aug10Evening = new(2027, 8, 10, 11, 0, 0, DateTimeKind.Utc); // 19:00 當地

    private async Task<Guid> CreateMatchAsync(HttpClient client, string date, string? kickoff, string opponent, string status = "scheduled", Guid? venueId = null)
    {
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        var d1 = await B1Test.TeamIdAsync("D1");
        var response = await client.PostAsync("/api/v1/admin/tcrfc/matches", BizTest.Json(new
        {
            seasonId, teamIds = new[] { d1 }, venueId, matchOn = date, kickoff, homeAway = "HOME", opponent, status,
        }));
        return (await BizTest.ReadAsync<Tcrfc.Api.Features.AdminMatches.AdminMatchDetailDto>(response)).Id;
    }

    private static Task DeleteMatchAsync(Guid id) => BizTest.ExecuteSqlAsync("DELETE FROM matches WHERE id = @Id", ("@Id", id));

    private async Task<Guid> CreateEventAsync(HttpClient client, DateTime startsAt, DateTime? endsAt, string title, Guid? venueId, bool isPublic = true, Guid? eventTypeId = null, bool withTeam = true)
    {
        var d1 = await B1Test.TeamIdAsync("D1");
        var response = await client.PostAsync($"{Base}/custom-events", BizTest.Multipart(new
        {
            startsAt, endsAt, isAllDay = false, isPublic, venueId, eventTypeId, teamIds = withTeam ? new[] { d1 } : Array.Empty<Guid>(),
            content = new { zh = new { title } },
        }));
        return (await BizTest.ReadAsync<AdminCalendarCustomEventDetailDto>(response)).Id;
    }

    // ═════════════ L1 分軌與衝突 ═════════════

    [Fact]
    public async Task 分軌與衝突_同場地同梯隊時段重疊被偵測出來_不重疊或已取消的不算()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var venue = await B1Test.VenueIdAsync("西屯");
        var match = await CreateMatchAsync(admin, "2027-08-10", "19:00", "【測試】衝突對手", venueId: venue);
        var cancelled = await CreateMatchAsync(admin, "2027-08-10", "19:00", "【測試】已取消對手", status: "cancelled", venueId: venue);
        var overlap = await CreateEventAsync(admin, Aug10Evening.AddMinutes(30), Aug10Evening.AddHours(2), "【測試】重疊活動", venue);
        var apart = await CreateEventAsync(admin, Aug10Evening.AddHours(5), Aug10Evening.AddHours(6), "【測試】不重疊活動", venue);
        try
        {
            var tracks = await BizTest.ReadAsync<AdminCalendarTracksDto>(await admin.GetAsync($"{Base}/tracks?from=2027-08-01&to=2027-09-01"));
            var d1Track = Assert.Single(tracks.Tracks, t => t.TeamCode == "D1");
            Assert.Contains(d1Track.Events, e => e.SourceId == match);
            Assert.Contains(d1Track.Events, e => e.SourceId == overlap);
            Assert.Equal("一線隊", d1Track.Name); // L3 顯示名稱
            Assert.Equal("#0B3D91", d1Track.Colour);

            var conflict = Assert.Single(tracks.Conflicts, c => c.First.SourceId == match || c.Second.SourceId == match);
            Assert.Contains("venue", conflict.Reasons);
            Assert.Contains("team", conflict.Reasons);
            Assert.Contains("D1", conflict.SharedTeamCodes);
            Assert.Contains("西屯", conflict.VenueName);
            var pair = new[] { conflict.First.SourceId, conflict.Second.SourceId };
            Assert.Contains(overlap, pair);
            Assert.DoesNotContain(tracks.Conflicts, c => new[] { c.First.SourceId, c.Second.SourceId }.Contains(cancelled)); // 已取消的賽事不參與
            Assert.DoesNotContain(tracks.Conflicts, c => new[] { c.First.SourceId, c.Second.SourceId }.Contains(apart));

            var conflicts = await BizTest.ReadAsync<List<AdminCalendarConflictDto>>(await admin.GetAsync($"{Base}/conflicts?from=2027-08-01&to=2027-09-01"));
            Assert.Contains(conflicts, c => new[] { c.First.SourceId, c.Second.SourceId }.Contains(overlap));

            // 範圍防呆
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"{Base}/tracks?from=2027-01-01&to=2029-01-01")).StatusCode);
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base}/tracks")).StatusCode);
            using var academy = await BizTest.ClientAsync(fixture, "academy.manager@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await academy.GetAsync($"{Base}/tracks")).StatusCode);
        }
        finally
        {
            foreach (var id in new[] { overlap, apart })
            {
                await admin.DeleteAsync($"{Base}/custom-events/{id}");
            }

            await DeleteMatchAsync(match);
            await DeleteMatchAsync(cancelled);
        }
    }

    [Fact]
    public async Task 總覽篩選_場地_狀態_類型與試訓來源()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var venue = await B1Test.VenueIdAsync("西屯");
        var other = await B1Test.VenueIdAsync("太原");
        var match = await CreateMatchAsync(admin, "2027-08-12", "19:00", "【測試】篩選對手", venueId: venue);
        try
        {
            string Url(string q) => $"{Base}/events?from=2027-08-01&to=2027-09-01&{q}";
            Assert.Contains(await Events(admin, Url($"venueId={venue}")), e => e.SourceId == match);
            Assert.DoesNotContain(await Events(admin, Url($"venueId={other}")), e => e.SourceId == match);
            Assert.Contains(await Events(admin, Url("status=scheduled")), e => e.SourceId == match);
            Assert.DoesNotContain(await Events(admin, Url("status=played")), e => e.SourceId == match);
            Assert.All(await Events(admin, Url("status=scheduled")), e => Assert.Equal("match", e.SourceType)); // 依狀態篩選時沒有自建活動
            var found = (await Events(admin, Url($"venueId={venue}"))).First(e => e.SourceId == match);
            Assert.Equal("19:00", found.Kickoff);
            Assert.Equal(venue, found.VenueId);
        }
        finally
        {
            await DeleteMatchAsync(match);
        }
    }

    private static async Task<List<AdminCalendarEventDto>> Events(HttpClient client, string url)
        => await BizTest.ReadAsync<List<AdminCalendarEventDto>>(await client.GetAsync(url));

    // ═════════════ L1 拖曳改期 ═════════════

    [Fact]
    public async Task 賽事改期_衝突時不寫入並回衝突清單_確認後才寫入_延賽記下原定日期()
    {
        using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var a = await CreateMatchAsync(manager, "2027-08-17", "19:00", "【測試】改期甲");
        var b = await CreateMatchAsync(manager, "2027-08-19", "19:00", "【測試】改期乙");
        var played = await CreateMatchAsync(manager, "2027-08-21", "19:00", "【測試】已結束", status: "played");
        try
        {
            // 把 a 拖到 b 的日期：同梯隊（D1）同時段 → 409，body 含 conflicts，沒有寫入
            var blocked = await manager.PostAsync($"{Base}/matches/{a}/reschedule", BizTest.Json(new { matchOn = "2027-08-19" }));
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("排程衝突", problem.GetProperty("title").GetString());
            Assert.Contains("時段重疊", problem.GetProperty("conflicts")[0].GetProperty("description").GetString());
            Assert.False(problem.GetProperty("saved").GetBoolean());
            Assert.Equal(new DateOnly(2027, 8, 17), (await MatchAsync(manager, a)).MatchOn);

            // 確認衝突後寫入；標為延賽會記下原定日期與時間；改開賽時間
            var moved = await BizTest.ReadAsync<AdminCalendarRescheduleResultDto>(await manager.PostAsync($"{Base}/matches/{a}/reschedule",
                BizTest.Json(new { matchOn = "2027-08-19", kickoff = "20:30", markAsPostponed = true, acknowledgeConflicts = true })));
            Assert.True(moved.Saved);
            Assert.False(moved.NotificationSent);
            Assert.Equal("postponed", moved.Status);
            Assert.Equal(new DateOnly(2027, 8, 17), moved.OriginalMatchOn);
            var detail = await MatchAsync(manager, a);
            Assert.Equal(new DateOnly(2027, 8, 19), detail.MatchOn);
            Assert.Equal("20:30", detail.Kickoff);
            Assert.Equal("19:00", detail.OriginalKickoff);

            // 不衝突的改期直接成功（只改日期，不標延賽）；再改期不覆蓋原定日期
            var again = await BizTest.ReadAsync<AdminCalendarRescheduleResultDto>(await manager.PostAsync($"{Base}/matches/{a}/reschedule", BizTest.Json(new { matchOn = "2027-08-25", markAsPostponed = true })));
            Assert.Equal(new DateOnly(2027, 8, 17), again.OriginalMatchOn);

            // 驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync($"{Base}/matches/{b}/reschedule", BizTest.Json(new { matchOn = "2027-08-30", kickoff = "25:99" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync($"{Base}/matches/{played}/reschedule", BizTest.Json(new { matchOn = "2027-08-30" }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsync($"{Base}/matches/{Guid.NewGuid()}/reschedule", BizTest.Json(new { matchOn = "2027-08-30" }))).StatusCode);

            // 權限：唯讀角色與別的俱樂部的帳號都不行
            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Base}/matches/{b}/reschedule", BizTest.Json(new { matchOn = "2027-08-30" }))).StatusCode);
            using var academy = await BizTest.ClientAsync(fixture, "academy.manager@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await academy.PostAsync($"{Base}/matches/{b}/reschedule", BizTest.Json(new { matchOn = "2027-08-30" }))).StatusCode);
        }
        finally
        {
            foreach (var id in new[] { a, b, played })
            {
                await DeleteMatchAsync(id);
            }
        }
    }

    private static async Task<Tcrfc.Api.Features.AdminMatches.AdminMatchDetailDto> MatchAsync(HttpClient client, Guid id)
        => await BizTest.ReadAsync<Tcrfc.Api.Features.AdminMatches.AdminMatchDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/matches/{id}"));

    [Fact]
    public async Task 自建活動改期_衝突需確認_權限跟隨自建事件()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var venue = await B1Test.VenueIdAsync("西屯");
        var one = await CreateEventAsync(editor, Aug10Evening.AddDays(7), Aug10Evening.AddDays(7).AddHours(1), "【測試】活動一", venue);
        var two = await CreateEventAsync(editor, Aug10Evening.AddDays(7).AddHours(6), Aug10Evening.AddDays(7).AddHours(7), "【測試】活動二", venue);
        try
        {
            var blocked = await editor.PostAsync($"{Base}/custom-events/{two}/move", BizTest.Json(new { startsAt = Aug10Evening.AddDays(7).AddMinutes(20), endsAt = Aug10Evening.AddDays(7).AddMinutes(80) }));
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var still = await BizTest.ReadAsync<AdminCalendarCustomEventDetailDto>(await editor.GetAsync($"{Base}/custom-events/{two}"));
            Assert.Equal(Aug10Evening.AddDays(7).AddHours(6), still.StartsAt);

            var acknowledged = await BizTest.ReadAsync<AdminCalendarRescheduleResultDto>(await editor.PostAsync($"{Base}/custom-events/{two}/move",
                BizTest.Json(new { startsAt = Aug10Evening.AddDays(7).AddMinutes(20), endsAt = Aug10Evening.AddDays(7).AddMinutes(80), acknowledgeConflicts = true })));
            Assert.True(acknowledged.Saved);
            Assert.Equal(HttpStatusCode.OK, (await editor.PostAsync($"{Base}/custom-events/{two}/move", BizTest.Json(new { startsAt = Aug10Evening.AddDays(9) }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PostAsync($"{Base}/custom-events/{two}/move",
                BizTest.Json(new { startsAt = Aug10Evening.AddDays(9), endsAt = Aug10Evening.AddDays(8) }))).StatusCode);

            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Base}/custom-events/{two}/move", BizTest.Json(new { startsAt = Aug10Evening }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await editor.PostAsync($"{Base}/custom-events/{Guid.NewGuid()}/move", BizTest.Json(new { startsAt = Aug10Evening }))).StatusCode);
        }
        finally
        {
            await editor.DeleteAsync($"{Base}/custom-events/{one}");
            await editor.DeleteAsync($"{Base}/custom-events/{two}");
        }
    }

    // ═════════════ L3 ═════════════

    [Fact]
    public async Task 設定_讀取與更新_驗證_試訓同步開關連動全部場次與行事曆總覽()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var original = await BizTest.ReadAsync<AdminCalendarSettingsDto>(await editor.GetAsync($"{Base}/settings"));
        try
        {
            Assert.Contains(original.Teams, t => t.Code == "D1");
            Assert.Contains(original.EventTypes, t => t.Code == "press_conference");
            Assert.False(original.SyncTrials);

            object Update(string view = "month", string range = "season", string? team = "D1", string[]? home = null, string? first = "D1", bool sync = false)
                => new { defaultView = view, defaultRange = range, defaultTeamCode = team, homeTeamCodes = home ?? new[] { "D1" }, firstTeamCode = first, syncTrials = sync };

            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(view: "grid")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(range: "forever")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(team: "BW1")))).StatusCode); // 別的俱樂部的隊別
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(home: new[] { "NOPE" })))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(first: "NOPE")))).StatusCode);

            var saved = await BizTest.ReadAsync<AdminCalendarSettingsDto>(await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(sync: true))));
            Assert.Equal("month", saved.DefaultView);
            Assert.Equal("season", saved.DefaultRange);
            Assert.Equal("D1", saved.DefaultTeamCode);
            Assert.Equal(new[] { "D1" }, saved.HomeTeamCodes);
            Assert.True(saved.SyncTrials);

            // 開關連動：所有試訓場次的同步旗標一起打開，行事曆總覽出現試訓來源（種子場次 2026-11-14）
            var unsynced = await BizTest.ScalarGuidAsync("SELECT CASE WHEN EXISTS (SELECT 1 FROM trials t JOIN clubs c ON c.id = t.club_id WHERE c.code = N'tcrfc' AND t.sync_to_calendar = 0) THEN NEWID() ELSE '00000000-0000-0000-0000-000000000000' END");
            Assert.Equal(Guid.Empty, unsynced);
            var trials = await Events(editor, $"{Base}/events?from=2026-11-01&to=2026-12-01&sourceType=trial");
            Assert.Contains(trials, e => e.SourceType == "trial" && e.Title.StartsWith("試訓"));

            var off = await BizTest.ReadAsync<AdminCalendarSettingsDto>(await editor.PutAsync($"{Base}/settings", BizTest.Json(Update(sync: false))));
            Assert.False(off.SyncTrials);
            Assert.Empty(await Events(editor, $"{Base}/events?from=2026-11-01&to=2026-12-01&sourceType=trial"));
        }
        finally
        {
            await editor.PutAsync($"{Base}/settings", BizTest.Json(new
            {
                defaultView = original.DefaultView, defaultRange = original.DefaultRange, defaultTeamCode = original.DefaultTeamCode,
                homeTeamCodes = original.HomeTeamCodes, firstTeamCode = original.FirstTeamCode, syncTrials = original.SyncTrials,
            }));
        }

        // 只有檢視權限的角色不能改
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{Base}/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsync($"{Base}/settings", BizTest.Json(new { defaultView = "list", defaultRange = "upcoming", syncTrials = false }))).StatusCode);
    }

    [Fact]
    public async Task 隊別分類設定_顯示名稱與代表色與是否公開_不公開的隊別訂閱feed回404()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var original = (await BizTest.ReadAsync<AdminCalendarSettingsDto>(await editor.GetAsync($"{Base}/settings"))).Teams.First(t => t.Code == "D1");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        try
        {
            object Team(string? zh = "【測試】一線隊", string? colour = "#123456", bool isPublic = true, Guid? teamId = null)
                => new { teams = new[] { new { teamId = teamId ?? original.TeamId, displayNameZh = zh, displayNameEn = "Test First Team", colour, sortOrder = 5, isPublic } } };

            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(Team(colour: "red")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(Team(teamId: Guid.NewGuid())))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(new { teams = Array.Empty<object>() }))).StatusCode);

            var teams = await BizTest.ReadAsync<List<AdminCalendarTeamSettingDto>>(await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(Team())));
            var d1 = teams.First(t => t.Code == "D1");
            Assert.Equal("【測試】一線隊", d1.DisplayNameZh);
            Assert.Equal("#123456", d1.EffectiveColour);
            Assert.Equal(5, d1.EffectiveSortOrder);

            var publicSettings = await BizTest.ReadAsync<PublicSettingsProbe>(await anonymous.GetAsync("/api/v1/tcrfc/calendar/settings?lang=en"));
            Assert.Equal("Test First Team", publicSettings.Teams.First(t => t.Code == "D1").DisplayName);

            await BizTest.ReadAsync<List<AdminCalendarTeamSettingDto>>(await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(Team(isPublic: false))));
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tcrfc/calendar/feed.ics?team=D1")).StatusCode);
            var hidden = await BizTest.ReadAsync<PublicSettingsProbe>(await anonymous.GetAsync("/api/v1/tcrfc/calendar/settings"));
            Assert.DoesNotContain(hidden.Teams, t => t.Code == "D1");
        }
        finally
        {
            await editor.PutAsync($"{Base}/settings/teams", BizTest.Json(new
            {
                teams = new[] { new { teamId = original.TeamId, displayNameZh = original.DisplayNameZh, displayNameEn = original.DisplayNameEn, colour = original.Colour, sortOrder = original.SortOrder, isPublic = original.IsPublic } },
            }));
        }
    }

    private sealed record PublicSettingsProbe(List<PublicTeamProbe> Teams);
    private sealed record PublicTeamProbe(string Code, string DisplayName);

    [Fact]
    public async Task 賽事類型_兩隊共用_只有系統管理員能寫_圖示只能從預設圖示集選_使用中不能刪除()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var code = "b1_" + Guid.NewGuid().ToString("N")[..8];
        object Type(string c, string? icon = "trophy", string? colour = "#00AA00") => new { code = c, nameZh = "【測試】類型", nameEn = "Test type", colour, icon, isPublic = true, sortOrder = 50 };

        // 內容編輯有 L3 編輯權限，但賽事類型是共用資料 → 403「共用內容唯讀」
        var denied = await editor.PostAsync($"{Base}/event-types", BizTest.Json(Type(code)));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("共用", await denied.Content.ReadAsStringAsync());
        var icons = await BizTest.ReadAsync<List<AdminEventTypeIconDto>>(await editor.GetAsync($"{Base}/event-types/icons"));
        Assert.Contains(icons, i => i.Code == "trophy");

        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        Guid? typeId = null;
        Guid? eventId = null;
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{Base}/event-types", BizTest.Json(Type(code, icon: "not-an-icon")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{Base}/event-types", BizTest.Json(Type(code, colour: "green")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{Base}/event-types", BizTest.Json(Type("Bad Code")))).StatusCode);

            var created = await BizTest.ReadAsync<AdminEventTypeDto>(await admin.PostAsync($"{Base}/event-types", BizTest.Json(Type(code))));
            typeId = created.Id;
            Assert.Equal("#00AA00", created.Colour);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Base}/event-types", BizTest.Json(Type(code)))).StatusCode);

            var updated = await BizTest.ReadAsync<AdminEventTypeDto>(await admin.PutAsync($"{Base}/event-types/{typeId}", BizTest.Json(Type(code, icon: "star"))));
            Assert.Equal("star", updated.Icon);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"{Base}/event-types/{typeId}", BizTest.Json(Type(code + "x")))).StatusCode); // 代碼不可變更
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsync($"{Base}/event-types/{typeId}", BizTest.Json(Type(code)))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"{Base}/event-types/order", BizTest.Json(new { ids = new[] { typeId } }))).StatusCode);

            // 有自建活動使用 → 不能刪除
            eventId = await CreateEventAsync(admin, Aug10Evening.AddDays(30), null, "【測試】使用類型的活動", null, eventTypeId: typeId, withTeam: false);
            var withUsage = (await BizTest.ReadAsync<List<AdminEventTypeDto>>(await admin.GetAsync($"{Base}/event-types"))).First(t => t.Id == typeId);
            Assert.Equal(1, withUsage.UsageCount);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{Base}/event-types/{typeId}")).StatusCode);
        }
        finally
        {
            if (eventId is not null)
            {
                await admin.DeleteAsync($"{Base}/custom-events/{eventId}");
            }

            if (typeId is not null)
            {
                Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Base}/event-types/{typeId}")).StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"{Base}/event-types/{Guid.NewGuid()}")).StatusCode);
    }

    // ═════════════ L4 訂閱與匯出 ═════════════

    [Fact]
    public async Task 訂閱_feed內容依隊別過濾_訂閱數統計_不公開活動與已取消賽事的處理()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var venue = await B1Test.VenueIdAsync("西屯");
        var match = await CreateMatchAsync(admin, "2027-08-24", "19:00", "【測試】訂閱對手", venueId: venue);
        var cancelled = await CreateMatchAsync(admin, "2027-08-26", "19:00", "【測試】訂閱取消對手", status: "cancelled");
        var publicEvent = await CreateEventAsync(admin, Aug10Evening.AddDays(20), Aug10Evening.AddDays(20).AddHours(1), "【測試】公開活動", venue, withTeam: false);
        var privateEvent = await CreateEventAsync(admin, Aug10Evening.AddDays(21), Aug10Evening.AddDays(21).AddHours(1), "【測試】不公開活動", venue, isPublic: false, withTeam: false);
        try
        {
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var all = await anonymous.GetAsync("/api/v1/tcrfc/calendar/feed.ics");
            Assert.Equal(HttpStatusCode.OK, all.StatusCode);
            Assert.Equal("text/calendar", all.Content.Headers.ContentType!.MediaType);
            var ics = await all.Content.ReadAsStringAsync();
            Assert.StartsWith("BEGIN:VCALENDAR", ics);
            Assert.Contains("X-WR-CALNAME:", ics);
            Assert.Contains("台中磐石", ics);
            Assert.Contains("vs 【測試】訂閱對手".Replace("【", "【"), ics);
            Assert.Contains($"UID:match-{match}@tcrfc", ics);
            Assert.Contains("DTSTART:20270824T110000Z", ics); // 當地 19:00 → UTC 11:00
            Assert.Contains("STATUS:CANCELLED", ics); // 取消的賽事保留並標記取消，客戶端才會同步
            Assert.Contains("【測試】公開活動", ics);
            Assert.DoesNotContain("【測試】不公開活動", ics);
            Assert.Contains("\r\n", ics);

            var team = await (await anonymous.GetAsync("/api/v1/tcrfc/calendar/feed.ics?team=D1")).Content.ReadAsStringAsync();
            Assert.Contains($"UID:match-{match}@tcrfc", team);
            Assert.DoesNotContain("【測試】公開活動", team); // 沒有隊別的俱樂部活動只出現在全站 feed
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tcrfc/calendar/feed.ics?team=BW1")).StatusCode); // 別的俱樂部的隊別
            var en = await (await anonymous.GetAsync("/api/v1/tcrfc/calendar/feed.ics?lang=en")).Content.ReadAsStringAsync();
            Assert.Contains("X-WR-CALNAME:", en);
            var bw = await (await anonymous.GetAsync("/api/v1/bw/calendar/feed.ics")).Content.ReadAsStringAsync();
            Assert.DoesNotContain($"match-{match}@tcrfc", bw);

            // 訂閱網址與訂閱數統計
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            var subscriptions = await BizTest.ReadAsync<AdminCalendarSubscriptionsDto>(await editor.GetAsync($"{Base}/subscriptions"));
            var allFeed = subscriptions.Feeds.First(f => f.FeedKey == "all");
            Assert.StartsWith("webcal://", allFeed.WebcalUrl);
            Assert.EndsWith("/api/v1/tcrfc/calendar/feed.ics", allFeed.WebcalUrl);
            Assert.StartsWith("http", allFeed.HttpsUrl);
            Assert.True(allFeed.Subscribers30d >= 1);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), allFeed.LastFetchedOn);
            var d1Feed = subscriptions.Feeds.First(f => f.FeedKey == "D1");
            Assert.EndsWith("?team=D1", d1Feed.WebcalUrl);
            Assert.True(d1Feed.Subscribers30d >= 1);
            Assert.Contains("估計", subscriptions.StatsNote);

            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{Base}/subscriptions")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base}/subscriptions")).StatusCode);
        }
        finally
        {
            foreach (var id in new[] { publicEvent, privateEvent })
            {
                await admin.DeleteAsync($"{Base}/custom-events/{id}");
            }

            await DeleteMatchAsync(match);
            await DeleteMatchAsync(cancelled);
        }
    }

    [Fact]
    public async Task 匯出_CSV與ics_格式與範圍驗證_不公開活動只給有自建事件檢視權的人()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var match = await CreateMatchAsync(admin, "2027-08-14", "19:00", "【測試】匯出對手");
        var privateEvent = await CreateEventAsync(admin, Aug10Evening.AddDays(5), null, "【測試】匯出不公開活動", null, isPublic: false, withTeam: false);
        try
        {
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            var csvResponse = await editor.GetAsync($"{Base}/export?from=2027-08-01&to=2027-09-01");
            Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
            var bytes = await csvResponse.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
            var csv = Encoding.UTF8.GetString(bytes);
            Assert.Contains("類別,日期,開始時間", csv);
            Assert.Contains("【測試】匯出對手", csv);
            Assert.Contains("賽事", csv);
            Assert.Contains("【測試】匯出不公開活動", csv); // 內容編輯有自建事件檢視權

            var icsResponse = await editor.GetAsync($"{Base}/export?format=ics&from=2027-08-01&to=2027-09-01&team=D1");
            var ics = await icsResponse.Content.ReadAsStringAsync();
            Assert.Equal("text/calendar", icsResponse.Content.Headers.ContentType!.MediaType);
            Assert.Contains("BEGIN:VEVENT", ics);
            Assert.Contains("【測試】匯出對手", ics);
            Assert.DoesNotContain("【測試】匯出不公開活動", ics); // 這個活動沒有隊別，依 D1 篩選掉

            // 競技／球隊管理有匯出權限，但沒有自建事件檢視權 → 看不到不公開活動
            using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
            var managerCsv = await (await manager.GetAsync($"{Base}/export?from=2027-08-01&to=2027-09-01")).Content.ReadAsStringAsync();
            Assert.Contains("【測試】匯出對手", managerCsv);
            Assert.DoesNotContain("【測試】匯出不公開活動", managerCsv);

            Assert.Equal(HttpStatusCode.BadRequest, (await editor.GetAsync($"{Base}/export?format=pdf")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.GetAsync($"{Base}/export?from=2027-01-01&to=2029-01-01")).StatusCode);
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base}/export")).StatusCode);
        }
        finally
        {
            await admin.DeleteAsync($"{Base}/custom-events/{privateEvent}");
            await DeleteMatchAsync(match);
        }
    }

    [Fact]
    public async Task 整季賽程CSV匯入_與C4共用機制_權限跟隨賽事建立()
    {
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Base}/matches/import", new StringContent("a,b", Encoding.UTF8, "text/csv"))).StatusCode);

        using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var response = await manager.PostAsync($"{Base}/matches/import", new StringContent("not,a,valid,header\n1,2,3,4", Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // CSV 表頭不符：與 C4 匯入同一套驗證與訊息
    }

    [Fact]
    public async Task 公開設定端點_不需要登入_回傳預設檢視與公開隊別與賽事類型()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var response = await anonymous.GetAsync("/api/v1/tcrfc/calendar/settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains(doc.GetProperty("defaultView").GetString(), new[] { "list", "month" });
        Assert.True(doc.GetProperty("teams").GetArrayLength() >= 1);
        Assert.True(doc.GetProperty("eventTypes").GetArrayLength() >= 1);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/nope/calendar/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/nope/calendar/feed.ics")).StatusCode);
    }
}
