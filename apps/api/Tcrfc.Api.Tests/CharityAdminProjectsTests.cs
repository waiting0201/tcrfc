using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>N2 捐款項目管理：授權、上下架、金額選項與範圍、分潤的獨立授權與稽核與 100% 約束、撥付對象快照、雙語、封面。</summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminProjectsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Projects = AdminBase + "/projects";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private static string NewSlug() => $"ct-{Guid.NewGuid():N}"[..16];

    private static Dictionary<string, object?> Body(string? slug = null, Action<Dictionary<string, object?>>? tweak = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["slug"] = slug ?? NewSlug(),
            ["nameZh"] = "CT專案名稱",
            ["nameEn"] = "CT Project Name",
            ["oneLinerZh"] = "一句話說明",
            ["oneLinerEn"] = "One liner",
            ["descriptionZh"] = JsonDocument.Parse("""{"blocks":[{"type":"paragraph","text":"內文"}]}""").RootElement.Clone(),
            ["fundUsageZh"] = "款項用途說明",
            ["coverAltZh"] = "封面",
            ["minAmount"] = 100,
            ["maxAmount"] = 10000,
            ["amountOptions"] = new[] { 500, 100, 300 },
            ["invoiceMode"] = "b2c_invoice",
        };
        tweak?.Invoke(body);
        return body;
    }

    private async Task<HttpClient> ClientAsync(bool superAdmin, params string[] roles)
        => fx.CreateClientFor(await fx.CreateAdminAsync(superAdmin, roles));

    private static async Task<AdminProjectDetailDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminProjectDetailDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task 授權_客服角色沒有N2權限回403_唯讀只能看_商務可建立編輯與上下架()
    {
        using var cs = await ClientAsync(false, "customer_service_admin");
        using var viewer = await ClientAsync(false, "viewer");
        using var biz = await ClientAsync(false, "business_sponsorship");

        Assert.Equal(HttpStatusCode.Forbidden, (await cs.GetAsync(Projects)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Projects)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions)).StatusCode);

        var created = await ReadAsync(await biz.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Projects}/{created.Id}/publish", null)).StatusCode);
        Assert.Equal("published", (await ReadAsync(await biz.PostAsync($"{Projects}/{created.Id}/publish", null))).Status);
    }

    [Fact]
    public async Task 建立項目_預設是草稿_上架後前台看得到_下架後消失()
    {
        using var client = await ClientAsync(true);
        using var publicClient = fx.CreateClient();
        var created = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));

        Assert.Equal("draft", created.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"{Base}/projects/{created.Slug}")).StatusCode);

        await ReadAsync(await client.PostAsync($"{Projects}/{created.Id}/publish", null));
        var detail = (await publicClient.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{created.Slug}", TestJson.Options))!;
        Assert.Equal([100, 300, 500], detail.AmountOptions);
        Assert.Equal("CT專案名稱", detail.Name);
        var cards = (await publicClient.GetFromJsonAsync<List<PublicProjectCardDto>>($"{Base}/projects", TestJson.Options))!;
        Assert.Contains(cards, c => c.Slug == created.Slug);

        await ReadAsync(await client.PostAsync($"{Projects}/{created.Id}/unpublish", null));
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"{Base}/projects/{created.Slug}")).StatusCode);
    }

    [Fact]
    public async Task 網址名稱_重複回409_格式不對回400_沒給就自動產生()
    {
        using var client = await ClientAsync(true);
        var slug = NewSlug();
        await ReadAsync(await client.PostAsJsonAsync(Projects, Body(slug), TestJson.WriteOptions));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Projects, Body(slug), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body("Has Spaces"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body("UPPER"), TestJson.WriteOptions)).StatusCode);

        var auto = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => { b["slug"] = null; b["nameEn"] = $"ct-auto-{Guid.NewGuid():N}"[..14]; }), TestJson.WriteOptions));
        Assert.Matches("^ct-auto-[a-z0-9]+$", auto.Slug);
    }

    [Fact]
    public async Task 金額選項_存成由小到大_重複與超出範圍與過多都擋下_省略代表不變_空陣列代表清空()
    {
        using var client = await ClientAsync(true);
        var created = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));
        Assert.Equal([100, 300, 500], created.AmountOptions);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["amountOptions"] = new[] { 100, 100 }), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["amountOptions"] = new[] { 50 }), TestJson.WriteOptions)).StatusCode);      // 低於下限
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["amountOptions"] = new[] { 20000 }), TestJson.WriteOptions)).StatusCode);   // 高於上限
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["amountOptions"] = Enumerable.Range(1, 13).Select(i => i * 100).ToArray()), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => { b["minAmount"] = 5000; b["maxAmount"] = 100; }), TestJson.WriteOptions)).StatusCode);

        var unchanged = await ReadAsync(await client.PutAsJsonAsync($"{Projects}/{created.Id}", Body(created.Slug, b => b.Remove("amountOptions")), TestJson.WriteOptions));
        Assert.Equal([100, 300, 500], unchanged.AmountOptions);
        Assert.Equal(3, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_amount_options WHERE donation_project_id = @i", ("@i", created.Id)));

        var replaced = await ReadAsync(await client.PutAsJsonAsync($"{Projects}/{created.Id}", Body(created.Slug, b => b["amountOptions"] = new[] { 200, 1000 }), TestJson.WriteOptions));
        Assert.Equal([200, 1000], replaced.AmountOptions);
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_amount_options WHERE donation_project_id = @i", ("@i", created.Id)));

        var cleared = await ReadAsync(await client.PutAsJsonAsync($"{Projects}/{created.Id}", Body(created.Slug, b => b["amountOptions"] = Array.Empty<int>()), TestJson.WriteOptions));
        Assert.Empty(cleared.AmountOptions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 分潤
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 設定項目分潤需要獨立授權_商務不行_系統管理員可以並留稽核_不變就不留稽核()
    {
        var sys = await fx.CreateAdminAsync(isSuperAdmin: true);
        using var sysClient = fx.CreateClientFor(sys);
        using var biz = await ClientAsync(false, "business_sponsorship");

        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = 8m), TestJson.WriteOptions)).StatusCode);

        var project = await ReadAsync(await sysClient.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = 8m), TestJson.WriteOptions));
        Assert.Equal(8m, project.ProjectSharePct);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'project.share_pct_set' AND target_id = @t", ("@t", project.Id)));

        // 商務編輯其他欄位：沿用或省略分潤都可以，不新增稽核
        await ReadAsync(await biz.PutAsJsonAsync($"{Projects}/{project.Id}", Body(project.Slug, b => { b["projectSharePct"] = 8m; b["nameZh"] = "CT專案改名"; }), TestJson.WriteOptions));
        var omitted = await ReadAsync(await biz.PutAsJsonAsync($"{Projects}/{project.Id}", Body(project.Slug), TestJson.WriteOptions));
        Assert.Equal(8m, omitted.ProjectSharePct);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'project.share_pct_set' AND target_id = @t", ("@t", project.Id)));

        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PutAsJsonAsync($"{Projects}/{project.Id}", Body(project.Slug, b => b["projectSharePct"] = 9m), TestJson.WriteOptions)).StatusCode);
        await ReadAsync(await sysClient.PutAsJsonAsync($"{Projects}/{project.Id}", Body(project.Slug, b => b["projectSharePct"] = 15m), TestJson.WriteOptions));
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'project.share_pct_set' AND target_id = @t", ("@t", project.Id)));
    }

    [Fact]
    public async Task 儲存時驗證_項目分潤加合作中店家最高分潤不得超過100()
    {
        await CreateStoreAsync(fx, storePct: 50m);
        await CreateStoreAsync(fx, storePct: 90m, status: "inactive"); // 已停止的店家不算
        using var client = await ClientAsync(true);

        var tooHigh = await client.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = 51m), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);
        Assert.Contains("100", await tooHigh.Content.ReadAsStringAsync());

        await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = 50m), TestJson.WriteOptions));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = 100.5m), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["projectSharePct"] = -1m), TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 撥付對象與慈善計畫：值複製快照
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 撥付對象與慈善計畫_只收參照碼_名稱從唯讀複本值複製為快照_錯誤組合擋下_可清除()
    {
        using var client = await ClientAsync(true);
        var refs = (await client.GetFromJsonAsync<AdminCharityRefOptionsDto>($"{Projects}/charity-refs", TestJson.Options))!;
        Assert.NotEmpty(refs.Charities);
        var program = refs.Programs.First();
        var charity = refs.Charities.First(c => c.RefCode == program.CharityRefCode);
        var otherCharity = refs.Charities.First(c => c.RefCode != program.CharityRefCode);

        // 只選慈善計畫：撥付對象自動帶出所屬公益團體
        var withProgram = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => b["charityProgramRefCode"] = program.RefCode), TestJson.WriteOptions));
        Assert.Equal(program.Name, withProgram.CharityProgramName);
        Assert.Equal(charity.RefCode, withProgram.CharityRefCode);
        Assert.Equal(charity.Name, withProgram.CharityName);

        // 只選公益團體：計畫快照為空
        var onlyCharity = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => b["charityRefCode"] = charity.RefCode), TestJson.WriteOptions));
        Assert.Equal(charity.Name, onlyCharity.CharityName);
        Assert.Null(onlyCharity.CharityProgramRefCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => { b["charityRefCode"] = otherCharity.RefCode; b["charityProgramRefCode"] = program.RefCode; }), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["charityRefCode"] = "NO-SUCH-CODE"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["charityProgramRefCode"] = "NO-SUCH-CODE"), TestJson.WriteOptions)).StatusCode);

        // 快照是值複製：之後主站（唯讀複本）改名，已存的項目快照不變
        await fx.ExecuteAsync("UPDATE charity_refs SET name = name + N'（改名）' WHERE ref_code = @c", ("@c", charity.RefCode));
        try
        {
            var reread = await ReadAsync(await client.GetAsync($"{Projects}/{withProgram.Id}"));
            Assert.Equal(charity.Name, reread.CharityName);
        }
        finally
        {
            await fx.ExecuteAsync("UPDATE charity_refs SET name = @n WHERE ref_code = @c", ("@n", charity.Name), ("@c", charity.RefCode));
        }

        var cleared = await ReadAsync(await client.PutAsJsonAsync($"{Projects}/{withProgram.Id}", Body(withProgram.Slug), TestJson.WriteOptions));
        Assert.Null(cleared.CharityRefCode);
        Assert.Null(cleared.CharityName);
        Assert.Null(cleared.CharityProgramName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 雙語與內文
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 雙語_英文有填前台英文版回英文_清空英文後回退繁中並標示()
    {
        using var client = await ClientAsync(true);
        using var publicClient = fx.CreateClient();
        var created = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));
        await ReadAsync(await client.PostAsync($"{Projects}/{created.Id}/publish", null));

        var en = (await publicClient.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{created.Slug}?lang=en", TestJson.Options))!;
        Assert.Equal("CT Project Name", en.Name);
        Assert.False(en.IsFallback);
        Assert.Equal("款項用途說明", en.FundUsage); // 英文款項用途沒填，欄位級回退繁中

        var noEnglish = Body(created.Slug, b => { b["nameEn"] = null; b["oneLinerEn"] = null; });
        await ReadAsync(await client.PutAsJsonAsync($"{Projects}/{created.Id}", noEnglish, TestJson.WriteOptions));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_projects_i18n WHERE donation_project_id = @i AND locale = N'en'", ("@i", created.Id)));
        var fallback = (await publicClient.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{created.Slug}?lang=en", TestJson.Options))!;
        Assert.Equal("CT專案名稱", fallback.Name);
        Assert.True(fallback.IsFallback);
    }

    [Fact]
    public async Task 說明內文_區塊編輯器JSON原樣存取_太大回400()
    {
        using var client = await ClientAsync(true);
        var created = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));
        Assert.Equal("paragraph", created.DescriptionZh!.Value.GetProperty("blocks")[0].GetProperty("type").GetString());

        var huge = Body(tweak: b => b["descriptionZh"] = JsonDocument.Parse($"{{\"text\":\"{new string('x', 250_000)}\"}}").RootElement.Clone());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, huge, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 驗證_名稱必填_憑證模式必選_未知項目回404()
    {
        using var client = await ClientAsync(true);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["nameZh"] = " "), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["invoiceMode"] = null), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Projects, Body(tweak: b => b["invoiceMode"] = "something"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Projects}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Projects}/{Guid.NewGuid()}", Body(), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{Projects}/{Guid.NewGuid()}/publish", null)).StatusCode);
    }

    [Fact]
    public async Task 排序與列表_依sortOrder排列_可依狀態篩選()
    {
        using var client = await ClientAsync(true);
        var a = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => b["sortOrder"] = 9001), TestJson.WriteOptions));
        var b2 = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(tweak: b => b["sortOrder"] = 9000), TestJson.WriteOptions));

        var all = (await client.GetFromJsonAsync<List<AdminProjectListItemDto>>(Projects, TestJson.Options))!;
        Assert.True(all.FindIndex(x => x.Id == b2.Id) < all.FindIndex(x => x.Id == a.Id));

        var drafts = (await client.GetFromJsonAsync<List<AdminProjectListItemDto>>($"{Projects}?status=draft", TestJson.Options))!;
        Assert.Contains(drafts, x => x.Id == a.Id);
        Assert.All(drafts, x => Assert.Equal("draft", x.Status));
    }

    [Fact]
    public async Task 封面上傳與刪除_規則同店家Logo()
    {
        using var client = await ClientAsync(true);
        var created = await ReadAsync(await client.PostAsJsonAsync(Projects, Body(), TestJson.WriteOptions));

        using var form = new MultipartFormDataContent { { new ByteArrayContent(TestImages.SmallPng()), "file", "cover.png" } };
        var uploaded = await ReadAsync(await client.PostAsync($"{Projects}/{created.Id}/cover", form));
        Assert.StartsWith("https://img.charity-test.invalid/projects/", uploaded.CoverUrl);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Projects}/{created.Id}/cover")).StatusCode);
        Assert.Empty(fx.Images.Keys);
        Assert.Null((await ReadAsync(await client.GetAsync($"{Projects}/{created.Id}"))).CoverUrl);
    }
}
