using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善後台帳號與角色管理（規劃書 §10）：比照主站 AdminAccountsTests／AdminRolesTests。
/// 🔴 測試帳號一律 <c>ct-*@charity-test.invalid</c>、測試角色 <c>ctrole_*</c>，由 <see cref="CharityApiFixture.CleanupAsync"/> 依標記清掉，絕不碰種子。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminAccessTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Accounts = AdminBase + "/accounts";
    private const string Roles = AdminBase + "/roles";
    private const string Login = AdminBase + "/auth/login";
    private const string NewPassword = "NewPassword-123";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static string NewUsername(string tag = "acct") => $"ct-{tag}{Guid.NewGuid():N}@{CharityApiFixture.TestEmailDomain}";

    private static string NewRoleCode() => $"ctrole_{Guid.NewGuid():N}"[..20];

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient c, string url, object body)
        => c.PostAsJsonAsync(url, body, TestJson.WriteOptions);

    private async Task<CharityAdminAccountDetailDto> CreateAccountAsync(
        HttpClient sys, string? username = null, string password = "InitialPass-123", bool superAdmin = false, params string[] roles)
        => await ReadAsync<CharityAdminAccountDetailDto>(await PostAsync(sys, Accounts, new
        {
            username = username ?? NewUsername(), displayName = "測試帳號", initialPassword = password, isSuperAdmin = superAdmin, roleCodes = roles,
        }));

    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString() ?? string.Empty;

    private async Task<HttpResponseMessage> LoginAsync(string username, string password)
    {
        using var client = fx.CreateClient();
        return await client.PostAsJsonAsync(Login, new LoginRequest(username, password, null), TestJson.WriteOptions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 授權
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 未登入_帳號與角色端點全部擋下_401()
    {
        using var anonymous = fx.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Accounts)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Roles)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Roles}/permissions")).StatusCode);
    }

    [Fact]
    public async Task 非系統管理員_即使是客服行政角色_帳號與角色端點一律403()
    {
        var cs = await fx.CreateAdminAsync(false, "customer_service_admin");
        using var client = fx.CreateClientFor(cs);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Accounts)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Roles)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, Accounts, new { username = NewUsername(), displayName = "x", initialPassword = "InitialPass-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, Roles, new { code = NewRoleCode(), nameZh = "x" })).StatusCode);
    }

    [Fact]
    public async Task 四個帳號角色權限碼全為sysadmin_only_且種子只掛給系統管理員角色()
    {
        using var sys = await SysAsync();
        var permissions = await ReadAsync<List<CharityAdminPermissionDto>>(await sys.GetAsync($"{Roles}/permissions"));
        var access = permissions.Where(p => p.Code.StartsWith("n7.admin_", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, access.Count);
        Assert.All(access, p => Assert.True(p.SysadminOnly));
        Assert.All(permissions, p => Assert.False(p.AppliesToClub));
        Assert.Contains("\"isClubScoped\":false", await (await sys.GetAsync($"{Roles}/permissions")).Content.ReadAsStringAsync());

        Assert.Equal(0, await fx.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id JOIN admin_roles r ON r.id = rp.admin_role_id
            WHERE p.code LIKE N'n7.admin[_]%' AND r.code <> N'system_admin'
            """));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 帳號
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 建立帳號_中文帳號可用_不回雜湊_預設不需改密碼_寫稽核且摘要無密碼()
    {
        var actor = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(actor);
        var username = $"ct-測試帳號{Guid.NewGuid():N}@{CharityApiFixture.TestEmailDomain}";

        var response = await PostAsync(sys, Accounts, new
        {
            username = $"  {username}  ", displayName = "王小明", email = "wang@charity-test.invalid", initialPassword = "InitialPass-123", roleCodes = new[] { "viewer" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("twoFactorSecret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InitialPass-123", raw);

        var created = JsonSerializer.Deserialize<CharityAdminAccountDetailDto>(raw, TestJson.Options)!;
        Assert.Equal(username, created.Username);      // 前後空白已修剪
        Assert.False(created.MustChangePassword);
        Assert.False(created.TwoFactorEnabled);
        Assert.False(created.IsSuperAdmin);
        Assert.Equal("active", created.Status);
        Assert.Equal(["viewer"], created.RoleCodes);
        Assert.Empty(created.ClubGrants);
        Assert.Empty(created.TeamGrants);
        Assert.Null(created.PrimaryClubId);

        // 新帳號可以直接登入
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username, "InitialPass-123")).StatusCode);

        // 稽核：同一次提交寫入，且不含密碼
        var summary = await fx.ScalarAsync<string>(
            "SELECT change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'admin_account.create' AND target_id = @t",
            ("@a", actor.Id), ("@t", created.Id));
        Assert.NotNull(summary);
        Assert.DoesNotContain("InitialPass", summary);
    }

    [Fact]
    public async Task 建立帳號_密碼下限九字元_重複帳號409_空白與過長帳號400_未知角色400()
    {
        using var sys = await SysAsync();

        var tooShort = await PostAsync(sys, Accounts, new { username = NewUsername(), displayName = "x", initialPassword = "12345678" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Contains("9", await ProblemDetailAsync(tooShort));

        var nine = await PostAsync(sys, Accounts, new { username = NewUsername(), displayName = "x", initialPassword = "123456789" });
        Assert.Equal(HttpStatusCode.Created, nine.StatusCode);

        var username = NewUsername();
        await CreateAccountAsync(sys, username);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(sys, Accounts, new { username, displayName = "x", initialPassword = "InitialPass-123" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Accounts, new { username = "含 空白", displayName = "x", initialPassword = "InitialPass-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Accounts, new { username = new string('a', 65), displayName = "x", initialPassword = "InitialPass-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Accounts, new { username = NewUsername(), displayName = "x", initialPassword = "InitialPass-123", roleCodes = new[] { "no_such_role" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Accounts, new { username = "ct-same", displayName = "x", initialPassword = "ct-same" })).StatusCode); // 密碼不得與帳號相同（且長度不足亦同樣 400）
    }

    [Fact]
    public async Task 列表與詳情_分頁關鍵字狀態篩選_不含任何雜湊欄位()
    {
        using var sys = await SysAsync();
        var marker = $"CTKW{Guid.NewGuid():N}"[..16];
        var a = await CreateAccountAsync(sys, $"ct-{marker}-a@{CharityApiFixture.TestEmailDomain}");
        await CreateAccountAsync(sys, $"ct-{marker}-b@{CharityApiFixture.TestEmailDomain}");

        var raw = await (await sys.GetAsync($"{Accounts}?keyword={marker}&pageSize=1&page=1")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        var page = JsonSerializer.Deserialize<PagedResult<CharityAdminAccountListItemDto>>(raw, TestJson.Options)!;
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);

        await PostAsync(sys, $"{Accounts}/{a.Id}/status", new { status = "disabled" });
        var disabled = await ReadAsync<PagedResult<CharityAdminAccountListItemDto>>(await sys.GetAsync($"{Accounts}?keyword={marker}&status=disabled"));
        Assert.Equal(a.Id, Assert.Single(disabled.Items).Id);

        Assert.Equal(HttpStatusCode.OK, (await sys.GetAsync($"{Accounts}/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.GetAsync($"{Accounts}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task 更新帳號_換角色立即影響權限_指派的角色權限生效()
    {
        using var sys = await SysAsync();
        var storeView = (await ReadAsync<List<CharityAdminPermissionDto>>(await sys.GetAsync($"{Roles}/permissions"))).Single(p => p.Code == "n1.donation_store.view");
        var role = await ReadAsync<CharityAdminRoleDetailDto>(await PostAsync(sys, Roles, new { code = NewRoleCode(), nameZh = "測試店家檢視" }));
        await sys.PutAsJsonAsync($"{Roles}/{role.Id}/permissions", new { permissions = new[] { new { permissionCode = storeView.Code, scopeType = "all" } } }, TestJson.WriteOptions);

        var username = NewUsername();
        var account = await CreateAccountAsync(sys, username);
        using var asNew = fx.CreateClientFor(new CharityTestAdmin(account.Id, username, false, CharityApiFixture.IssueToken(account.Id, username, false)));
        Assert.Equal(HttpStatusCode.Forbidden, (await asNew.GetAsync($"{AdminBase}/stores")).StatusCode);

        var updated = await ReadAsync<CharityAdminAccountDetailDto>(await sys.PutAsJsonAsync($"{Accounts}/{account.Id}",
            new { displayName = "改名後", email = "", isSuperAdmin = false, roleCodes = new[] { role.Code } }, TestJson.WriteOptions));
        Assert.Equal("改名後", updated.DisplayName);
        Assert.Null(updated.Email);
        Assert.Equal([role.Code], updated.RoleCodes);
        Assert.Equal(HttpStatusCode.OK, (await asNew.GetAsync($"{AdminBase}/stores")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Accounts}/{Guid.NewGuid()}",
            new { displayName = "x", roleCodes = Array.Empty<string>() }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 停用帳號_登入被擋_既有更新權杖立即撤銷_重新啟用後可登入()
    {
        using var sys = await SysAsync();
        var username = NewUsername();
        var account = await CreateAccountAsync(sys, username);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username, "InitialPass-123")).StatusCode);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM admin_refresh_tokens WHERE admin_user_id = @i AND revoked_at IS NULL", ("@i", account.Id)));

        var disabled = await ReadAsync<CharityAdminAccountDetailDto>(await PostAsync(sys, $"{Accounts}/{account.Id}/status", new { status = "disabled" }));
        Assert.Equal("disabled", disabled.Status);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM admin_refresh_tokens WHERE admin_user_id = @i AND revoked_at IS NULL", ("@i", account.Id)));
        Assert.NotEqual(HttpStatusCode.OK, (await LoginAsync(username, "InitialPass-123")).StatusCode);

        await PostAsync(sys, $"{Accounts}/{account.Id}/status", new { status = "active" });
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username, "InitialPass-123")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, $"{Accounts}/{account.Id}/status", new { status = "banana" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(sys, $"{Accounts}/{Guid.NewGuid()}/status", new { status = "disabled" })).StatusCode);
    }

    [Fact]
    public async Task 重設密碼_舊密碼失效_新密碼可登入_標記待改密碼_清鎖定_密碼政策把關()
    {
        var actor = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(actor);
        var username = NewUsername();
        var account = await CreateAccountAsync(sys, username);
        await LoginAsync(username, "InitialPass-123");
        await fx.ExecuteAsync("UPDATE admin_users SET failed_attempt_count = 4, locked_until = DATEADD(HOUR, 1, SYSUTCDATETIME()) WHERE id = @i", ("@i", account.Id));

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, $"{Accounts}/{account.Id}/reset-password", new { newPassword = "12345678" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(sys, $"{Accounts}/{account.Id}/reset-password", new { newPassword = NewPassword })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(username, "InitialPass-123")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username, NewPassword)).StatusCode);
        var detail = await ReadAsync<CharityAdminAccountDetailDto>(await sys.GetAsync($"{Accounts}/{account.Id}"));
        Assert.True(detail.MustChangePassword);

        var summary = await fx.ScalarAsync<string>("SELECT change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'admin_account.reset_password' AND target_id = @t", ("@a", actor.Id), ("@t", account.Id));
        Assert.NotNull(summary);
        Assert.DoesNotContain(NewPassword, summary);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(sys, $"{Accounts}/{Guid.NewGuid()}/reset-password", new { newPassword = NewPassword })).StatusCode);
    }

    [Fact]
    public async Task 重設兩階段驗證_清空密鑰並撤銷工作階段()
    {
        using var sys = await SysAsync();
        var account = await CreateAccountAsync(sys);
        await fx.ExecuteAsync("UPDATE admin_users SET two_factor_enabled = 1, two_factor_secret_encrypted = N'x', two_factor_confirmed_at = SYSUTCDATETIME() WHERE id = @i", ("@i", account.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(sys, $"{Accounts}/{account.Id}/reset-totp", new { })).StatusCode);
        Assert.False(await fx.ScalarAsync<bool>("SELECT two_factor_enabled FROM admin_users WHERE id = @i", ("@i", account.Id)));
        Assert.Null(await fx.ScalarAsync<string>("SELECT two_factor_secret_encrypted FROM admin_users WHERE id = @i", ("@i", account.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(sys, $"{Accounts}/{Guid.NewGuid()}/reset-totp", new { })).StatusCode);
    }

    [Fact]
    public async Task 不能讓最後一位啟用中的系統管理員被停用或降級_包含自己()
    {
        // 暫時停用其他所有啟用中的系統管理員，讓測試用的這位成為唯一一位（結束後原樣還原）。
        var actor = await fx.CreateAdminAsync(true);
        var others = new List<Guid>();
        await using (var scratch = new Microsoft.Data.SqlClient.SqlConnection(fx.ConnectionString))
        {
            await scratch.OpenAsync();
            await using var read = scratch.CreateCommand();
            read.CommandText = "SELECT id FROM admin_users WHERE is_super_admin = 1 AND status = N'active' AND id <> @a";
            read.Parameters.AddWithValue("@a", actor.Id);
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                others.Add(reader.GetGuid(0));
            }
        }

        try
        {
            foreach (var id in others)
            {
                await fx.ExecuteAsync("UPDATE admin_users SET status = N'disabled' WHERE id = @i", ("@i", id));
            }

            using var sys = fx.CreateClientFor(actor);
            var disable = await PostAsync(sys, $"{Accounts}/{actor.Id}/status", new { status = "disabled" });
            Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
            var demote = await sys.PutAsJsonAsync($"{Accounts}/{actor.Id}", new { displayName = "x", isSuperAdmin = false, roleCodes = Array.Empty<string>() }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
            Assert.True(await fx.ScalarAsync<bool>("SELECT is_super_admin FROM admin_users WHERE id = @i", ("@i", actor.Id)));

            // 多一位啟用中的系統管理員後，同樣操作就放行
            var second = await CreateAccountAsync(sys, superAdmin: true);
            Assert.Equal(HttpStatusCode.OK, (await PostAsync(sys, $"{Accounts}/{second.Id}/status", new { status = "disabled" })).StatusCode); // 停用的是第二位，仍剩自己
        }
        finally
        {
            foreach (var id in others)
            {
                await fx.ExecuteAsync("UPDATE admin_users SET status = N'active' WHERE id = @i", ("@i", id));
            }
        }
    }

    [Fact]
    public async Task 沒有刪除帳號的端點_DELETE一律405或404()
    {
        using var sys = await SysAsync();
        var account = await CreateAccountAsync(sys);
        var response = await sys.DeleteAsync($"{Accounts}/{account.Id}");
        Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM admin_users WHERE id = @i", ("@i", account.Id)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 角色
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 角色列表_含九個種子系統角色_形狀與主站相同()
    {
        using var sys = await SysAsync();
        var roles = await ReadAsync<List<CharityAdminRoleListItemDto>>(await sys.GetAsync(Roles));
        Assert.True(roles.Count(r => r.IsSystem) >= 9);
        Assert.Contains(roles, r => r.Code == "system_admin" && r.IsSystem);
        Assert.All(roles, r => Assert.Equal("all_clubs", r.ScopeMode));

        var detail = await ReadAsync<CharityAdminRoleDetailDto>(await sys.GetAsync($"{Roles}/{roles.Single(r => r.Code == "viewer").Id}"));
        Assert.Contains(detail.Permissions, p => p.PermissionCode == "n3.donation.view" && p.ScopeType == "masked");
        Assert.Contains(detail.Permissions, p => p.PermissionCode == "n6.report.view" && p.ScopeType == "all"); // 種子 scope_type 為 NULL → 回 all
        Assert.Equal(HttpStatusCode.NotFound, (await sys.GetAsync($"{Roles}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task 建立角色_自訂角色可刪_代碼格式與重複把關_寫稽核()
    {
        var actor = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(actor);
        var code = NewRoleCode();

        var create = await PostAsync(sys, Roles, new { code, nameZh = "自訂角色", nameEn = "Custom", scopeMode = "own_clubs" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var role = (await create.Content.ReadFromJsonAsync<CharityAdminRoleDetailDto>(TestJson.Options))!;
        Assert.False(role.IsSystem);
        Assert.Empty(role.Permissions);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'admin_role.create' AND target_id = @t", ("@a", actor.Id), ("@t", role.Id)));

        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(sys, Roles, new { code, nameZh = "重複" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Roles, new { code = "Has-Upper", nameZh = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(sys, Roles, new { code = "", nameZh = "x" })).StatusCode);

        var renamed = await ReadAsync<CharityAdminRoleDetailDto>(await sys.PutAsJsonAsync($"{Roles}/{role.Id}", new { nameZh = "改名", nameEn = (string?)null }, TestJson.WriteOptions));
        Assert.Equal("改名", renamed.NameZh);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Roles}/{Guid.NewGuid()}", new { nameZh = "x" }, TestJson.WriteOptions)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await sys.DeleteAsync($"{Roles}/{role.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.GetAsync($"{Roles}/{role.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.DeleteAsync($"{Roles}/{role.Id}")).StatusCode);
    }

    [Fact]
    public async Task 刪除角色_系統角色403_仍有帳號指派409()
    {
        using var sys = await SysAsync();
        var roles = await ReadAsync<List<CharityAdminRoleListItemDto>>(await sys.GetAsync(Roles));
        Assert.Equal(HttpStatusCode.Forbidden, (await sys.DeleteAsync($"{Roles}/{roles.First(r => r.IsSystem).Id}")).StatusCode);

        var role = await ReadAsync<CharityAdminRoleDetailDto>(await PostAsync(sys, Roles, new { code = NewRoleCode(), nameZh = "使用中" }));
        var account = await CreateAccountAsync(sys, null, "InitialPass-123", false, role.Code);
        var inUse = await sys.DeleteAsync($"{Roles}/{role.Id}");
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        Assert.Contains("1", await ProblemDetailAsync(inUse));
        Assert.Equal(1, (await ReadAsync<CharityAdminRoleDetailDto>(await sys.GetAsync($"{Roles}/{role.Id}"))).AssignedAccountCount);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public async Task 取代角色權限_整份換掉_sysadmin_only與未知碼與不合法範圍一律400_不洩權限碼()
    {
        var actor = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(actor);
        var role = await ReadAsync<CharityAdminRoleDetailDto>(await PostAsync(sys, Roles, new { code = NewRoleCode(), nameZh = "權限測試" }));
        var url = $"{Roles}/{role.Id}/permissions";

        var first = await ReadAsync<CharityAdminRoleDetailDto>(await sys.PutAsJsonAsync(url, new
        {
            permissions = new[] { new { permissionCode = "n1.donation_store.view", scopeType = "all" }, new { permissionCode = "n6.report.view", scopeType = "masked" } },
        }, TestJson.WriteOptions));
        Assert.Equal(2, first.Permissions.Count);

        var second = await ReadAsync<CharityAdminRoleDetailDto>(await sys.PutAsJsonAsync(url, new { permissions = new[] { new { permissionCode = "n2.donation_project.view", scopeType = "all" } } }, TestJson.WriteOptions));
        Assert.Equal(["n2.donation_project.view"], second.Permissions.Select(p => p.PermissionCode));

        var sysOnly = await sys.PutAsJsonAsync(url, new { permissions = new[] { new { permissionCode = "n7.admin_account.manage", scopeType = "all" } } }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, sysOnly.StatusCode);
        Assert.DoesNotContain("n7.", await ProblemDetailAsync(sysOnly));

        var unknown = await sys.PutAsJsonAsync(url, new { permissions = new[] { new { permissionCode = "no.such.code", scopeType = "all" } } }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.DoesNotContain("no.such.code", await ProblemDetailAsync(unknown));

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync(url, new { permissions = new[] { new { permissionCode = "n1.donation_store.view", scopeType = "bogus" } } }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Roles}/{Guid.NewGuid()}/permissions", new { permissions = Array.Empty<object>() }, TestJson.WriteOptions)).StatusCode);

        // 失敗的請求不改動既有指派
        Assert.Single((await ReadAsync<CharityAdminRoleDetailDto>(await sys.GetAsync($"{Roles}/{role.Id}"))).Permissions);

        var auditSummary = await fx.ScalarAsync<string>("SELECT TOP 1 change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'admin_role.permissions' AND target_id = @t ORDER BY occurred_at DESC", ("@a", actor.Id), ("@t", role.Id));
        Assert.DoesNotContain("n2.", auditSummary);
    }

    [Fact]
    public async Task 取代系統管理員角色權限_不動到sysadmin_only的既有指派()
    {
        using var sys = await SysAsync();
        var admin = (await ReadAsync<List<CharityAdminRoleListItemDto>>(await sys.GetAsync(Roles))).Single(r => r.Code == "system_admin");
        var before = await fx.ScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE rp.admin_role_id = @r AND p.sysadmin_only = 1", ("@r", admin.Id));
        Assert.True(before >= 4);

        var snapshot = new List<(Guid PermissionId, string? ScopeType)>();
        await using (var c = new Microsoft.Data.SqlClient.SqlConnection(fx.ConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT rp.permission_id, rp.scope_type FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE rp.admin_role_id = @r AND p.sysadmin_only = 0";
            cmd.Parameters.AddWithValue("@r", admin.Id);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snapshot.Add((reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
            }
        }

        try
        {
            await ReadAsync<CharityAdminRoleDetailDto>(await sys.PutAsJsonAsync($"{Roles}/{admin.Id}/permissions", new { permissions = Array.Empty<object>() }, TestJson.WriteOptions));
            Assert.Equal(before, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE rp.admin_role_id = @r AND p.sysadmin_only = 1", ("@r", admin.Id)));
        }
        finally
        {
            // 還原：把被清掉的非 sysadmin_only 指派原樣放回（共用種子，絕不讓測試吃掉）
            await fx.ExecuteAsync("DELETE rp FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE rp.admin_role_id = @r AND p.sysadmin_only = 0", ("@r", admin.Id));
            foreach (var (permissionId, scopeType) in snapshot)
            {
                await fx.ExecuteAsync("INSERT INTO role_permissions (admin_role_id, permission_id, scope_type) VALUES (@r, @p, @s)",
                    ("@r", admin.Id), ("@p", permissionId), ("@s", (object?)scopeType ?? DBNull.Value));
            }
        }
    }
}
