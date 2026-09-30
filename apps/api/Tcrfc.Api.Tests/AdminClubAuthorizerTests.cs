using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 任務要求的「四種擋下情境」逐一有專屬測試，打真正的 HTTP 管線與真正的
/// <c>tcrfc_club</c>，反例用真實的攻擊形狀（偽造／過期／越權的權杖與授權組合），
/// 不是隨便塞錯值（docs/18-work-errors.md <c>E-39</c> 的教訓）。種子帳號與其授權組合見
/// <c>db/seed/generate-club-seed-sql.py</c>「18.4 admin_users」。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminClubAuthorizerTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 情境一_未登入打後台端點_擋下()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 情境二_登入但未被授權該俱樂部_擋下()
    {
        using var client = fixture.CreateClient();
        // partner.club@tcrfc.test 只被授權 bw（藍鯨），打 tcrfc 的後台應該被擋下。
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("tcrfc", body); // 訊息要指名是哪個俱樂部沒有授權，方便使用者理解。
    }

    [Fact]
    public async Task 情境二反向_打有授權的俱樂部_成功()
    {
        // 同一個帳號（partner.club@tcrfc.test）打自己有授權的 bw，應該正常通過——
        // 證明上一個測試擋下的原因確實是「範圍」，不是這個帳號整體壞掉或權限碼本身有問題。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/bw/news");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 情境三_授權已過期_擋下()
    {
        using var client = fixture.CreateClient();
        // expired.grant@tcrfc.test 對 tcrfc 的 admin_user_clubs.expires_on 是昨天。
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("expired.grant@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 情境四_own_clubs角色打別的俱樂部_擋下()
    {
        // partner_club_manager 的 scope_mode = own_clubs，帳號只授權 bw。這條與情境二是同一個
        // 帳號但刻意另立一筆——情境二驗的是「一般帳號打沒授權的俱樂部」這個通用機制，
        // 這一條額外確認 own_clubs 角色本身（合作球隊管理，設計上就是給另一法人團隊用的角色）
        // 沒有任何特殊旁路能繞過範圍檢查，兩者驗的是不同的擔憂，即使實測用同一組資料。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/news");
        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news",
            new { slug = "partner-club-escape-attempt", categoryCode = "club", content = new { zh = new { title = "越權測試" } } });

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task 額外情境_未改密且未啟用2FA的帳號_可直接存取俱樂部範圍端點與me()
    {
        // 2026-09-30 使用者裁決：後台不再強制首次改密與啟用 2FA。fresh.setup@tcrfc.test 種子狀態是
        // must_change_password=1 且 two_factor_enabled=0（先前會被 AdminAccountGate 兩道檢查擋下 403）——
        // 現在什麼都不用做就該通過。不修改帳號狀態，因此不需要清理。
        // ⚠️ 若種子被先前的驗收弄髒（已改密或已啟用 2FA），結果仍然是 200，這條測試不依賴該狀態成立，
        // 但「未改密＋未啟用 2FA」的覆蓋力需要種子是乾淨的（reset-admin-accounts.sh）。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("fresh.setup@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newsResponse = await client.GetAsync("/api/v1/admin/tcrfc/news");
        var meResponse = await client.GetAsync("/api/v1/admin/auth/me");

        Assert.Equal(HttpStatusCode.OK, newsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task 額外情境_公開唯讀端點的行為完全不受影響()
    {
        // 硬性約束：不得改變公開唯讀 API 的既有行為。不帶任何權杖，五組既有 GET 端點應該照常運作。
        using var client = fixture.CreateClient();

        var clubs = await client.GetAsync("/api/v1/clubs");
        var players = await client.GetAsync("/api/v1/tcrfc/players");
        var staff = await client.GetAsync("/api/v1/tcrfc/staff");
        var news = await client.GetAsync("/api/v1/tcrfc/news");
        var schedule = await client.GetAsync("/api/v1/tcrfc/schedule");

        Assert.Equal(HttpStatusCode.OK, clubs.StatusCode);
        Assert.Equal(HttpStatusCode.OK, players.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staff.StatusCode);
        Assert.Equal(HttpStatusCode.OK, news.StatusCode);
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
    }
}
