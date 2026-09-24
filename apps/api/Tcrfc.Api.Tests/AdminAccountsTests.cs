using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminAccounts;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// J1 帳號管理（含掛在帳號底下的 J4 俱樂部授權）端對端測試：打真正的 HTTP 管線與真正的
/// <c>tcrfc_club_dev</c>。種子帳號見 <c>db/seed/generate-club-seed-sql.py</c>「18.4 admin_users」。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminAccountsTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 情境一_未登入打帳號列表_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 情境二_非超管帳號打帳號列表_擋下()
    {
        // content.editor@tcrfc.test 是 content_editor 角色，不具備任何 system.* 權限碼，
        // 且 system.account.view 是 sysadmin_only——即使角色曾經被誤指派這個碼也一樣會被擋下
        // （PermissionChecker 的 sysadmin_only 檢查），這裡驗的是「一般角色帳號本來就沒有這個碼」。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 超管_建立帳號_成功且不回傳任何雜湊或秘密欄位()
    {
        using var client = await CreateSuperAdminClientAsync();
        var username = $"test.create.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/accounts", new CreateAdminAccountRequest
            {
                Username = username,
                DisplayName = "建立帳號測試",
                InitialPassword = "InitialPassword-123",
                RoleCodes = ["viewer"],
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("twoFactorSecret", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InitialPassword-123", raw);

            var created = await response.Content.ReadFromJsonAsync<AdminAccountDetailDto>(TestJson.Options);
            Assert.NotNull(created);
            Assert.True(created!.MustChangePassword); // 一律強制首次登入改密，不接受呼叫端關閉。
            Assert.False(created.TwoFactorEnabled);
            Assert.Contains("viewer", created.RoleCodes);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    [Fact]
    public async Task 建立帳號_帳號重複_回409()
    {
        using var client = await CreateSuperAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/admin/accounts", new CreateAdminAccountRequest
        {
            Username = "content.editor@tcrfc.test", // 種子既有帳號，一定衝突。
            DisplayName = "重複帳號測試",
            InitialPassword = "InitialPassword-123",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task 建立帳號_密碼不符政策_回400()
    {
        using var client = await CreateSuperAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/admin/accounts", new CreateAdminAccountRequest
        {
            Username = $"test.badpw.{Guid.NewGuid():N}@tcrfc.test",
            DisplayName = "密碼政策測試",
            InitialPassword = "short",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 停用帳號_立即撤銷既有更新權杖()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.disable.{Guid.NewGuid():N}@tcrfc.test";
        const string password = "InitialPassword-123";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, password, ["viewer"]);

            // 這個帳號自己登入，拿到一把有效的更新權杖（AdminAuthService.LoginAsync 不檢查
            // must_change_password，這裡只是要一把真的能用來 refresh 的 Cookie，不是要驗證
            // 這個帳號能不能打俱樂部範圍端點）。
            using var ownClient = fixture.CreateClient();
            var login = await ownClient.PostAsJsonAsync("/api/v1/admin/auth/login", new LoginRequest(username, password, null));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var refreshCookie = ExtractSetCookieValue(login, "__Host-tcrfc-admin-rt");

            var disableResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/status",
                new SetAdminAccountStatusRequest { Status = "disabled" });
            Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

            using var refreshClient = fixture.CreateClient();
            SetCookie(refreshClient, refreshCookie);
            var refreshAfterDisable = await refreshClient.PostAsync("/api/v1/admin/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterDisable.StatusCode);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    [Fact]
    public async Task 重設密碼_舊密碼失效_新密碼可登入_且撤銷既有更新權杖()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.resetpw.{Guid.NewGuid():N}@tcrfc.test";
        const string oldPassword = "InitialPassword-123";
        const string newPassword = "ResetPassword-456";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, oldPassword, []);

            using var ownClient = fixture.CreateClient();
            var login = await ownClient.PostAsJsonAsync("/api/v1/admin/auth/login", new LoginRequest(username, oldPassword, null));
            var refreshCookie = ExtractSetCookieValue(login, "__Host-tcrfc-admin-rt");

            var resetResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/reset-password",
                new ResetAdminAccountPasswordRequest { NewPassword = newPassword });
            Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

            // 舊密碼登入失敗。
            using var oldLoginClient = fixture.CreateClient();
            var oldLogin = await oldLoginClient.PostAsJsonAsync("/api/v1/admin/auth/login", new LoginRequest(username, oldPassword, null));
            Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

            // 新密碼登入成功。
            using var newLoginClient = fixture.CreateClient();
            var newLogin = await newLoginClient.PostAsJsonAsync("/api/v1/admin/auth/login", new LoginRequest(username, newPassword, null));
            Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

            // 重設前發出的更新權杖已被撤銷。
            using var refreshClient = fixture.CreateClient();
            SetCookie(refreshClient, refreshCookie);
            var refreshAfterReset = await refreshClient.PostAsync("/api/v1/admin/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterReset.StatusCode);

            var detail = await adminClient.GetFromJsonAsync<AdminAccountDetailDto>($"/api/v1/admin/accounts/{created.Id}", TestJson.Options);
            Assert.True(detail!.MustChangePassword);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    [Fact]
    public async Task 重設兩階段驗證_清空既有設定()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.resettotp.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, "InitialPassword-123", []);

            // 直接在資料庫種一組已完成的 2FA 設定，模擬「這個帳號原本已經設好 2FA」。
            await using (var connection = new SqlConnection(RequireConnectionString()))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE admin_users
                    SET two_factor_enabled = 1, two_factor_secret_encrypted = N'placeholder', two_factor_confirmed_at = SYSUTCDATETIME()
                    WHERE id = @Id
                    """;
                command.Parameters.AddWithValue("@Id", created.Id);
                await command.ExecuteNonQueryAsync();
            }

            var resetResponse = await adminClient.PostAsync($"/api/v1/admin/accounts/{created.Id}/reset-totp", null);
            Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

            var detail = await adminClient.GetFromJsonAsync<AdminAccountDetailDto>($"/api/v1/admin/accounts/{created.Id}", TestJson.Options);
            Assert.False(detail!.TwoFactorEnabled);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    [Fact]
    public async Task 俱樂部授權_新增後立即生效_撤銷後立即失效()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.grant.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, "InitialPassword-123", ["content_editor"]);
            var bwClubId = await GetClubIdByCodeAsync("bw");

            var grantResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/club-grants",
                new CreateAdminAccountClubGrantRequest { ClubId = bwClubId });
            Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
            var grant = await grantResponse.Content.ReadFromJsonAsync<AdminAccountClubGrantDto>(TestJson.Options);
            Assert.True(grant!.IsCurrentlyEffective);

            var grantsAfterCreate = await adminClient.GetFromJsonAsync<List<AdminAccountClubGrantDto>>(
                $"/api/v1/admin/accounts/{created.Id}/club-grants", TestJson.Options);
            Assert.Contains(grantsAfterCreate!, g => g.ClubId == bwClubId && g.IsCurrentlyEffective);

            var revokeResponse = await adminClient.DeleteAsync($"/api/v1/admin/accounts/{created.Id}/club-grants/{bwClubId}");
            Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

            var grantsAfterRevoke = await adminClient.GetFromJsonAsync<List<AdminAccountClubGrantDto>>(
                $"/api/v1/admin/accounts/{created.Id}/club-grants", TestJson.Options);
            var bwGrant = Assert.Single(grantsAfterRevoke!, g => g.ClubId == bwClubId);
            Assert.False(bwGrant.IsActive);
            Assert.False(bwGrant.IsCurrentlyEffective);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    /// <summary>
    /// J4 球隊授權（<c>admin_user_teams</c>，規劃書第 1223–1231 行）。只授權過 tcrfc 俱樂部的
    /// 帳號，應該可以拿到 tcrfc 底下球隊（D1）的授權，新增立即生效；撤銷立即失效——跟俱樂部授權
    /// 是同一套 upsert／軟撤銷語意。
    /// </summary>
    [Fact]
    public async Task 球隊授權_新增後立即生效_撤銷後立即失效()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.teamgrant.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, "InitialPassword-123", ["content_editor"]);
            var tcrfcClubId = await GetClubIdByCodeAsync("tcrfc");
            var d1TeamId = await GetTeamIdByCodeAsync("D1");

            var grantClubResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/club-grants",
                new CreateAdminAccountClubGrantRequest { ClubId = tcrfcClubId });
            Assert.Equal(HttpStatusCode.OK, grantClubResponse.StatusCode);

            var grantTeamResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/team-grants",
                new CreateAdminAccountTeamGrantRequest { TeamId = d1TeamId });
            Assert.Equal(HttpStatusCode.OK, grantTeamResponse.StatusCode);
            var grant = await grantTeamResponse.Content.ReadFromJsonAsync<AdminAccountTeamGrantDto>(TestJson.Options);
            Assert.True(grant!.IsCurrentlyEffective);
            Assert.Equal("tcrfc", grant.ClubCode);
            Assert.Equal("D1", grant.TeamCode);

            var grantsAfterCreate = await adminClient.GetFromJsonAsync<List<AdminAccountTeamGrantDto>>(
                $"/api/v1/admin/accounts/{created.Id}/team-grants", TestJson.Options);
            Assert.Contains(grantsAfterCreate!, g => g.TeamId == d1TeamId && g.IsCurrentlyEffective);

            var revokeResponse = await adminClient.DeleteAsync($"/api/v1/admin/accounts/{created.Id}/team-grants/{d1TeamId}");
            Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

            var grantsAfterRevoke = await adminClient.GetFromJsonAsync<List<AdminAccountTeamGrantDto>>(
                $"/api/v1/admin/accounts/{created.Id}/team-grants", TestJson.Options);
            var d1Grant = Assert.Single(grantsAfterRevoke!, g => g.TeamId == d1TeamId);
            Assert.False(d1Grant.IsActive);
            Assert.False(d1Grant.IsCurrentlyEffective);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    /// <summary>
    /// 🔴 防呆核心（coordinator 指示）：不能授權一個帳號目前沒有有效俱樂部授權的球隊——
    /// 這裡故意只給帳號 tcrfc 的俱樂部授權，卻嘗試指派 bw 底下的球隊（BW1），應該被擋下（400）。
    /// </summary>
    [Fact]
    public async Task 球隊授權_不在有效俱樂部授權範圍內_擋下()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var username = $"test.tg.oos.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var created = await CreateAccountAsync(adminClient, username, "InitialPassword-123", ["content_editor"]);
            var tcrfcClubId = await GetClubIdByCodeAsync("tcrfc");
            var bw1TeamId = await GetTeamIdByCodeAsync("BW1");

            var grantClubResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/club-grants",
                new CreateAdminAccountClubGrantRequest { ClubId = tcrfcClubId });
            Assert.Equal(HttpStatusCode.OK, grantClubResponse.StatusCode);

            var grantTeamResponse = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/team-grants",
                new CreateAdminAccountTeamGrantRequest { TeamId = bw1TeamId });
            Assert.Equal(HttpStatusCode.BadRequest, grantTeamResponse.StatusCode);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
        }
    }

    [Fact]
    public async Task 球隊授權_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/team-grants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 球隊授權_非超管帳號_擋下()
    {
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/team-grants");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// 🔴 執行層安全措施（task 5，規劃書未明文）：不能把系統操作到「沒有任何啟用中的最高管理權限
    /// 帳號」。用一個全新建立的測試帳號當「最後一根稻草」，暫時把種子超管（sa@system.local、
    /// clean.login@tcrfc.test）的 is_super_admin 降為 false（只剩呼叫端本身 super.admin@tcrfc.test
    /// 與這個新帳號兩個啟用中的超管），先停用新帳號（此時仍有 super.admin 在，應該成功），
    /// 再嘗試呼叫端把自己也停用（此時只剩它自己一個，應該被擋下）——全程在 finally 還原種子資料，
    /// 呼叫端本身的帳號從未被改動過，測試中途失敗也不會讓它變成無法登入。
    /// </summary>
    [Fact]
    public async Task 防呆_不能讓系統歸零到沒有啟用中的最高管理權限帳號()
    {
        using var adminClient = await CreateSuperAdminClientAsync();
        var callerId = await GetAccountIdByUsernameAsync("super.admin@tcrfc.test");
        var username = $"test.lastsuper.{Guid.NewGuid():N}@tcrfc.test";

        var demoted = new List<string> { "sa@system.local", "clean.login@tcrfc.test" };
        try
        {
            var created = await CreateAccountAsync(adminClient, username, "InitialPassword-123", [], isSuperAdmin: true);

            await SetIsSuperAdminAsync(demoted, isSuperAdmin: false);

            // 此時啟用中的超管只剩「呼叫端 super.admin@tcrfc.test」與「新建的 created」兩個，
            // 停用 created 應該成功（還剩呼叫端一個）。
            var disableCreated = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{created.Id}/status",
                new SetAdminAccountStatusRequest { Status = "disabled" });
            Assert.Equal(HttpStatusCode.OK, disableCreated.StatusCode);

            // 現在只剩呼叫端自己一個啟用中的超管，把自己也停用應該被擋下（409）。
            var disableSelf = await adminClient.PostAsJsonAsync($"/api/v1/admin/accounts/{callerId}/status",
                new SetAdminAccountStatusRequest { Status = "disabled" });
            Assert.Equal(HttpStatusCode.Conflict, disableSelf.StatusCode);

            // 呼叫端自己仍然是 active——上面的操作被擋下，不是「先停用再回滾」。
            var callerDetail = await adminClient.GetFromJsonAsync<AdminAccountDetailDto>(
                $"/api/v1/admin/accounts/{callerId}", TestJson.Options);
            Assert.Equal("active", callerDetail!.Status);
        }
        finally
        {
            await SetIsSuperAdminAsync(demoted, isSuperAdmin: true);
            await DeleteAccountByUsernameAsync(username);
        }
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<AdminAccountDetailDto> CreateAccountAsync(
        HttpClient adminClient, string username, string password, IReadOnlyList<string> roleCodes, bool isSuperAdmin = false)
    {
        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/accounts", new CreateAdminAccountRequest
        {
            Username = username,
            DisplayName = "測試帳號",
            InitialPassword = password,
            RoleCodes = roleCodes,
            IsSuperAdmin = isSuperAdmin,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminAccountDetailDto>(TestJson.Options))!;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetTeamIdByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM teams WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetClubIdByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM clubs WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> GetAccountIdByUsernameAsync(string username)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM admin_users WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetIsSuperAdminAsync(IReadOnlyList<string> usernames, bool isSuperAdmin)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        foreach (var username in usernames)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE admin_users SET is_super_admin = @IsSuperAdmin WHERE username = @Username";
            command.Parameters.AddWithValue("@IsSuperAdmin", isSuperAdmin);
            command.Parameters.AddWithValue("@Username", username);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteAccountByUsernameAsync(string username)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // 依序清掉三張關聯表再刪帳號本身，避免外鍵約束擋下（測試建立的帳號通常不會有這些關聯，
        // 但 admin_user_clubs 測試會真的寫入一列，這裡一律清乾淨保證可重複執行）。
        command.CommandText = """
            DELETE FROM admin_refresh_tokens WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_user_clubs WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_user_roles WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_users WHERE username = @Username;
            """;
        command.Parameters.AddWithValue("@Username", username);
        await command.ExecuteNonQueryAsync();
    }

    private static string ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "回應沒有 Set-Cookie 標頭。");
        var setCookieHeader = string.Join(";", cookies!);
        var marker = $"{cookieName}=";
        var start = setCookieHeader.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"回應標頭裡找不到 {cookieName}：{setCookieHeader}");
        var end = setCookieHeader.IndexOf(';', start);
        return end < 0 ? setCookieHeader[start..] : setCookieHeader[start..end];
    }

    private static void SetCookie(HttpClient client, string cookieValue)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookieValue);
    }
}
