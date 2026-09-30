using System.Net;
using Tcrfc.Api.Features.AdminSecurity;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>J3 稽核與備份的可查閱部分：帳號活動概況與登入異常提醒。🔴 操作稽核記錄與登入歷程沒有資料表（委託方指示，docs/12 §13.1），
/// 這裡只驗證「帳號目前的狀態」能回答什麼、以及回應如實說明沒有稽核記錄。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminSecurityTests(AdminWriteApiFixture fixture)
{
    private const string Url = "/api/v1/admin/security/overview";

    [Fact]
    public async Task 僅系統管理員_未登入401_客服與檢視者403()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task 概況_如實說明沒有稽核記錄_鎖定連續失敗久未登入從未登入各自告警_停用帳號不告警_不含密碼與2FA資料()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        const string locked = "lockout.test@tcrfc.test";
        const string failing = "clean.login@tcrfc.test";
        const string dormant = "viewer@tcrfc.test";
        const string never = "translator.never@tcrfc.test";
        var original = new Dictionary<string, (int Failed, DateTime? Locked, DateTime? LastLogin)>();
        foreach (var u in new[] { locked, failing, dormant })
        {
            original[u] = (
                await C1Test.ScalarAsync<int>("SELECT failed_attempt_count FROM admin_users WHERE username = @U", ("@U", u)),
                await C1Test.ScalarAsync<DateTime?>("SELECT locked_until FROM admin_users WHERE username = @U", ("@U", u)),
                await C1Test.ScalarAsync<DateTime?>("SELECT last_login_at FROM admin_users WHERE username = @U", ("@U", u)));
        }

        var neverId = Guid.NewGuid();
        try
        {
            await BizTest.ExecuteSqlAsync("UPDATE admin_users SET failed_attempt_count = 5, locked_until = DATEADD(minute, 20, SYSUTCDATETIME()) WHERE username = @U", ("@U", locked));
            await BizTest.ExecuteSqlAsync("UPDATE admin_users SET failed_attempt_count = 4, locked_until = NULL WHERE username = @U", ("@U", failing));
            await BizTest.ExecuteSqlAsync("UPDATE admin_users SET last_login_at = DATEADD(day, -200, SYSUTCDATETIME()) WHERE username = @U", ("@U", dormant));
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO admin_users (id, username, password_hash, display_name, status, is_super_admin, created_at, updated_at) VALUES (@I, @U, N'x', N'ZZTEST 從未登入', N'active', 0, DATEADD(day, -30, SYSUTCDATETIME()), SYSUTCDATETIME())",
                ("@I", neverId), ("@U", never));
            var suspended = Guid.NewGuid();
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO admin_users (id, username, password_hash, display_name, status, is_super_admin, created_at, updated_at) VALUES (@I, N'zztest.suspended@tcrfc.test', N'x', N'ZZTEST 停用', N'suspended', 0, DATEADD(day, -300, SYSUTCDATETIME()), SYSUTCDATETIME())",
                ("@I", suspended));

            var response = await admin.GetAsync(Url);
            var overview = await AppTest.ReadAsync<AdminSecurityOverviewDto>(response);
            Assert.False(overview.AuditTrailAvailable);
            Assert.Contains("沒有保存操作稽核記錄", overview.AuditTrailMessage);
            Assert.Contains(overview.Alerts, a => a.Kind == "locked" && a.Username == locked);
            Assert.Contains(overview.Alerts, a => a.Kind == "failed_attempts" && a.Username == failing);
            Assert.Contains(overview.Alerts, a => a.Kind == "dormant" && a.Username == dormant);
            Assert.Contains(overview.Alerts, a => a.Kind == "never_logged_in" && a.Username == never);
            Assert.DoesNotContain(overview.Alerts, a => a.Username == "zztest.suspended@tcrfc.test"); // 已停用的帳號不告警
            Assert.True(overview.LockedAccounts >= 1);
            Assert.True(overview.DormantAccounts >= 2);
            Assert.True(overview.Accounts.Single(a => a.Username == locked).IsLockedNow);
            Assert.Contains(overview.Accounts, a => a.Username == "zztest.suspended@tcrfc.test" && a.Status == "suspended");

            // 帳號密碼雜湊與雙因素密鑰從不外流
            var raw = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("twoFactorSecret", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("argon2", raw, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM admin_users WHERE username IN (@A, N'zztest.suspended@tcrfc.test')", ("@A", never));
            foreach (var (u, o) in original)
            {
                await BizTest.ExecuteSqlAsync("UPDATE admin_users SET failed_attempt_count = @F, locked_until = @L, last_login_at = @T WHERE username = @U",
                    ("@F", o.Failed), ("@L", o.Locked), ("@T", o.LastLogin), ("@U", u));
            }
        }
    }
}
