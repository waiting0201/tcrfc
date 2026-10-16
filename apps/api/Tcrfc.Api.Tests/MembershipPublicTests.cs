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
    public async Task 會籍方案_未登入可讀_磐石兩級方案_只列上架且球季未結束的方案_藍鯨沒有現行方案()
    {
        using var client = fixture.CreateClient();
        var plans = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans"));
        var codes = plans.EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToArray();
        Assert.Equal(new[] { "standard", "premium" }, codes); // v3.29：磐石會員、尊尚會員；舊的單人／家庭方案已下架
        var standard = plans.EnumerateArray().First(p => p.GetProperty("code").GetString() == "standard");
        Assert.Equal("standard", standard.GetProperty("tier").GetString());
        Assert.Equal(400, standard.GetProperty("fee").GetInt32());
        Assert.Equal(1, standard.GetProperty("cardQuota").GetInt32());
        Assert.Equal(0, standard.GetProperty("jerseyQuota").GetInt32());
        Assert.Equal(10, standard.GetProperty("shopDiscountPercent").GetInt32());
        Assert.Equal("2026-27", standard.GetProperty("seasonCode").GetString());
        Assert.Equal("磐石會員", standard.GetProperty("name").GetString());
        var premium = plans.EnumerateArray().First(p => p.GetProperty("code").GetString() == "premium");
        Assert.Equal("premium", premium.GetProperty("tier").GetString());
        Assert.Equal(2400, premium.GetProperty("fee").GetInt32());
        Assert.Equal(1, premium.GetProperty("jerseyQuota").GetInt32()); // 尊尚禮盒含球衣一件
        Assert.Equal("Prestige Member", (await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans?lang=en")))[1].GetProperty("name").GetString());

        // 藍鯨 2025 球季的方案期間早已結束 → 沒有現行方案
        Assert.Equal(0, (await ReadJsonAsync(await client.GetAsync("/api/v1/bw/membership/plans"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/nope/membership/plans")).StatusCode);

        // 下架的方案不會出現
        await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET status = 'draft' WHERE code = 'premium' AND club_id = (SELECT id FROM clubs WHERE code = 'tcrfc')");
        try
        {
            var after = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/plans"));
            Assert.DoesNotContain("premium", after.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET status = 'published' WHERE code = 'premium' AND club_id = (SELECT id FROM clubs WHERE code = 'tcrfc')");
        }
    }

    [Fact]
    public async Task 權益對照表_未登入可讀_以方案為欄_依分組排序_只列上架條目_英文缺漏回退繁中()
    {
        using var client = fixture.CreateClient();
        var table = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits"));
        var planCodes = table.GetProperty("plans").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToArray();
        Assert.Equal(new[] { "standard", "premium" }, planCodes);
        var groups = table.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(new[] { "member_card", "discount", "gift", "event", "content" }, groups.Select(g => g.GetProperty("group").GetString()).ToArray());
        Assert.Equal("會員卡", groups[0].GetProperty("groupLabel").GetString());
        Assert.Equal("折扣與優惠", groups[1].GetProperty("groupLabel").GetString());

        // 每一列的 values 與 plans 同序、每個方案一格
        var items = groups.SelectMany(g => g.GetProperty("items").EnumerateArray()).ToList();
        Assert.All(items, i =>
        {
            Assert.Equal(planCodes, i.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("planCode").GetString()).ToArray());
            Assert.False(string.IsNullOrWhiteSpace(i.GetProperty("name").GetString()));
        });
        string?[] ValuesOf(string code) => items.First(i => i.GetProperty("code").GetString() == code).GetProperty("values").EnumerateArray()
            .Select(v => v.GetProperty("value").ValueKind == JsonValueKind.Null ? null : v.GetProperty("value").GetString()).ToArray();
        Assert.Equal(new string?[] { "✓", "✓" }, ValuesOf("member_card"));
        Assert.Equal(new string?[] { null, "✓" }, ValuesOf("includes_standard")); // 尊尚會員「包含磐石會員全部權益」，磐石會員沒有這一條
        Assert.Equal(new string?[] { "10%", "10%" }, ValuesOf("shop_discount"));
        Assert.Equal(new string?[] { null, "10%–15%" }, ValuesOf("training_discount"));
        Assert.Equal(new string?[] { "✓", "✓" }, ValuesOf("draw_eligibility"));

        var en = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits?lang=en"));
        Assert.Equal("Membership card", en.GetProperty("groups")[0].GetProperty("groupLabel").GetString());
        Assert.Equal("Prestige Member", en.GetProperty("plans")[1].GetProperty("name").GetString());
        Assert.All(en.GetProperty("groups").EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray()),
            item => Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("name").GetString())));

        // 藍鯨沒有現行方案 → 空表（不是 404）；俱樂部不存在 404
        var bw = await ReadJsonAsync(await client.GetAsync("/api/v1/bw/membership/benefits"));
        Assert.Equal(0, bw.GetProperty("plans").GetArrayLength());
        Assert.Equal(0, bw.GetProperty("groups").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/nope/membership/benefits")).StatusCode);

        // 下架的條目不會出現（兩個方案的活動條目都下架 → 整個分組消失）
        const string EventPlans = "membership_plan_id IN (SELECT p.id FROM membership_plans p JOIN clubs c ON c.id = p.club_id WHERE c.code = 'tcrfc' AND p.code IN ('standard', 'premium'))";
        await BizTest.ExecuteSqlAsync($"UPDATE membership_benefits SET status = 'draft' WHERE benefit_group = 'event' AND {EventPlans}");
        try
        {
            var after = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/membership/benefits"));
            Assert.DoesNotContain("event", after.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("group").GetString()));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync($"UPDATE membership_benefits SET status = 'published' WHERE benefit_group = 'event' AND {EventPlans}");
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
        Assert.Equal("所有會員", cafe.GetProperty("applicableTierLabel").GetString());
        // 適用層級（v3.29）：所有會員（all）／限尊尚層級（premium）；舊的「限付費」fan_club 已不存在。
        var enAll = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?lang=en"));
        Assert.Equal("All members", enAll.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-cafe").GetProperty("applicableTierLabel").GetString());
        Assert.Equal("Prestige members only", enAll.EnumerateArray().First(s => s.GetProperty("slug").GetString() == "test-store-sports").GetProperty("applicableTierLabel").GetString());
        Assert.True(cafe.TryGetProperty("lat", out _) && cafe.TryGetProperty("lng", out _)); // 座標給 App 的附近地圖
        Assert.False(string.IsNullOrEmpty(cafe.GetProperty("name").GetString()));

        // 藍鯨看得到藍鯨店家與共同店家，看不到磐石專屬
        var bwSlugs = (await ReadJsonAsync(await client.GetAsync("/api/v1/bw/partner-stores"))).EnumerateArray().Select(s => s.GetProperty("slug").GetString()).ToArray();
        Assert.Contains("test-store-bw", bwSlugs);
        Assert.Contains("test-store-shared", bwSlugs);
        Assert.DoesNotContain("test-store-cafe", bwSlugs);

        // 篩選
        var premium = await ReadJsonAsync(await client.GetAsync("/api/v1/tcrfc/partner-stores?tier=premium"));
        Assert.All(premium.EnumerateArray(), s => Assert.Equal("premium", s.GetProperty("applicableTier").GetString()));
        Assert.Contains("test-store-sports", premium.EnumerateArray().Select(s => s.GetProperty("slug").GetString()));
        Assert.DoesNotContain("test-store-cafe", premium.EnumerateArray().Select(s => s.GetProperty("slug").GetString()));
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
