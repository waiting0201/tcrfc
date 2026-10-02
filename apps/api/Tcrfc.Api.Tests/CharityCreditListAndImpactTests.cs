using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// CH-5：捐款徵信名單（規劃書 §3.6、§11.1）與成果回顧頁。
/// 🔴 徵信名單的公開條件缺一不可：paid、捐款人明示具名、後台沒有逐筆隱藏、站台沒有整站關閉；只輸出姓名（不含金額、Email、店家、單號、時間）。
/// 徵信名單用 projectSlug 限定到測試建立的專案（共用本機庫上有種子捐款）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityCreditListAndImpactTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string CreditList = Base + "/credit-list";
    private string? _creditRuleBefore;
    private bool _creditRuleExisted;
    private string? _clubUrlBefore;
    private bool _clubUrlExisted;

    public async Task InitializeAsync()
    {
        fx.ResetDoubles();
        _creditRuleExisted = await fx.ScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE setting_key = N'donation.credit_list_display_rule'") > 0;
        _creditRuleBefore = await fx.ScalarAsync<string>("SELECT value FROM settings WHERE setting_key = N'donation.credit_list_display_rule'");
        _clubUrlExisted = await fx.ScalarAsync<int>("SELECT COUNT(*) FROM settings WHERE setting_key = N'donation.club_site_url'") > 0;
        _clubUrlBefore = await fx.ScalarAsync<string>("SELECT value FROM settings WHERE setting_key = N'donation.club_site_url'");
    }

    public async Task DisposeAsync()
    {
        // 還原整站開關與俱樂部網址（共用單一份設定）。
        // updated_by 一併清空：被測試帳號寫過的設定列會擋住測試帳號的清理（外鍵）。
        if (_creditRuleExisted)
        {
            await fx.ExecuteAsync("UPDATE settings SET value = @v, updated_by = NULL WHERE setting_key = N'donation.credit_list_display_rule'", ("@v", (object?)_creditRuleBefore ?? DBNull.Value));
        }
        else
        {
            await fx.ExecuteAsync("DELETE FROM settings WHERE setting_key = N'donation.credit_list_display_rule'");
        }

        if (_clubUrlExisted)
        {
            await fx.ExecuteAsync("UPDATE settings SET value = @v, updated_by = NULL WHERE setting_key = N'donation.club_site_url'", ("@v", (object?)_clubUrlBefore ?? DBNull.Value));
        }
        else
        {
            await fx.ExecuteAsync("DELETE FROM settings WHERE setting_key = N'donation.club_site_url'");
        }

        await fx.CleanupAsync();
    }

    private async Task<(Guid ProjectId, string Slug)> ArrangeAsync(string marker)
    {
        var (projectId, slug) = await CreateProjectAsync(fx);
        var june = NoonUtc(2025, 6, 10);
        await SeedPaidDonationAsync(fx, projectId, null, 100, june, donorName: $"{marker}王小明");
        await SeedPaidDonationAsync(fx, projectId, null, 200, june, donorName: $"{marker}王小明");          // 同名：去重
        await SeedPaidDonationAsync(fx, projectId, null, 300, NoonUtc(2025, 7, 20), donorName: $"{marker}李小華");
        await SeedPaidDonationAsync(fx, projectId, null, 400, june, donorName: $"{marker}匿名者", anonymous: true);  // 匿名：不列
        await SeedPaidDonationAsync(fx, projectId, null, 500, june, donorName: $"{marker}已退款", status: "refunded"); // 非 paid：不列
        await SeedPaidDonationAsync(fx, projectId, null, 600, june, donorName: $"{marker}失敗", status: "failed");
        return (projectId, slug);
    }

    private async Task<PublicCreditListDto> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{CreditList}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicCreditListDto>(TestJson.Options))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 徵信名單
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 徵信名單_只列已付款且具名的捐款人_姓名去重並依姓名排序_不含任何其他資訊()
    {
        var marker = $"CT{Guid.NewGuid():N}"[..8];
        var (_, slug) = await ArrangeAsync(marker);
        using var anonymous = fx.CreateClient();

        var list = await ListAsync(anonymous, $"projectSlug={slug}");

        Assert.True(list.Enabled);
        Assert.Equal(new[] { $"{marker}李小華", $"{marker}王小明" }.OrderBy(n => n, StringComparer.Ordinal).ToArray(), list.Names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(2, list.TotalCount);

        // 回應的 JSON 只有名單與分頁資訊：沒有金額、Email、店家、單號、時間、捐款次數
        var raw = await (await anonymous.GetAsync($"{CreditList}?projectSlug={slug}")).Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(new[] { "enabled", "names", "page", "pageSize", "totalCount" }, doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("charity-test.invalid", raw);
        Assert.DoesNotContain($"{marker}匿名者", raw);
        Assert.DoesNotContain($"{marker}已退款", raw);
        Assert.DoesNotContain($"{marker}失敗", raw);
    }

    [Fact]
    public async Task 徵信名單_可依項目與期間篩選_並分頁()
    {
        var marker = $"CT{Guid.NewGuid():N}"[..8];
        var (_, slug) = await ArrangeAsync(marker);
        var (otherProjectId, otherSlug) = await CreateProjectAsync(fx);
        await SeedPaidDonationAsync(fx, otherProjectId, null, 100, NoonUtc(2025, 6, 10), donorName: $"{marker}別的項目");
        using var client = fx.CreateClient();

        Assert.Equal(new[] { $"{marker}別的項目" }, (await ListAsync(client, $"projectSlug={otherSlug}")).Names.ToArray());
        Assert.Equal(new[] { $"{marker}王小明" }, (await ListAsync(client, $"projectSlug={slug}&from=2025-06-01&to=2025-06-30")).Names.ToArray());   // 六月
        Assert.Equal(new[] { $"{marker}李小華" }, (await ListAsync(client, $"projectSlug={slug}&from=2025-07-01&to=2025-07-31")).Names.ToArray());   // 七月
        Assert.Empty((await ListAsync(client, $"projectSlug={slug}&from=2001-01-01&to=2001-01-31")).Names);
        Assert.Empty((await ListAsync(client, "projectSlug=ct-not-exist")).Names);

        var page1 = await ListAsync(client, $"projectSlug={slug}&pageSize=1&page=1");
        var page2 = await ListAsync(client, $"projectSlug={slug}&pageSize=1&page=2");
        Assert.Single(page1.Names);
        Assert.Single(page2.Names);
        Assert.NotEqual(page1.Names[0], page2.Names[0]);
        Assert.Equal(2, page1.TotalCount);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{CreditList}?from=2025-07-01&to=2025-06-01")).StatusCode);
    }

    [Fact]
    public async Task 徵信名單_後台整站關閉時不輸出任何名單_公開設定的開關同步()
    {
        var marker = $"CT{Guid.NewGuid():N}"[..8];
        var (_, slug) = await ArrangeAsync(marker);
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        using var anonymous = fx.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await sys.PutAsJsonAsync($"{AdminBase}/settings", new { creditListEnabled = false }, TestJson.WriteOptions)).StatusCode);

        var off = await ListAsync(anonymous, $"projectSlug={slug}");
        Assert.False(off.Enabled);
        Assert.Empty(off.Names);
        Assert.Equal(0, off.TotalCount);
        Assert.False((await anonymous.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings", TestJson.Options))!.CreditListEnabled);

        Assert.Equal(HttpStatusCode.OK, (await sys.PutAsJsonAsync($"{AdminBase}/settings", new { creditListEnabled = true }, TestJson.WriteOptions)).StatusCode);
        Assert.True((await ListAsync(anonymous, $"projectSlug={slug}")).Enabled);
        Assert.Equal(2, (await ListAsync(anonymous, $"projectSlug={slug}")).TotalCount);
    }

    [Fact]
    public async Task 逐筆隱藏_客服可隱藏與恢復_名單即時反映_寫稽核_同名另一筆仍在則姓名仍列出()
    {
        var marker = $"CT{Guid.NewGuid():N}"[..8];
        var (projectId, slug) = await ArrangeAsync(marker);
        var lee = await fx.ScalarAsync<Guid>("SELECT id FROM donations WHERE donation_project_id = @p AND donor_name = @n", ("@p", projectId), ("@n", $"{marker}李小華"));
        var wang = await fx.ScalarAsync<Guid>("SELECT TOP 1 id FROM donations WHERE donation_project_id = @p AND donor_name = @n ORDER BY seq", ("@p", projectId), ("@n", $"{marker}王小明"));
        var admin = await fx.CreateAdminAsync(false, "customer_service_admin");
        using var cs = fx.CreateClientFor(admin);
        using var anonymous = fx.CreateClient();

        var hidden = await cs.PostAsJsonAsync($"{AdminBase}/donations/{lee}/credit-visibility", new { hidden = true }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        Assert.True((await hidden.Content.ReadFromJsonAsync<AdminCreditVisibilityDto>(TestJson.Options))!.IsCreditHidden);
        Assert.Equal(new[] { $"{marker}王小明" }, (await ListAsync(anonymous, $"projectSlug={slug}")).Names.ToArray());

        // 同名的兩筆只隱藏一筆：另一筆仍具名顯示，姓名仍在名單上
        Assert.Equal(HttpStatusCode.OK, (await cs.PostAsJsonAsync($"{AdminBase}/donations/{wang}/credit-visibility", new { hidden = true }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(new[] { $"{marker}王小明" }, (await ListAsync(anonymous, $"projectSlug={slug}")).Names.ToArray());

        // 後台詳情與列表看得到隱藏旗標
        var detail = await cs.GetFromJsonAsync<AdminDonationDetailDto>($"{AdminBase}/donations/{lee}", TestJson.Options);
        Assert.True(detail!.IsCreditHidden);

        var shown = await cs.PostAsJsonAsync($"{AdminBase}/donations/{lee}/credit-visibility", new { hidden = false }, TestJson.WriteOptions);
        Assert.False((await shown.Content.ReadFromJsonAsync<AdminCreditVisibilityDto>(TestJson.Options))!.IsCreditHidden);
        Assert.Equal(2, (await ListAsync(anonymous, $"projectSlug={slug}")).Names.Count);

        Assert.Equal(3, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'donation.credit_visibility'", ("@a", admin.Id)));
        // 沒變化的重複操作不重複寫稽核
        await cs.PostAsJsonAsync($"{AdminBase}/donations/{lee}/credit-visibility", new { hidden = false }, TestJson.WriteOptions);
        Assert.Equal(3, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'donation.credit_visibility'", ("@a", admin.Id)));
    }

    [Fact]
    public async Task 逐筆隱藏_授權_檢視者與商務不能隱藏_找不到的捐款404()
    {
        var marker = $"CT{Guid.NewGuid():N}"[..8];
        var (projectId, _) = await ArrangeAsync(marker);
        var id = await fx.ScalarAsync<Guid>("SELECT TOP 1 id FROM donations WHERE donation_project_id = @p AND donor_name = @n", ("@p", projectId), ("@n", $"{marker}李小華"));
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));

        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{AdminBase}/donations/{id}/credit-visibility", new { hidden = true }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PostAsJsonAsync($"{AdminBase}/donations/{id}/credit-visibility", new { hidden = true }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cs.PostAsJsonAsync($"{AdminBase}/donations/{Guid.NewGuid()}/credit-visibility", new { hidden = true }, TestJson.WriteOptions)).StatusCode);
        Assert.False(await fx.ScalarAsync<bool>("SELECT is_credit_hidden FROM donations WHERE id = @i", ("@i", id)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 成果回顧
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 成果回顧_已上架且有關聯慈善計畫的項目依計畫分組_名稱為快照_未上架與無關聯的不出現_附俱樂部網址()
    {
        var (a1, slugA1) = await CreateProjectAsync(fx, nameEn: "CT Project A1");
        var (a2, slugA2) = await CreateProjectAsync(fx);
        var (onlyCharity, slugCharity) = await CreateProjectAsync(fx);
        var (draft, slugDraft) = await CreateProjectAsync(fx, status: "draft");
        var (_, slugPlain) = await CreateProjectAsync(fx);
        var programCode = $"CT-PRG-{Guid.NewGuid():N}"[..14];
        await fx.ExecuteAsync(
            """
            UPDATE donation_projects SET charity_ref_code = N'CT-CH-1', charity_name_snapshot = N'CT公益團體', charity_program_ref_code = @p, charity_program_name_snapshot = N'CT少年足球計畫' WHERE id IN (@a1, @a2, @d);
            UPDATE donation_projects SET charity_ref_code = N'CT-CH-2', charity_name_snapshot = N'CT另一個團體' WHERE id = @c;
            """,
            ("@p", programCode), ("@a1", a1), ("@a2", a2), ("@d", draft), ("@c", onlyCharity));
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        await sys.PutAsJsonAsync($"{AdminBase}/settings", new { clubSiteUrl = "https://club.example.org" }, TestJson.WriteOptions);
        using var anonymous = fx.CreateClient();

        var zh = await anonymous.GetFromJsonAsync<PublicImpactDto>($"{Base}/impact?lang=zh", TestJson.Options);

        Assert.Equal("https://club.example.org", zh!.ClubSiteUrl);
        var program = Assert.Single(zh.Programs, p => p.ProgramRefCode == programCode);
        Assert.Equal("CT少年足球計畫", program.ProgramName);
        Assert.Equal("CT公益團體", program.CharityName);
        Assert.Equal(2, program.Projects.Count);
        Assert.Contains(program.Projects, p => p.Slug == slugA1);
        Assert.Contains(program.Projects, p => p.Slug == slugA2);
        Assert.DoesNotContain(zh.Programs.SelectMany(p => p.Projects), p => p.Slug == slugDraft);   // 草稿不出現
        Assert.DoesNotContain(zh.Programs.SelectMany(p => p.Projects), p => p.Slug == slugPlain);   // 沒有任何關聯的不出現
        var charityOnly = Assert.Single(zh.Programs, p => p.ProgramRefCode is null && p.CharityName == "CT另一個團體");
        Assert.Contains(charityOnly.Projects, p => p.Slug == slugCharity);

        // 英文：有英文名稱的用英文，其餘回退繁中並標示
        var en = await anonymous.GetFromJsonAsync<PublicImpactDto>($"{Base}/impact?lang=en", TestJson.Options);
        Assert.Contains(en!.Programs.Single(p => p.ProgramRefCode == programCode).Projects, p => p.Name == "CT Project A1");
        Assert.True(en.IsFallback);

        // 公開回應沒有分潤、金流、物件鍵等欄位
        var raw = await (await anonymous.GetAsync($"{Base}/impact")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("share", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coverKey", raw, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>徵信名單會成批輸出人名、成果回顧是公開端點：兩者共用「公開讀取」依 IP 限流，額度用盡回 429（獨立測試主機，額度壓到 3）。</summary>
[Collection(CharityRecognitionRateLimitCollection.Name)]
public sealed class CharityRecognitionRateLimitTests(CharityRecognitionRateLimitApiFixture fx)
{
    [Fact]
    public async Task 徵信名單與成果回顧依IP限流_額度用盡回429()
    {
        using var client = fx.CreateClient();
        for (var i = 0; i < CharityRecognitionRateLimitApiFixture.Permits; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/credit-list?projectSlug=ct-not-exist")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync($"{Base}/credit-list?projectSlug=ct-not-exist")).StatusCode);
        // 同一個額度也涵蓋成果回顧頁
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync($"{Base}/impact")).StatusCode);
    }
}
