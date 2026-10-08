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
/// N5 發票與收據管理（規劃書 §6.5）：列表與篩選、手動填入外部號碼、作廢（限當期）、折讓（跨期）、供會計申報的明細 CSV。
/// 🔴 列表與 CSV 不含捐款人個資；每個操作記錄經辦人與原因（稽核＋void_reason／voided_by）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminInvoicesTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Invoices = AdminBase + "/invoices";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<(CharityTestAdmin Admin, HttpClient Client)> LoginAsync(bool superAdmin, params string[] roles)
    {
        var admin = await fx.CreateAdminAsync(superAdmin, roles);
        return (admin, fx.CreateClientFor(admin));
    }

    private static async Task<AdminInvoiceListItemDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminInvoiceListItemDto>(TestJson.Options))!;
    }

    private async Task<Guid> InvoiceIdAsync(Guid donationId)
        => await fx.ScalarAsync<Guid>("SELECT id FROM donation_invoices WHERE donation_id = @d", ("@d", donationId));

    private async Task<(Guid ProjectId, Guid InvoiceId, string OrderNo, Guid DonationId)> InvoiceAsync(
        string issueStatus, DateTime? paidAt = null, string? invoiceNo = null, string mode = "b2c_invoice")
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var d = await SeedPaidDonationAsync(fx, projectId, null, 500, paidAt ?? DateTime.UtcNow, invoiceIssueStatus: issueStatus, invoiceMode: mode, invoiceNo: invoiceNo);
        return (projectId, await InvoiceIdAsync(d.Id), d.OrderNo, d.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 列表與篩選
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 列表_依開立狀態類型與單號篩選_不含捐款人個資()
    {
        var failed = await InvoiceAsync("failed");
        var issued = await InvoiceAsync("issued");
        var receipt = await InvoiceAsync("pending", mode: "donation_receipt");
        var (_, client) = await LoginAsync(true);

        var failedList = await client.GetFromJsonAsync<PagedResult<AdminInvoiceListItemDto>>($"{Invoices}?issueStatus=failed&keyword={failed.OrderNo}", TestJson.Options);
        var one = Assert.Single(failedList!.Items);
        Assert.Equal(failed.InvoiceId, one.InvoiceId);
        Assert.Equal("failed", one.IssueStatus);
        Assert.Null(one.InvoiceNo);

        var byType = await client.GetFromJsonAsync<PagedResult<AdminInvoiceListItemDto>>($"{Invoices}?invoiceType=donation_receipt&keyword={receipt.OrderNo}", TestJson.Options);
        Assert.Equal("donation_receipt", Assert.Single(byType!.Items).InvoiceType);

        var raw = await (await client.GetAsync($"{Invoices}?keyword={issued.OrderNo}")).Content.ReadAsStringAsync();
        Assert.Contains(issued.OrderNo, raw);
        Assert.DoesNotContain("charity-test.invalid", raw);   // 捐款人 Email
        Assert.DoesNotContain("測試捐款人", raw);              // 捐款人姓名
        Assert.DoesNotContain("donorName", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nationalId", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 手動填入外部號碼
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 手動填入號碼_失敗的憑證轉為已開立_寄出通知_留稽核_重複號碼與格式錯誤被擋()
    {
        var failed = await InvoiceAsync("failed");
        var other = await InvoiceAsync("failed");
        var (admin, client) = await LoginAsync(true);
        var no = $"AB-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

        // 格式與原因
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = "ab", reason = "補開" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = no, reason = "" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = no, reason = "補開", issuedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)) }, TestJson.WriteOptions)).StatusCode);

        var done = await ReadAsync(await client.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number",
            new { invoiceNo = no.ToLowerInvariant(), reason = "加值中心後台人工補開" }, TestJson.WriteOptions));
        Assert.Equal("issued", done.IssueStatus);
        Assert.Equal(no, done.InvoiceNo);      // 小寫輸入被正規化成大寫
        Assert.NotNull(done.IssuedAt);
        Assert.Equal(12, done.IssuedAt!.Value.AddHours(8).Hour); // 預設開立日的台灣時間中午，不是清晨 4 點

        Assert.Contains(fx.Mail.Sent, m => m.TemplateCode == "invoice_issued");
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'invoice.manual_number' AND target_id = @t", ("@a", admin.Id), ("@t", failed.InvoiceId)));

        // 已開立不可再填；號碼不可重複登記在另一筆
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = "ZZ-99999999", reason = "再填一次" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{other.InvoiceId}/manual-number", new { invoiceNo = no, reason = "同號碼" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Invoices}/{Guid.NewGuid()}/manual-number", new { invoiceNo = "ZZ-12345678", reason = "找不到" }, TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 作廢與折讓
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 作廢_開立當期內可作廢_呼叫加值中心_記錄原因與經辦人_不可重複作廢()
    {
        var current = await InvoiceAsync("issued");
        var (admin, client) = await LoginAsync(true);

        var voided = await ReadAsync(await client.PostAsJsonAsync($"{Invoices}/{current.InvoiceId}/void", new { reason = "捐款人資料填錯" }, TestJson.WriteOptions));

        Assert.Equal("voided", voided.VoidStatus);
        Assert.Equal("捐款人資料填錯", voided.VoidReason);
        Assert.Equal(1, fx.Invoices.VoidCalls);
        Assert.Equal(admin.Id, await fx.ScalarAsync<Guid>("SELECT voided_by FROM donation_invoices WHERE id = @i", ("@i", current.InvoiceId)));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'invoice.void' AND target_id = @t", ("@a", admin.Id), ("@t", current.InvoiceId)));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{current.InvoiceId}/void", new { reason = "再作廢一次" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{current.InvoiceId}/allowance", new { reason = "作廢後折讓" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Invoices}/{current.InvoiceId}/void", new { reason = " " }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 跨期不可作廢只能折讓_尚未開立的憑證作廢不呼叫加值中心_折讓只限已開立()
    {
        var crossPeriod = await InvoiceAsync("issued", paidAt: NoonUtc(2025, 1, 10));       // 開立在 2025 年 1 月，早已跨期
        var notIssued = await InvoiceAsync("failed");
        var (_, client) = await LoginAsync(true);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{crossPeriod.InvoiceId}/void", new { reason = "跨期作廢" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(0, fx.Invoices.VoidCalls);

        var allowance = await ReadAsync(await client.PostAsJsonAsync($"{Invoices}/{crossPeriod.InvoiceId}/allowance", new { reason = "跨期折讓" }, TestJson.WriteOptions));
        Assert.Equal("allowance", allowance.VoidStatus);
        Assert.Equal(1, fx.Invoices.AllowanceCalls);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Invoices}/{notIssued.InvoiceId}/allowance", new { reason = "尚未開立不能折讓" }, TestJson.WriteOptions)).StatusCode);
        var voided = await ReadAsync(await client.PostAsJsonAsync($"{Invoices}/{notIssued.InvoiceId}/void", new { reason = "不再開立" }, TestJson.WriteOptions));
        Assert.Equal("voided", voided.VoidStatus);
        Assert.Equal(0, fx.Invoices.VoidCalls); // 沒開立過，不需要對加值中心動作
    }

    [Fact]
    public async Task 加值中心連不上_作廢與折讓回503_憑證狀態不變()
    {
        var issued = await InvoiceAsync("issued");
        fx.Invoices.VoidFails = true;
        var (_, client) = await LoginAsync(true);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync($"{Invoices}/{issued.InvoiceId}/void", new { reason = "測試連線失敗" }, TestJson.WriteOptions)).StatusCode);

        Assert.Equal("none", await fx.ScalarAsync<string>("SELECT void_status FROM donation_invoices WHERE id = @i", ("@i", issued.InvoiceId)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 匯出、授權
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 匯出CSV_含憑證號碼與金額狀態_不含捐款人個資_留稽核()
    {
        var issued = await InvoiceAsync("issued", invoiceNo: "EXPORT-00000001");
        var (admin, client) = await LoginAsync(true);

        var response = await client.GetAsync($"{Invoices}/export?keyword={issued.OrderNo}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("EXPORT-00000001", text);
        Assert.Contains("電子發票", text);
        Assert.Contains("已開立", text);
        Assert.DoesNotContain("charity-test.invalid", text);
        Assert.DoesNotContain("測試捐款人", text);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'invoice.export'", ("@a", admin.Id)));
    }

    [Fact]
    public async Task 授權_檢視者只能看_客服可補開與作廢_商務沒有N5權限_未登入401()
    {
        var issued = await InvoiceAsync("issued");
        var failed = await InvoiceAsync("failed");
        var (_, viewer) = await LoginAsync(false, "viewer");
        var (_, cs) = await LoginAsync(false, "customer_service_admin");
        var (_, biz) = await LoginAsync(false, "business_sponsorship");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Invoices)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = "VW-12345678", reason = "檢視者不可" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Invoices}/{issued.InvoiceId}/void", new { reason = "檢視者不可" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync(Invoices)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync(Invoices)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await cs.PostAsJsonAsync($"{Invoices}/{failed.InvoiceId}/manual-number", new { invoiceNo = "CS-12345678", reason = "客服補開" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cs.PostAsJsonAsync($"{Invoices}/{issued.InvoiceId}/void", new { reason = "客服作廢" }, TestJson.WriteOptions)).StatusCode);
    }
}
