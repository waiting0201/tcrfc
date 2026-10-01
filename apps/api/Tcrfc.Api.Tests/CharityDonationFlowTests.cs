using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.CharityPlatform.Workers;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// CH-2 捐款主幹的端對端測試：建單（冪等）→ 發起付款 → 確認／取消 → 結果 → 憑證 → 感謝信，打真正的 HTTP 管線與真正的
/// <c>tcrfc_charity</c>；金流、發票、寄信用可編排的替身。重點是<b>冪等與並發</b>（重複 Confirm 不得重複入帳、開票、寄信）與
/// <b>「已扣款但 Confirm 結果未知」不得被當成失敗或靜默丟棄</b>。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityDonationFlowTests(CharityApiFixture fx) : IAsyncLifetime
{
    private static readonly System.Linq.Expressions.Expression<Func<Tcrfc.Api.CharityPlatform.Data.Entities.Donation, bool>> OnlyTestDonations
        = d => d.DonorEmail.EndsWith("@charity-test.invalid");

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<CharityMaintenanceResult> RunMaintenanceAsync()
    {
        await using var scope = fx.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CharityMaintenanceRunner>().RunOnceAsync(default, OnlyTestDonations);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 建單
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 建單_成功回201_寫入單號_憑證待開立_手機條碼加密儲存()
    {
        var (projectId, slug) = await CreateProjectAsync(fx, projectPct: 8m);
        using var client = fx.CreateClient();

        var request = NewRequest(slug, 1000, invoice: B2cMobileInvoice("/ABC+123"));
        var response = await PostDonationAsync(client, request, NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<CreateDonationResponse>(TestJson.Options))!;
        Assert.Matches("^CH[0-9A-Z]{16}$", body.OrderNo);
        Assert.Equal("created", body.Status);
        Assert.True(body.Created);

        Assert.Equal("created", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", body.OrderNo)));
        Assert.Equal(projectId, await fx.ScalarAsync<Guid>("SELECT donation_project_id FROM donations WHERE order_no = @o", ("@o", body.OrderNo)));
        // 建單時的分潤是暫算：無店家 → 店家 0、項目 8%、協會留存 920。
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT store_amount FROM donations WHERE order_no = @o", ("@o", body.OrderNo)));
        Assert.Equal(80, await fx.ScalarAsync<int>("SELECT project_amount FROM donations WHERE order_no = @o", ("@o", body.OrderNo)));
        Assert.Equal(920, await fx.ScalarAsync<int>("SELECT association_amount FROM donations WHERE order_no = @o", ("@o", body.OrderNo)));

        var invoiceStatus = await fx.ScalarAsync<string>(
            "SELECT i.issue_status FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", body.OrderNo));
        Assert.Equal("pending", invoiceStatus);
        Assert.Equal("mobile_carrier", await fx.ScalarAsync<string>(
            "SELECT i.carrier_type FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", body.OrderNo)));

        var cipher = await fx.ScalarAsync<string>(
            "SELECT i.carrier_id_encrypted FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", body.OrderNo));
        Assert.False(string.IsNullOrEmpty(cipher));
        Assert.DoesNotContain("ABC", cipher!);
    }

    [Fact]
    public async Task 建單_捐贈收據模式_身分證字號只以密文入庫_收據抬頭預設帶入姓名()
    {
        var (_, slug) = await CreateProjectAsync(fx, invoiceMode: "donation_receipt");
        using var client = fx.CreateClient();

        var request = NewRequest(slug, 500, invoice: new { nationalId = "A123456789", address = "臺中市西區測試路 1 號" });
        var orderNo = await CreateDonationAsync(client, request);

        var cipher = await fx.ScalarAsync<string>(
            "SELECT i.national_id_encrypted FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", orderNo));
        Assert.False(string.IsNullOrEmpty(cipher));
        Assert.DoesNotContain("A123456789", cipher!);
        Assert.Equal("測試捐款人", await fx.ScalarAsync<string>(
            "SELECT i.receipt_title FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
        Assert.Equal("donation_receipt", await fx.ScalarAsync<string>("SELECT invoice_mode FROM donations WHERE order_no = @o", ("@o", orderNo)));
    }

    [Fact]
    public async Task 冪等_同一個鍵重複送出_只建一張單_第二次回200沿用原單()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var request = NewRequest(slug, 300);
        var key = NewKey();

        var first = await PostDonationAsync(client, request, key);
        var second = await PostDonationAsync(client, request, key);
        var third = await PostDonationAsync(client, request, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var a = (await first.Content.ReadFromJsonAsync<CreateDonationResponse>(TestJson.Options))!;
        var b = (await second.Content.ReadFromJsonAsync<CreateDonationResponse>(TestJson.Options))!;
        Assert.Equal(a.OrderNo, b.OrderNo);
        Assert.False(b.Created);

        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE donor_email = @e", ("@e", request.DonorEmail!)));
        Assert.Equal(1, await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.donor_email = @e", ("@e", request.DonorEmail!)));
    }

    [Fact]
    public async Task 冪等_並發送出同一個鍵_資料庫唯一鍵保證只有一張單()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var request = NewRequest(slug, 300);
        var key = NewKey();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostDonationAsync(client, request, key)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"非預期狀態碼 {r.StatusCode}"));
        var orderNos = new HashSet<string>();
        foreach (var r in responses)
        {
            orderNos.Add((await r.Content.ReadFromJsonAsync<CreateDonationResponse>(TestJson.Options))!.OrderNo);
        }

        Assert.Single(orderNos);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE donor_email = @e", ("@e", request.DonorEmail!)));
    }

    [Fact]
    public async Task 冪等_同一個鍵送了不同金額_回409_不覆蓋原單()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var key = NewKey();
        var email = Email();

        Assert.Equal(HttpStatusCode.Created, (await PostDonationAsync(client, NewRequest(slug, 300, email: email), key)).StatusCode);
        var conflict = await PostDonationAsync(client, NewRequest(slug, 500, email: email), key);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(300, await fx.ScalarAsync<int>("SELECT amount FROM donations WHERE donor_email = @e", ("@e", email)));
    }

    [Fact]
    public async Task 建單_沒帶冪等鍵或格式不對_回400()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await PostDonationAsync(client, NewRequest(slug), key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostDonationAsync(client, NewRequest(slug), key: "too-short")).StatusCode);
    }

    public static IEnumerable<object[]> InvalidRequests() =>
    [
        ["no-consent"],
        ["bad-email"],
        ["empty-name"],
        ["zero-amount"],
        ["bad-carrier"],
        ["bad-love-code"],
        ["bad-tax-id"],
        ["tax-id-without-title"],
        ["missing-invoice-type"],
    ];

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task 建單驗證_格式錯誤一律回400_不建單(string scenario)
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var email = Email();
        var request = NewRequest(slug, 500, email: email);
        request = scenario switch
        {
            "no-consent" => request with { ConsentPrivacy = false },
            "bad-email" => request with { DonorEmail = "not-an-email" },
            "empty-name" => request with { DonorName = "  " },
            "zero-amount" => request with { Amount = 0 },
            "bad-carrier" => request with { Invoice = new DonationInvoiceInput { Type = "mobile_carrier", MobileCarrier = "ABC1234" } },
            "bad-love-code" => request with { Invoice = new DonationInvoiceInput { Type = "love_code", LoveCode = "12" } },
            "bad-tax-id" => request with { Invoice = new DonationInvoiceInput { Type = "tax_id", TaxId = "12345678", InvoiceTitle = "測試公司" } },
            "tax-id-without-title" => request with { Invoice = new DonationInvoiceInput { Type = "tax_id", TaxId = ValidTaxId } },
            "missing-invoice-type" => request with { Invoice = new DonationInvoiceInput() },
            _ => throw new InvalidOperationException(scenario),
        };

        var response = await PostDonationAsync(client, request, NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE donor_email = @e", ("@e", email)));
    }

    [Fact]
    public async Task 建單_統一編號通過檢核碼時成功()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var request = NewRequest(slug, 500) with
        {
            Invoice = new DonationInvoiceInput { Type = "tax_id", TaxId = ValidTaxId, InvoiceTitle = "測試股份有限公司" },
        };

        var orderNo = await CreateDonationAsync(client, request);

        Assert.Equal(ValidTaxId, await fx.ScalarAsync<string>(
            "SELECT i.tax_id FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
    }

    [Fact]
    public async Task 建單_金額超出單筆上下限回422_項目沒上架或不存在回404()
    {
        var (_, slug) = await CreateProjectAsync(fx, min: 100, max: 2000);
        var (_, draftSlug) = await CreateProjectAsync(fx, status: "draft");
        using var client = fx.CreateClient();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostDonationAsync(client, NewRequest(slug, 99), NewKey())).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostDonationAsync(client, NewRequest(slug, 2001), NewKey())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostDonationAsync(client, NewRequest(slug, 100), NewKey())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostDonationAsync(client, NewRequest(slug, 2000), NewKey())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostDonationAsync(client, NewRequest(draftSlug), NewKey())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostDonationAsync(client, NewRequest("ct-does-not-exist"), NewKey())).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 店家歸屬（規劃書 §2.2）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 店家歸屬_有效店家帶入店家與分潤_無效店家視同無歸屬不報錯()
    {
        var (_, slug) = await CreateProjectAsync(fx, projectPct: 10m);
        var (storeId, storeSlug) = await CreateStoreAsync(fx, storePct: 5m);
        var (_, inactiveSlug) = await CreateStoreAsync(fx, status: "inactive");
        var (_, endedSlug) = await CreateStoreAsync(fx, endOn: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        var (_, futureSlug) = await CreateStoreAsync(fx, startOn: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        using var client = fx.CreateClient();

        var withStore = await CreateDonationAsync(client, NewRequest(slug, 1000, storeSlug));
        Assert.Equal(storeId, await fx.ScalarAsync<Guid>("SELECT donation_store_id FROM donations WHERE order_no = @o", ("@o", withStore)));
        Assert.Equal(50, await fx.ScalarAsync<int>("SELECT store_amount FROM donations WHERE order_no = @o", ("@o", withStore)));
        Assert.Equal(100, await fx.ScalarAsync<int>("SELECT project_amount FROM donations WHERE order_no = @o", ("@o", withStore)));
        Assert.Equal(850, await fx.ScalarAsync<int>("SELECT association_amount FROM donations WHERE order_no = @o", ("@o", withStore)));

        foreach (var invalid in new[] { inactiveSlug, endedSlug, futureSlug, "no-such-store", "" })
        {
            var orderNo = await CreateDonationAsync(client, NewRequest(slug, 1000, invalid));
            Assert.Null(await fx.ScalarAsync<Guid?>("SELECT donation_store_id FROM donations WHERE order_no = @o", ("@o", orderNo)));
            Assert.Equal(0, await fx.ScalarAsync<int>("SELECT store_amount FROM donations WHERE order_no = @o", ("@o", orderNo)));
            Assert.Equal(100, await fx.ScalarAsync<int>("SELECT project_amount FROM donations WHERE order_no = @o", ("@o", orderNo))); // 項目分潤照算
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 發起付款
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 發起付款_回付款網址_狀態轉pending_寫付款紀錄_重複發起沿用同一筆不再呼叫金流()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));

        var first = await StartPaymentAsync(client, orderNo);
        var second = await StartPaymentAsync(client, orderNo);

        Assert.Equal("pending", first.Status);
        Assert.StartsWith("https://pay.test.invalid/", first.PaymentUrl);
        Assert.Equal(first.PaymentUrl, second.PaymentUrl);
        Assert.Equal(1, fx.Gateway.RequestCalls);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
        Assert.Equal("pending", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));
        var sent = Assert.Single(fx.Gateway.Requests);
        Assert.Equal(orderNo, sent.OrderNo);
        Assert.Equal(1, sent.Attempt);
        Assert.Equal($"{CharityApiFixture.PublicBaseUrl}/zh/result/{orderNo}", sent.ConfirmUrl);
    }

    [Fact]
    public async Task 發起付款_金流服務中斷回503_捐款單狀態不變_沒有留下付款紀錄()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        fx.Gateway.RequestMode = ScriptedPaymentGateway.Mode.Unavailable;

        var response = await client.PostAsJsonAsync($"{Base}/donations/{orderNo}/pay", new StartPaymentRequest(), TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("created", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
    }

    [Fact]
    public async Task 發起付款_單號不存在回404_已付款的單回409()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var paid = await CreatePaidDonationAsync(fx, client, slug);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"{Base}/donations/CHZZZZZZZZZZZZZZZZ/pay", new StartPaymentRequest(), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"{Base}/donations/{paid}/pay", new StartPaymentRequest(), TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 確認：成功路徑與冪等
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 確認成功_轉paid_分潤快照以成立當下的設定為準_開立憑證_感謝信與憑證通知各一封()
    {
        var (projectId, slug) = await CreateProjectAsync(fx, projectPct: 10m);
        var (storeId, storeSlug) = await CreateStoreAsync(fx, storePct: 5m);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 1000, storeSlug));
        await StartPaymentAsync(client, orderNo);

        // 建單之後、付款成立之前調整分潤率：快照必須取「成立（paid）當下」的值（規劃書 §8.4）。
        await fx.ExecuteAsync("UPDATE donation_projects SET project_share_pct = 20 WHERE id = @i", ("@i", projectId));
        await fx.ExecuteAsync("UPDATE donation_stores SET store_share_pct = 6 WHERE id = @i", ("@i", storeId));

        var confirm = await ConfirmAsync(client, orderNo, await LatestTransactionIdAsync(fx, orderNo));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var result = (await confirm.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!;

        Assert.Equal("paid", result.Status);
        Assert.False(result.Processing);
        Assert.False(result.CanRetry);
        Assert.NotNull(result.PaidAt);
        Assert.Equal("issued", result.InvoiceStatus);
        Assert.Equal($"TEST-{orderNo}", result.InvoiceNo);

        Assert.Equal(6.00m, await fx.ScalarAsync<decimal>("SELECT store_share_pct_snapshot FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal(20.00m, await fx.ScalarAsync<decimal>("SELECT project_share_pct_snapshot FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal(60, await fx.ScalarAsync<int>("SELECT store_amount FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal(200, await fx.ScalarAsync<int>("SELECT project_amount FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal(740, await fx.ScalarAsync<int>("SELECT association_amount FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal("confirmed", await fx.ScalarAsync<string>("SELECT TOP 1 p.status FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC", ("@o", orderNo)));

        var email = (await fx.ScalarAsync<string>("SELECT donor_email FROM donations WHERE order_no = @o", ("@o", orderNo)))!;
        var mails = fx.Mail.Sent.Where(m => m.ToAddress == email).ToList();
        var thanks = Assert.Single(mails, m => m.TemplateCode == "donation_thanks");
        Assert.Contains(orderNo, thanks.Body);              // 感謝信含單號、金額、項目與款項用途（規劃書 §3.5）
        Assert.Contains("1,000", thanks.Body);
        Assert.Contains("測試用款項用途", thanks.Body);
        Assert.Single(mails, m => m.TemplateCode == "invoice_issued");
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM email_logs WHERE recipient_email = @e AND status = N'sent'", ("@e", email)));
    }

    [Fact]
    public async Task 確認_重複呼叫三次_金流只確認一次_憑證只開一次_感謝信只寄一次()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);
        var tx = await LatestTransactionIdAsync(fx, orderNo);

        for (var i = 0; i < 3; i++)
        {
            var response = await ConfirmAsync(client, orderNo, tx);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("paid", (await response.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!.Status);
        }

        Assert.Equal(1, fx.Gateway.ConfirmCalls);
        Assert.Equal(1, fx.Invoices.IssueCalls);
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o AND i.issue_status = N'issued'", ("@o", orderNo)));
    }

    [Fact]
    public async Task 確認_並發六個請求_只有一個贏得入帳_感謝信與開票各只做一次()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);
        var tx = await LatestTransactionIdAsync(fx, orderNo);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => ConfirmAsync(client, orderNo, tx)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE order_no = @o AND status = N'paid'", ("@o", orderNo)));
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));
        Assert.Equal(1, fx.Invoices.IssueCalls);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o AND p.status = N'confirmed'", ("@o", orderNo)));
    }

    [Fact]
    public async Task 確認_交易識別碼與捐款單不符_回400_不呼叫金流()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);

        Assert.Equal(HttpStatusCode.BadRequest, (await ConfirmAsync(client, orderNo, "SOMEONE-ELSES-TX")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Base}/donations/{orderNo}/confirm", new ConfirmPaymentRequest(), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(0, fx.Gateway.ConfirmCalls);
        Assert.Equal("pending", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));
    }

    [Fact]
    public async Task 確認_尚未發起付款的單_回409()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));

        Assert.Equal(HttpStatusCode.Conflict, (await ConfirmAsync(client, orderNo, "ANY")).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 失敗、取消、重試
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 金流明確拒絕_轉failed_可重試_沿用原單_第二次付款成功()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);
        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Declined;

        var declined = await ConfirmAsync(client, orderNo, await LatestTransactionIdAsync(fx, orderNo));
        var failed = (await declined.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!;
        Assert.Equal("failed", failed.Status);
        Assert.True(failed.CanRetry);
        Assert.Equal(0, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));

        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Ok;
        var retry = await StartPaymentAsync(client, orderNo);
        Assert.Equal("pending", retry.Status);
        Assert.Equal(2, fx.Gateway.Requests.Last().Attempt);
        var paid = await ConfirmAsync(client, orderNo, await LatestTransactionIdAsync(fx, orderNo));

        Assert.Equal("paid", (await paid.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!.Status);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE order_no = @o", ("@o", orderNo))); // 沒有重複建單
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
    }

    [Fact]
    public async Task 使用者取消_pending轉failed_付款紀錄記cancelled_重複取消冪等_已付款的單取消不動()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsync($"{Base}/donations/{orderNo}/cancel", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!;
            Assert.Equal("failed", body.Status);
            Assert.True(body.CanRetry);
        }

        Assert.Equal("cancelled", await fx.ScalarAsync<string>("SELECT TOP 1 p.status FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC", ("@o", orderNo)));

        var paid = await CreatePaidDonationAsync(fx, client, slug);
        var afterCancel = await client.PostAsync($"{Base}/donations/{paid}/cancel", null);
        Assert.Equal("paid", (await afterCancel.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 🔴 已扣款但 Confirm 失敗（結果未知）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Confirm結果未知_維持pending_付款標failed_不當成失敗_不能重付_不會逾時_再確認成功後正常收尾()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);
        var tx = await LatestTransactionIdAsync(fx, orderNo);
        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Unavailable;

        var unknown = await ConfirmAsync(client, orderNo, tx);

        // ① 對使用者是「處理中」，不是失敗，也不是 5xx
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        var body = (await unknown.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!;
        Assert.Equal("pending", body.Status);
        Assert.True(body.Processing);
        Assert.False(body.CanRetry);
        Assert.Equal("pending", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));
        Assert.Equal("failed", await fx.ScalarAsync<string>("SELECT TOP 1 p.status FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC", ("@o", orderNo)));
        Assert.Contains("confirmError", (await fx.ScalarAsync<string>("SELECT TOP 1 p.raw_response FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC", ("@o", orderNo)))!);
        Assert.Equal(0, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));

        // ② 禁止重新發起付款（會重複扣款）
        var repay = await client.PostAsJsonAsync($"{Base}/donations/{orderNo}/pay", new StartPaymentRequest(), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, repay.StatusCode);
        Assert.Equal(1, fx.Gateway.RequestCalls);

        // ③ 按取消也不能當作沒扣款
        await client.PostAsync($"{Base}/donations/{orderNo}/cancel", null);
        Assert.Equal("pending", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));

        // ④ 背景逾時工作不會把它轉成 expired（即使時間早已超過）
        await fx.ExecuteAsync("UPDATE donations SET created_at = DATEADD(HOUR, -5, SYSUTCDATETIME()) WHERE order_no = @o", ("@o", orderNo));
        await fx.ExecuteAsync("UPDATE p SET requested_at = DATEADD(HOUR, -5, SYSUTCDATETIME()) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", orderNo));
        await RunMaintenanceAsync();
        Assert.Equal("pending", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));

        // ⑤ 金流恢復後再確認一次（使用者重新整理、或後台重新確認）→ 正常入帳與收尾
        fx.Gateway.ConfirmMode = ScriptedPaymentGateway.Mode.Ok;
        var recheck = await ConfirmAsync(client, orderNo, tx);
        Assert.Equal("paid", (await recheck.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!.Status);
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));
        Assert.Equal(1, fx.Invoices.IssueCalls);
        Assert.Equal("confirmed", await fx.ScalarAsync<string>("SELECT TOP 1 p.status FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC", ("@o", orderNo)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 逾時
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 逾時_created與pending超過時限轉expired_逾時後仍可沿用原單重試付款()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var createdOnly = await CreateDonationAsync(client, NewRequest(slug, 500));
        var pending = await CreateDonationAsync(client, NewRequest(slug, 500));
        var fresh = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, pending);
        await fx.ExecuteAsync("UPDATE donations SET created_at = DATEADD(MINUTE, -45, SYSUTCDATETIME()) WHERE order_no IN (@a, @b)", ("@a", createdOnly), ("@b", pending));
        await fx.ExecuteAsync("UPDATE p SET requested_at = DATEADD(MINUTE, -45, SYSUTCDATETIME()) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", pending));

        var result = await RunMaintenanceAsync();

        Assert.Equal(2, result.Expired);
        Assert.Equal("expired", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", createdOnly)));
        Assert.Equal("expired", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", pending)));
        Assert.Equal("created", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", fresh)));

        var retry = await StartPaymentAsync(client, createdOnly);
        Assert.Equal("pending", retry.Status);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donations WHERE order_no = @o", ("@o", createdOnly)));
    }

    [Fact]
    public async Task 逾時後使用者才完成付款_晚到的確認仍然收下_不吞掉真實的捐款()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreateDonationAsync(client, NewRequest(slug, 500));
        await StartPaymentAsync(client, orderNo);
        var tx = await LatestTransactionIdAsync(fx, orderNo);
        await fx.ExecuteAsync("UPDATE p SET requested_at = DATEADD(MINUTE, -45, SYSUTCDATETIME()) FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o", ("@o", orderNo));
        await RunMaintenanceAsync();
        Assert.Equal("expired", await fx.ScalarAsync<string>("SELECT status FROM donations WHERE order_no = @o", ("@o", orderNo)));

        var late = await ConfirmAsync(client, orderNo, tx);

        Assert.Equal("paid", (await late.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!.Status);
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 憑證與信件
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 憑證開立暫時失敗_付款照常成立_維持待開立_背景重試後開立並寄憑證通知()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Unavailable;
        var orderNo = await CreatePaidDonationAsync(fx, client, slug);

        var after = await GetResultAsync(client, orderNo);
        Assert.Equal("paid", after.Status);
        Assert.Equal("pending", after.InvoiceStatus);
        Assert.Null(after.InvoiceNo);

        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Ok;
        var run = await RunMaintenanceAsync();

        Assert.Equal(1, run.InvoicesIssued);
        var final = await GetResultAsync(client, orderNo);
        Assert.Equal("issued", final.InvoiceStatus);
        Assert.NotNull(final.InvoiceNo);
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "invoice_issued"));
        Assert.Equal(1, fx.Mail.Sent.Count(m => m.TemplateCode == "donation_thanks")); // 感謝信沒有因為重試而重寄
    }

    [Fact]
    public async Task 憑證超過重試期限仍失敗_標記failed_寄開立失敗通知給協會_不含捐款人個資()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Unavailable;
        var orderNo = await CreatePaidDonationAsync(fx, client, slug);
        await fx.ExecuteAsync("UPDATE donations SET paid_at = DATEADD(MINUTE, -30, SYSUTCDATETIME()) WHERE order_no = @o", ("@o", orderNo));

        var run = await RunMaintenanceAsync();

        Assert.Equal(1, run.InvoicesFailed);
        Assert.Equal("failed", await fx.ScalarAsync<string>("SELECT i.issue_status FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
        var notice = Assert.Single(fx.Mail.Sent, m => m.TemplateCode == "invoice_failed");
        Assert.Equal(CharityApiFixture.AssociationNotifyEmail, notice.ToAddress);
        Assert.DoesNotContain("測試捐款人", notice.Body);
        // 對捐款人而言憑證仍是「處理中」，不嚇人
        Assert.Equal("pending", (await GetResultAsync(client, orderNo)).InvoiceStatus);
    }

    [Fact]
    public async Task 加值中心明確拒絕_不重試_立即標記failed()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        fx.Invoices.IssueMode = ScriptedInvoiceIssuer.Mode.Rejected;

        var orderNo = await CreatePaidDonationAsync(fx, client, slug);

        Assert.Equal("failed", await fx.ScalarAsync<string>("SELECT i.issue_status FROM donation_invoices i JOIN donations d ON d.id = i.donation_id WHERE d.order_no = @o", ("@o", orderNo)));
        Assert.Equal(1, fx.Invoices.IssueCalls);
        await RunMaintenanceAsync();
        Assert.Equal(1, fx.Invoices.IssueCalls); // failed 的不會被背景工作自動再開
    }

    [Fact]
    public async Task 開票請求帶的是協會自己的字軌與解密後的載具_不是主站的任何設定()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var orderNo = await CreatePaidDonationAsync(fx, client, slug, invoice: B2cMobileInvoice("/ABC+123"));

        var request = Assert.Single(fx.Invoices.Requests);
        Assert.Equal(orderNo, request.OrderNo);
        Assert.Equal("TEST", request.TrackPrefix); // 慈善庫 payment_channels（einvoice／sandbox）的 invoice_prefix
        Assert.Equal("/ABC+123", request.CarrierId);
        Assert.Equal("mobile_carrier", request.CarrierType);
    }

    [Fact]
    public async Task 寄信失敗_不影響付款成立_記成email_logs失敗()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        fx.Mail.Fail = true;
        var email = Email();

        var orderNo = await CreatePaidDonationAsync(fx, client, slug, email: email);

        Assert.Equal("paid", (await GetResultAsync(client, orderNo)).Status);
        Assert.Equal(2, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM email_logs WHERE recipient_email = @e AND status = N'failed'", ("@e", email)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 結果頁
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 結果頁_姓名與Email遮罩_不含完整個資_不含金流交易識別碼與分潤()
    {
        var (_, slug) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var email = Email("alice");
        var orderNo = await CreatePaidDonationAsync(fx, client, slug, email: email);

        var response = await client.GetAsync($"{Base}/donations/{orderNo}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(email, json);
        Assert.DoesNotContain("測試捐款人", json);
        Assert.Contains("a***@", json);
        Assert.DoesNotContain("TEST-TX-", json);
        Assert.DoesNotContain("share", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("encrypted", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CHZZZZZZZZZZZZZZZZ")]
    [InlineData("not a valid order no!")]
    [InlineData("x")]
    public async Task 結果頁_查無或格式不對一律404(string orderNo)
    {
        using var client = fx.CreateClient();

        var response = await client.GetAsync($"{Base}/donations/{Uri.EscapeDataString(orderNo)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task 結果頁_語系參數決定項目名稱_英文缺漏回退繁中()
    {
        var (_, slug) = await CreateProjectAsync(fx, nameEn: "CT Project EN");
        var (_, slugNoEn) = await CreateProjectAsync(fx);
        using var client = fx.CreateClient();
        var a = await CreateDonationAsync(client, NewRequest(slug, 500));
        var b = await CreateDonationAsync(client, NewRequest(slugNoEn, 500));

        var en = (await client.GetFromJsonAsync<PublicDonationResultDto>($"{Base}/donations/{a}?lang=en", TestJson.Options))!;
        var zh = (await client.GetFromJsonAsync<PublicDonationResultDto>($"{Base}/donations/{a}?lang=zh", TestJson.Options))!;
        var fallback = (await client.GetFromJsonAsync<PublicDonationResultDto>($"{Base}/donations/{b}?lang=en", TestJson.Options))!;

        Assert.Equal("CT Project EN", en.ProjectName);
        Assert.StartsWith("CT專案", zh.ProjectName);
        Assert.StartsWith("CT專案", fallback.ProjectName);
    }
}
