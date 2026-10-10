using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-18c（2026-09-29，補齊公開寫入端點的限流缺口）：走真正的 <c>WebApplicationFactory</c> HTTP
/// 管線，驗證 <c>Program.cs</c> 確實把 <see cref="PublicRateLimitPolicies.LightInteraction"/>／
/// <see cref="PublicRateLimitPolicies.Submission"/> 兩個政策掛到對應路由上——
/// <see cref="PublicRateLimitPoliciesTests"/> 已經驗證這兩個政策「本身」的行為（額度用盡拒絕、
/// 分區互相獨立），這裡補的是「有沒有真的接上」這件事，兩者分工不同。
///
/// 🔴 用共用的 <see cref="ApiCollection"/>（<see cref="ApiFixture"/>）：這個 collection 目前只有
/// 純讀取端點測試在用（<c>ClubScopingTests</c>／<c>PagingNormalizationTests</c> 等），沒有任何
/// 其他檔案呼叫本檔用到的端點，不會互相污染分區計數；反過來說，本檔也刻意每個政策只寫「一支」
/// 會把額度用盡的測試方法（不是好幾支各自呼叫幾次），避免同一個 collection 內、同一個政策的
/// 「unknown」分區（<c>TestServer</c> 底下 <c>RemoteIpAddress</c> 恆為 <c>null</c>，見
/// <c>ClientIpResolver</c> 檔頭）被兩支測試方法的執行順序互相干擾——每支測試方法從頭到尾
/// 自己一次用完整個額度、自成一組，不依賴也不留下跨方法的殘餘狀態。
///
/// 呼叫時刻意用不存在的 slug／sessionId：限流中介軟體掛在路由層，會在進到 handler、查資料庫之前
/// 就先擋下超額請求，不需要真的準備測試資料就能驗證這件事。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PublicWriteEndpointRateLimitingTests(ApiFixture fixture)
{
    [Fact]
    public async Task Faqs瀏覽數端點_超過LightInteraction額度後回429()
    {
        using var client = fixture.CreateClient();
        var slug = $"rate-limit-probe-{Guid.NewGuid():N}";

        for (var i = 1; i <= PublicRateLimitPolicies.LightInteractionPermitLimit; i++)
        {
            var response = await client.PostAsync($"/api/v1/tcrfc/faqs/{slug}/views", content: null);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var exceeded = await client.PostAsync($"/api/v1/tcrfc/faqs/{slug}/views", content: null);
        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
    }

    [Fact]
    public async Task 課程報名端點_超過Submission額度後回429()
    {
        using var client = fixture.CreateClient();
        var request = new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = "限流測試訪客", Phone = "0912345678" };

        for (var i = 1; i <= PublicRateLimitPolicies.SubmissionPermitLimit; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/tcrfc/programs/sessions/{Guid.NewGuid()}/registrations", request, TestJson.WriteOptions);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var exceeded = await client.PostAsJsonAsync(
            $"/api/v1/tcrfc/programs/sessions/{Guid.NewGuid()}/registrations", request, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
    }
}
