using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 2026-09-29（後台登入端點依 IP 限流，修正版）：走真正的 <c>WebApplicationFactory</c> HTTP 管線，
/// 驗證 <c>Program.cs</c> 確實把 <see cref="AdminAuthEndpoints.LoginRateLimitPolicyName"/>／
/// <see cref="AdminAuthEndpoints.RefreshRateLimitPolicyName"/> 兩個政策掛到 <c>/login</c>／
/// <c>/refresh</c> 路由上——跟 <see cref="PublicWriteEndpointRateLimitingTests"/> 分工相同
/// （「政策本身」的行為已由 <see cref="AdminAuthRateLimitPoliciesTests"/> 驗過，這裡補的是
/// 「有沒有真的接上」）。
///
/// 🔴 **用專屬的 <see cref="AdminAuthRateLimitTestApiFixture"/>（<see cref="AdminAuthRateLimitTestCollection"/>），
/// 不跟其他任何測試共用**：`admin-login`／`admin-refresh` 的正式環境預設額度已經改成嚴格數字
/// （每 IP 每分鐘 5／30，見 <see cref="AdminAuthRateLimitOptions"/>），一般用途的 fixture
/// （<see cref="ApiFixture"/>／<see cref="AdminWriteApiFixture"/> 等）都已經覆寫成寬鬆值
/// （<see cref="TestRateLimitOverrides"/>），刻意讓一般測試不會被誤傷——但這代表沒辦法在那些
/// fixture 上驗證「額度用盡後真的回 429」這件事。這支測試需要的是**相反的**設定：一個很小、
/// 專門設來被打爆的額度（<see cref="AdminAuthRateLimitTestApiFixture.TestPermitLimit"/>），
/// 兩種需求互斥，所以獨立成自己的 fixture／collection，不共用。
///
/// 呼叫時刻意用不存在的帳號／不帶更新權杖 Cookie：限流中介軟體掛在路由層，會在進到 handler、
/// 查資料庫之前就先擋下超額請求，不需要真的登入成功就能驗證這件事（跟
/// <see cref="PublicWriteEndpointRateLimitingTests"/> 用不存在的 slug／sessionId 是同一個原理）。
/// </summary>
[Collection(AdminAuthRateLimitTestCollection.Name)]
public sealed class AdminAuthRateLimitingTests(AdminAuthRateLimitTestApiFixture fixture)
{
    [Fact]
    public async Task 登入端點_超過額度後回429()
    {
        using var client = fixture.CreateClient();

        for (var i = 1; i <= AdminAuthRateLimitTestApiFixture.TestPermitLimit; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
                new LoginRequest($"rate-limit-probe-{i}@tcrfc.test", "WhateverPassword123", null));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var exceeded = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new LoginRequest("rate-limit-probe-exceeded@tcrfc.test", "WhateverPassword123", null));
        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
    }

    [Fact]
    public async Task 更新權杖端點_超過額度後回429()
    {
        using var client = fixture.CreateClient();

        for (var i = 1; i <= AdminAuthRateLimitTestApiFixture.TestPermitLimit; i++)
        {
            var response = await client.PostAsync("/api/v1/admin/auth/refresh", content: null);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var exceeded = await client.PostAsync("/api/v1/admin/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
    }
}
