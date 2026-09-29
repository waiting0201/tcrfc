using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 2026-09-29（後台登入端點依 IP 限流，第二次修正）：專門給
/// <see cref="Tcrfc.Api.Tests.AdminAuthRateLimitingTests"/> 驗證「額度用盡後真的回 429」用的
/// 獨立 fixture——**不跟 <see cref="TestRateLimitOverrides.ApplyLooseAdminAuthOverrides"/>
/// 用的一般用途 fixture 共用**，因為那些 fixture 刻意把額度調得很寬鬆（讓一般測試不會被
/// 誤傷），沒辦法在裡面驗證「額度用盡後真的擋下來」這件事——這支 fixture 反過來，把
/// <c>admin-login</c>／<c>admin-refresh</c> 的額度都設成一個很小的數字（<see cref="TestPermitLimit"/>），
/// 讓測試不需要真的打上百次請求就能快速、可靠地驗證額度用盡後的行為。
///
/// 獨立成自己的 collection（<see cref="AdminAuthRateLimitTestCollection"/>），不跟任何其他測試檔
/// 共用——避免其他測試檔不小心也打中 <c>/login</c>／<c>/refresh</c>，把這個很小的額度提早用光。
///
/// ⚠️ 覆寫額度用 <see cref="ConfigureWebHost"/>＋<c>ConfigureAppConfiguration</c>，不是
/// <c>Environment.SetEnvironmentVariable</c>——這支 fixture 需要的覆寫值（很小的數字）跟一般用途
/// fixture 需要的覆寫值（很寬鬆的數字）**互斥**，如果兩邊都用行程全域環境變數覆寫，一旦初始化
/// 時機有任何重疊就會互相踩到彼此，這正是這支 fixture 存在的意義（驗證「額度用盡回 429」）反而
/// 最容易被這種競態波及的地方。完整理由與已實測驗證的框架行為見
/// <see cref="TestRateLimitOverrides"/> 檔頭。
/// </summary>
public sealed class AdminAuthRateLimitTestApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>刻意設一個很小的數字：夠大到能驗證「額度內都放行」，夠小到測試不需要迴圈跑很多次。</summary>
    public const int TestPermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        TestRateLimitOverrides.ApplyAdminAuthOverrides(builder, TestPermitLimit.ToString());
    }

    public async Task InitializeAsync()
    {
        var connectionString = await TestDatabaseGuard.ResolveAndVerifyAsync();

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);

        // 立刻建立測試主機，確保上面設定的環境變數在 Program.cs 執行的當下就是這個 fixture 要的值
        // （原因見 ApiFixture 的同一段註解）。
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
