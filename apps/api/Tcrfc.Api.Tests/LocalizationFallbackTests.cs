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
    public async Task 俱樂部名稱_有英文的用英文_沒有的回退中文()
    {
        using var client = fixture.CreateClient();

        var clubs = await client.GetFromJsonAsync<List<ClubDto>>("/api/v1/clubs?lang=en", TestJson.Options);
        Assert.NotNull(clubs);

        var tcrfc = clubs!.Single(c => c.Code == "tcrfc");
        var bw = clubs.Single(c => c.Code == "bw");

        // tcrfc 兩個語系都有英文名，?lang=en 應該拿到英文（README 驗收紀錄：Taichung Rock FC）。
        Assert.False(ContainsCjk(tcrfc.Name), $"tcrfc 在 lang=en 應為英文名，實際是「{tcrfc.Name}」");

        // bw（台中藍鯨）name 只有 zh-Hant 列，README 驗收紀錄：?lang=en 回退成「台中藍鯨」。
        Assert.True(ContainsCjk(bw.Name), $"bw 目前沒有英文俱樂部名，lang=en 應回退成中文，實際是「{bw.Name}」");
    }

    [Fact]
    public async Task 教練職稱完全沒有英文列_lang為en時全部回退中文_姓名仍逐筆各自判斷()
    {
        using var client = fixture.CreateClient();

        var staff = await client.GetFromJsonAsync<PagedResult<StaffDto>>("/api/v1/tcrfc/staff?lang=en&pageSize=100", TestJson.Options);
        Assert.NotNull(staff);
        Assert.True(staff!.Items.Count > 0, "種子資料應該有 tcrfc 教練/職員資料");

        // staff_i18n.title 完全沒有英文列（README 驗收紀錄）——不論這筆的 name 有沒有英文，
        // title 在 lang=en 時應該全部回退中文。
        foreach (var member in staff.Items.Where(m => m.Title is not null))
        {
            Assert.True(ContainsCjk(member.Title!), $"title 應回退為中文（目前無任何英文列），實際是「{member.Title}」");
        }

        // 逐欄位回退不是整筆記錄二選一：name 欄位應該有些有英文、有些沒有（回退中文）——
        // 這正是「同一筆記錄可以一半英文一半中文」要驗證的東西。若種子資料剛好全部都有或
        // 全部都沒有英文名，這個斷言會提醒維護者種子資料已經不符合本測試假設的前提，而不是
        // 靜靜地通過一個其實沒驗證到任何東西的測試。
        var namesWithEnglish = staff.Items.Count(m => m.Name is not null && !ContainsCjk(m.Name));
        var namesWithoutEnglish = staff.Items.Count(m => m.Name is not null && ContainsCjk(m.Name));
        Assert.True(namesWithEnglish > 0 && namesWithoutEnglish > 0,
            $"預期種子資料同時存在有英文名與沒有英文名的教練（各自獨立回退），實際：有英文 {namesWithEnglish} 筆、無英文 {namesWithoutEnglish} 筆");
    }

    [Fact]
    public async Task 賽程的對手場地與賽事名稱_種子資料無英文列_lang為en時全部回退中文()
    {
        using var client = fixture.CreateClient();

        var schedule = await client.GetFromJsonAsync<PagedResult<MatchDto>>("/api/v1/tcrfc/schedule?lang=en&pageSize=200", TestJson.Options);
        Assert.NotNull(schedule);
        Assert.True(schedule!.Items.Count > 0, "種子資料應該有 tcrfc 賽程資料");

        // README 驗收紀錄：matches_i18n 的 opponent／venue、competitions_i18n 的賽事名稱
        // 種子資料完全沒有任何英文列，lang=en 時應該全部回退中文（不是 null，也不是遺漏成 null
        // ——docs/18-work-errors.md 記錄過 LEFT JOIN 寫法會讓這個情境悄悄變成 null 的坑）。
        foreach (var match in schedule.Items)
        {
            if (match.Opponent is not null)
            {
                Assert.True(ContainsCjk(match.Opponent), $"opponent 應回退為中文，實際是「{match.Opponent}」");
            }

            if (match.CompetitionName is not null)
            {
                Assert.True(ContainsCjk(match.CompetitionName), $"competitionName 應回退為中文，實際是「{match.CompetitionName}」");
            }
        }
    }

    private static bool ContainsCjk(string value) => value.Any(ch => ch is >= '一' and <= '鿿');
}
