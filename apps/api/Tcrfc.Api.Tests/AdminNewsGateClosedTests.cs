using System.Net;
using System.Net.Http.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴 驗收要求「關閉時不是回 403，是整個路由不存在（404）」。用既有的 <see cref="ApiFixture"/>
/// （<see cref="ApiCollection"/>）——那個 fixture 刻意不設定 <c>ENABLE_UNSAFE_DEV_WRITES</c>，
/// 這正是「大多數測試在關閉狀態下跑」要驗證的事：忘記開這個旗標＝這組端點真的不存在，
/// 不是「反正測試環境永遠開著」（見 <see cref="AdminWriteApiFixture"/> 上的說明）。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminNewsGateClosedTests(ApiFixture fixture)
{
    [Fact]
    public async Task 開關關閉時_後台清單端點回404不是403()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/tcrfc/news");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task 開關關閉時_建立文章端點回404不是403()
    {
        using var client = fixture.CreateClient();

        // 帶一個形狀完全正確的建立請求也一樣——路由根本沒註冊，連模型繫結都不會發生。
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/tcrfc/news",
            new { slug = "gate-closed-probe", categoryCode = "club", content = new { zh = new { title = "測試" } } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task 開關關閉時_後台單篇端點回404()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/admin/tcrfc/news/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
