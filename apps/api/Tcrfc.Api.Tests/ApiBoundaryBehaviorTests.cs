using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Tcrfc.Api.Features.AdminDraws;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 第二輪重驗缺陷：抽獎公布稿標題雙層【】、刪除端點缺 <c>expectedUpdatedAt</c> 回 500、CORS 未 expose
/// <c>Content-Disposition</c>／<c>Retry-After</c>。限流 429 的 <c>Retry-After</c> 見 <see cref="AdminAuthRateLimitingTests"/>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ApiBoundaryBehaviorTests(AdminWriteApiFixture fixture)
{
    [Theory]
    [InlineData("球迷抽獎", "【球迷抽獎】中獎名單公布")]
    [InlineData("【測試】主場賽事日球迷抽獎", "【測試】主場賽事日球迷抽獎 中獎名單公布")]
    [InlineData("【球迷抽獎】", "【球迷抽獎】 中獎名單公布")]
    [InlineData("主場【開幕戰】抽獎", "主場【開幕戰】抽獎 中獎名單公布")]
    public void 公布稿標題_名稱含括號就不再外包括號(string name, string expected)
    {
        var title = AdminDrawsRepository.BuildAnnouncementTitle(name);
        Assert.Equal(expected, title);
        Assert.DoesNotContain("【【", title);
        Assert.DoesNotContain("】】", title);
    }

    private async Task<HttpClient> EditorAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    [Theory]
    [InlineData("news")] // 頁面（pages）已改為固定頁、沒有 DELETE 端點（2026-10-07），見 AdminPagesWriteTests
    public async Task 刪除_缺expectedUpdatedAt回400中文訊息_格式錯也是400(string resource)
    {
        using var client = await EditorAsync();
        var url = $"/api/v1/admin/tcrfc/{resource}/{Guid.NewGuid()}";

        var missing = await client.DeleteAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var body = await missing.Content.ReadAsStringAsync();
        Assert.Contains("最後更新時間", body);
        Assert.DoesNotContain("Required parameter", body);

        // 值不是日期：綁定階段的 BadHttpRequestException，由全域處理轉 400，不是 500
        var malformed = await client.DeleteAsync(url + "?expectedUpdatedAt=not-a-date");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain("not-a-date", await malformed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CORS_跨來源回應expose_ContentDisposition與RetryAfter()
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/auth/me");
        request.Headers.Add("Origin", "http://localhost:5174");
        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Expose-Headers", out var values), "回應缺少 Access-Control-Expose-Headers。");
        var exposed = string.Join(",", values!);
        Assert.Contains("Content-Disposition", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry-After", exposed, StringComparison.OrdinalIgnoreCase);
    }
}
