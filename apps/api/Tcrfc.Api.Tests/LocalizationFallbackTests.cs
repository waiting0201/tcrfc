using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Clubs;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Features.Staff;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 語系逐欄位回退——對應 apps/api/README.md「英文缺漏時的回退行為」段落已用 curl 驗證過的規則：
/// 請求語系非空白用它，否則用 zh-Hant，兩者皆無回傳 null；**逐欄位判斷，不是整筆記錄二選一**。
/// 這裡刻意不硬編碼種子資料的確切姓名／職稱字串（種子資料之後可能重灌／改版），
/// 只驗證「規則本身」：用 Unicode 範圍判斷是中文還是英文，而不是比對特定字串。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LocalizationFallbackTests(ApiFixture fixture)
{
    [Fact]
    public async Task 俱樂部名稱_兩家都有英文名_lang為en時取英文()
    {
        using var client = fixture.CreateClient();

        var clubs = await client.GetFromJsonAsync<List<ClubDto>>("/api/v1/clubs?lang=en", TestJson.Options);
        Assert.NotNull(clubs);

        var tcrfc = clubs!.Single(c => c.Code == "tcrfc");
        var bw = clubs.Single(c => c.Code == "bw");

        // tcrfc 兩個語系都有英文名，?lang=en 應該拿到英文（README 驗收紀錄：Taichung Rock FC）。
        Assert.False(ContainsCjk(tcrfc.Name), $"tcrfc 在 lang=en 應為英文名，實際是「{tcrfc.Name}」");

        // bw（台中藍鯨）：B-5 已於 2026-10-05 定案（全名 Taichung Blue Whale Women's Football Club），種子帶入英文列，
        // ?lang=en 取得英文名。俱樂部英文列缺失時的回退行為由 AppContractBatch4Tests 自建測資驗證（經後台移除英文列再還原）。
        Assert.False(ContainsCjk(bw.Name), $"bw 在 lang=en 應為英文名（B-5 已定案），實際是「{bw.Name}」");
        Assert.Equal("Taichung Blue Whale Women's Football Club", bw.Name);
    }

    [Fact]
    public async Task 教練職稱完全沒有英文列_lang為en時全部回退中文_姓名仍逐筆各自判斷()
    {
        // 規則本身才是被測行為，不依賴種子缺英文（種子已補英文列）：自建兩位教練——
        // A 只有 zh-Hant 列；B 有 zh-Hant 列＋只含 name 的 en 列（title 沒有英文）。
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var tag = BizTest.Unique("lfs");
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        try
        {
            await BizTest.ExecuteSqlAsync("INSERT INTO staff (id, club_id) VALUES (@A, @C), (@B, @C)", ("@A", idA), ("@B", idB), ("@C", clubId));
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@A, N'zh-Hant', @NA, N'測試教練職稱甲'), (@B, N'zh-Hant', @NB, N'測試教練職稱乙')",
                ("@A", idA), ("@B", idB), ("@NA", $"測試{tag}甲"), ("@NB", $"測試{tag}乙"));
            await BizTest.ExecuteSqlAsync("INSERT INTO staff_i18n (staff_id, locale, name) VALUES (@B, N'en', @N)", ("@B", idB), ("@N", $"Test Coach {tag}"));

            using var client = fixture.CreateClient();
            var staff = await client.GetFromJsonAsync<PagedResult<StaffDto>>("/api/v1/tcrfc/staff?lang=en&pageSize=100", TestJson.Options);
            Assert.NotNull(staff);
            Assert.True(staff!.TotalCount <= 100, $"教練總數 {staff.TotalCount} 超過單頁 100，測試資料可能被分頁擠出");

            var a = staff.Items.Single(m => m.Id == idA);
            var b = staff.Items.Single(m => m.Id == idB);

            // 完全沒有英文列：name 與 title 都回退中文（不是 null）。
            Assert.True(ContainsCjk(a.Name!), $"A.name 應回退為中文，實際是「{a.Name}」");
            Assert.True(ContainsCjk(a.Title!), $"A.title 應回退為中文，實際是「{a.Title}」");

            // 逐欄位回退不是整筆記錄二選一：B 的 name 用英文、title（沒有英文列值）回退中文。
            Assert.Equal($"Test Coach {tag}", b.Name);
            Assert.True(ContainsCjk(b.Title!), $"B.title 應回退為中文（en 列沒有 title），實際是「{b.Title}」");
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM staff_i18n WHERE staff_id IN (@A, @B); DELETE FROM staff WHERE id IN (@A, @B)", ("@A", idA), ("@B", idB));
        }
    }

    [Fact]
    public async Task 賽程的對手場地與賽事名稱_沒有英文列_lang為en時全部回退中文()
    {
        // 規則本身才是被測行為，不依賴種子缺英文（種子已補英文列）：自建一個只有繁中的賽事系列與一場掛在其下的賽事，
        // 用 ?competition= 篩出這一場，驗證 opponent／venue／competitionName 在 lang=en 時全部回退中文。
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var seasonId = await BizTest.ScalarGuidAsync("SELECT id FROM seasons WHERE club_id = @C AND code = N'2026-27'", ("@C", clubId));
        var teamId = await BizTest.ScalarGuidAsync("SELECT id FROM teams WHERE code = N'D1'");
        var code = BizTest.Unique("lfm");
        var competitionId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        try
        {
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO competitions (id, club_id, season_id, code, status) VALUES (@Cp, @C, @S, @Code, N'published')",
                ("@Cp", competitionId), ("@C", clubId), ("@S", seasonId), ("@Code", code));
            await BizTest.ExecuteSqlAsync("INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@Cp, N'zh-Hant', N'測試賽事系列')", ("@Cp", competitionId));
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO matches (id, club_id, season_id, competition_id, match_on, opponent, status) VALUES (@M, @C, @S, @Cp, '2099-01-01', N'測試對手', N'scheduled')",
                ("@M", matchId), ("@C", clubId), ("@S", seasonId), ("@Cp", competitionId));
            await BizTest.ExecuteSqlAsync("INSERT INTO match_teams (match_id, team_id) VALUES (@M, @T)", ("@M", matchId), ("@T", teamId));
            await BizTest.ExecuteSqlAsync("INSERT INTO matches_i18n (match_id, locale, venue) VALUES (@M, N'zh-Hant', N'測試球場')", ("@M", matchId));

            using var client = fixture.CreateClient();
            var schedule = await client.GetFromJsonAsync<PagedResult<MatchDto>>($"/api/v1/tcrfc/schedule?lang=en&competition={code}", TestJson.Options);
            Assert.NotNull(schedule);

            // 不是 null（docs/18-work-errors.md 記錄過 LEFT JOIN 寫法會讓這個情境悄悄變成 null 的坑），而是回退中文。
            var match = Assert.Single(schedule!.Items);
            Assert.True(ContainsCjk(match.Opponent!), $"opponent 應回退為中文，實際是「{match.Opponent}」");
            Assert.True(ContainsCjk(match.Venue!), $"venue 應回退為中文，實際是「{match.Venue}」");
            Assert.True(ContainsCjk(match.CompetitionName!), $"competitionName 應回退為中文，實際是「{match.CompetitionName}」");
        }
        finally
        {
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM matches_i18n WHERE match_id = @M; DELETE FROM match_teams WHERE match_id = @M; DELETE FROM matches WHERE id = @M; "
                + "DELETE FROM competitions_i18n WHERE competition_id = @Cp; DELETE FROM competitions WHERE id = @Cp",
                ("@M", matchId), ("@Cp", competitionId));
        }
    }

    private static bool ContainsCjk(string value) => value.Any(ch => ch is >= '一' and <= '鿿');
}
