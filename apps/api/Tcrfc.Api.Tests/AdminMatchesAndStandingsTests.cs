using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminStandings;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-8：C4（賽程與賽果／積分榜）後台 CRUD ＋ CSV 批次匯入 ＋ **列級授權強制**
/// （<c>role_permissions.scope_type</c> 的 <c>academy_only</c>，
/// <c>Security/TeamRowScope.cs</c>）。打真正的 HTTP 管線與真正的 <c>tcrfc_club</c>，
/// 形狀比照 <c>AdminTeamsPlayersStaffTests</c>／<c>AdminClubsAndCompetitionsTests</c>。
///
/// 種子資料現況（見 apps/api/README.md「種子測試帳號」）：<c>tcrfc</c> 只有 <c>D1</c>
/// （<c>first_team</c>）一支球隊，沒有學院梯隊；<c>bw</c> 有 <c>BW1</c>（<c>first_team</c>）／
/// <c>BW-U15</c>／<c>BW-U12</c>（<c>academy</c>）。**測 <c>academy_only</c> 因此借用 <c>bw</c>
/// 俱樂部**（<c>academy.manager@tcrfc.test</c>，<c>academy_program</c> 角色，只被授權 <c>bw</c>）
/// ——tcrfc 目前沒有 academy 球隊可供測試。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminMatchesAndStandingsTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════════ 基本授權：未登入／跨俱樂部／唯讀角色 ═════════════════════════════

    [Fact]
    public async Task Match_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/matches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Match_跨俱樂部_擋下()
    {
        // team.manager@tcrfc.test（team_competition，all_clubs 角色）只被授權 tcrfc，沒有 bw。
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/bw/matches");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Match_檢視者角色只有唯讀_建立會被擋下()
    {
        using var client = await CreateClientAsync("viewer@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/matches");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");
        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]));
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    // ═════════════════════════════ CRUD、值域驗證、場次編號唯一 ═════════════════════════════

    [Fact]
    public async Task Match_建立成功_可取得_可更新_可刪除()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
        Assert.NotNull(created);

        try
        {
            var getResponse = await client.GetAsync($"/api/v1/admin/tcrfc/matches/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
            Assert.Equal("測試對手", fetched!.Opponent);
            Assert.Equal([teamId], fetched.TeamIds);

            var updateRequest = new UpdateAdminMatchRequest
            {
                SeasonId = seasonId,
                TeamIds = [teamId],
                MatchOn = created.MatchOn,
                Opponent = "測試對手（更新後）",
                Status = "played",
                ScoreHome = 3,
                ScoreAway = 1,
            };
            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/matches/{created.Id}", updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
            Assert.Equal("測試對手（更新後）", updated!.Opponent);
            Assert.Equal("played", updated.Status);
            Assert.Equal(3, updated.ScoreHome);
        }
        finally
        {
            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/matches/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        // 硬刪除——刪除後再取得應為 404。
        var afterDelete = await client.GetAsync($"/api/v1/admin/tcrfc/matches/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Match_狀態值域錯誤回400_對手為必填()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        var badStatus = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]) with { Status = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

        var emptyOpponent = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]) with { Opponent = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyOpponent.StatusCode);

        var noTeams = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, []));
        Assert.Equal(HttpStatusCode.BadRequest, noTeams.StatusCode);
    }

    /// <summary>v3.14：「取消」補進五值。跟「延賽」語意不同——取消不受
    /// <c>ValidatePostponedFields</c> 的原定時間規則約束（既不必填也不能填原定日期），
    /// 逐字比照「非延賽」分支的既有行為。</summary>
    [Fact]
    public async Task Match_狀態為取消_建立成功_不受原定日期規則約束()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        var created = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]) with { Status = "cancelled" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var match = await created.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
        try
        {
            Assert.Equal("cancelled", match!.Status);
            Assert.Null(match.OriginalMatchOn);

            // 取消狀態不能填原定日期（跟「非延賽」分支同一種擋法）。
            var cancelledWithOriginal = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/matches",
                NewMatchRequest(seasonId, [teamId]) with { Status = "cancelled", OriginalMatchOn = new DateOnly(2026, 10, 1) });
            Assert.Equal(HttpStatusCode.BadRequest, cancelledWithOriginal.StatusCode);
        }
        finally
        {
            await DeleteMatchByIdAsync(match!.Id);
        }
    }

    [Fact]
    public async Task Match_延賽須填原定日期_非延賽不能填原定日期()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");

        // 延賽卻沒填原定日期 → 400。
        var postponedWithoutOriginal = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]) with { Status = "postponed" });
        Assert.Equal(HttpStatusCode.BadRequest, postponedWithoutOriginal.StatusCode);

        // 非延賽卻填了原定日期 → 400。
        var scheduledWithOriginal = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/matches",
            NewMatchRequest(seasonId, [teamId]) with { Status = "scheduled", OriginalMatchOn = new DateOnly(2026, 10, 1) });
        Assert.Equal(HttpStatusCode.BadRequest, scheduledWithOriginal.StatusCode);

        // 延賽且填了原定日期 → 成功。
        var validPostponed = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/matches",
            NewMatchRequest(seasonId, [teamId]) with { Status = "postponed", OriginalMatchOn = new DateOnly(2026, 10, 1), OriginalKickoff = "14:00" });
        Assert.Equal(HttpStatusCode.Created, validPostponed.StatusCode);
        var created = await validPostponed.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
        try
        {
            Assert.Equal(new DateOnly(2026, 10, 1), created!.OriginalMatchOn);
            Assert.Equal("14:00", created.OriginalKickoff);
        }
        finally
        {
            await DeleteMatchByIdAsync(created!.Id);
        }
    }

    [Fact]
    public async Task Match_場次編號同季同聯賽唯一_不同聯賽不衝突()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var teamId = await GetTeamIdAsync("tcrfc", "D1");
        var matchNo = Random.Shared.Next(90000, 99999);

        var first = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/matches", NewMatchRequest(seasonId, [teamId]) with { MatchNo = matchNo });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstCreated = await first.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);

        try
        {
            // 同季、同樣沒有掛賽事系列（competitionId 皆為 null）、同場次編號 → 409。
            var conflict = await client.PostAsJsonAsync(
                "/api/v1/admin/tcrfc/matches",
                NewMatchRequest(seasonId, [teamId]) with { MatchNo = matchNo, MatchOn = firstCreated!.MatchOn.AddDays(7) });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

            // 更新自己（排除自己）不應該誤判成衝突。
            var updateSelf = await client.PutAsJsonAsync(
                $"/api/v1/admin/tcrfc/matches/{firstCreated.Id}",
                new UpdateAdminMatchRequest
                {
                    SeasonId = seasonId, TeamIds = [teamId], MatchOn = firstCreated.MatchOn,
                    Opponent = firstCreated.Opponent!, Status = "scheduled", MatchNo = matchNo,
                });
            Assert.Equal(HttpStatusCode.OK, updateSelf.StatusCode);
        }
        finally
        {
            await DeleteMatchByIdAsync(firstCreated!.Id);
        }
    }

    // ═════════════════════════════ 🔴 列級授權：academy_only ═════════════════════════════

    [Fact]
    public async Task 列級授權_academy_only_只能碰學院梯隊_一線隊擋下()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test"); // academy_program，僅授權 bw
        var seasonId = await GetSeasonIdAsync("bw", "2023");
        var academyTeamId = await GetTeamIdAsync("bw", "BW-U15");
        var firstTeamId = await GetTeamIdAsync("bw", "BW1");

        // 學院梯隊（academy）→ 成功。
        var academyResponse = await client.PostAsJsonAsync("/api/v1/admin/bw/matches", NewMatchRequest(seasonId, [academyTeamId]));
        Assert.Equal(HttpStatusCode.Created, academyResponse.StatusCode);
        var academyMatch = await academyResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);

        // 一線隊（first_team，即使同一個俱樂部）→ 403，不是 400／404。
        var firstTeamResponse = await client.PostAsJsonAsync("/api/v1/admin/bw/matches", NewMatchRequest(seasonId, [firstTeamId]));
        Assert.Equal(HttpStatusCode.Forbidden, firstTeamResponse.StatusCode);

        try
        {
            // 跨梯隊友誼賽同時掛學院＋一線隊 → 整筆擋下（不是「挑得到一支允許的就放行」）。
            var mixedResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/bw/matches", NewMatchRequest(seasonId, [academyTeamId, firstTeamId]));
            Assert.Equal(HttpStatusCode.Forbidden, mixedResponse.StatusCode);

            // 用超管確認：把既有學院賽事改指派到一線隊 → 403（防止繞過範圍限制逃脫）。
            var escapeAttempt = await client.PutAsJsonAsync(
                $"/api/v1/admin/bw/matches/{academyMatch!.Id}",
                new UpdateAdminMatchRequest
                {
                    SeasonId = seasonId, TeamIds = [firstTeamId], MatchOn = academyMatch.MatchOn,
                    Opponent = academyMatch.Opponent!, Status = "scheduled",
                });
            Assert.Equal(HttpStatusCode.Forbidden, escapeAttempt.StatusCode);

            // 刪除既有學院賽事（範圍內）→ 成功。
            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/bw/matches/{academyMatch.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            academyMatch = null;
        }
        finally
        {
            if (academyMatch is not null)
            {
                await DeleteMatchByIdAsync(academyMatch.Id);
            }
        }
    }

    [Fact]
    public async Task 列級授權_academy_only_無法碰一線隊既有賽事()
    {
        // 用系統管理員先建一筆一線隊賽事，再用 academy_program 帳號嘗試修改／刪除，應一律 403。
        using var superAdmin = await CreateClientAsync("super.admin@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("bw", "2023");
        var firstTeamId = await GetTeamIdAsync("bw", "BW1");

        var createResponse = await superAdmin.PostAsJsonAsync("/api/v1/admin/bw/matches", NewMatchRequest(seasonId, [firstTeamId]));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);

        try
        {
            using var academyClient = await CreateClientAsync("academy.manager@tcrfc.test");

            var updateAttempt = await academyClient.PutAsJsonAsync(
                $"/api/v1/admin/bw/matches/{created!.Id}",
                new UpdateAdminMatchRequest
                {
                    SeasonId = seasonId, TeamIds = [firstTeamId], MatchOn = created.MatchOn,
                    Opponent = "攔截測試", Status = "scheduled",
                });
            Assert.Equal(HttpStatusCode.Forbidden, updateAttempt.StatusCode);

            var deleteAttempt = await academyClient.DeleteAsync($"/api/v1/admin/bw/matches/{created.Id}");
            Assert.Equal(HttpStatusCode.Forbidden, deleteAttempt.StatusCode);
        }
        finally
        {
            await DeleteMatchByIdAsync(created!.Id);
        }
    }

    // ═════════════════════════════ CSV 批次匯入：賽程 ═════════════════════════════

    [Fact]
    public async Task Match_CSV匯入_成功案例()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var matchNoA = Random.Shared.Next(80000, 89999);
        var csv = BuildMatchCsv(
            ("D1", "2026-27", "enterprise-a", "2026-11-01", "19:00", "主場", "CSV測試對手A", "", "測試場地", "", "聯賽", matchNoA.ToString(), "1", "未開始"),
            ("D1", "2026-27", "", "2026-11-08", "", "", "CSV測試對手B", "CSV Opponent B", "", "", "", "", "", "已結束"),
            // v3.14：「取消」CSV 匯入（AdminMatchesRepository.StatusZhLabels）。
            ("D1", "2026-27", "", "2026-11-22", "", "", "CSV測試對手C", "", "", "", "", "", "", "取消"));

        var response = await PostCsvAsync(client, "/api/v1/admin/tcrfc/matches/import", csv);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MatchCsvImportResultDto>(TestJson.Options);
        Assert.Equal(3, result!.ImportedCount);
        Assert.Empty(result.Errors);

        try
        {
            var list = await client.GetFromJsonAsync<List<AdminMatchListItemDto>>(
                $"/api/v1/admin/tcrfc/matches?seasonId={await GetSeasonIdAsync("tcrfc", "2026-27")}", TestJson.Options);
            Assert.Contains(list!, m => m.Opponent == "CSV測試對手A" && m.MatchNo == matchNoA);
            Assert.Contains(list!, m => m.Opponent == "CSV測試對手B" && m.Status == "played");
            Assert.Contains(list!, m => m.Opponent == "CSV測試對手C" && m.Status == "cancelled");
        }
        finally
        {
            await DeleteMatchesByOpponentAsync("CSV測試對手A", "CSV測試對手B", "CSV測試對手C");
        }
    }

    [Fact]
    public async Task Match_CSV匯入_整批驗證_任一列有錯就整批不寫入()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var csv = BuildMatchCsv(
            ("D1", "2026-27", "", "2026-11-15", "", "", "CSV驗證對手A", "", "", "", "", "", "", "未開始"),
            ("D1", "2026-27", "", "not-a-date", "", "", "CSV驗證對手B", "", "", "", "", "", "", "未開始")); // 日期格式錯誤

        var response = await PostCsvAsync(client, "/api/v1/admin/tcrfc/matches/import", csv);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MatchCsvImportResultDto>(TestJson.Options);
        Assert.Equal(0, result!.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].RowNumber); // 表頭第 1 行，第一筆資料第 2 行，第二筆第 3 行

        // 確認第一列（本身合法）也沒有被單獨寫入——整批不寫入。
        var list = await client.GetFromJsonAsync<List<AdminMatchListItemDto>>("/api/v1/admin/tcrfc/matches", TestJson.Options);
        Assert.DoesNotContain(list!, m => m.Opponent == "CSV驗證對手A");
    }

    [Fact]
    public async Task Match_CSV匯入_場次編號檔案內重複與跨球隊列級授權()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var dupMatchNo = Random.Shared.Next(70000, 79999);
        var csv = BuildMatchCsv(
            ("D1", "2026-27", "enterprise-a", "2026-11-20", "", "", "CSV重複編號A", "", "", "", "", dupMatchNo.ToString(), "", "未開始"),
            ("D1", "2026-27", "enterprise-a", "2026-11-21", "", "", "CSV重複編號B", "", "", "", "", dupMatchNo.ToString(), "", "未開始"));

        var response = await PostCsvAsync(client, "/api/v1/admin/tcrfc/matches/import", csv);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MatchCsvImportResultDto>(TestJson.Options);
        Assert.Equal(0, result!.ImportedCount);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Match_CSV匯入_academy_only帳號碰一線隊球隊代號_回報列級授權錯誤()
    {
        using var client = await CreateClientAsync("academy.manager@tcrfc.test"); // 僅授權 bw、academy_only
        var csv = BuildMatchCsv(
            ("BW1", "2023", "mulan", "2026-12-01", "", "", "CSV學院授權測試", "", "", "", "", "", "", "未開始"));

        var response = await PostCsvAsync(client, "/api/v1/admin/bw/matches/import", csv);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MatchCsvImportResultDto>(TestJson.Options);
        Assert.Equal(0, result!.ImportedCount);
        Assert.Contains(result.Errors, e => e.Reason.Contains("資料範圍"));
    }

    // ═════════════════════════════ 積分榜 CRUD ＋ CSV（整季替換） ═════════════════════════════

    [Fact]
    public async Task Standing_建立成功_可更新_可刪除()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest
        {
            SeasonId = seasonId, TeamName = "測試積分榜球隊", TeamNameEn = "Test Standings Team", Rank = 1, Played = 10, Points = 25,
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminStandingDetailDto>(TestJson.Options);
        Assert.Equal("測試積分榜球隊", created!.TeamName);
        Assert.Equal("Test Standings Team", created.TeamNameEn);

        try
        {
            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/standings/{created!.Id}", new UpdateAdminStandingRequest
            {
                SeasonId = seasonId, TeamName = "測試積分榜球隊", Rank = 2, Played = 11, Points = 26,
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminStandingDetailDto>(TestJson.Options);
            Assert.Equal(2, updated!.Rank);
            Assert.Null(updated.TeamNameEn); // 省略英文名稱＝清除英文列，前台回退繁中

            var tooLong = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest
            {
                SeasonId = seasonId, TeamName = "x", TeamNameEn = new string('a', 129),
            });
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            Assert.Contains("teamNameEn", await tooLong.Content.ReadAsStringAsync());
        }
        finally
        {
            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/standings/{created!.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Standing_CSV匯入_整季替換()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");

        // 🔴 種子資料（db/seed/backoffice_seed.py 第 29 節）在這個球季本來就有一批積分榜列；「整季替換」
        // 會把它們一併換掉，所以先記下既有列，斷言刪除筆數時要算進去，結束後再還原，不能讓測試把種子吃掉。
        var seededRows = await client.GetFromJsonAsync<List<AdminStandingListItemDto>>(
            $"/api/v1/admin/tcrfc/standings?seasonId={seasonId}", TestJson.Options);

        // 先種一筆舊資料，確認匯入後會被整批換掉。
        var staleResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest
        {
            SeasonId = seasonId, TeamName = "應該被替換掉的舊資料", Rank = 9, Played = 1, Points = 0,
        });
        Assert.Equal(HttpStatusCode.Created, staleResponse.StatusCode);

        try
        {
            var csv = CsvUtils.BuildCsv(
            [
                ["賽季代碼", "名次", "球隊名稱", "出賽場次", "積分"],
                ["2026-27", "1", "積分榜CSV測試A", "10", "28"],
                ["2026-27", "2", "積分榜CSV測試B", "10", "25"],
            ]);

            var response = await PostCsvAsync(client, "/api/v1/admin/tcrfc/standings/import", csv);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<StandingCsvImportResultDto>(TestJson.Options);
            Assert.Equal(2, result!.ReplacedCount);
            Assert.Equal(seededRows!.Count + 1, result.DeletedCount); // 種子列（若有）＋剛建立的舊資料
            Assert.Empty(result.Errors);

            var list = await client.GetFromJsonAsync<List<AdminStandingListItemDto>>(
                $"/api/v1/admin/tcrfc/standings?seasonId={seasonId}", TestJson.Options);
            Assert.DoesNotContain(list!, s => s.TeamName == "應該被替換掉的舊資料");
            Assert.Contains(list!, s => s.TeamName == "積分榜CSV測試A");
            Assert.Contains(list!, s => s.TeamName == "積分榜CSV測試B");
            Assert.All(list!, s => Assert.Null(s.TeamNameEn)); // 五欄檔案沒有英文欄＝英文名稱留空

            // 可選的第六欄「球隊名稱（英文）」寫入 standings_i18n(en)。
            var csvEn = CsvUtils.BuildCsv(
            [
                ["賽季代碼", "名次", "球隊名稱", "出賽場次", "積分", "球隊名稱（英文）"],
                ["2026-27", "1", "積分榜CSV測試A", "10", "28", "CSV Test A"],
                ["2026-27", "2", "積分榜CSV測試B", "10", "25", ""],
            ]);
            var responseEn = await PostCsvAsync(client, "/api/v1/admin/tcrfc/standings/import", csvEn);
            Assert.Equal(HttpStatusCode.OK, responseEn.StatusCode);
            var listEn = await client.GetFromJsonAsync<List<AdminStandingListItemDto>>(
                $"/api/v1/admin/tcrfc/standings?seasonId={seasonId}", TestJson.Options);
            Assert.Equal("CSV Test A", listEn!.Single(s => s.TeamName == "積分榜CSV測試A").TeamNameEn);
            Assert.Null(listEn!.Single(s => s.TeamName == "積分榜CSV測試B").TeamNameEn);
        }
        finally
        {
            await DeleteStandingsBySeasonAsync(seasonId, "應該被替換掉的舊資料", "積分榜CSV測試A", "積分榜CSV測試B");

            // 還原種子列：整季替換可能已經把它們刪掉；只補目前不存在的（測試中途失敗、尚未匯入時它們仍在）。
            var currentRows = await client.GetFromJsonAsync<List<AdminStandingListItemDto>>(
                $"/api/v1/admin/tcrfc/standings?seasonId={seasonId}", TestJson.Options);
            foreach (var seeded in seededRows!.Where(r => currentRows!.All(c => c.TeamName != r.TeamName)))
            {
                var restoreResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/standings", new CreateAdminStandingRequest
                {
                    SeasonId = seasonId, TeamName = seeded.TeamName, Rank = seeded.Rank, Played = seeded.Played, Points = seeded.Points,
                });
                Assert.Equal(HttpStatusCode.Created, restoreResponse.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Standing_CSV匯入_混雜不同賽季代碼_回400()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var csv = CsvUtils.BuildCsv(
        [
            ["賽季代碼", "名次", "球隊名稱", "出賽場次", "積分"],
            ["2026-27", "1", "混雜測試A", "1", "3"],
            ["not-a-real-season", "2", "混雜測試B", "1", "0"],
        ]);

        var response = await PostCsvAsync(client, "/api/v1/admin/tcrfc/standings/import", csv);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═════════════════════════════ 公開端點向後相容 ═════════════════════════════

    [Fact]
    public async Task 公開賽程端點不受影響_既有team與season篩選仍正常()
    {
        using var client = fixture.CreateClient();
        var result = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
            "/api/v1/tcrfc/schedule?team=D1&season=2026-27&pageSize=5", TestJson.Options);
        Assert.NotNull(result);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static CreateAdminMatchRequest NewMatchRequest(Guid seasonId, IReadOnlyList<Guid> teamIds) => new()
    {
        SeasonId = seasonId,
        TeamIds = teamIds,
        MatchOn = new DateOnly(2026, DateTime.UtcNow.Month == 12 ? 1 : DateTime.UtcNow.Month + 1, 15),
        Opponent = "測試對手",
        Status = "scheduled",
    };

    private static string BuildMatchCsv(params (string TeamCodes, string SeasonCode, string CompetitionCode, string Date,
        string Kickoff, string HomeAway, string Opponent, string OpponentEn, string Venue, string VenueEn,
        string CompetitionTag, string MatchNo, string RoundNo, string Status)[] rows)
    {
        List<IEnumerable<string?>> lines =
        [
            ["所屬球隊", "賽季代碼", "賽事系列代碼", "日期", "時間", "主客場", "對手", "對手英文", "場地", "場地英文", "賽事類型", "場次編號", "輪次", "狀態"],
        ];
        lines.AddRange(rows.Select(r => (IEnumerable<string?>)
        [
            r.TeamCodes, r.SeasonCode, r.CompetitionCode, r.Date, r.Kickoff, r.HomeAway, r.Opponent, r.OpponentEn,
            r.Venue, r.VenueEn, r.CompetitionTag, r.MatchNo, r.RoundNo, r.Status,
        ]));
        return CsvUtils.BuildCsv(lines);
    }

    private static async Task<HttpResponseMessage> PostCsvAsync(HttpClient client, string path, string csvText)
    {
        using var content = new StringContent(csvText, Encoding.UTF8, "text/csv");
        return await client.PostAsync(path, content);
    }

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetSeasonIdAsync(string clubCode, string seasonCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id
            WHERE c.code = @ClubCode AND s.code = @SeasonCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@SeasonCode", seasonCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetTeamIdAsync(string clubCode, string teamCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id
            WHERE c.code = @ClubCode AND t.code = @TeamCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@TeamCode", teamCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>硬刪除——<c>match_teams</c>／<c>matches_i18n</c>／<c>match_goals</c>／
    /// <c>match_cards</c>／<c>match_lineups</c> 皆為 <c>ON DELETE CASCADE</c>（docs/12b §11.3），
    /// 只需要刪主表列。</summary>
    private static async Task DeleteMatchByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM matches WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteMatchesByOpponentAsync(params string[] opponents)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM matches WHERE opponent IN (SELECT value FROM STRING_SPLIT(@Opponents, '|'));";
        command.Parameters.AddWithValue("@Opponents", string.Join('|', opponents));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteStandingsBySeasonAsync(Guid seasonId, params string[] teamNames)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM standings WHERE season_id = @SeasonId AND id IN (SELECT standing_id FROM standings_i18n WHERE locale = N'zh-Hant' AND team_name IN (SELECT value FROM STRING_SPLIT(@Names, '|')));";
        command.Parameters.AddWithValue("@SeasonId", seasonId);
        command.Parameters.AddWithValue("@Names", string.Join('|', teamNames));
        await command.ExecuteNonQueryAsync();
    }
}
