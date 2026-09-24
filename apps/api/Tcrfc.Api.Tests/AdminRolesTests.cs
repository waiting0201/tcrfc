using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminAccounts;
using Tcrfc.Api.Features.AdminRoles;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>J2 角色與權限端對端測試：打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminRolesTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 情境一_未登入打角色列表_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 情境二_非超管帳號打角色列表_擋下()
    {
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 超管_列出角色_含十個種子角色()
    {
        using var client = await CreateSuperAdminClientAsync();
        var roles = await client.GetFromJsonAsync<List<AdminRoleListItemDto>>("/api/v1/admin/roles", TestJson.Options);
        Assert.NotNull(roles);
        Assert.True(roles!.Count >= 10);
        Assert.Contains(roles, r => r.Code == "system_admin" && r.IsSystem);
        Assert.Contains(roles, r => r.Code == "partner_club_manager" && r.ScopeMode == "own_clubs");
    }

    [Fact]
    public async Task 超管_列出權限碼字典_含本輪新增的三個代碼()
    {
        using var client = await CreateSuperAdminClientAsync();
        var permissions = await client.GetFromJsonAsync<List<AdminPermissionDto>>("/api/v1/admin/roles/permissions", TestJson.Options);
        Assert.NotNull(permissions);
        Assert.Contains(permissions!, p => p.Code == "system.account.view" && p.SysadminOnly);
        Assert.Contains(permissions!, p => p.Code == "system.club.view" && p.SysadminOnly);
        Assert.Contains(permissions!, p => p.Code == "team.competition.view" && !p.SysadminOnly && p.IsClubScoped);
    }

    [Fact]
    public async Task 建立自訂角色_成功_代碼重複回409_可更新_可刪除()
    {
        using var client = await CreateSuperAdminClientAsync();
        var code = $"test_role_{Guid.NewGuid():N}"[..32];

        try
        {
            var createResponse = await client.PostAsJsonAsync("/api/v1/admin/roles", new CreateAdminRoleRequest
            {
                Code = code,
                NameZh = "測試角色",
                ScopeMode = "own_clubs",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminRoleDetailDto>(TestJson.Options);
            Assert.False(created!.IsSystem);

            var conflictResponse = await client.PostAsJsonAsync("/api/v1/admin/roles", new CreateAdminRoleRequest
            {
                Code = code,
                NameZh = "測試角色（重複）",
                ScopeMode = "own_clubs",
            });
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/roles/{created.Id}", new UpdateAdminRoleRequest
            {
                NameZh = "測試角色（已更新）",
                ScopeMode = "all_clubs",
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminRoleDetailDto>(TestJson.Options);
            Assert.Equal("all_clubs", updated!.ScopeMode);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/roles/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getAfterDelete = await client.GetAsync($"/api/v1/admin/roles/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
        finally
        {
            await DeleteRoleByCodeAsync(code);
        }
    }

    [Fact]
    public async Task 刪除系統角色_擋下()
    {
        using var client = await CreateSuperAdminClientAsync();
        var viewerRoleId = await GetRoleIdByCodeAsync("viewer");

        var response = await client.DeleteAsync($"/api/v1/admin/roles/{viewerRoleId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 刪除仍被帳號指派的角色_擋下()
    {
        using var client = await CreateSuperAdminClientAsync();
        var code = $"test_role_inuse_{Guid.NewGuid():N}"[..32];
        var username = $"test.roleinuse.{Guid.NewGuid():N}@tcrfc.test";

        try
        {
            var role = await CreateRoleAsync(client, code, "own_clubs");

            var accountResponse = await client.PostAsJsonAsync("/api/v1/admin/accounts", new CreateAdminAccountRequest
            {
                Username = username,
                DisplayName = "角色使用中測試",
                InitialPassword = "InitialPassword-123",
                RoleCodes = [code],
            });
            Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/roles/{role.Id}");
            Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        }
        finally
        {
            await DeleteAccountByUsernameAsync(username);
            await DeleteRoleByCodeAsync(code);
        }
    }

    [Fact]
    public async Task 指派權限_成功_且拒絕指派sysadmin_only權限碼()
    {
        using var client = await CreateSuperAdminClientAsync();
        var code = $"test_role_perm_{Guid.NewGuid():N}"[..32];

        try
        {
            var role = await CreateRoleAsync(client, code, "own_clubs");

            var validResponse = await client.PutAsJsonAsync($"/api/v1/admin/roles/{role.Id}/permissions",
                new ReplaceRolePermissionsRequest
                {
                    Permissions = [new RolePermissionInput { PermissionCode = "team.competition.view", ScopeType = "own_clubs" }],
                });
            Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
            var updated = await validResponse.Content.ReadFromJsonAsync<AdminRoleDetailDto>(TestJson.Options);
            var assignment = Assert.Single(updated!.Permissions);
            Assert.Equal("team.competition.view", assignment.PermissionCode);
            Assert.Equal("own_clubs", assignment.ScopeType);

            var invalidResponse = await client.PutAsJsonAsync($"/api/v1/admin/roles/{role.Id}/permissions",
                new ReplaceRolePermissionsRequest
                {
                    Permissions = [new RolePermissionInput { PermissionCode = "system.account.view", ScopeType = "all" }],
                });
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }
        finally
        {
            await DeleteRoleByCodeAsync(code);
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

    private static async Task<AdminRoleDetailDto> CreateRoleAsync(HttpClient client, string code, string scopeMode)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/roles", new CreateAdminRoleRequest
        {
            Code = code,
            NameZh = "測試角色",
            ScopeMode = scopeMode,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminRoleDetailDto>(TestJson.Options))!;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetRoleIdByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM admin_roles WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteRoleByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM role_permissions WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = @Code);
            DELETE FROM admin_user_roles WHERE admin_role_id = (SELECT id FROM admin_roles WHERE code = @Code);
            DELETE FROM admin_roles WHERE code = @Code;
            """;
        command.Parameters.AddWithValue("@Code", code);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteAccountByUsernameAsync(string username)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM admin_refresh_tokens WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_user_clubs WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_user_roles WHERE admin_user_id = (SELECT id FROM admin_users WHERE username = @Username);
            DELETE FROM admin_users WHERE username = @Username;
            """;
        command.Parameters.AddWithValue("@Username", username);
        await command.ExecuteNonQueryAsync();
    }
}
