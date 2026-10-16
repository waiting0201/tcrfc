using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.AdminMemberships;

namespace Tcrfc.Api.Tests;

/// <summary>B1（P4 試訓／K1–K4 會員系統／L3–L4 行事曆進階）測試共用工具：查 id、建立與清除測試會員。
/// 測試資料一律在 finally 清掉；不動種子（會員編號 M9000xx、方案 standard／premium〔磐石〕與 single〔藍鯨〕等是唯讀依據）。</summary>
internal static class B1Test
{
    public static Task<Guid> ScalarAsync(string sql, params (string Name, object? Value)[] parameters) => BizTest.ScalarGuidAsync(sql, parameters);

    public static Task<Guid> MemberIdAsync(string memberNo) =>
        BizTest.ScalarGuidAsync("SELECT id FROM members WHERE member_no = @No", ("@No", memberNo));

    public static Task<Guid> ClubIdAsync(string code) =>
        BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = @Code", ("@Code", code));

    public static Task<Guid> SeasonIdAsync(string clubCode, string seasonCode) =>
        BizTest.ScalarGuidAsync(
            "SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id WHERE c.code = @C AND s.code = @S", ("@C", clubCode), ("@S", seasonCode));

    public static Task<Guid> PlanIdAsync(string clubCode, string seasonCode, string planCode) =>
        BizTest.ScalarGuidAsync(
            """
            SELECT p.id FROM membership_plans p JOIN clubs c ON c.id = p.club_id JOIN seasons s ON s.id = p.season_id
            WHERE c.code = @C AND s.code = @S AND p.code = @P
            """, ("@C", clubCode), ("@S", seasonCode), ("@P", planCode));

    public static Task<Guid> TeamIdAsync(string teamCode) =>
        BizTest.ScalarGuidAsync("SELECT id FROM teams WHERE code = @Code", ("@Code", teamCode));

    public static Task<Guid> VenueIdAsync(string keyword) =>
        BizTest.ScalarGuidAsync(
            "SELECT TOP 1 v.id FROM venues v JOIN venues_i18n i ON i.venue_id = v.id AND i.locale = N'zh-Hant' WHERE i.name LIKE @K", ("@K", "%" + keyword + "%"));

    /// <summary>建立測試會員（走 K1 建立端點，用客服帳號）。Email／電話是 example.com／全 0，編號依目前規則產生。</summary>
    public static async Task<AdminMemberDetailDto> CreateMemberAsync(HttpClient client, string club, string tag, string? phone = null)
    {
        var response = await client.PostAsync($"/api/v1/admin/{club}/members", BizTest.Json(new
        {
            name = $"【測試】{tag}", email = $"b1-{tag}-{Guid.NewGuid():N}@example.com".ToLowerInvariant(),
            phone = phone ?? "0900-000-777", locale = "zh-Hant",
        }));
        return await BizTest.ReadAsync<AdminMemberDetailDto>(response);
    }

    /// <summary>
    /// 後台手動開通一份會籍（v3.29：註冊帳號不是會籍，後台也沒有「建立免費會籍」；會籍只由開通方案而來）。
    /// 起訖日取該球季的起訖（過去的球季如藍鯨 2025 也能開通，補登歷史會籍）。回傳開通後的會籍詳情。
    /// </summary>
    public static async Task<AdminMembershipDetailDto> ActivateAsync(HttpClient client, string club, Guid memberId, string seasonCode, string planCode, int amount = 0)
    {
        var planId = await PlanIdAsync(club, seasonCode, planCode);
        var startOn = await BizTest.ScalarStringAsync("SELECT CONVERT(varchar(10), s.start_on, 23) FROM seasons s JOIN clubs c ON c.id = s.club_id WHERE c.code = @C AND s.code = @S", ("@C", club), ("@S", seasonCode));
        var endOn = await BizTest.ScalarStringAsync("SELECT CONVERT(varchar(10), s.end_on, 23) FROM seasons s JOIN clubs c ON c.id = s.club_id WHERE c.code = @C AND s.code = @S", ("@C", club), ("@S", seasonCode));
        var response = await client.PostAsync($"/api/v1/admin/{club}/memberships/activate", BizTest.Json(new
        {
            memberId, planId, paymentMethod = "onsite", amount, paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"), startOn, endOn,
        }));
        return await BizTest.ReadAsync<AdminMembershipDetailDto>(response);
    }

    public static async Task DeleteMembersAsync(params Guid[] memberIds)
    {
        foreach (var id in memberIds)
        {
            await BizTest.ExecuteSqlAsync(
                """
                DELETE FROM jersey_issues WHERE member_id = @Id;
                DELETE FROM member_cards WHERE membership_id IN (SELECT id FROM memberships WHERE member_id = @Id);
                DELETE FROM membership_payments WHERE membership_id IN (SELECT id FROM memberships WHERE member_id = @Id);
                DELETE FROM memberships WHERE member_id = @Id;
                UPDATE members SET merged_into_member_id = NULL WHERE merged_into_member_id = @Id;
                DELETE FROM members WHERE id = @Id;
                """, ("@Id", id));
        }
    }
}
