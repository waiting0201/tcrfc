using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// `AdminNews`／`AdminAuth` 相關測試共用的 fixture。⚠️ 不要把這個 fixture 拿去給既有的五組
/// 唯讀測試共用——分開是為了讓「純唯讀端點」與「需要登入與授權的端點」各自的測試資料互不干擾，
/// 不是因為有任何開發模式旗標需要區分（那個機制已於 2026-09-23 移除，見 apps/api/README.md「S1」）。
/// </summary>
public sealed class AdminWriteApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 2026-09-29：admin-login／admin-refresh 兩個政策的正式環境預設額度改成嚴格數字後，一般
    // 用途 fixture 一律要覆寫成寬鬆值——這個 fixture 尤其重要，AdminAuthTests／
    // AdminAccountsTests 真的會打 HTTP /login／/refresh。改用 ConfigureAppConfiguration
    // （只影響這一個測試主機自己的 IConfiguration），不是 Environment.SetEnvironmentVariable
    // （行程全域狀態），完整理由見 TestRateLimitOverrides 檔頭。
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        TestRateLimitOverrides.ApplyLooseAdminAuthOverrides(builder);
    }

    public async Task InitializeAsync()
    {
        var connectionString = await TestDatabaseGuard.ResolveAndVerifyAsync();

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);

        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        // 保險：即使個別測試忘記清自己建立的資料，至少不留任何殘留的旗標狀態影響到行程本身
        // （行程本身即將被回收，這裡主要是文件用途，說明「這個 fixture 的生命週期結束＝這組
        // 開關的效力也結束」）。
        await base.DisposeAsync();
    }
}
