using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 稽核紀錄查詢（規劃書 §11.2）與 N2 區塊內文編輯。稽核是 append-only 的勸募法遵紀錄：只有查詢、沒有改刪；
/// 查詢權限 <c>n7.audit_log.view</c> 是 <c>sysadmin_only</c>（連被指派了這個碼的其他角色也看不到）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAuditAndContentTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Audit = AdminBase + "/audit-logs";
    private const string Projects = AdminBase + "/projects";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 稽核紀錄查詢
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 稽核查詢_已寫入的明文檢視與匯出可查到_動作與對象為日常中文_對象帶好讀名稱_摘要不含個資()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var donation = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, donorName: "王小明");
        var cs = await fx.CreateAdminAsync(false, "customer_service_admin");
        using var csClient = fx.CreateClientFor(cs);
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));

        // 客服解除個資遮罩（reveal）與含個資匯出（purpose 必填）——兩個都必須留下稽核
        Assert.Equal(HttpStatusCode.OK, (await csClient.GetAsync($"{AdminBase}/donations/{donation.Id}?reveal=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await csClient.GetAsync($"{AdminBase}/donations/export?keyword={donation.OrderNo}&purpose=年度會計查核")).StatusCode);

        var page = await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}"));

        Assert.Equal(2, page.TotalCount);
        var reveal = Assert.Single(page.Items, i => i.Action == "donation.reveal_pii");
        Assert.Equal("檢視捐款人個資明文", reveal.ActionLabel);
        Assert.Equal("捐款", reveal.TargetTypeLabel);
        Assert.Equal(donation.OrderNo, reveal.TargetLabel);
        Assert.Equal(donation.Id, reveal.TargetId);
        Assert.Equal("測試後台人員", reveal.AdminName);
        var export = Assert.Single(page.Items, i => i.Action == "donation.export_pii");
        Assert.Equal("年度會計查核", export.PurposeNote);
        Assert.Contains("1 筆", export.ChangeSummary);

        // 摘要與用途備註不含個資明文（Email、姓名）
        var raw = await (await sys.GetAsync($"{Audit}?adminUserId={cs.Id}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("charity-test.invalid", raw);
        Assert.DoesNotContain("王小明", raw);
        Assert.True(page.Items.All(i => i.OccurredAt > DateTime.UtcNow.AddMinutes(-5) && i.OccurredAt <= DateTime.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public async Task 稽核查詢_可依動作對象類型對象編號操作者關鍵字與期間篩選_並分頁_由新到舊()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var d1 = await SeedPaidDonationAsync(fx, projectId, null, 100, DateTime.UtcNow);
        var d2 = await SeedPaidDonationAsync(fx, projectId, null, 200, DateTime.UtcNow);
        var cs = await fx.CreateAdminAsync(false, "customer_service_admin");
        using var csClient = fx.CreateClientFor(cs);
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        await csClient.GetAsync($"{AdminBase}/donations/{d1.Id}?reveal=true");
        await csClient.GetAsync($"{AdminBase}/donations/{d2.Id}?reveal=true");
        await csClient.PostAsJsonAsync($"{AdminBase}/donations/{d2.Id}/credit-visibility", new { hidden = true }, TestJson.WriteOptions);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var byAction = await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&action=donation.reveal_pii"));
        Assert.Equal(2, byAction.TotalCount);
        Assert.Equal(1, (await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&targetId={d1.Id}"))).TotalCount);
        Assert.Equal(3, (await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&targetType=donation"))).TotalCount);
        Assert.Equal(1, (await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&keyword=徵信名單"))).TotalCount);
        Assert.Equal(3, (await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}"))).TotalCount);
        Assert.Equal(0, (await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&from=2001-01-01&to=2001-01-31"))).TotalCount);

        var p1 = await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&pageSize=2&page=1"));
        var p2 = await ReadAsync<PagedResult<AdminAuditLogDto>>(await sys.GetAsync($"{Audit}?adminUserId={cs.Id}&pageSize=2&page=2"));
        Assert.Equal(2, p1.Items.Count);
        Assert.Single(p2.Items);
        Assert.Equal(2, p1.TotalPages);
        var all = p1.Items.Concat(p2.Items).ToList();
        Assert.Equal(all.OrderByDescending(i => i.OccurredAt).Select(i => i.Id), all.Select(i => i.Id)); // 由新到舊
        Assert.Equal("donation.credit_visibility", p1.Items[0].Action);
    }

    [Fact]
    public async Task 稽核查詢_只有系統管理員_其他角色與被指派了這個權限碼的角色也一律403_未登入401()
    {
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));
        // sysadmin_only：即使有人把權限碼指派給別的角色，非系統管理員仍然看不到
        var sneakyRole = await fx.CreateRoleAsync("n7.audit_log.view");
        using var sneaky = fx.CreateClientFor(await fx.CreateAdminAsync(false, sneakyRole));
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));

        foreach (var client in new[] { viewer, cs, biz, sneaky })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Audit)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Audit}/actions")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await sys.GetAsync(Audit)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync(Audit)).StatusCode);
    }

    [Fact]
    public async Task 稽核紀錄不可改刪_沒有任何寫入端點_動作清單提供日常中文標籤()
    {
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await sys.DeleteAsync(Audit)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await sys.PutAsJsonAsync(Audit, new { }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await sys.PostAsJsonAsync(Audit, new { }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.DeleteAsync($"{Audit}/{id}")).StatusCode);

        var actions = await ReadAsync<List<AdminAuditActionOptionDto>>(await sys.GetAsync($"{Audit}/actions"));
        Assert.Contains(actions, a => a.Action == "donation.refund" && a.Label == "人工退款");
        Assert.Contains(actions, a => a.Action == "settlement.mark_paid" && a.Label == "登記已付款");
        Assert.All(actions, a => Assert.DoesNotMatch("^[a-z_.]+$", a.Label)); // 標籤是日常中文，不是代碼
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N2 區塊內文編輯
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 項目內文編輯_只動內文三項_省略的欄位不變_不碰分潤金額選項與名稱_寫稽核()
    {
        var (projectId, slug) = await CreateProjectAsync(fx, projectPct: 12.5m, options: [100, 300, 500]);
        var admin = await fx.CreateAdminAsync(false, "business_sponsorship"); // 商務：可編輯項目，但沒有「設定分潤」權限
        using var biz = fx.CreateClientFor(admin);
        var before = await ReadAsync<AdminProjectDetailDto>(await biz.GetAsync($"{Projects}/{projectId}"));

        var blocks = JsonSerializer.Deserialize<JsonElement>("""{"blocks":[{"type":"heading","text":"新標題"},{"type":"paragraph","text":"新內文"}]}""");
        var updated = await ReadAsync<AdminProjectDetailDto>(await biz.PutAsJsonAsync($"{Projects}/{projectId}/content",
            new { descriptionZh = blocks, fundUsageZh = "新的款項用途", oneLinerEn = "English one liner" }, TestJson.WriteOptions));

        Assert.Contains("新內文", updated.DescriptionZh!.Value.GetRawText());
        Assert.Equal("新的款項用途", updated.FundUsageZh);
        Assert.Equal("English one liner", updated.OneLinerEn);
        Assert.Equal(before.OneLinerZh, updated.OneLinerZh);                // 沒帶的欄位不變
        Assert.Equal(before.NameZh, updated.NameZh);
        Assert.Equal(12.5m, updated.ProjectSharePct);                       // 分潤沒被碰到
        Assert.Equal(before.AmountOptions, updated.AmountOptions);
        Assert.Equal(before.Status, updated.Status);
        Assert.Equal(before.InvoiceMode, updated.InvoiceMode);
        Assert.Equal(before.NameZh, updated.NameEn);                        // 英文列不存在時以繁中名稱頂著（欄位必填）

        // 公開端點立刻反映（已上架）
        using var anonymous = fx.CreateClient();
        var publicDetail = await anonymous.GetFromJsonAsync<PublicProjectDetailDtoShape>($"{Base}/projects/{slug}?lang=zh", TestJson.Options);
        Assert.Equal("新的款項用途", publicDetail!.FundUsage);

        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'project.content_update' AND target_id = @t", ("@a", admin.Id), ("@t", projectId)));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'project.share_pct_set'", ("@a", admin.Id)));
    }

    private sealed record PublicProjectDetailDtoShape(string? FundUsage);

    [Fact]
    public async Task 項目內文編輯_空物件或空陣列清空說明內文_空字串清空文字欄位_沒帶任何欄位等於沒有變更()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        var before = await ReadAsync<AdminProjectDetailDto>(await sys.GetAsync($"{Projects}/{projectId}"));
        Assert.NotNull(before.DescriptionZh);

        var cleared = await ReadAsync<AdminProjectDetailDto>(await sys.PutAsJsonAsync($"{Projects}/{projectId}/content",
            new { descriptionZh = new { }, oneLinerZh = "", fundUsageZh = "" }, TestJson.WriteOptions));
        Assert.Null(cleared.DescriptionZh);
        Assert.Null(cleared.OneLinerZh);
        Assert.Null(cleared.FundUsageZh);
        Assert.Equal(before.NameZh, cleared.NameZh);

        var noop = await ReadAsync<AdminProjectDetailDto>(await sys.PutAsJsonAsync($"{Projects}/{projectId}/content", new { }, TestJson.WriteOptions));
        Assert.Equal(cleared.UpdatedAtOrDefault(), noop.UpdatedAtOrDefault());
    }

    [Fact]
    public async Task 項目內文編輯_格式與長度錯誤回400_找不到404_檢視者403_未登入401()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        using var sys = fx.CreateClientFor(await fx.CreateAdminAsync(true));
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Projects}/{projectId}/content", new { descriptionZh = "純文字不是區塊內容" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Projects}/{projectId}/content", new { fundUsageZh = new string('字', 5001) }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Projects}/{projectId}/content", new { oneLinerZh = new string('字', 256) }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Projects}/{Guid.NewGuid()}/content", new { fundUsageZh = "x" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"{Projects}/{projectId}/content", new { fundUsageZh = "x" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().PutAsJsonAsync($"{Projects}/{projectId}/content", new { fundUsageZh = "x" }, TestJson.WriteOptions)).StatusCode);
    }
}

internal static class ProjectDetailTestExtensions
{
    /// <summary>用整份詳情的序列化結果當「有沒有變」的指紋（詳情沒有 updatedAt 欄位時的替代）。</summary>
    public static string UpdatedAtOrDefault(this AdminProjectDetailDto dto) => JsonSerializer.Serialize(dto);
}
