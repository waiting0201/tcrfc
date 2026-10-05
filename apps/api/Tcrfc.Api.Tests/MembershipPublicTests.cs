using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>會籍方案、權益對照表、特約店家的公開讀取（未登入即可）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class MembershipPublicTests(AdminWriteApiFixture fixture)
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task 會籍方案_未登入可讀_只列上架且球季未結束的方案_藍鯨沒有現行方案()
    {
        using var client = fixture.CreateClient();
        var plans = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans"));
        var codes = plans.EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToArray();
        Assert.Contains("single", codes);
        Assert.Contains("family", codes);
        var single = plans.EnumerateArray().First(p => p.GetProperty("code").GetString() == "single");
        Assert.Equal(1200, single.GetProperty("fee").GetInt32());
        Assert.Equal(1, single.GetProperty("cardQuota").GetInt32());
        Assert.Equal(1, single.GetProperty("jerseyQuota").GetInt32());
        Assert.Equal("2026-27", single.GetProperty("seasonCode").GetString());
        Assert.False(string.IsNullOrEmpty(single.GetProperty("name").GetString()));

        // 藍鯨 2025 球季的方案期間早已結束 → 沒有現行方案
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync("/api/v1/bw/membership/plans"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/nope/membership/plans")).StatusCode);

        // 下架的方案不會出現
        await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET status = 'draft' WHERE code = 'family' AND club_id = (SELECT id FROM clubs WHERE code = 'tcrfc')");
        try
        {
            var after = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans"));
            Assert.DoesNotContain("family", after.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET status = 'published' WHERE code = 'family' AND club_id = (SELECT id FROM clubs WHERE code = 'tcrfc')");
        }
    }

    [Fact]
    public async Task 權益對照表_未登入可讀_依分組排序_只列上架條目_英文缺漏回退繁中()
    {
        using var client = fixture.CreateClient();
        var table = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?planCode=single"));
        Assert.Equal("single", table.GetProperty("planCode").GetString());
        var groups = table.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(new[] { "member_card", "store_discount", "jersey", "event" }, groups.Select(g => g.GetProperty("group").GetString()).ToArray());
        Assert.Equal("會員卡", groups[0].GetProperty("groupLabel").GetString());
        Assert.Equal(2, groups[1].GetProperty("items").GetArrayLength()); // 店家折扣：全會員適用、限付費會員
        Assert.False(string.IsNullOrEmpty(groups[0].GetProperty("items")[0].GetProperty("name").GetString()));

        var en = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?planCode=single&lang=en"));
        Assert.Equal("Membership card", en.GetProperty("groups")[0].GetProperty("groupLabel").GetString());
        // 英文欄位缺漏時回退繁中（不是空字串）
        Assert.All(en.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray()),
            item => Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("name").GetString())));

        // 沒指定方案：取最早的上架方案；方案不存在 404；藍鯨沒有現行方案 → 空表
        Assert.NotEqual(0, (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits"))).GetProperty("groups").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/membership/benefits?planCode=nope")).StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync("/api/v1/bw/membership/benefits"))).GetProperty("groups").GetArrayLength());

        // 下架的條目不會出現
        await BizTest.ExecuteSqlAsync("UPDATE membership_benefits SET status = 'draft' WHERE benefit_group = 'event' AND membership_plan_id = (SELECT p.id FROM membership_plans p JOIN clubs c ON c.id = p.club_id WHERE c.code = 'tcrfc' AND p.code = 'single')");
        try
        {
            var after = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?planCode=single"));
            Assert.DoesNotContain("event", after.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("group").GetString()));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE membership_benefits SET status = 'published' WHERE benefit_group = 'event' AND membership_plan_id = (SELECT p.id FROM membership_plans p JOIN clubs c ON c.id = p.club_id WHERE c.code = 'tcrfc' AND p.code = 'single')");
        }
    }

    [Fact]
    public async Task 特約店家_未登入可讀_本俱樂部加共同店家_草稿與別隊店家不出現_可依類別地區適用層級篩選()
    {
        using var client = fixture.CreateClient();
        var all = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores"));
        var slugs = all.EnumerateArray().Select(s => s.GetProperty("slug").GetString()).ToArray();
        Assert.Contains("test-store-cafe", slugs);
        Assert.Contains("test-store-shared", slugs); // 兩隊共同
        Assert.DoesNotContain("test-store-gym", slugs); // 草稿
        Assert.DoesNotContain("test-store-bw", slugs); // 藍鯨專屬
        var shared = all.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-shared");
        Assert.True(shared.GetProperty("isShared").GetBoolean());
        var cafe = all.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-cafe");
        Assert.Equal("all", cafe.GetProperty("applicableTier").GetString());
        Assert.Equal("全會員適用", cafe.GetProperty("applicableTierLabel").GetString());
        // 英文標籤須與前台、docs/06 §1.1 對照表一致（Paid Fan Club member），不得出現舊寫法 "Fan club members only"。
        var enAll = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?lang=en"));
        Assert.Equal("All members", enAll.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-cafe").GetProperty("applicableTierLabel").GetString());
        Assert.Equal("Paid Fan Club members only", enAll.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-sports").GetProperty("applicableTierLabel").GetString());
        Assert.True(cafe.TryGetProperty("lat", out _) && cafe.TryGetProperty("lng", out _)); // 座標給 App 的附近地圖
        Assert.False(string.IsNullOrEmpty(cafe.GetProperty("name").GetString()));

        // 藍鯨看得到藍鯨店家與共同店家，看不到磐石專屬
        var bwSlugs = (await ReadJsonAsync(await client.GetAsync("/api/v1/bw/partner-stores"))).EnumerateArray().Select(s => s.GetProperty("slug").GetString()).ToArray();
        Assert.Contains("test-store-bw", bwSlugs);
        Assert.Contains("test-store-shared", bwSlugs);
        Assert.DoesNotContain("test-store-cafe", bwSlugs);

        // 篩選
        var fan = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?tier=fan_club"));
        Assert.All(fan.EnumerateArray(), s => Assert.Equal("fan_club", s.GetProperty("applicableTier").GetString()));
        Assert.Contains("test-store-sports", fan.EnumerateArray().Select(s => s.GetProperty("slug").GetString()));
        var filters = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores/filters"));
        var firstCategory = filters.GetProperty("categories")[0].GetString();
        Assert.NotNull(firstCategory);
        var byCategory = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?category=" + Uri.EscapeDataString(firstCategory!)));
        Assert.All(byCategory.EnumerateArray(), s => Assert.Equal(firstCategory, s.GetProperty("category").GetString()));

        // 詳情：草稿 404；合作期間已結束的店家不出現
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tcrfc/partner-stores/test-store-cafe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/partner-stores/test-store-gym")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/partner-stores/test-store-bw")).StatusCode);
        await BizTest.ExecuteSqlAsync("UPDATE partner_stores SET end_on = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date)) WHERE slug = 'test-store-food'");
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/tcrfc/partner-stores/test-store-food")).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE partner_stores SET end_on = NULL WHERE slug = 'test-store-food'");
        }
    }
}
