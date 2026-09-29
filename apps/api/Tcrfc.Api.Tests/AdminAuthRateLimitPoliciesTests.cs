using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.Features.AdminAuth;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 2026-09-29（後台登入端點依 IP 限流，修正版）：驗證兩件事——
/// ① <see cref="AdminAuthRateLimitOptions"/> 在「沒有任何環境變數覆寫」時，解析出來的額度就是
/// 正式環境應有的嚴格預設值（登入每 IP 每分鐘 5 次、更新權杖每 IP 每分鐘 30 次），不是被測試
/// 用量放寬過的數字——這是回應「不要讓測試用量決定正式環境的安全額度」這個回饋修正的核心驗證。
/// ② 對這組（無論是預設值還是被覆寫的值）數值建一個真正的
/// <see cref="PartitionedRateLimiter{TResource}"/>，額度用盡後拒絕、不同分區鍵互不影響
/// （跟 <see cref="PublicRateLimitPoliciesTests"/> 同一種寫法）。
///
/// 「有沒有真的接上 <c>/login</c>／<c>/refresh</c> 路由」＋「額度用盡後 429 真的生效」的 HTTP 層
/// 驗證見 <see cref="AdminAuthRateLimitingTests"/>，三支測試分工不同、互相補足。
/// </summary>
public sealed class AdminAuthRateLimitPoliciesTests
{
    [Fact]
    public void 未設定任何環境變數時_登入額度預設值是嚴格的個位數量級()
    {
        var emptyConfiguration = new ConfigurationBuilder().Build();

        var resolved = AdminAuthRateLimitOptions.ResolveLoginPermitLimit(emptyConfiguration);

        Assert.Equal(AdminAuthRateLimitOptions.LoginPermitLimitDefault, resolved);
        Assert.True(resolved is > 0 and <= 9,
            $"登入端點的正式環境預設額度應該是每分鐘個位數量級（實際解析出 {resolved}）" +
            "——這條斷言直接寫死上限 9，不是只比對常數，避免哪天有人把常數本身改壞也沒人發現。");
    }

    [Fact]
    public void 未設定任何環境變數時_更新權杖額度預設值等於程式碼常數()
    {
        var emptyConfiguration = new ConfigurationBuilder().Build();

        var resolved = AdminAuthRateLimitOptions.ResolveRefreshPermitLimit(emptyConfiguration);

        Assert.Equal(AdminAuthRateLimitOptions.RefreshPermitLimitDefault, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public void 設定值壞掉時_退回嚴格預設值_不會悄悄變成不限流(string invalidValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminAuthRateLimitOptions.LoginPermitLimitConfigKey] = invalidValue,
            })
            .Build();

        var resolved = AdminAuthRateLimitOptions.ResolveLoginPermitLimit(configuration);

        Assert.Equal(AdminAuthRateLimitOptions.LoginPermitLimitDefault, resolved);
    }

    [Fact]
    public void 設定合法正整數時_採用覆寫值_不是預設值()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminAuthRateLimitOptions.LoginPermitLimitConfigKey] = "12345",
            })
            .Build();

        var resolved = AdminAuthRateLimitOptions.ResolveLoginPermitLimit(configuration);

        Assert.Equal(12345, resolved);
    }

    [Fact]
    public async Task Login政策_以預設額度_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(
            AdminAuthRateLimitOptions.LoginPermitLimitDefault, AdminAuthRateLimitOptions.LoginWindow);

        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, AdminAuthRateLimitOptions.LoginPermitLimitDefault, ipA: "203.0.113.30", ipB: "203.0.113.40");
    }

    [Fact]
    public async Task Refresh政策_以預設額度_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(
            AdminAuthRateLimitOptions.RefreshPermitLimitDefault, AdminAuthRateLimitOptions.RefreshWindow);

        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, AdminAuthRateLimitOptions.RefreshPermitLimitDefault, ipA: "198.51.100.30", ipB: "198.51.100.40");
    }

    /// <summary>兩個政策都是 <c>FixedWindowRateLimiter</c>＋<c>QueueLimit = 0</c>——跟
    /// <c>Program.cs</c> 實際註冊時的寫法逐字對應。</summary>
    private static PartitionedRateLimiter<string> CreateFixedWindowLimiter(int permitLimit, TimeSpan window)
        => PartitionedRateLimiter.Create<string, string>(partitionKey =>
            RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            }));

    private static async Task AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
        PartitionedRateLimiter<string> limiter, int permitLimit, string ipA, string ipB)
    {
        for (var i = 1; i <= permitLimit; i++)
        {
            using var lease = await limiter.AcquireAsync(ipA);
            Assert.True(lease.IsAcquired, $"{ipA} 第 {i} 次請求應該在額度（{permitLimit}）內被允許。");
        }

        using var exceeded = await limiter.AcquireAsync(ipA);
        Assert.False(exceeded.IsAcquired, $"{ipA} 超過額度（{permitLimit}）後應該被拒絕，對應 HTTP 層的 429。");

        using var otherPartitionFirstRequest = await limiter.AcquireAsync(ipB);
        Assert.True(otherPartitionFirstRequest.IsAcquired,
            $"{ipB} 是獨立分區，不應該被 {ipA} 用盡額度這件事影響。");
    }
}
