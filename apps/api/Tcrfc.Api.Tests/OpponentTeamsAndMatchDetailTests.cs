using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminOpponentTeams;
using Tcrfc.Api.Features.AdminStandings;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 主站規劃書 v3.31（C4）：對手球隊庫 CRUD ＋ 刪除被擋、賽事／積分榜挑對手（跨俱樂部 400）、替補 <c>came_on</c> 強制規則、
/// 球員數據自動彙總新規則（出賽＝先發或 came_on）、公開賽程單場詳情（進球／牌／出賽名單）、賽程列表 <c>order=desc</c> 與多狀態篩選。
/// 打真實 HTTP 管線與真實資料庫；隊徽上傳打真實 Azurite（沒有 Azurite 時只跳過那一項）。
/// 每個測試自建獨立球季與「【TOT】」開頭的測試球隊，結束時清掉，不碰種子賽事。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class OpponentTeamsAndMatchDetailTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private const string Manager = "team.manager@tcrfc.test";
    private const string Prefix = "【TOT】";

    // ───────────────────────────── 測試脈絡 ─────────────────────────────

    private sealed class Ctx : IAsyncDisposable
    {
        public required Guid ClubId;
        public required Guid SeasonId;
        public required string SeasonCode;
        public required Guid TeamId;
        public required List<Guid> Players;

        public async ValueTask DisposeAsync()
        {
            await BizTest.ExecuteSqlAsync(
                """
                DELETE FROM match_goals WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM match_cards WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM match_lineups WHERE match_id IN (SELECT id FROM matches WHERE season_id = @S);
                DELETE FROM matches WHERE season_id = @S;
                DELETE FROM standings WHERE season_id = @S;
                DELETE FROM player_season_stats WHERE season_id = @S;
                DELETE FROM seasons WHERE id = @S;
                DELETE FROM opponent_teams WHERE name LIKE N'【TOT】%';
                """, ("@S", SeasonId));
        }
    }

    private static async Task<Ctx> NewCtxAsync()
    {
        // 前一次失敗可能留下殘骸球隊（名稱唯一鍵會擋住這次）；先清。
        await BizTest.ExecuteSqlAsync("DELETE FROM opponent_teams WHERE name LIKE N'【TOT】%' AND NOT EXISTS (SELECT 1 FROM matches m WHERE m.opponent_team_id = opponent_teams.id)");
        var clubId = await C1Test.ClubIdAsync("tcrfc");
        var seasonId = Guid.NewGuid();
        var code = "T" + Guid.NewGuid().ToString("N")[..8];
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO seasons (id, club_id, code, start_on, end_on) VALUES (@I, @C, @K, '2019-01-01', '2019-12-31')", ("@I", seasonId), ("@C", clubId), ("@K", code));
        var teamId = await BizTest.ScalarGuidAsync("SELECT t.id FROM teams t WHERE t.club_id = @C AND t.code = N'D1'", ("@C", clubId));
        var players = new List<Guid>();
        for (var skip = 0; skip < 4; skip++)
        {
            players.Add(await BizTest.ScalarGuidAsync(
                "SELECT p.id FROM players p WHERE p.club_id = @C AND p.team_id = @T AND (p.status IS NULL OR p.status = 'active') ORDER BY p.row_seq OFFSET @K ROWS FETCH NEXT 1 ROWS ONLY",
                ("@C", clubId), ("@T", teamId), ("@K", skip)));
        }

        return new Ctx { ClubId = clubId, SeasonId = seasonId, SeasonCode = code, TeamId = teamId, Players = players };
    }

    private static string Unique(string label) => $"{Prefix}{label}{Guid.NewGuid().ToString("N")[..6]}";

    private Task<HttpClient> ManagerAsync() => BizTest.ClientAsync(fixture, Manager);

    private static async Task<AdminOpponentTeamDetailDto> CreateTeamAsync(HttpClient client, string club, string name, string? nameEn = null, bool withCrest = false)
    {
        var files = withCrest ? new[] { ("crest", TestImages.SmallPng(), "crest.png", "image/png") } : [];
        var response = await client.PostAsync($"/api/v1/admin/{club}/opponent-teams", BizTest.Multipart(new { name, nameEn, sortOrder = 0 }, files));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(response);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static CreateAdminMatchRequest NewMatch(Ctx ctx, int day, string status = "played", Guid? opponentTeamId = null, string? opponent = "測試對手") => new()
    {
        SeasonId = ctx.SeasonId,
        TeamIds = [ctx.TeamId],
        MatchOn = new DateOnly(2019, 3, day),
        Kickoff = "19:00",
        Opponent = opponent,
        OpponentTeamId = opponentTeamId,
        Status = status,
    };

    private static async Task<AdminMatchDetailDto> CreateMatchAsync(HttpClient client, CreateAdminMatchRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", request);
        return await BizTest.ReadAsync<AdminMatchDetailDto>(response);
    }

    // ═════════════════════════ 對手球隊庫：授權與 CRUD ═════════════════════════

    [Fact]
    public async Task 對手球隊_未登入401_跨俱樂部403_唯讀角色不能建立()
    {
        using var anon = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/tcrfc/opponent-teams")).StatusCode);

        using var manager = await ManagerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/admin/bw/opponent-teams")).StatusCode); // 只授權 tcrfc

        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/opponent-teams")).StatusCode);
        var create = await viewer.PostAsync("/api/v1/admin/tcrfc/opponent-teams", BizTest.Multipart(new { name = Unique("唯讀") }));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [AzuriteFact]
    public async Task 對手球隊_建立含隊徽_讀取_更新換隊徽_同名409_刪除並清隊徽物件()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var name = Unique("甲隊");
        var team = await CreateTeamAsync(client, "tcrfc", name, "Team A", withCrest: true);
        var prefix = $"tcrfc/opponent-teams/{team.Id}/";
        Assert.NotNull(team.CrestUrl);
        Assert.NotNull(team.CrestThumbUrl);
        Assert.True(team.CrestWidth > 0 && team.CrestHeight > 0);
        Assert.Equal("Team A", team.NameEn);
        Assert.Equal(5, await CountBlobsAsync(prefix)); // 主檔＋1280／640／320／160

        // 列表含此隊，且有使用數。
        var list = await BizTest.ReadAsync<List<AdminOpponentTeamListItemDto>>(await client.GetAsync("/api/v1/admin/tcrfc/opponent-teams"));
        var item = Assert.Single(list, t => t.Id == team.Id);
        Assert.Equal(0, item.UsageCount);

        // 同俱樂部同名 → 409 opponent_team_name_duplicate。
        var dup = await client.PostAsync("/api/v1/admin/tcrfc/opponent-teams", BizTest.Multipart(new { name }));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("opponent_team_name_duplicate", await ProblemCodeAsync(dup));

        // 更新：改名、英文清空、換隊徽（舊物件被刪、新一組 5 個）。
        var newName = Unique("甲隊改");
        var updated = await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}",
            BizTest.Multipart(new { name = newName, nameEn = (string?)null, sortOrder = 3 }, ("crest", TestImages.SmallPng(), "c2.png", "image/png"))));
        Assert.Equal(newName, updated.Name);
        Assert.Null(updated.NameEn);
        Assert.Equal(3, updated.SortOrder);
        Assert.NotEqual(team.CrestUrl, updated.CrestUrl);
        Assert.Equal(5, await CountBlobsAsync(prefix));

        // 不帶檔案維持隊徽；removeCrest 清空隊徽並刪物件。
        var kept = await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}", BizTest.Multipart(new { name = newName })));
        Assert.Equal(updated.CrestUrl, kept.CrestUrl);
        var removed = await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}", BizTest.Multipart(new { name = newName, removeCrest = true })));
        Assert.Null(removed.CrestUrl);
        Assert.Equal(0, await CountBlobsAsync(prefix));

        // 再傳一次隊徽後刪除：資料列與物件一併消失。
        await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}", BizTest.Multipart(new { name = newName }, ("crest", TestImages.SmallPng(), "c3.png", "image/png")));
        Assert.Equal(5, await CountBlobsAsync(prefix));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);
        Assert.Equal(0, await CountBlobsAsync(prefix));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);
    }

    private async Task<int> CountBlobsAsync(string prefix)
    {
        var count = 0;
        await foreach (var _ in fixture.InspectorContainer.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public async Task 對手球隊_驗證錯誤400_不能別的俱樂部讀寫()
    {
        using var client = await ManagerAsync();
        var empty = await client.PostAsync("/api/v1/admin/tcrfc/opponent-teams", BizTest.Multipart(new { name = "  " }));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        var tooLong = await client.PostAsync("/api/v1/admin/tcrfc/opponent-teams", BizTest.Multipart(new { name = new string('長', 129) }));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        await using var ctx = await NewCtxAsync();
        using var super = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var bwTeam = await CreateTeamAsync(super, "bw", Unique("藍鯨庫"));
        // tcrfc 的管理者用 tcrfc 路徑讀不到 bw 的球隊（俱樂部硬過濾）。
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/opponent-teams/{bwTeam.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{bwTeam.Id}", BizTest.Multipart(new { name = "改" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{bwTeam.Id}")).StatusCode);
        // 不同俱樂部可以有同名球隊。
        var same = await super.PostAsync("/api/v1/admin/tcrfc/opponent-teams", BizTest.Multipart(new { name = bwTeam.Name }));
        Assert.Equal(HttpStatusCode.Created, same.StatusCode);
    }

    // ═════════════════════════ 賽事／積分榜挑對手 ═════════════════════════

    [Fact]
    public async Task 賽事挑對手球隊_帶入隊名_不用填文字_跨俱樂部400_改名同步後備文字()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var team = await CreateTeamAsync(client, "tcrfc", Unique("乙隊"), "Team B");

        var created = await CreateMatchAsync(client, NewMatch(ctx, 1, opponentTeamId: team.Id, opponent: null));
        Assert.Equal(team.Id, created.OpponentTeamId);
        Assert.Equal(team.Name, created.Opponent); // 隊名帶入 matches.opponent 作後備
        Assert.Equal("Team B", created.OpponentEn);

        // 沒挑球隊庫又沒填文字 → 400。
        var none = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatch(ctx, 2, opponent: null));
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);

        // 別的俱樂部的球隊 → 400（欄位 opponentTeamId）。
        using var super = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var bwTeam = await CreateTeamAsync(super, "bw", Unique("藍鯨隊"));
        var cross = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatch(ctx, 3, opponentTeamId: bwTeam.Id));
        Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
        Assert.Contains("opponentTeamId", await cross.Content.ReadAsStringAsync());
        var missing = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatch(ctx, 3, opponentTeamId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // 更新時同樣驗證。
        var crossUpdate = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{created.Id}", new UpdateAdminMatchRequest
        {
            SeasonId = ctx.SeasonId, TeamIds = [ctx.TeamId], MatchOn = created.MatchOn, Status = "played", OpponentTeamId = bwTeam.Id,
        });
        Assert.Equal(HttpStatusCode.BadRequest, crossUpdate.StatusCode);

        // 球隊改名 → 引用它的賽事後備文字同步。
        var renamed = Unique("乙隊改");
        await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(await client.PutAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}", BizTest.Multipart(new { name = renamed, nameEn = "Team B2" })));
        var after = await BizTest.ReadAsync<AdminMatchDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/matches/{created.Id}"));
        Assert.Equal(renamed, after.Opponent);
        Assert.Equal("Team B2", after.OpponentEn);

        // 解除球隊庫（改回純文字）：opponentTeamId 空、文字必填。
        var unlink = await BizTest.ReadAsync<AdminMatchDetailDto>(await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{created.Id}", new UpdateAdminMatchRequest
        {
            SeasonId = ctx.SeasonId, TeamIds = [ctx.TeamId], MatchOn = created.MatchOn, Status = "played", Opponent = "純文字對手",
        }));
        Assert.Null(unlink.OpponentTeamId);
        Assert.Equal("純文字對手", unlink.Opponent);
    }

    [Fact]
    public async Task 積分榜挑對手球隊_隊名帶入_跨俱樂部400()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var team = await CreateTeamAsync(client, "tcrfc", Unique("丙隊"), "Team C");

        var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest
        {
            SeasonId = ctx.SeasonId, OpponentTeamId = team.Id, Rank = 1, Played = 3, Points = 9,
        });
        var standing = await BizTest.ReadAsync<AdminStandingDetailDto>(response);
        Assert.Equal(team.Id, standing.OpponentTeamId);
        Assert.Equal(team.Name, standing.TeamName);
        Assert.Equal("Team C", standing.TeamNameEn);

        // 沒有文字也沒有球隊 → 400（舊行為不變）。
        var none = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest { SeasonId = ctx.SeasonId });
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);

        using var super = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var bwTeam = await CreateTeamAsync(super, "bw", Unique("藍鯨隊"));
        var cross = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest { SeasonId = ctx.SeasonId, OpponentTeamId = bwTeam.Id });
        Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);

        // 公開積分榜：用庫內隊名（請求語系 → 繁中）與 opponentTeamId。
        using var pub = fixture.CreateClient();
        var pubZh = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/standings?season={ctx.SeasonCode}");
        var row = pubZh.GetProperty("items")[0];
        Assert.Equal(team.Name, row.GetProperty("teamName").GetString());
        Assert.Equal(team.Id, row.GetProperty("opponentTeamId").GetGuid());
        var pubEn = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/standings?season={ctx.SeasonCode}&lang=en");
        Assert.Equal("Team C", pubEn.GetProperty("items")[0].GetProperty("teamName").GetString());
    }

    // ═════════════════════════ 刪除被擋 ═════════════════════════

    [Fact]
    public async Task 對手球隊_被賽事或積分榜引用_刪除409_opponent_team_in_use_解除後可刪()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var team = await CreateTeamAsync(client, "tcrfc", Unique("丁隊"));
        var match = await CreateMatchAsync(client, NewMatch(ctx, 5, opponentTeamId: team.Id));
        var standing = await BizTest.ReadAsync<AdminStandingDetailDto>(await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings",
            new CreateAdminStandingRequest { SeasonId = ctx.SeasonId, OpponentTeamId = team.Id }));

        var detail = await BizTest.ReadAsync<AdminOpponentTeamDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}"));
        Assert.Equal(1, detail.MatchCount);
        Assert.Equal(1, detail.StandingCount);
        Assert.Equal(2, detail.UsageCount);

        var blocked = await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("opponent_team_in_use", await ProblemCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);

        // 只剩積分榜引用時仍擋；全解除後可刪。
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/matches/{match.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/standings/{standing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}")).StatusCode);
    }

    // ═════════════════════════ 替補 came_on ═════════════════════════

    [Fact]
    public async Task 出賽名單_先發恆存false_有進球或牌的替補強制true_其餘照請求值()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var (starter, subGoal, subCard, subIdleOn, subIdleOff) = (ctx.Players[0], ctx.Players[1], ctx.Players[2], ctx.Players[3], Guid.Empty);
        // 只有四位現役球員時，subIdleOff 用 starter 之外的第五位；不足就略過該列（下面以 Players.Count 判斷）。
        var lineups = new List<AdminMatchLineupInput>
        {
            new() { PlayerId = starter, IsStarter = true, CameOn = true },        // 先發：請求值被忽略 → false
            new() { PlayerId = subGoal, IsStarter = false, CameOn = false },     // 替補有進球 → 強制 true
            new() { PlayerId = subCard, IsStarter = false, CameOn = false },      // 替補有牌 → 強制 true
            new() { PlayerId = subIdleOn, IsStarter = false, CameOn = true },     // 替補沒事件但請求有上場 → true
        };
        var request = NewMatch(ctx, 7) with
        {
            Goals = [new AdminMatchGoalInput { PlayerId = subGoal, Minute = 70 }],
            Cards = [new AdminMatchCardInput { PlayerId = subCard, CardType = "yellow", Minute = 80 }],
            Lineups = lineups,
        };
        var match = await CreateMatchAsync(client, request);
        CameOnOf(match, starter, false);
        CameOnOf(match, subGoal, true);
        CameOnOf(match, subCard, true);
        CameOnOf(match, subIdleOn, true);

        // 沒上場的替補照請求值 false。
        var update = new UpdateAdminMatchRequest
        {
            SeasonId = ctx.SeasonId, TeamIds = [ctx.TeamId], MatchOn = match.MatchOn, Opponent = "測試對手", Status = "played",
            Lineups = [new() { PlayerId = subIdleOn, IsStarter = false, CameOn = false }],
        };
        var idle = await BizTest.ReadAsync<AdminMatchDetailDto>(await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{match.Id}", update));
        CameOnOf(idle, subIdleOn, false);

        // 只更新進球（名單省略＝維持）：新進球的替補要被補成 true。
        var goalsOnly = new UpdateAdminMatchRequest
        {
            SeasonId = ctx.SeasonId, TeamIds = [ctx.TeamId], MatchOn = match.MatchOn, Opponent = "測試對手", Status = "played",
            Goals = [new AdminMatchGoalInput { PlayerId = subIdleOn, Minute = 88 }],
        };
        var forced = await BizTest.ReadAsync<AdminMatchDetailDto>(await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{match.Id}", goalsOnly));
        CameOnOf(forced, subIdleOn, true);

        // 烏龍球也算（人一定上場了）；先發即使被改成替補名單的 isStarter=true 仍存 false。
        var ownGoal = new UpdateAdminMatchRequest
        {
            SeasonId = ctx.SeasonId, TeamIds = [ctx.TeamId], MatchOn = match.MatchOn, Opponent = "測試對手", Status = "played",
            Goals = [new AdminMatchGoalInput { PlayerId = subGoal, GoalType = "own_goal" }],
            Lineups = [new() { PlayerId = subGoal, IsStarter = false }, new() { PlayerId = starter, IsStarter = true, CameOn = true }],
        };
        var own = await BizTest.ReadAsync<AdminMatchDetailDto>(await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{match.Id}", ownGoal));
        CameOnOf(own, subGoal, true);
        CameOnOf(own, starter, false);
        _ = subIdleOff;
    }

    private static void CameOnOf(AdminMatchDetailDto match, Guid playerId, bool expected)
        => Assert.Equal(expected, Assert.Single(match.Lineups, l => l.PlayerId == playerId).CameOn);

    // ═════════════════════════ 自動彙總新規則 ═════════════════════════

    [Fact]
    public async Task 球員數據自動彙總_出賽等於先發或came_on_替補有進球但沒上場旗標不算出賽_不在名單的進球仍計_烏龍球不算進球()
    {
        await using var ctx = await NewCtxAsync();
        var (a, b, c, d) = (ctx.Players[0], ctx.Players[1], ctx.Players[2], ctx.Players[3]);
        var m = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync("INSERT INTO matches (id, club_id, season_id, match_on, status, opponent) VALUES (@I, @C, @S, '2019-03-01', 'played', N'【TOT】直寫')", ("@I", m), ("@C", ctx.ClubId), ("@S", ctx.SeasonId));
        // A 先發；B 替補 came_on=1；C 替補 came_on=0 但有進球（舊規則會算出賽，新規則不算）；D 不在名單、有黃牌＋烏龍球。
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO match_lineups (match_id, player_id, is_starter, came_on) VALUES (@M, @A, 1, 0), (@M, @B, 0, 1), (@M, @C, 0, 0);
            INSERT INTO match_goals (match_id, player_id, minute) VALUES (@M, @C, 50);
            INSERT INTO match_goals (match_id, player_id, minute, goal_type) VALUES (@M, @D, 60, N'own_goal');
            INSERT INTO match_cards (match_id, player_id, card_type, minute) VALUES (@M, @D, N'yellow', 30);
            """, ("@M", m), ("@A", a), ("@B", b), ("@C", c), ("@D", d));
        using var pub = fixture.CreateClient();

        var stats = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/stats/players?season={ctx.SeasonCode}");
        var items = stats.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("playerId").GetGuid());
        Assert.Equal(1, items[a].GetProperty("appearances").GetInt32());
        Assert.Equal(1, items[b].GetProperty("appearances").GetInt32());
        Assert.Equal(0, items[c].GetProperty("appearances").GetInt32());   // 新規則：沒有 came_on 不算出賽
        Assert.Equal(1, items[c].GetProperty("goals").GetInt32());          // 進球照算
        Assert.Equal(0, items[d].GetProperty("appearances").GetInt32());   // 不在名單
        Assert.Equal(0, items[d].GetProperty("goals").GetInt32());          // 烏龍球不算該球員進球
        Assert.Equal(1, items[d].GetProperty("yellowCards").GetInt32());    // 牌照算
    }

    // ═════════════════════════ 公開賽程：列表篩選／排序 ═════════════════════════

    [Fact]
    public async Task 賽程列表_order_desc與status逗號多值_分頁穩定_非法order400()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        foreach (var (day, status) in new[] { (1, "played"), (2, "played"), (3, "played"), (4, "scheduled"), (5, "postponed"), (6, "cancelled") })
        {
            await CreateMatchAsync(client, NewMatch(ctx, day, status) with
            {
                OriginalMatchOn = status == "postponed" ? new DateOnly(2019, 3, 5) : null,
            });
        }

        using var pub = fixture.CreateClient();
        async Task<(int[] Days, int Total)> QueryAsync(string query)
        {
            var json = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule?season={ctx.SeasonCode}&{query}");
            var days = json.GetProperty("items").EnumerateArray().Select(i => DateOnly.Parse(i.GetProperty("matchOn").GetString()!).Day).ToArray();
            return (days, json.GetProperty("totalCount").GetInt32());
        }

        // 賽果：已結束、由新到舊，分頁。
        var (page1, total) = await QueryAsync("status=played&order=desc&pageSize=2&page=1");
        Assert.Equal(new[] { 3, 2 }, page1);
        Assert.Equal(3, total);
        Assert.Equal(new[] { 1 }, (await QueryAsync("status=played&order=desc&pageSize=2&page=2")).Days);

        // 預設仍是由舊到新（相容舊行為）。
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, (await QueryAsync("pageSize=20")).Days);
        Assert.Equal(new[] { 1, 2, 3 }, (await QueryAsync("status=played&order=asc")).Days);

        // 賽程：多狀態逗號分隔，由舊到新。
        Assert.Equal(new[] { 4, 5 }, (await QueryAsync("status=scheduled,postponed,live&order=asc")).Days);
        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, (await QueryAsync("order=DESC")).Days);

        var bad = await pub.GetAsync($"/api/v1/tcrfc/schedule?season={ctx.SeasonCode}&order=sideways");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    // ═════════════════════════ 公開賽程：列表欄位與單場詳情 ═════════════════════════

    [AzuriteFact]
    public async Task 賽程_對手球隊名稱與隊徽_場地庫名稱_單場詳情含進球牌與出賽名單_非現役球員不外洩()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var team = await CreateTeamAsync(client, "tcrfc", Unique("戊隊"), "Team E", withCrest: true);
        var (starter, sub, left, other) = (ctx.Players[0], ctx.Players[1], ctx.Players[2], ctx.Players[3]);
        var venueId = await BizTest.ScalarGuidAsync("SELECT TOP 1 v.id FROM venues v JOIN venues_i18n i ON i.venue_id = v.id AND i.locale = N'zh-Hant' WHERE i.name = N'西屯足球場'");
        var venueEn = await C1Test.ScalarAsync<string>("SELECT i.name FROM venues_i18n i WHERE i.venue_id = @V AND i.locale = N'en'", ("@V", venueId));

        var request = NewMatch(ctx, 9, opponentTeamId: team.Id) with
        {
            VenueId = venueId,
            HomeAway = "HOME",
            ScoreHome = 2,
            ScoreAway = 1,
            Goals =
            [
                new AdminMatchGoalInput { PlayerId = sub, Minute = 70, GoalType = "header" },
                new AdminMatchGoalInput { PlayerId = starter, Minute = 12 },
                new AdminMatchGoalInput { PlayerId = left, Minute = 30, GoalType = "own_goal" },
            ],
            Cards = [new AdminMatchCardInput { PlayerId = starter, CardType = "yellow", Minute = 55 }],
            Lineups =
            [
                new AdminMatchLineupInput { PlayerId = starter, IsStarter = true },
                new AdminMatchLineupInput { PlayerId = sub, IsStarter = false },
                new AdminMatchLineupInput { PlayerId = left, IsStarter = true },
                new AdminMatchLineupInput { PlayerId = other, IsStarter = false, CameOn = false },
            ],
        };
        var match = await CreateMatchAsync(client, request);
        var leftSlug = await C1Test.ScalarAsync<string>("SELECT slug FROM players WHERE id = @P", ("@P", left));
        // 讓 left 變成非現役（離隊）：公開詳情不得外洩其姓名／網址代稱，進球列仍保留。
        await BizTest.ExecuteSqlAsync("UPDATE players SET status = 'left' WHERE id = @P", ("@P", left));
        try
        {
            using var pub = fixture.CreateClient();

            // 列表欄位（zh）。
            var list = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule?season={ctx.SeasonCode}");
            var item = list.GetProperty("items")[0];
            Assert.Equal(team.Id, item.GetProperty("opponentTeamId").GetGuid());
            Assert.Equal(team.Name, item.GetProperty("opponentName").GetString());
            Assert.Equal(team.Name, item.GetProperty("opponent").GetString());
            var crest = item.GetProperty("opponentCrest");
            Assert.False(string.IsNullOrEmpty(crest.GetProperty("url").GetString()));
            Assert.False(string.IsNullOrEmpty(crest.GetProperty("url320").GetString()));
            Assert.True(crest.GetProperty("width").GetInt32() > 0);
            Assert.Equal("西屯足球場", item.GetProperty("venue").GetString()); // 場地庫名稱，不是空白
            Assert.False(item.GetProperty("isFallbackLocale").GetBoolean());

            // en：庫內英文名、場地英文名。
            var listEn = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule?season={ctx.SeasonCode}&lang=en");
            var itemEn = listEn.GetProperty("items")[0];
            Assert.Equal("Team E", itemEn.GetProperty("opponentName").GetString());
            Assert.Equal(venueEn, itemEn.GetProperty("venue").GetString());
            Assert.False(itemEn.GetProperty("isFallbackLocale").GetBoolean());

            // 沒挑球隊庫的賽事：opponentTeamId／opponentCrest 為 null，名稱退回文字。
            var plain = await CreateMatchAsync(client, NewMatch(ctx, 10, "scheduled", opponent: "純文字隊"));
            var plainJson = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{plain.Id}");
            Assert.Equal(JsonValueKind.Null, plainJson.GetProperty("opponentTeamId").ValueKind);
            Assert.Equal(JsonValueKind.Null, plainJson.GetProperty("opponentCrest").ValueKind);
            Assert.Equal("純文字隊", plainJson.GetProperty("opponentName").GetString());
            Assert.Equal(0, plainJson.GetProperty("goals").GetArrayLength());

            // 單場詳情。
            var detail = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{match.Id}");
            Assert.Equal(team.Name, detail.GetProperty("opponentName").GetString());
            Assert.Equal(2, detail.GetProperty("scoreHome").GetInt32());

            var goals = detail.GetProperty("goals").EnumerateArray().ToList();
            Assert.Equal(new[] { 12, 30, 70 }, goals.Select(g => g.GetProperty("minute").GetInt32()).ToArray()); // 依分鐘排序
            Assert.Equal(starter, goals[0].GetProperty("playerId").GetGuid());
            Assert.False(string.IsNullOrEmpty(goals[0].GetProperty("playerSlug").GetString()));
            Assert.False(string.IsNullOrEmpty(goals[0].GetProperty("playerName").GetString()));
            Assert.Equal("club", goals[0].GetProperty("side").GetString());
            Assert.Equal(JsonValueKind.Null, goals[0].GetProperty("goalType").ValueKind);
            Assert.Equal("header", goals[2].GetProperty("goalType").GetString());
            // 離隊球員：進球列保留（比分對得上），球員欄位為 null；烏龍球算對手。
            Assert.Equal("opponent", goals[1].GetProperty("side").GetString());
            Assert.Equal(JsonValueKind.Null, goals[1].GetProperty("playerId").ValueKind);
            Assert.Equal(JsonValueKind.Null, goals[1].GetProperty("playerName").ValueKind);
            Assert.Equal(JsonValueKind.Null, goals[1].GetProperty("playerSlug").ValueKind);

            var cards = detail.GetProperty("cards").EnumerateArray().ToList();
            Assert.Equal("yellow", Assert.Single(cards).GetProperty("cardType").GetString());

            var lineup = detail.GetProperty("lineup").EnumerateArray().ToList();
            Assert.DoesNotContain(lineup, l => l.GetProperty("playerId").GetGuid() == left); // 非現役整列略過
            Assert.Equal(3, lineup.Count);
            Assert.True(lineup[0].GetProperty("isStarter").GetBoolean());
            Assert.Equal(starter, lineup[0].GetProperty("playerId").GetGuid());
            var subRow = lineup.Single(l => l.GetProperty("playerId").GetGuid() == sub);
            Assert.True(subRow.GetProperty("cameOn").GetBoolean()); // 有進球 → 強制 true
            Assert.False(lineup.Single(l => l.GetProperty("playerId").GetGuid() == other).GetProperty("cameOn").GetBoolean());
            // 只輸出公開安全欄位。
            var allowed = new[] { "playerId", "playerSlug", "playerName", "shirtNumber", "position", "isStarter", "cameOn" };
            Assert.All(lineup, l => Assert.Empty(l.EnumerateObject().Select(p => p.Name).Except(allowed)));
            var body = detail.GetRawText();
            Assert.DoesNotContain(leftSlug!, body);

            // 英文請求：球員姓名走英文回退規則，不丟例外。
            var detailEn = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{match.Id}?lang=en");
            Assert.Equal("Team E", detailEn.GetProperty("opponentName").GetString());

            // 別的俱樂部看不到（硬過濾）。
            Assert.Equal(HttpStatusCode.NotFound, (await pub.GetAsync($"/api/v1/bw/schedule/{match.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await pub.GetAsync($"/api/v1/tcrfc/schedule/{Guid.NewGuid()}")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE players SET status = 'active' WHERE id = @P AND status = 'left'", ("@P", left));
            await BizTest.ExecuteSqlAsync("DELETE FROM matches WHERE season_id = @S", ("@S", ctx.SeasonId));
            await client.DeleteAsync($"/api/v1/admin/tcrfc/opponent-teams/{team.Id}"); // 順便清隊徽物件
        }
    }

    // ═════════════════════════ 賽後報導（matchReport） ═════════════════════════

    [Fact]
    public async Task 單場詳情_賽後報導_取最新已發布的比賽報導分類文章_草稿與其他分類不算_語系回退()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var match = await CreateMatchAsync(client, NewMatch(ctx, 12));
        var tag = Guid.NewGuid().ToString("N")[..8];
        async Task<string> AddArticleAsync(string category, string status, int daysAgo, string titleZh, string? titleEn)
        {
            var id = Guid.NewGuid();
            var slug = $"tot-{tag}-{Guid.NewGuid().ToString("N")[..6]}";
            await BizTest.ExecuteSqlAsync(
                """
                INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
                VALUES (@I, @C, @S, (SELECT id FROM article_categories WHERE code = @Cat), @St, DATEADD(day, -@D, SYSUTCDATETIME()));
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@I, N'zh-Hant', @Tz);
                INSERT INTO article_relations (article_id, target_type, target_id) VALUES (@I, N'match', @M);
                """, ("@I", id), ("@C", ctx.ClubId), ("@S", slug), ("@Cat", category), ("@St", status), ("@D", daysAgo), ("@Tz", titleZh), ("@M", match.Id));
            if (titleEn is not null)
            {
                await BizTest.ExecuteSqlAsync("INSERT INTO articles_i18n (article_id, locale, title) VALUES (@I, N'en', @T)", ("@I", id), ("@T", titleEn));
            }

            return slug;
        }

        using var pub = fixture.CreateClient();
        try
        {
            var none = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{match.Id}");
            Assert.Equal(JsonValueKind.Null, none.GetProperty("matchReport").ValueKind);

            await AddArticleAsync("match", "published", 5, "舊報導", null);
            var newest = await AddArticleAsync("match", "published", 2, "新報導", "New report");
            await AddArticleAsync("match", "draft", 0, "草稿報導", null);          // 草稿不算
            await AddArticleAsync("club", "published", 1, "不是比賽報導", null); // 分類不對不算

            var zh = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{match.Id}");
            Assert.Equal(newest, zh.GetProperty("matchReport").GetProperty("slug").GetString());
            Assert.Equal("新報導", zh.GetProperty("matchReport").GetProperty("title").GetString());
            var en = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{match.Id}?lang=en");
            Assert.Equal("New report", en.GetProperty("matchReport").GetProperty("title").GetString());
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM articles WHERE slug LIKE @P", ("@P", $"tot-{tag}-%")); // i18n／關聯 FK 連帶清除
        }
    }

    // ═════════════════════════ 列表：scorers／matchReport（批次） ═════════════════════════

    [Fact]
    public async Task 賽程列表_scorers只在已結束賽事填_排除烏龍球_非現役姓名為null_matchReport與詳情一致()
    {
        await using var ctx = await NewCtxAsync();
        using var client = await ManagerAsync();
        var (a, b, left, d) = (ctx.Players[0], ctx.Players[1], ctx.Players[2], ctx.Players[3]);
        var played = await CreateMatchAsync(client, NewMatch(ctx, 1) with
        {
            Goals =
            [
                new AdminMatchGoalInput { PlayerId = b, Minute = 70, GoalType = "header" },
                new AdminMatchGoalInput { PlayerId = a, Minute = 12 },
                new AdminMatchGoalInput { PlayerId = d, Minute = 40, GoalType = "own_goal" },
                new AdminMatchGoalInput { PlayerId = left, Minute = 55 },
            ],
        });
        var played2 = await CreateMatchAsync(client, NewMatch(ctx, 2) with { Goals = [new AdminMatchGoalInput { PlayerId = a, Minute = 5 }] });
        var scheduled = await CreateMatchAsync(client, NewMatch(ctx, 3, "scheduled") with { Goals = [new AdminMatchGoalInput { PlayerId = a, Minute = 9 }] });
        var tag = Guid.NewGuid().ToString("N")[..8];
        var slug = $"tot-{tag}-r";
        var articleId = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
            VALUES (@I, @C, @S, (SELECT id FROM article_categories WHERE code = N'match'), 'published', SYSUTCDATETIME());
            INSERT INTO articles_i18n (article_id, locale, title) VALUES (@I, N'zh-Hant', N'列表報導');
            INSERT INTO article_relations (article_id, target_type, target_id) VALUES (@I, N'match', @M);
            """, ("@I", articleId), ("@C", ctx.ClubId), ("@S", slug), ("@M", played.Id));
        await BizTest.ExecuteSqlAsync("UPDATE players SET status = 'left' WHERE id = @P", ("@P", left));
        try
        {
            using var pub = fixture.CreateClient();
            var list = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule?season={ctx.SeasonCode}");
            var items = list.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid());

            var scorers = items[played.Id].GetProperty("scorers").EnumerateArray().ToList();
            Assert.Equal(new[] { 12, 55, 70 }, scorers.Select(x => x.GetProperty("minute").GetInt32()).ToArray()); // 烏龍球（40）排除，依分鐘
            Assert.False(string.IsNullOrEmpty(scorers[0].GetProperty("playerName").GetString()));
            Assert.Equal(JsonValueKind.Null, scorers[0].GetProperty("goalType").ValueKind);
            Assert.Equal(JsonValueKind.Null, scorers[1].GetProperty("playerName").ValueKind); // 非現役：進球保留、姓名 null
            Assert.Equal("header", scorers[2].GetProperty("goalType").GetString());
            Assert.Single(items[played2.Id].GetProperty("scorers").EnumerateArray());
            Assert.Equal(0, items[scheduled.Id].GetProperty("scorers").GetArrayLength()); // 非已結束不填

            Assert.Equal(slug, items[played.Id].GetProperty("matchReport").GetProperty("slug").GetString());
            Assert.Equal("列表報導", items[played.Id].GetProperty("matchReport").GetProperty("title").GetString());
            Assert.Equal(JsonValueKind.Null, items[played2.Id].GetProperty("matchReport").ValueKind);

            // 詳情同規則。
            var detail = await pub.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/schedule/{played.Id}");
            Assert.Equal(slug, detail.GetProperty("matchReport").GetProperty("slug").GetString());
            Assert.Equal(3, detail.GetProperty("scorers").GetArrayLength());
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE players SET status = 'active' WHERE id = @P AND status = 'left'", ("@P", left));
            await BizTest.ExecuteSqlAsync("DELETE FROM articles WHERE slug = @S", ("@S", slug));
        }
    }
}
