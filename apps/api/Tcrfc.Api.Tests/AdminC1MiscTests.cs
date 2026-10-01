using System.Net;
using System.Reflection;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>C1 批的零碎項目：新聞挑選搜尋、會籍球季下拉、商店類別不得讀快取（架構檢查）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminC1MiscTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 新聞挑選搜尋_關鍵字比對中英文標題與網址名稱_分頁_ids解回標題_範圍_權限()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var lookup = "/api/v1/admin/tcrfc/news/lookup";

        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(lookup)).StatusCode); // 客服／行政沒有新聞權限
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(lookup)).StatusCode);

        // 分頁：每頁上限 50，總數超過 100 篇也選得到舊文章（原本清單一次最多 100 篇）
        var page1 = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?pageSize=5"));
        Assert.Equal(5, page1.Items.Count);
        Assert.True(page1.TotalCount > 5);
        var page2 = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?pageSize=5&page=2"));
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
        Assert.Equal(50, (await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?pageSize=500"))).PageSize);
        var last = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?pageSize=50&page={(page1.TotalCount + 49) / 50}"));
        Assert.NotEmpty(last.Items); // 最舊的文章也選得到

        // 關鍵字：中文標題、網址名稱、英文標題
        var sample = page1.Items[0];
        var bySlug = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?keyword={Uri.EscapeDataString(sample.Slug)}"));
        Assert.Contains(bySlug.Items, i => i.Id == sample.Id);
        var zhTitle = (await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?pageSize=50"))).Items.First(i => !string.IsNullOrWhiteSpace(i.TitleZh)).TitleZh!;
        var byZh = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?keyword={Uri.EscapeDataString(zhTitle[..Math.Min(4, zhTitle.Length)])}"));
        Assert.Contains(byZh.Items, i => i.TitleZh == zhTitle);
        var enRow = await C1Test.ScalarAsync<string>(
            "SELECT TOP 1 i.title FROM articles_i18n i JOIN articles a ON a.id = i.article_id JOIN clubs c ON c.id = a.club_id WHERE c.code = 'tcrfc' AND i.locale = 'en' AND i.title IS NOT NULL ORDER BY a.row_seq");
        if (enRow is not null)
        {
            var byEn = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?keyword={Uri.EscapeDataString(enRow)}"));
            Assert.Contains(byEn.Items, i => i.TitleEn == enRow);
        }

        Assert.Empty((await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?keyword=zzzz-not-exist-{Guid.NewGuid():N}"))).Items);

        // ids：把已選取的 id 解回標題；狀態與分類篩選
        var ids = string.Join("&", page1.Items.Take(3).Select(i => "ids=" + i.Id));
        var resolved = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?{ids}"));
        Assert.Equal(3, resolved.TotalCount);
        Assert.All(resolved.Items, i => Assert.Contains(page1.Items, p => p.Id == i.Id));
        Assert.All((await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?status=published&pageSize=50"))).Items, i => Assert.Equal("已發布", i.StatusLabel));
        var category = page1.Items[0].CategoryCode;
        Assert.All((await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await editor.GetAsync($"{lookup}?category={category}&pageSize=50"))).Items, i => Assert.Equal(category, i.CategoryCode));

        // 範圍：藍鯨路由只有藍鯨與共用的文章，看不到磐石專屬的
        var bw = await BizTest.ReadAsync<PagedResult<AdminNewsLookupItemDto>>(await admin.GetAsync("/api/v1/admin/bw/news/lookup?pageSize=50"));
        Assert.DoesNotContain(bw.Items, i => !i.IsShared && page1.Items.Any(p => p.Id == i.Id && !p.IsShared));
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/bw/news/lookup")).StatusCode);

        // 既有清單的關鍵字也一併支援英文標題與網址名稱
        var list = await BizTest.ReadAsync<PagedResult<AdminArticleListItemDto>>(await editor.GetAsync($"/api/v1/admin/tcrfc/news?keyword={Uri.EscapeDataString(sample.Slug)}"));
        Assert.Contains(list.Items, i => i.Id == sample.Id);
    }

    [Fact]
    public async Task 會籍球季下拉_客服行政也能讀_不需要球隊賽事權限_只限本俱樂部授權()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/membership-seasons")).StatusCode);
        // 舊做法借用的 /seasons 客服／行政 403（沒有 team.competition.view）——這就是要補獨立端點的原因
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/tcrfc/seasons")).StatusCode);
        var seasons = await BizTest.ReadAsync<List<AdminSeasonListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/membership-seasons"));
        Assert.Contains(seasons, s => s.Code == "2026-27");
        Assert.All(seasons, s => Assert.True(s.EndOn >= s.StartOn));
        Assert.Equal(seasons.OrderByDescending(s => s.StartOn).Select(s => s.Id), seasons.Select(s => s.Id)); // 新→舊
        // 檢視者沒有會籍權限；客服沒被授權藍鯨
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/tcrfc/membership-seasons")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/bw/membership-seasons")).StatusCode);
    }

    /// <summary>
    /// 種子基線檢查（<c>docs/18</c> E-81 升級後的機制）：C1 的示範資料（漫畫企劃設定、商店設定、商品、訂單、抽獎…）若被某支測試的清理吃掉，
    /// 下一次跑全套就會在這裡失敗並指出缺哪一類——測試「不得以『這批鍵理論上不存在』當還原手段」這條紀律沒有辦法靠人記。
    /// 失敗時重灌種子（<c>./db/seed/apply-seed.sh</c>，冪等）；要找出兇手，看上一次跑的哪支測試碰了同一批資料。
    /// </summary>
    [Fact]
    public async Task 種子基線_C1示範資料沒有被測試清理吃掉()
    {
        async Task<int> Count(string sql) => await ShopTest.CountAsync(sql);
        var missing = new List<string>();
        if (await Count("SELECT COUNT(*) FROM settings s JOIN clubs c ON c.id = s.club_id WHERE c.code = 'tcrfc' AND s.setting_key IN ('comic.about_title','comic.about_body')") != 2) missing.Add("漫畫企劃設定（settings comic.about_*）");
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            if (await Count($"SELECT COUNT(*) FROM settings s JOIN clubs c ON c.id = s.club_id WHERE c.code = '{club}' AND s.setting_key LIKE 'shop.%'") < 9) missing.Add($"{club} 商店設定（settings shop.*）");
        }

        if (await Count("SELECT COUNT(*) FROM settings s JOIN clubs c ON c.id = s.club_id WHERE c.code = 'tcrfc' AND s.setting_key = 'member.draw_notice_confirmed'") != 1) missing.Add("抽獎蒐集告知確認");
        if (await Count("SELECT COUNT(*) FROM collections WHERE slug LIKE 'test-%collection'") < 4) missing.Add("商品系列");
        if (await Count("SELECT COUNT(*) FROM products WHERE slug LIKE 'test-%' AND slug NOT LIKE 't-%'") < 6) missing.Add("商品");
        if (await Count("SELECT COUNT(*) FROM product_variants WHERE sku LIKE 'TEST-%'") < 11) missing.Add("商品規格");
        if (await Count("SELECT COUNT(*) FROM orders WHERE order_no LIKE '%-SEED-%'") < 9) missing.Add("示範訂單");
        if (await Count("SELECT COUNT(*) FROM refund_requests r JOIN orders o ON o.id = r.order_id WHERE o.order_no LIKE '%-SEED-%'") < 2) missing.Add("示範退款案件");
        if (await Count("SELECT COUNT(*) FROM member_draws WHERE draw_code IN ('TEST-DRAW-01','TEST-DRAW-02')") != 2) missing.Add("示範抽獎活動");
        if (await Count("SELECT COUNT(*) FROM draw_rosters r JOIN member_draws d ON d.id = r.member_draw_id WHERE d.draw_code = 'TEST-DRAW-01'") != 2) missing.Add("示範抽獎名單");
        if (await Count("SELECT COUNT(*) FROM fan_events WHERE slug LIKE 'test-%'") < 4) missing.Add("球迷會活動");
        if (await Count("SELECT COUNT(*) FROM comic_episodes WHERE club_id = (SELECT id FROM clubs WHERE code = 'tcrfc') AND episode_no IN (1,2,3)") != 3) missing.Add("漫畫集數");
        if (await Count("SELECT COUNT(*) FROM invoice_donation_codes WHERE code IN ('9990001','9990002')") != 2) missing.Add("發票捐贈碼");
        Assert.True(missing.Count == 0, "種子資料被吃掉了：" + string.Join("、", missing) + "。請重灌種子（./db/seed/apply-seed.sh，冪等），並找出上一輪哪支測試清掉了它（docs/18 E-81）。");
    }

    /// <summary>docs/14：庫存、金流冪等、會員卡驗證、會籍與訂單付款狀態、購物車「不得讀快取」。站內商店整組（Features/AdminShop、前台 Features/Shop）
    /// 與庫存／訂單有關的類別一律不注入快取服務——靠人記半年後一定會破，所以用反射鎖住。</summary>
    [Fact]
    public void 站內商店與抽獎類別不注入快取服務()
    {
        var assembly = typeof(Program).Assembly;
        var offenders = assembly.GetTypes()
            .Where(t => t.Namespace is "Tcrfc.Api.Features.AdminShop" or "Tcrfc.Api.Features.AdminDraws" or "Tcrfc.Api.Features.Shop") // F 批：前台商店（目錄、購物車、結帳、訂單）同樣不得注入快取
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .SelectMany(c => c.GetParameters()).Where(p => typeof(IQueryCache).IsAssignableFrom(p.ParameterType)).Select(p => $"{t.Name}({p.Name})"))
            .ToList();
        Assert.Empty(offenders);
        // 反過來確認掃描有效：後台夥伴 repository 確實注入了快取（同樣的反射手法看得到）
        var partner = assembly.GetType("Tcrfc.Api.Features.AdminPartners.AdminPartnersRepository");
        Assert.NotNull(partner);
        Assert.Contains(partner!.GetConstructors().SelectMany(c => c.GetParameters()), p => typeof(IQueryCache).IsAssignableFrom(p.ParameterType));
        // 庫存服務是唯一寫庫存的入口：庫存量與保留量的欄位不得被 InventoryService 以外的 repository 直接指定（掃描寫入 StockQty／ReservedQty 的原始碼）
        var root = FindApiRoot();
        var violations = Directory.GetFiles(Path.Combine(root, "Features"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith("InventoryService.cs", StringComparison.Ordinal))
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"\.(StockQty|ReservedQty)\s*(=|\+=|-=)[^=]"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(violations);
    }

    private static string FindApiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tcrfc.Api.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("找不到 apps/api 專案根目錄。");
    }
}
