using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-18c（2026-09-29，補齊公開寫入端點的限流缺口）：直接對
/// <see cref="PublicRateLimitPolicies"/> 實際設定的數值建一個
/// <see cref="PartitionedRateLimiter{TResource}"/>，驗證兩件事：① 額度用盡後下一次請求會被拒絕
/// （對應 HTTP 層的 429）；② 不同分區鍵（對應不同訪客 IP）互不影響，其中一個用盡額度不會擋到
/// 另一個。
///
/// 🔴 刻意不透過 <c>WebApplicationFactory</c> 打 HTTP 驗證「不同 IP 分區獨立」這件事——
/// <c>TestServer</c> 底下 <c>HttpContext.Connection.RemoteIpAddress</c> 恆為 <c>null</c>
/// （<c>TrustedProxyConfigurationTests.cs</c>、<c>AdminFormsEnquiriesTests.cs</c> 都記過同一個
/// 事實），代表透過真正 HTTP 請求送進來的所有測試呼叫，<c>ClientIpResolver.Resolve</c> 一律解析
/// 成同一個「unknown」分區鍵，沒有辦法在那個環境下產生兩個不同的分區鍵來驗證「互相獨立」。
/// 這裡改成直接對 <c>System.Threading.RateLimiting</c> 的公開型別餵兩個不同的字串分區鍵
/// （模擬兩個不同訪客 IP），繞開這個環境限制，同時因為讀的是
/// <see cref="PublicRateLimitPolicies"/> 的同一組常數，能確保驗證的是「本專案實際註冊的設定值」
/// 而不是另外編造的數字。
///
/// 「額度用盡後 429」這件事的 HTTP 層真正生效與否（<c>Program.cs</c> 有沒有把政策掛到正確的
/// 路由上），由 <see cref="PublicWriteEndpointRateLimitingTests"/> 走真正的
/// <c>WebApplicationFactory</c> 驗證，兩支測試分工不同，互相補足彼此的涵蓋邊界。
/// </summary>
public sealed class PublicRateLimitPoliciesTests
{
    [Fact]
    public async Task LightInteraction政策_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(
            PublicRateLimitPolicies.LightInteractionPermitLimit,
            PublicRateLimitPolicies.LightInteractionWindow);

        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, PublicRateLimitPolicies.LightInteractionPermitLimit, ipA: "203.0.113.10", ipB: "203.0.113.20");
    }

    [Fact]
    public async Task Submission政策_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(
            PublicRateLimitPolicies.SubmissionPermitLimit,
            PublicRateLimitPolicies.SubmissionWindow);

        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, PublicRateLimitPolicies.SubmissionPermitLimit, ipA: "198.51.100.10", ipB: "198.51.100.20");
    }

    [Fact]
    public async Task Search政策_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(PublicRateLimitPolicies.SearchPermitLimit, PublicRateLimitPolicies.SearchWindow);
        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, PublicRateLimitPolicies.SearchPermitLimit, ipA: "203.0.113.30", ipB: "203.0.113.40");
    }

    [Fact]
    public async Task Newsletter政策_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(PublicRateLimitPolicies.NewsletterPermitLimit, PublicRateLimitPolicies.NewsletterWindow);
        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, PublicRateLimitPolicies.NewsletterPermitLimit, ipA: "198.51.100.30", ipB: "198.51.100.40");
    }

    [Fact]
    public async Task TrialRegistration政策_額度用盡後拒絕_不同分區互不影響()
    {
        using var limiter = CreateFixedWindowLimiter(PublicRateLimitPolicies.TrialRegistrationPermitLimit, PublicRateLimitPolicies.TrialRegistrationWindow);
        await AssertPermitLimitEnforcedAndPartitionsIndependentAsync(
            limiter, PublicRateLimitPolicies.TrialRegistrationPermitLimit, ipA: "192.0.2.30", ipB: "192.0.2.40");
    }

    [Theory]
    [InlineData(PublicRateLimitPolicies.SearchPermitLimitConfigKey, PublicRateLimitPolicies.SearchPermitLimit)]
    [InlineData(PublicRateLimitPolicies.NewsletterPermitLimitConfigKey, PublicRateLimitPolicies.NewsletterPermitLimit)]
    [InlineData(PublicRateLimitPolicies.TrialRegistrationPermitLimitConfigKey, PublicRateLimitPolicies.TrialRegistrationPermitLimit)]
    public void 額度可由設定覆寫_缺值或非法值回到預設(string key, int expectedDefault)
    {
        var empty = new ConfigurationBuilder().Build();
        var overridden = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "777" }).Build();
        var invalid = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "-3" }).Build();

        int Resolve(IConfiguration c) => key switch
        {
            PublicRateLimitPolicies.SearchPermitLimitConfigKey => PublicRateLimitPolicies.ResolveSearchPermitLimit(c),
            PublicRateLimitPolicies.NewsletterPermitLimitConfigKey => PublicRateLimitPolicies.ResolveNewsletterPermitLimit(c),
            _ => PublicRateLimitPolicies.ResolveTrialRegistrationPermitLimit(c),
        };

        Assert.Equal(expectedDefault, Resolve(empty));
        Assert.Equal(777, Resolve(overridden));
        Assert.Equal(expectedDefault, Resolve(invalid));
    }

    /// <summary>兩個政策目前用的都是 <c>FixedWindowRateLimiter</c>＋<c>QueueLimit = 0</c>——跟
    /// <c>Program.cs</c> 實際註冊時的寫法逐字對應，見 <c>Program.cs</c>「S1-18c」段與
    /// <c>PublicRateLimitPolicies</c> 檔頭說明。</summary>
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

        // 不同分區鍵（模擬不同訪客 IP）互不影響：即使 ipA 已用盡額度，ipB 仍應該有自己完整的額度。
        using var otherPartitionFirstRequest = await limiter.AcquireAsync(ipB);
        Assert.True(otherPartitionFirstRequest.IsAcquired,
            $"{ipB} 是獨立分區，不應該被 {ipA} 用盡額度這件事影響。");
    }
}
