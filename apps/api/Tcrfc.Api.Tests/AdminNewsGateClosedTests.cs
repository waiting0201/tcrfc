using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1（J1–J3 登入與授權）起，AdminNews 端點不再靠環境旗標決定「這組路由存不存在」——路由一律
/// 註冊，改由 <see cref="Tcrfc.Api.Security.IAdminClubAuthorizer"/> 逐請求驗證。本檔驗收的是
/// 這個行為：**沒有帶存取權杖打這些端點一律 401**，不是 404（端點本身確實存在，只是沒有登入）。
/// 2026-09-23（使用者裁決）：曾經把關這組端點的開發模式開關機制已整支移除，見
/// apps/api/README.md「S1」整節。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminNewsGateClosedTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 未登入打後台清單端點_回401不是404()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 未登入打建立文章端點_回401不是404()
    {
        using var client = fixture.CreateClient();

        // 帶一個形狀完全正確的建立請求也一樣——路由確實存在（不是 404），但沒有登入就是 401。
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/news",
            new { slug = "gate-closed-probe", categoryCode = "club", content = new { zh = new { title = "測試" } } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 未登入打後台單篇端點_回401()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/admin/tcrfc/news/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 帶著格式不正確的權杖_回401()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 公開唯讀端點不受影響_不需要登入()
    {
        using var client = fixture.CreateClient();

        // 既有五組公開 GET 端點的行為完全不變——本輪明訂「不得改變公開唯讀 API 的既有行為」。
        var response = await client.GetAsync("/api/v1/tcrfc/news");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
