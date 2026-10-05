using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 本機驗收測資（<c>db/seed/backoffice_seed.py</c> 區段 60）的**唯讀**守門：確認 S2-11 球衣登記與 S3-2 漫畫閱讀器
/// 手動驗收需要的最小資料真的存在、而且是人可以直接拿來走流程的形狀。不寫入任何資料，所以不會干擾其他測試。
/// 種子被改掉（或庫被重建但沒灌這一段）時這裡會先紅燈，不必等人在瀏覽器上才發現「又缺資料」。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class DevAcceptanceSeedTests(AdminWriteApiFixture fixture)
{
    private const string SeedPassword = "ContentEditor@123"; // 與其他種子會員相同（backoffice_seed.py TEST_HASH）

    private async Task<JsonElement> JerseyGroupAsync(string email)
    {
        using var client = fixture.CreateClient();
        var login = await MemberTestScope.LoginAsync(client, email, SeedPassword);
        using var authed = fixture.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await authed.GetAsync("/api/v1/member/jerseys");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Assert.Single(groups.EnumerateArray().ToList(), g => g.GetProperty("clubCode").GetString() == "tcrfc");
    }

    [Theory]
    [InlineData("jersey-single@example.com", 1)]
    [InlineData("jersey-family@example.com", 3)]
    public async Task 球衣登記驗收會員_可登入_有效付費會籍_額度全新可登記(string email, int quota)
    {
        var group = await JerseyGroupAsync(email);
        Assert.Equal(quota, group.GetProperty("quota").GetInt32());
        Assert.Equal(0, group.GetProperty("used").GetInt32());
        Assert.True(group.GetProperty("canRegister").GetBoolean());
        Assert.Equal(0, group.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task 漫畫閱讀器驗收集數_第101與102集已發布_各三頁_上下集導覽相連()
    {
        using var client = fixture.CreateClient();
        var list = (await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/comic/episodes")).EnumerateArray()
            .Where(e => e.GetProperty("episodeNo").GetInt32() is 101 or 102).ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Equal(3, e.GetProperty("pageCount").GetInt32()));

        var first = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/comic/episodes/101");
        Assert.Equal(3, first.GetProperty("pages").GetArrayLength());
        Assert.Equal(102, first.GetProperty("nextEpisodeNo").GetInt32());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/comic/episodes/102");
        Assert.Equal(101, second.GetProperty("previousEpisodeNo").GetInt32());
    }
}
