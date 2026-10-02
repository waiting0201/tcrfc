using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N6 捐款報表（規劃書 §7）：捐款總覽（含轉換率與趨勢）、依店家、依項目、發票開立狀況、逐筆明細。預設只計 paid，可切換含已退款。
/// 🔴 全部報表不含捐款人個資；CSV 匯出需要匯出權限（能看不等於能匯出）。
/// 資料量分析一律用 projectId／storeId 限定到測試建立的資料（共用本機庫上有種子與其他測試資料）；付款日期落在 2025 年 6～7 月。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminReportsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Reports = AdminBase + "/reports";
    private const string Range = "from=2025-06-01&to=2025-07-31";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    /// <summary>一個專案、兩家店。三筆已付款（100／200／300）、一筆已退款（400）、一筆只建單沒付款（500）。</summary>
    private async Task<(Guid ProjectId, Guid StoreA, Guid StoreB)> ArrangeAsync()
    {
        var (projectId, _) = await CreateProjectAsync(fx, projectPct: 5m);
        var (storeA, _) = await CreateStoreAsync(fx, 10m);
        var (storeB, _) = await CreateStoreAsync(fx, 10m);
        await SeedPaidDonationAsync(fx, projectId, storeA, 100, NoonUtc(2025, 6, 10), 10, 5, 10m, 5m);
        await SeedPaidDonationAsync(fx, projectId, storeA, 200, NoonUtc(2025, 6, 10), 20, 10, 10m, 5m, invoiceIssueStatus: "failed");
        await SeedPaidDonationAsync(fx, projectId, storeB, 300, NoonUtc(2025, 7, 2), 30, 15, 10m, 5m, invoiceIssueStatus: "pending", invoiceMode: "donation_receipt");
        await SeedPaidDonationAsync(fx, projectId, null, 400, NoonUtc(2025, 6, 11), 0, 20, 0m, 5m, status: "refunded");
        await SeedPaidDonationAsync(fx, projectId, null, 500, NoonUtc(2025, 6, 12), 0, 0, 0m, 0m, status: "created");
        return (projectId, storeA, storeB);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 捐款總覽
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 捐款總覽_筆數總額平均_預設只計已付款_含已退款切換_轉換率以建單時間為母體()
    {
        var (projectId, _, _) = await ArrangeAsync();
        using var sys = await SysAsync();

        var paid = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}");
        Assert.Equal(3, paid.DonationCount);
        Assert.Equal(600, paid.TotalAmount);
        Assert.Equal(200, paid.AverageAmount);
        Assert.Equal(5, paid.CreatedCount);        // 母體：期間內建單的 5 筆
        Assert.Equal(4, paid.ConvertedCount);      // 其中成功付款（paid＋後來退款）的 4 筆
        Assert.Equal(0.8m, paid.ConversionRate);

        var all = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&paymentStatus=all");
        Assert.Equal(4, all.DonationCount);
        Assert.Equal(1000, all.TotalAmount);

        var refunded = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&paymentStatus=refunded");
        Assert.Equal(1, refunded.DonationCount);
        Assert.Equal(400, refunded.TotalAmount);
    }

    [Fact]
    public async Task 捐款總覽_趨勢可選日或月_期間篩選以台灣日期_空結果不除以零()
    {
        var (projectId, _, _) = await ArrangeAsync();
        using var sys = await SysAsync();

        var daily = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}");
        Assert.Equal("day", daily.Granularity);
        Assert.Equal(["2025-06-10", "2025-07-02"], daily.Trend.Select(t => t.Period).ToArray());
        Assert.Equal(300L, daily.Trend[0].Amount);
        Assert.Equal(2, daily.Trend[0].Count);

        var monthly = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&granularity=month");
        Assert.Equal(["2025-06", "2025-07"], monthly.Trend.Select(t => t.Period).ToArray());

        var june = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?from=2025-06-01&to=2025-06-30&projectId={projectId}");
        Assert.Equal(2, june.DonationCount);

        var empty = await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?from=2001-01-01&to=2001-01-31&projectId={projectId}");
        Assert.Equal(0, empty.DonationCount);
        Assert.Equal(0, empty.AverageAmount);
        Assert.Equal(0m, empty.ConversionRate);
        Assert.Empty(empty.Trend);
    }

    [Fact]
    public async Task 共通維度篩選_店家無店家歸屬金額級距憑證類型()
    {
        var (projectId, storeA, _) = await ArrangeAsync();
        using var sys = await SysAsync();

        Assert.Equal(2, (await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&storeId={storeA}")).DonationCount);
        Assert.Equal(0, (await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&noStore=true")).DonationCount); // 無店家的 400 是已退款
        Assert.Equal(1, (await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&noStore=true&paymentStatus=refunded")).DonationCount);
        Assert.Equal(1, (await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&amountMin=250&amountMax=350")).DonationCount);
        Assert.Equal(1, (await GetAsync<AdminReportOverviewDto>(sys, $"{Reports}/overview?{Range}&projectId={projectId}&invoiceType=donation_receipt")).DonationCount);
    }

    [Fact]
    public async Task 篩選參數錯誤回400()
    {
        using var sys = await SysAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.GetAsync($"{Reports}/overview?paymentStatus=bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.GetAsync($"{Reports}/overview?from=2025-07-01&to=2025-06-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.GetAsync($"{Reports}/overview?invoiceType=bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.GetAsync($"{Reports}/overview?amountMin=500&amountMax=100")).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 依店家、依項目（含已結算／未結算）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 依店家_筆數金額佔比_應付回饋金與已結算未結算_無店家歸屬合併一列()
    {
        var (projectId, storeA, storeB) = await ArrangeAsync();
        using var sys = await SysAsync();
        // 店家 A 的六月結算單：確認結算 → 應付 30 全數算已結算；店家 B（七月）未結算。
        var run = await sys.PostAsJsonAsync($"{AdminBase}/settlements/run", new { periodStart = "2025-06-01", periodEnd = "2025-06-30", payeeType = "store", payeeId = storeA }, TestJson.WriteOptions);
        var draft = (await run.Content.ReadFromJsonAsync<AdminSettlementRunResultDto>(TestJson.Options))!.Created.Single();
        Assert.Equal(HttpStatusCode.OK, (await sys.PostAsync($"{AdminBase}/settlements/{draft.Id}/settle", null)).StatusCode);

        var rows = await GetAsync<List<AdminReportStoreRowDto>>(sys, $"{Reports}/by-store?{Range}&projectId={projectId}&paymentStatus=all");

        var a = Assert.Single(rows, r => r.StoreId == storeA);
        Assert.Equal(2, a.DonationCount);
        Assert.Equal(300, a.TotalAmount);
        Assert.Equal(30, a.PayableAmount);
        Assert.Equal(30, a.SettledAmount);
        Assert.Equal(0, a.UnsettledAmount);
        Assert.Equal(10m, a.CurrentSharePct);

        var b = Assert.Single(rows, r => r.StoreId == storeB);
        Assert.Equal(300, b.TotalAmount);
        Assert.Equal(30, b.PayableAmount);
        Assert.Equal(0, b.SettledAmount);
        Assert.Equal(30, b.UnsettledAmount);

        var none = Assert.Single(rows, r => r.StoreId is null);
        Assert.Equal(400, none.TotalAmount);          // 已退款的 400，因為 paymentStatus=all
        Assert.Equal(0, none.PayableAmount);          // 已退款的捐款不產生應付回饋金
        Assert.Equal(1000, rows.Sum(r => r.TotalAmount));
        Assert.Equal(1m, Math.Round(rows.Sum(r => r.AmountRatio), 2));
    }

    [Fact]
    public async Task 依項目_應撥付金額與平均單筆_沒有目標達成率欄位()
    {
        var (projectId, _, _) = await ArrangeAsync();
        using var sys = await SysAsync();

        var rows = await GetAsync<List<AdminReportProjectRowDto>>(sys, $"{Reports}/by-project?{Range}&projectId={projectId}");

        var row = Assert.Single(rows);
        Assert.Equal(projectId, row.ProjectId);
        Assert.Equal(3, row.DonationCount);
        Assert.Equal(600, row.TotalAmount);
        Assert.Equal(200, row.AverageAmount);
        Assert.Equal(30, row.PayableAmount);   // 5 + 10 + 15
        Assert.Equal(30, row.UnsettledAmount);
        Assert.Equal(5m, row.CurrentSharePct);

        var raw = await (await sys.GetAsync($"{Reports}/by-project?{Range}&projectId={projectId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("goal", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("target", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 發票開立狀況、逐筆明細
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 發票開立狀況_依狀態統計_作廢與折讓另列()
    {
        var (projectId, _, _) = await ArrangeAsync();
        await fx.ExecuteAsync(
            "UPDATE donation_invoices SET void_status = N'voided' WHERE donation_id IN (SELECT id FROM donations WHERE donation_project_id = @p AND amount = 100)", ("@p", projectId));
        using var sys = await SysAsync();

        var s = await GetAsync<AdminReportInvoiceStatusDto>(sys, $"{Reports}/invoice-status?{Range}&projectId={projectId}");

        Assert.Equal(0, s.Issued);     // 唯一一張已開立的被作廢了
        Assert.Equal(1, s.Voided);
        Assert.Equal(1, s.Failed);
        Assert.Equal(1, s.Pending);
        Assert.Equal(0, s.Allowance);
        Assert.Equal(0, s.NoInvoice);
    }

    [Fact]
    public async Task 逐筆明細_分頁_不含任何捐款人個資_含分潤拆分()
    {
        var (projectId, _, _) = await ArrangeAsync();
        using var sys = await SysAsync();

        var page1 = await GetAsync<PagedResult<AdminReportDetailRowDto>>(sys, $"{Reports}/details?{Range}&projectId={projectId}&pageSize=2");
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
        var raw = await (await sys.GetAsync($"{Reports}/details?{Range}&projectId={projectId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("charity-test.invalid", raw);
        Assert.DoesNotContain("測試捐款人", raw);
        Assert.DoesNotContain("donorEmail", raw, StringComparison.OrdinalIgnoreCase);

        var all = await GetAsync<PagedResult<AdminReportDetailRowDto>>(sys, $"{Reports}/details?{Range}&projectId={projectId}");
        Assert.Equal(60, all.Items.Sum(i => i.StoreAmount));
        Assert.Equal(30, all.Items.Sum(i => i.ProjectAmount));
        Assert.Equal(510, all.Items.Sum(i => i.AssociationAmount)); // 600 − 60 − 30：三者加總永遠等於金額
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CSV 與授權
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CSV匯出_UTF8含BOM_需要匯出權限_檢視者與商務與客服能看不能匯出()
    {
        var (projectId, _, _) = await ArrangeAsync();
        using var sys = await SysAsync();
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));

        foreach (var name in new[] { "overview", "by-store", "by-project", "invoice-status", "details" })
        {
            var csv = await sys.GetAsync($"{Reports}/{name}?{Range}&projectId={projectId}&format=csv");
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Equal("text/csv; charset=utf-8", csv.Content.Headers.ContentType!.ToString());
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, (await csv.Content.ReadAsByteArrayAsync())[..3]);

            // 能看報表（JSON）不等於能匯出（CSV）：403，不會悄悄降級
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{Reports}/{name}?{Range}&projectId={projectId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{Reports}/{name}?{Range}&projectId={projectId}&format=csv")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await biz.GetAsync($"{Reports}/overview?{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync($"{Reports}/overview?{Range}&format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cs.GetAsync($"{Reports}/by-store?{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync($"{Reports}/overview")).StatusCode);

        var detailsCsv = Encoding.UTF8.GetString(await (await sys.GetAsync($"{Reports}/details?{Range}&projectId={projectId}&format=csv")).Content.ReadAsByteArrayAsync());
        Assert.Contains("單號", detailsCsv);
        Assert.DoesNotContain("charity-test.invalid", detailsCsv);
        Assert.Equal(4, detailsCsv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // 表頭＋3 筆
    }

    [Fact]
    public async Task 授權_公關媒體與內容編輯等無N6權限的角色回403()
    {
        using var contentEditor = fx.CreateClientFor(await fx.CreateAdminAsync(false, "content_editor"));

        Assert.Equal(HttpStatusCode.Forbidden, (await contentEditor.GetAsync($"{Reports}/overview")).StatusCode);
    }
}
