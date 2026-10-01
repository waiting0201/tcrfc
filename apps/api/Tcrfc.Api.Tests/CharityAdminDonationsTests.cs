using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N3 捐款紀錄：授權矩陣、個資遮罩與明文檢視的稽核、篩選、退款（三個連動）、含個資匯出的用途備註與稽核、異常佇列與處理動作。
/// 🔴 這裡的重點是<b>「三類操作全數留稽核」與「個資只在 API 層遮罩」</b>——前端隱藏不算數。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminDonationsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Donations = AdminBase + "/donations";

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

    private static async Task<AdminDonationDetailDto> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminDonationDetailDto>(TestJson.Options))!;
    }

    /// <summary>建一筆已付款的捐款（走完整的公開流程），回傳 (單號, id, 捐款人 Email)。</summary>
    private async Task<(string OrderNo, Guid Id, string Email)> PaidAsync(
        string projectSlug, int amount = 500, string? storeSlug = null, object? invoice = null, string? donorName = null)
    {
        var email = Email("paid");
        using var publicClient = fx.CreateClient();
        var request = NewRequest(projectSlug, amount, storeSlug, invoice, email);
        if (donorName is not null)
        {
            request = request with { DonorName = donorName };
        }

        var orderNo = await CreateDonationAsync(publicClient, request);
        await StartPaymentAsync(publicClient, orderNo);
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(publicClient, orderNo, await LatestTransactionIdAsync(fx, orderNo))).StatusCode);
        return (orderNo, await DonationIdAsync(fx, orderNo), email);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 授權矩陣
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 授權矩陣_商務沒有N3權限_唯讀與客服可看列表_退款只有系統管理員_個資明文只有客服與系統管理員()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug);
        var (_, biz) = await LoginAsync(false, "business_sponsorship");
        var (_, viewer) = await LoginAsync(false, "viewer");
        var (_, cs) = await LoginAsync(false, "customer_service_admin");
        var (_, sys) = await LoginAsync(true);

        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync(Donations)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Donations)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cs.GetAsync(Donations)).StatusCode);

        // 個資明文
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{Donations}/{paid.Id}?reveal=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync($"{Donations}/{paid.Id}?reveal=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cs.GetAsync($"{Donations}/{paid.Id}?reveal=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sys.GetAsync($"{Donations}/{paid.Id}?reveal=true")).StatusCode);

        // 退款：sysadmin_only——客服（有 reveal／export）也不行
        var body = new { reason = "測試退款" };
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", body, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cs.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", body, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(0, fx.Gateway.RefundCalls);
        Assert.Equal("paid", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE id = @i", ("@i", paid.Id)));

        // 匯出：唯讀不行
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{Donations}/export?purpose=月底核對帳務")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Donations}/{paid.Id}/resend-thanks", null)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 個資遮罩（API 層）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 詳情預設遮罩姓名與Email_身分證字號與地址也遮罩_明文需reveal且每次寫稽核()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx, invoiceMode: "donation_receipt");
        var paid = await PaidAsync(projectSlug, 800, invoice: new { nationalId = "A123456789", address = "臺中市西區測試路 8 號 5 樓" }, donorName: "王小明明");
        var (cs, csClient) = await LoginAsync(false, "customer_service_admin");

        var masked = await csClient.GetStringAsync($"{Donations}/{paid.Id}");

        Assert.DoesNotContain(paid.Email, masked);
        Assert.DoesNotContain("王小明明", masked);
        Assert.DoesNotContain("A123456789", masked);
        Assert.DoesNotContain("測試路 8 號", masked);
        Assert.Contains("A******789", masked);   // 身分證字號只留首字母與末三碼
        Assert.Contains("王○○明", masked);        // 姓名遮罩
        Assert.Contains("p***@", masked);          // Email 遮罩
        Assert.DoesNotContain("rawResponse", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("encrypted", masked, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.reveal_pii' AND admin_user_id = @a", ("@a", cs.Id)));

        var revealed = await ReadAsync(await csClient.GetAsync($"{Donations}/{paid.Id}?reveal=true"));
        Assert.True(revealed.Donor.Revealed);
        Assert.Equal("王小明明", revealed.Donor.Name);
        Assert.Equal(paid.Email, revealed.Donor.Email);
        Assert.Equal("A123456789", revealed.Invoice!.NationalId);
        Assert.Equal("臺中市西區測試路 8 號 5 樓", revealed.Invoice.ReceiptAddress);

        await csClient.GetAsync($"{Donations}/{paid.Id}?reveal=true");
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.reveal_pii' AND admin_user_id = @a AND target_id = @t", ("@a", cs.Id), ("@t", paid.Id)));
        var summaries = await fx.ScalarAsync<string>("SELECT TOP 1 ISNULL(change_summary, N'') FROM audit_logs WHERE action = N'donation.reveal_pii' AND target_id = @t", ("@t", paid.Id));
        Assert.DoesNotContain(paid.Email, summaries!); // 稽核本身不含個資明文
    }

    [Fact]
    public async Task 詳情_含金流交易_分潤快照_憑證_時間軸_不含金流原始回應()
    {
        var (projectId, projectSlug) = await CreateProjectAsync(fx, projectPct: 10m);
        var (_, storeSlug) = await CreateStoreAsync(fx, storePct: 5m);
        var paid = await PaidAsync(projectSlug, 1000, storeSlug);
        var (_, client) = await LoginAsync(true);

        var detail = await ReadAsync(await client.GetAsync($"{Donations}/{paid.Id}"));

        Assert.Equal("paid", detail.Status);
        Assert.Equal(projectId, detail.Project.Id);
        Assert.NotNull(detail.Store);
        Assert.Equal((5m, 10m, 50, 100, 850), (detail.Split.StoreSharePct, detail.Split.ProjectSharePct, detail.Split.StoreAmount, detail.Split.ProjectAmount, detail.Split.AssociationAmount));
        var payment = Assert.Single(detail.Payments);
        Assert.Equal("confirmed", payment.Status);
        Assert.StartsWith("TEST-TX-", payment.TransactionId);
        Assert.Equal("issued", detail.Invoice!.IssueStatus);
        Assert.Equal($"TEST-{paid.OrderNo}", detail.Invoice.InvoiceNo);
        Assert.Contains(detail.Timeline, t => t.Kind == "created");
        Assert.Contains(detail.Timeline, t => t.Kind == "payment_confirmed");
        Assert.Contains(detail.Timeline, t => t.Kind == "paid");
        Assert.False(detail.NeedsManualReview);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Donations}/{Guid.NewGuid()}")).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 列表與篩選
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 列表篩選_狀態_項目_店家_無店家_憑證狀態_金額級距_單號前綴_分頁()
    {
        var (projectA, slugA) = await CreateProjectAsync(fx);
        var (projectB, slugB) = await CreateProjectAsync(fx);
        var (storeId, storeSlug) = await CreateStoreAsync(fx);
        using var publicClient = fx.CreateClient();
        var paidA = await PaidAsync(slugA, 500, storeSlug);
        var paidB = await PaidAsync(slugB, 2500);
        var created = await CreateDonationAsync(publicClient, NewRequest(slugA, 300));
        var (_, client) = await LoginAsync(true);

        async Task<List<string>> OrderNosAsync(string query)
        {
            var page = (await client.GetFromJsonAsync<PagedResult<AdminDonationListItemDto>>($"{Donations}?{query}&pageSize=100", TestJson.Options))!;
            return page.Items.Select(i => i.OrderNo).ToList();
        }

        Assert.Equal([created], await OrderNosAsync($"projectId={projectA}&status=created"));
        Assert.Equal([paidB.OrderNo], await OrderNosAsync($"projectId={projectB}"));
        Assert.Equal([paidA.OrderNo], await OrderNosAsync($"storeId={storeId}"));
        Assert.Contains(paidB.OrderNo, await OrderNosAsync($"projectId={projectB}&noStore=true"));
        Assert.DoesNotContain(paidA.OrderNo, await OrderNosAsync($"projectId={projectA}&noStore=true"));
        Assert.Equal([paidA.OrderNo], await OrderNosAsync($"status=paid&amountMin=500&amountMax=2500&projectId={projectA}"));
        Assert.Equal([paidB.OrderNo], await OrderNosAsync($"status=paid&projectId={projectB}"));
        Assert.Equal([paidB.OrderNo], await OrderNosAsync($"projectId={projectB}&amountMin=2000"));
        Assert.Equal([paidA.OrderNo], await OrderNosAsync($"keyword={paidA.OrderNo[..12]}"));
        Assert.Equal([paidA.OrderNo], await OrderNosAsync($"invoiceStatus=issued&storeId={storeId}"));

        var page1 = (await client.GetFromJsonAsync<PagedResult<AdminDonationListItemDto>>($"{Donations}?pageSize=2", TestJson.Options))!;
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount >= 13);   // 種子 10 筆 ＋ 本測試 3 筆
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Donations}?pageSize=99999&page=-3")).StatusCode); // 不合理的分頁參數被正規化，不爆
    }

    [Fact]
    public async Task 列表篩選_期間以台灣日期計算_UTC邊界不會算錯天()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        using var publicClient = fx.CreateClient();
        var orderNo = await CreateDonationAsync(publicClient, NewRequest(projectSlug, 500));
        // UTC 2026-01-14 17:00 ＝ 台灣 2026-01-15 01:00
        await fx.ExecuteAsync("UPDATE donations SET created_at = '2026-01-14T17:00:00' WHERE order_no = @o", ("@o", orderNo));
        var (_, client) = await LoginAsync(true);

        async Task<bool> FoundAsync(string query)
            => (await client.GetFromJsonAsync<PagedResult<AdminDonationListItemDto>>($"{Donations}?{query}&pageSize=100", TestJson.Options))!.Items.Any(i => i.OrderNo == orderNo);

        Assert.True(await FoundAsync("from=2026-01-15&to=2026-01-15"));
        Assert.False(await FoundAsync("from=2026-01-14&to=2026-01-14"));
        Assert.False(await FoundAsync("from=2026-01-16"));
        Assert.True(await FoundAsync("to=2026-01-15"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 退款
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 退款_三個連動_金流退款_憑證作廢_退款通知信_並寫稽核_稽核不含個資()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 1200);
        var (sys, client) = await LoginAsync(true);

        var response = await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "誤按金額，客服確認" }, TestJson.WriteOptions);
        var detail = await ReadAsync(response);

        Assert.Equal("refunded", detail.Status);
        Assert.Equal("誤按金額，客服確認", detail.Refund!.Reason);
        Assert.Equal("測試後台人員", detail.Refund.RefundedByName);
        Assert.Equal(1, fx.Gateway.RefundCalls);
        Assert.Equal("refunded", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE id = @i", ("@i", paid.Id)));
        Assert.Equal(sys.Id, await fx.ScalarAsync<Guid>("SELECT refunded_by FROM donations WHERE id = @i", ("@i", paid.Id)));

        // 憑證：當期內 → 作廢（不是折讓）
        Assert.Equal(1, fx.Invoices.VoidCalls);
        Assert.Equal(0, fx.Invoices.AllowanceCalls);
        Assert.Equal("voided", detail.Invoice!.VoidStatus);
        Assert.Equal("誤按金額，客服確認", detail.Invoice.VoidReason);

        // 退款通知信
        var notice = Assert.Single(fx.Mail.Sent, m => m.TemplateCode == "refund_notice");
        Assert.Equal(paid.Email, notice.ToAddress);

        // 稽核：操作者、對象、原因；不含 Email
        var summary = await fx.ScalarAsync<string>("SELECT change_summary FROM audit_logs WHERE action = N'donation.refund' AND target_id = @t AND admin_user_id = @a", ("@t", paid.Id), ("@a", sys.Id));
        Assert.Contains("1,200", summary!);
        Assert.Contains("誤按金額", summary);
        Assert.DoesNotContain(paid.Email, summary);

        // 只能退一次
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "再退一次" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(1, fx.Gateway.RefundCalls);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.refund' AND target_id = @t", ("@t", paid.Id)));
    }

    [Fact]
    public async Task 退款_並發六個請求_交易層級應用程式鎖串行化_金流只退一次_只有一個成功_稽核只有一筆()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 900);
        fx.Gateway.RefundDelay = TimeSpan.FromMilliseconds(400); // 讓請求確定重疊在「金流退款進行中」
        var (_, client) = await LoginAsync(true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "並發退款測試" }, TestJson.WriteOptions)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, fx.Gateway.RefundCalls);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.refund' AND target_id = @t", ("@t", paid.Id)));
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "refund_notice"));
        Assert.Equal(1, fx.Invoices.VoidCalls);
    }

    [Fact]
    public async Task 退款_憑證已跨期_改開折讓而不是作廢()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 700);
        await fx.ExecuteAsync("UPDATE donation_invoices SET issued_at = DATEADD(MONTH, -4, SYSUTCDATETIME()) WHERE donation_id = @i", ("@i", paid.Id));
        var (_, client) = await LoginAsync(true);

        var detail = await ReadAsync(await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "跨期退款測試" }, TestJson.WriteOptions));

        Assert.Equal("allowance", detail.Invoice!.VoidStatus);
        Assert.Equal(1, fx.Invoices.AllowanceCalls);
        Assert.Equal(0, fx.Invoices.VoidCalls);
    }

    [Fact]
    public async Task 退款_原因必填_非已付款的單不能退_金流拒絕或中斷時本站狀態不動_也不寫稽核不寄信()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 500);
        using var publicClient = fx.CreateClient();
        var createdOnly = await CreateDonationAsync(publicClient, NewRequest(projectSlug, 300));
        var createdId = await DonationIdAsync(fx, createdOnly);
        var (_, client) = await LoginAsync(true);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = " " }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "字" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = new string('字', 256) }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Donations}/{createdId}/refund", new { reason = "不該能退" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Donations}/{Guid.NewGuid()}/refund", new { reason = "查無此單" }, TestJson.WriteOptions)).StatusCode);

        fx.Gateway.RefundSucceeds = false;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "金流拒絕測試" }, TestJson.WriteOptions)).StatusCode);
        fx.Gateway.RefundSucceeds = true;
        fx.Gateway.RefundUnavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "金流中斷測試" }, TestJson.WriteOptions)).StatusCode);

        Assert.Equal("paid", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE id = @i", ("@i", paid.Id)));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.refund' AND target_id = @t", ("@t", paid.Id)));
        Assert.Equal(0, fx.Mail.Sent.Count(m => m.TemplateCode == "refund_notice"));
        Assert.Equal(0, fx.Invoices.VoidCalls + fx.Invoices.AllowanceCalls);
    }

    [Fact]
    public async Task 退款_憑證作廢失敗不影響退款_進異常佇列等人工處理()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 500);
        fx.Invoices.VoidFails = true;
        var (_, client) = await LoginAsync(true);

        var detail = await ReadAsync(await client.PostAsJsonAsync($"{Donations}/{paid.Id}/refund", new { reason = "憑證作廢失敗測試" }, TestJson.WriteOptions));

        Assert.Equal("refunded", detail.Status);
        Assert.Equal("none", detail.Invoice!.VoidStatus);
        var anomalies = (await client.GetFromJsonAsync<List<AdminAnomalyDto>>($"{Donations}/anomalies?kind=invoice_void_pending", TestJson.Options))!;
        Assert.Contains(anomalies, a => a.DonationId == paid.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 含個資的明細匯出
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 匯出_需用途備註_寫稽核含備註與條件_CSV帶BOM_公式注入被中和()
    {
        var (projectId, projectSlug) = await CreateProjectAsync(fx);
        var normal = await PaidAsync(projectSlug, 500, donorName: "王小明");
        await PaidAsync(projectSlug, 600, donorName: "=HYPERLINK(\"http://evil.invalid\",\"點我\")");
        var (cs, client) = await LoginAsync(false, "customer_service_admin");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Donations}/export?projectId={projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Donations}/export?projectId={projectId}&purpose=abc")).StatusCode);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.export_pii' AND admin_user_id = @a", ("@a", cs.Id))); // 被拒絕的匯出不留稽核

        var response = await client.GetAsync($"{Donations}/export?projectId={projectId}&purpose={Uri.EscapeDataString("月底與會計師核對捐贈收據")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Contains("單號,建立時間,付款時間,狀態,金額", csv);
        Assert.Contains(normal.OrderNo, csv);
        Assert.Contains(normal.Email, csv);   // 匯出就是要含個資，所以才需要額外授權與稽核
        Assert.Contains("王小明", csv);
        Assert.Contains("'=HYPERLINK", csv);  // 以 = 開頭的欄位前面補單引號，Excel 不會當公式執行
        Assert.DoesNotContain(",=HYPERLINK", csv);
        Assert.Equal(3, csv.TrimEnd().Split("\r\n").Length); // 表頭 ＋ 2 筆（只含這個項目）

        var summary = await fx.ScalarAsync<string>("SELECT change_summary FROM audit_logs WHERE action = N'donation.export_pii' AND admin_user_id = @a", ("@a", cs.Id));
        var purpose = await fx.ScalarAsync<string>("SELECT purpose_note FROM audit_logs WHERE action = N'donation.export_pii' AND admin_user_id = @a", ("@a", cs.Id));
        Assert.Contains("2 筆", summary!);
        Assert.Contains(projectId.ToString(), summary);
        Assert.Equal("月底與會計師核對捐贈收據", purpose);
        Assert.DoesNotContain(normal.Email, summary);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.export_pii' AND admin_user_id = @a", ("@a", cs.Id)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 異常佇列與處理動作
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 異常佇列_已扣款但確認失敗_憑證開立失敗_對帳差異_各自列出_可重新確認與重開憑證()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        using var publicClient = fx.CreateClient();

        // ① 已扣款但確認失敗（結果未知）
        var unknown = await CreateDonationAsync(publicClient, NewRequest(projectSlug, 500, email: Email("unknown")));
        await StartPaymentAsync(publicClient, unknown);
        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Unavailable;
        await ConfirmAsync(publicClient, unknown, await LatestTransactionIdAsync(fx, unknown));
        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Ok;
        var unknownId = await DonationIdAsync(fx, unknown);

        // ② 憑證開立失敗（加值中心拒絕）
        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Rejected;
        var failedInvoice = await PaidAsync(projectSlug, 800);
        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Ok;

        var (_, sys) = await LoginAsync(true);
        var (cs, csClient) = await LoginAsync(false, "customer_service_admin");
        var (_, biz) = await LoginAsync(false, "business_sponsorship");

        var all = (await sys.GetFromJsonAsync<List<AdminAnomalyDto>>($"{Donations}/anomalies", TestJson.Options))!;
        Assert.Contains(all, a => a.Kind == "confirm_failed" && a.DonationId == unknownId);
        Assert.Contains(all, a => a.Kind == "invoice_failed" && a.DonationId == failedInvoice.Id);
        var reconciliation = all.Where(a => a.Kind == "reconciliation").ToList();
        Assert.NotEmpty(reconciliation);                                   // 種子有兩筆未處理的對帳差異
        Assert.All(reconciliation, r => Assert.Equal("pending", r.ResolutionStatus));
        Assert.All(reconciliation, r => Assert.Contains(r.DiscrepancyType, new[] { "site_only", "gateway_only", "amount_mismatch" }));

        var filtered = (await sys.GetFromJsonAsync<List<AdminAnomalyDto>>($"{Donations}/anomalies?kind=confirm_failed", TestJson.Options))!;
        Assert.All(filtered, a => Assert.Equal("confirm_failed", a.Kind));
        var counts = (await sys.GetFromJsonAsync<AdminAnomalyCountsDto>($"{Donations}/anomalies/counts", TestJson.Options))!;
        Assert.True(counts.ConfirmFailed >= 1 && counts.InvoiceFailed >= 1 && counts.Reconciliation >= 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync($"{Donations}/anomalies")).StatusCode);

        // 列表與詳情都標出「待人工處理」
        var detail = await ReadAsync(await sys.GetAsync($"{Donations}/{unknownId}"));
        Assert.True(detail.NeedsManualReview);

        // 重新確認付款結果：沒有權限的角色不行；客服可以；成功後走正常收尾並離開佇列
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PostAsync($"{Donations}/{unknownId}/recheck-payment", null)).StatusCode);
        var rechecked = await ReadAsync(await csClient.PostAsync($"{Donations}/{unknownId}/recheck-payment", null));
        Assert.Equal("paid", rechecked.Status);
        Assert.False(rechecked.NeedsManualReview);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.recheck_payment' AND admin_user_id = @a AND target_id = @t", ("@a", cs.Id), ("@t", unknownId)));
        Assert.DoesNotContain(
            (await sys.GetFromJsonAsync<List<AdminAnomalyDto>>($"{Donations}/anomalies?kind=confirm_failed", TestJson.Options))!, a => a.DonationId == unknownId);

        // 重新開立憑證：失敗的可以重開；已開立的不能重開
        var reissued = await ReadAsync(await csClient.PostAsync($"{Donations}/{failedInvoice.Id}/invoice/reissue", null));
        Assert.Equal("issued", reissued.Invoice!.IssueStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await csClient.PostAsync($"{Donations}/{failedInvoice.Id}/invoice/reissue", null)).StatusCode);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.reissue_invoice' AND target_id = @t", ("@t", failedInvoice.Id)));
        Assert.DoesNotContain(
            (await sys.GetFromJsonAsync<List<AdminAnomalyDto>>($"{Donations}/anomalies?kind=invoice_failed", TestJson.Options))!, a => a.DonationId == failedInvoice.Id);
    }

    [Fact]
    public async Task 重寄感謝信_只有已付款的可寄_寄信失敗如實回報_寫稽核()
    {
        var (_, projectSlug) = await CreateProjectAsync(fx);
        var paid = await PaidAsync(projectSlug, 500);
        using var publicClient = fx.CreateClient();
        var createdOnly = await CreateDonationAsync(publicClient, NewRequest(projectSlug, 300));
        var (cs, client) = await LoginAsync(false, "customer_service_admin");
        fx.Mail.Reset();

        var ok = await client.PostAsync($"{Donations}/{paid.Id}/resend-thanks", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("true", await ok.Content.ReadAsStringAsync());
        Assert.Single(fx.Mail.Sent, m => m.TemplateCode == "donation_thanks" && m.ToAddress == paid.Email);

        fx.Mail.Fail = true;
        var failed = await client.PostAsync($"{Donations}/{paid.Id}/resend-thanks", null);
        Assert.Contains("false", await failed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{Donations}/{await DonationIdAsync(fx, createdOnly)}/resend-thanks", null)).StatusCode);
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE action = N'donation.resend_thanks' AND admin_user_id = @a", ("@a", cs.Id)));
    }
}
