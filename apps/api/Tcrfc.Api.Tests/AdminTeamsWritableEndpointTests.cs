using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 「我能寫哪些球隊」唯讀端點（<c>GET /api/v1/admin/{club}/teams/writable?module=...</c>，
/// <c>AdminTeamsEndpoints</c>）——回應主站 coordinator 任務：C1–C4 的「參賽球隊／所屬球隊」下拉
/// 選單目前列出整個俱樂部全部球隊，不分一線隊／學院／個別授權，S1-8 的列級授權
/// （<c>Security/TeamRowScope.cs</c>）只擋得住寫入端點。這支測試驗證三種
/// <c>role_permissions.scope_type</c>：<c>all</c>（系統管理員全部看得到）、<c>academy_only</c>
/// （只看得到學院梯隊）、<c>own_teams</c>（只看得到 <c>admin_user_teams</c> 授權且未過期的球隊）。
///
/// 打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>，形狀比照 <c>AdminMatchesAndStandingsTests</c>
/// 「列級授權_own_teams_只能碰admin_user_teams授權的球隊」——**用 <c>bw</c> 俱樂部**（三支球隊：
/// <c>BW1</c> 一線隊、<c>BW-U15</c>／<c>BW-U12</c> 學院梯隊），不用 <c>tcrfc</c>（只有 <c>D1</c>
/// 一支球隊，示範不出「收斂成部分球隊」的過濾效果）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminTeamsWritableEndpointTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task module參數缺漏或不支援_擋下400()
    {
        using var client = await CreateClientAsync("super.admin@tcrfc.test");

        var missing = await client.GetAsync("/api/v1/admin/bw/teams/writable");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var invalid = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=coach");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task 系統管理員_看得到俱樂部全部球隊()
    {
        using var client = await CreateClientAsync("super.admin@tcrfc.test");

        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
        Assert.NotNull(teams);
        var codes = teams!.Select(t => t.Code).ToHashSet();
        Assert.Contains("BW1", codes);
        Assert.Contains("BW-U15", codes);
        Assert.Contains("BW-U12", codes);
    }

    [Fact]
    public async Task academy_only角色_只看得到學院梯隊_看不到一線隊()
    {
        // academy.manager@tcrfc.test：academy_program 角色，只授權 bw，team.team.* 為 academy_only
        // （S1-8 種子資料，見 apps/api/README.md「新增測試帳號」）。
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");

        var response = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=team");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var teams = await response.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
        Assert.NotNull(teams);
        var codes = teams!.Select(t => t.Code).ToHashSet();
        Assert.Contains("BW-U15", codes);
        Assert.Contains("BW-U12", codes);
        Assert.DoesNotContain("BW1", codes); // 一線隊不是 academy 類型，即使同俱樂部也擋下。
        Assert.All(teams!, t => Assert.Equal("academy", t.Type));
    }

    [Fact]
    public async Task own_teams角色_只看得到被授權且未過期的球隊()
    {
        // 種子資料沒有任何角色真的用 own_teams（規劃書 §7.4 把它綁在尚未建置的行事曆模組），
        // 這裡直接改一筆既有 role_permissions（partner_club_manager／team.match.update）示範機制
        // 本身，測完在 finally 還原——比照 AdminMatchesAndStandingsTests 既有的
        // WithTemporaryScopeTypeAsync 做法。partner.club@tcrfc.test 只授權 bw，符合本測試需要。
        var bwU15Id = await GetTeamIdAsync("bw", "BW-U15");
        var bw1Id = await GetTeamIdAsync("bw", "BW1");

        await WithTemporaryScopeTypeAsync("partner_club_manager", "team.match.update", "own_teams", async () =>
        {
            using var client = await CreateClientAsync("partner.club@tcrfc.test");

            // 授權前：own_teams 預設空集合（fail-closed），寫入碼算出的 TeamRowScope 不允許任何球隊，
            // 清單應為空——不是 403，這是列表端點，範圍外的球隊只是被過濾掉,不是被擋下整支請求。
            var beforeGrant = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=match");
            Assert.Equal(HttpStatusCode.OK, beforeGrant.StatusCode);
            var beforeTeams = await beforeGrant.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
            Assert.Empty(beforeTeams!);

            await GrantAdminUserTeamAsync("partner.club@tcrfc.test", bwU15Id);
            try
            {
                // 授權後：只看得到被授權的那一支，同俱樂部其餘球隊（含一線隊 BW1）依然看不到。
                var afterGrant = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=match");
                Assert.Equal(HttpStatusCode.OK, afterGrant.StatusCode);
                var afterTeams = await afterGrant.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
                var afterCodes = afterTeams!.Select(t => t.Code).ToHashSet();
                Assert.Contains("BW-U15", afterCodes);
                Assert.DoesNotContain("BW1", afterCodes);
                Assert.DoesNotContain("BW-U12", afterCodes);

                // 授權到期（expires_on 設昨天）→ 視同未授權，清單再度變空。
                await SetAdminUserTeamExpiryAsync("partner.club@tcrfc.test", bwU15Id, "yesterday");
                var afterExpiry = await client.GetAsync("/api/v1/admin/bw/teams/writable?module=match");
                var afterExpiryTeams = await afterExpiry.Content.ReadFromJsonAsync<List<AdminWritableTeamDto>>(TestJson.Options);
                Assert.Empty(afterExpiryTeams!);
            }
            finally
            {
                await RevokeAdminUserTeamAsync("partner.club@tcrfc.test", bwU15Id);
            }
        });

        // bw1Id 只是拿來在上面斷言「看不到」時對照用的常數，這裡沒有其他用途——保留變數名稱
        // 讓測試讀者一眼看到「一線隊的 id 我有查過，不是漏看」。
        _ = bw1Id;
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

    /// <summary>逐字比照 <c>AdminMatchesAndStandingsTests.WithTemporaryScopeTypeAsync</c>——
    /// 兩份測試各自維護一份是刻意的（既有專案慣例：每個測試檔獨立、不共用測試基底類別），
    /// 不是重複程式碼沒發現。</summary>
    private async Task WithTemporaryScopeTypeAsync(string roleCode, string permissionCode, string temporaryScopeType, Func<Task> action)
    {
        var connectionString = RequireConnectionString();
        string originalScopeType;

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var select = connection.CreateCommand();
            select.CommandText = """
                SELECT rp.scope_type FROM role_permissions rp
                JOIN admin_roles r ON r.id = rp.admin_role_id
                JOIN permissions p ON p.id = rp.permission_id
                WHERE r.code = @RoleCode AND p.code = @PermissionCode;
                """;
            select.Parameters.AddWithValue("@RoleCode", roleCode);
            select.Parameters.AddWithValue("@PermissionCode", permissionCode);
            originalScopeType = (string)(await select.ExecuteScalarAsync())!;
        }

        try
        {
            await SetScopeTypeAsync(roleCode, permissionCode, temporaryScopeType);
            await action();
        }
        finally
        {
            await SetScopeTypeAsync(roleCode, permissionCode, originalScopeType);
        }
    }

    private static async Task SetScopeTypeAsync(string roleCode, string permissionCode, string scopeType)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE rp SET rp.scope_type = @ScopeType
            FROM role_permissions rp
            JOIN admin_roles r ON r.id = rp.admin_role_id
            JOIN permissions p ON p.id = rp.permission_id
            WHERE r.code = @RoleCode AND p.code = @PermissionCode;
            """;
        command.Parameters.AddWithValue("@ScopeType", scopeType);
        command.Parameters.AddWithValue("@RoleCode", roleCode);
        command.Parameters.AddWithValue("@PermissionCode", permissionCode);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task GrantAdminUserTeamAsync(string username, Guid teamId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @UserId uniqueidentifier = (SELECT id FROM admin_users WHERE username = @Username);
            IF NOT EXISTS (SELECT 1 FROM admin_user_teams WHERE admin_user_id = @UserId AND team_id = @TeamId)
              INSERT INTO admin_user_teams (admin_user_id, team_id, expires_on, is_active) VALUES (@UserId, @TeamId, NULL, 1);
            ELSE
              UPDATE admin_user_teams SET is_active = 1, expires_on = NULL WHERE admin_user_id = @UserId AND team_id = @TeamId;
            """;
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@TeamId", teamId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetAdminUserTeamExpiryAsync(string username, Guid teamId, string when)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        var expiresSql = when == "yesterday" ? "DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date))" : "NULL";
        command.CommandText = $"""
            UPDATE admin_user_teams SET expires_on = {expiresSql}
            WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username) AND team_id = @TeamId;
            """;
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@TeamId", teamId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RevokeAdminUserTeamAsync(string username, Guid teamId)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM admin_user_teams
            WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username) AND team_id = @TeamId;
            """;
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@TeamId", teamId);
        await command.ExecuteNonQueryAsync();
    }
}
