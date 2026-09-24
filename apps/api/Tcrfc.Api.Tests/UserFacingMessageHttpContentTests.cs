using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Tcrfc.Api.Features.AdminRoles;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 2026-09-24（`S1-8` 續作，回應 <c>docs/18-work-errors.md</c> <c>E-52</c>）：
/// <see cref="UserFacingMessageContentTests"/> 是原始碼靜態掃描，覆蓋面廣但看不到「JSON 序列化
/// 之後實際送到瀏覽器的位元組長什麼樣子」。這支測試改用**代表性端點實打**——真正的 HTTP 管線、
/// 真正的 <c>tcrfc_club_dev</c>，對每一個已知會回傳 400／403 的路徑，直接檢查回應本文，
/// 兩支測試互補（各自的涵蓋範圍與邊界見 <see cref="UserFacingMessageContentTests"/> 類別上的
/// 完整說明），缺一不可：靜態掃描能看到「還沒有任何測試打到的程式碼路徑」，這支看到的是
/// 「同一段程式碼經過完整 middleware／JSON 序列化管線之後，真的長什麼樣子」。
///
/// **四個代表性端點，逐一對應到本輪實際發現或修正過的四個位置**：
/// ① <see cref="Security.AdminClubAuthorizer"/> 的權限碼檢查分支（<c>E-52</c> 原始案例，
///    俱樂部範圍端點）② <see cref="Security.AdminSystemAuthorizer"/> 的權限碼檢查分支
///    （<c>E-52</c> 原始案例，全域端點）③ <see cref="Features.AdminRoles.AdminRoleValidationException"/>
///    「找不到權限碼」分支（本輪新發現並修正）④
///    <see cref="Features.AdminRoles.AdminRoleSysadminOnlyPermissionException"/>（本輪新發現並修正）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class UserFacingMessageHttpContentTests(AdminWriteApiFixture fixture)
{
    private static readonly Regex PermissionCodeShape = new(
        @"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)*(?:\.[a-z][a-z0-9]*(?:_[a-z0-9]+)*){2,}\b",
        RegexOptions.Compiled);

    [Fact]
    public async Task 俱樂部範圍端點_角色缺該權限碼_403本文不含權限碼形狀()
    {
        // ① 對應 AdminClubAuthorizer.cs 的權限碼檢查分支（E-52 原始案例的其中一處）。
        // viewer@tcrfc.test 已被授權 tcrfc（能通過前三關），但角色沒有 content.article.create。
        using var client = await CreateClientAsync("viewer@tcrfc.test");

        var response = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/news",
            new { slug = "permission-leak-probe", categoryCode = "club", content = new { zh = new { title = "權限碼外洩探測" } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertNoLeakedPermissionCode(body, "content.article.create");
    }

    [Fact]
    public async Task 全域端點_角色缺該權限碼_403本文不含權限碼形狀()
    {
        // ② 對應 AdminSystemAuthorizer.cs 的權限碼檢查分支（E-52 原始案例的另一處）。
        // content.editor@tcrfc.test 只有 content.* 系列權限，system.account.create 是
        // sysadmin_only、且沒有指派給任何非超管角色。
        using var client = await CreateClientAsync("content.editor@tcrfc.test");

        var response = await client.PostAsJsonAsync("/api/v1/admin/accounts",
            new { username = "permission-leak-probe", displayName = "權限碼外洩探測", initialPassword = "Probe@12345", roleCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertNoLeakedPermissionCode(body, "system.account.create");
    }

    [Fact]
    public async Task 角色權限指派_送出查無資料的權限碼_400本文不含該權限碼()
    {
        // ③ 對應 AdminRolesRepository.ReplacePermissionsAsync 的「找不到權限碼」分支
        // （AdminRolesRepository.cs:166）——本輪發現並修正的既有落差，見
        // UserFacingMessageContentTests 類別上的說明。
        using var client = await CreateSuperAdminClientAsync();
        var code = $"probe_role_{Guid.NewGuid():N}"[..32];
        const string bogusPermissionCode = "no.such.permission";

        try
        {
            var role = await CreateRoleAsync(client, code);

            var response = await client.PutAsJsonAsync($"/api/v1/admin/roles/{role.Id}/permissions",
                new ReplaceRolePermissionsRequest
                {
                    Permissions = [new RolePermissionInput { PermissionCode = bogusPermissionCode, ScopeType = "all" }],
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            AssertNoLeakedPermissionCode(body, bogusPermissionCode);
        }
        finally
        {
            await DeleteRoleByCodeAsync(code);
        }
    }

    [Fact]
    public async Task 角色權限指派_送出sysadmin_only權限碼_400本文不含該權限碼()
    {
        // ④ 對應 AdminRoleSysadminOnlyPermissionException（AdminRoleExceptions.cs）——
        // 本輪發現並修正的既有落差。
        using var client = await CreateSuperAdminClientAsync();
        var code = $"probe_role_{Guid.NewGuid():N}"[..32];
        const string sysadminOnlyCode = "system.account.view";

        try
        {
            var role = await CreateRoleAsync(client, code);

            var response = await client.PutAsJsonAsync($"/api/v1/admin/roles/{role.Id}/permissions",
                new ReplaceRolePermissionsRequest
                {
                    Permissions = [new RolePermissionInput { PermissionCode = sysadminOnlyCode, ScopeType = "all" }],
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            AssertNoLeakedPermissionCode(body, sysadminOnlyCode);
        }
        finally
        {
            await DeleteRoleByCodeAsync(code);
        }
    }

    /// <summary>
    /// 兩層檢查：① 完全比對呼叫端明確知道會被拒絕的那一個真實權限碼字面值（最直接、零誤判的檢查）
    /// ② 一般化的「權限碼形狀」正則（三段小寫、句點分隔），防止訊息換了別的權限碼字面值一樣外洩。
    /// </summary>
    private static void AssertNoLeakedPermissionCode(string responseBody, string permissionCodeThatMustNotAppear)
    {
        Assert.DoesNotContain(permissionCodeThatMustNotAppear, responseBody);
        Assert.False(PermissionCodeShape.IsMatch(responseBody),
            $"回應本文疑似包含權限碼形狀的字串，介面不應顯示：{responseBody}");
    }

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> CreateSuperAdminClientAsync() => CreateClientAsync("super.admin@tcrfc.test");

    private static async Task<AdminRoleDetailDto> CreateRoleAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/roles", new CreateAdminRoleRequest
        {
            Code = code,
            NameZh = "權限碼外洩測試角色",
            ScopeMode = "all_clubs",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminRoleDetailDto>(TestJson.Options))!;
    }

    private static async Task DeleteRoleByCodeAsync(string code)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM admin_roles WHERE code = @Code";
        command.Parameters.AddWithValue("@Code", code);
        await command.ExecuteNonQueryAsync();
    }
}
