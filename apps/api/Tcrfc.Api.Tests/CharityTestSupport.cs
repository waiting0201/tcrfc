using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>慈善整合測試共用的資料建立與請求輔助。直接寫資料庫建立項目與店家（快、與被測的後台 API 解耦），標記規則見 <see cref="CharityApiFixture"/>。</summary>
public static class CharityTestSupport
{
    public const string Base = "/api/v1/donation-platform";
    public const string AdminBase = "/api/v1/donation-platform/admin";

    public static string NewKey() => Guid.NewGuid().ToString("N");

    public static string Email(string tag = "donor") => $"{tag}-{Guid.NewGuid():N}@{CharityApiFixture.TestEmailDomain}";

    public static async Task<(Guid Id, string Slug)> CreateProjectAsync(
        CharityApiFixture fx, string status = "published", string invoiceMode = "b2c_invoice", decimal projectPct = 10m,
        int? min = null, int? max = null, int[]? options = null, string? nameEn = null, string? fundUsage = "測試用款項用途")
    {
        var id = Guid.NewGuid();
        var slug = $"ct-{Guid.NewGuid():N}"[..14];
        await fx.ExecuteAsync(
            """
            INSERT INTO donation_projects (id, project_slug, min_amount, max_amount, project_share_pct, invoice_mode, status, sort_order)
            VALUES (@id, @slug, @min, @max, @pct, @mode, @status, 0);
            INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner, description, fund_usage, cover_alt)
            VALUES (@id, N'zh-Hant', @name, N'一句話說明', N'{"blocks":[{"type":"paragraph","text":"測試內文"}]}', @fund, N'封面替代文字');
            """,
            ("@id", id), ("@slug", slug), ("@min", (object?)min ?? DBNull.Value), ("@max", (object?)max ?? DBNull.Value),
            ("@pct", projectPct), ("@mode", invoiceMode), ("@status", status), ("@name", $"CT專案{slug}"),
            ("@fund", (object?)fundUsage ?? DBNull.Value));

        if (nameEn is not null)
        {
            await fx.ExecuteAsync(
                "INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner) VALUES (@id, N'en', @n, N'one liner')",
                ("@id", id), ("@n", nameEn));
        }

        var amounts = options ?? [100, 300, 500];
        var order = 0;
        foreach (var amount in amounts)
        {
            await fx.ExecuteAsync(
                "INSERT INTO donation_amount_options (donation_project_id, amount, sort_order) VALUES (@id, @a, @o)",
                ("@id", id), ("@a", amount), ("@o", order++));
        }

        return (id, slug);
    }

    public static async Task<(Guid Id, string Slug)> CreateStoreAsync(
        CharityApiFixture fx, decimal storePct = 5m, string status = "active", DateOnly? startOn = null, DateOnly? endOn = null,
        string? nameEn = null)
    {
        var id = Guid.NewGuid();
        var slug = $"ctstore{Guid.NewGuid():N}"[..16];
        await fx.ExecuteAsync(
            """
            INSERT INTO donation_stores (id, store_slug, store_share_pct, status, start_on, end_on, category)
            VALUES (@id, @slug, @pct, @status, @s, @e, N'測試類別');
            INSERT INTO donation_stores_i18n (donation_store_id, locale, name, logo_alt) VALUES (@id, N'zh-Hant', @name, N'店家標誌');
            """,
            ("@id", id), ("@slug", slug), ("@pct", storePct), ("@status", status),
            ("@s", (object?)startOn?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value), ("@e", (object?)endOn?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value),
            ("@name", $"CT店家{slug}"));

        if (nameEn is not null)
        {
            await fx.ExecuteAsync(
                "INSERT INTO donation_stores_i18n (donation_store_id, locale, name) VALUES (@id, N'en', @n)", ("@id", id), ("@n", nameEn));
        }

        return (id, slug);
    }

    public static object B2cMobileInvoice(string barcode = "/ABC+123") => new { type = "mobile_carrier", mobileCarrier = barcode };

    public static CreateDonationRequest NewRequest(string projectSlug, int amount = 500, string? storeSlug = null, object? invoice = null, string? email = null)
        => new()
        {
            ProjectSlug = projectSlug,
            StoreSlug = storeSlug,
            Amount = amount,
            DonorName = "測試捐款人",
            DonorEmail = email ?? Email(),
            IsAnonymous = false,
            ConsentPrivacy = true,
            Lang = "zh",
            Invoice = invoice is null ? new DonationInvoiceInput { Type = "mobile_carrier", MobileCarrier = "/ABC+123" } : ToInput(invoice),
        };

    private static DonationInvoiceInput ToInput(object invoice)
        => JsonSerializer.Deserialize<DonationInvoiceInput>(JsonSerializer.Serialize(invoice, TestJson.WriteOptions), TestJson.Options)!;

    public static async Task<HttpResponseMessage> PostDonationAsync(HttpClient client, CreateDonationRequest request, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Base}/donations") { Content = JsonContent.Create(request, options: TestJson.WriteOptions) };
        if (key is not null)
        {
            message.Headers.Add("Idempotency-Key", key);
        }

        return await client.SendAsync(message);
    }

    /// <summary>建單並回傳單號（斷言 201）。</summary>
    public static async Task<string> CreateDonationAsync(HttpClient client, CreateDonationRequest request, string? key = null)
    {
        var response = await PostDonationAsync(client, request, key ?? NewKey());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateDonationResponse>(TestJson.Options);
        return body!.OrderNo;
    }

    public static async Task<StartPaymentResponse> StartPaymentAsync(HttpClient client, string orderNo, string lang = "zh")
    {
        var response = await client.PostAsJsonAsync($"{Base}/donations/{orderNo}/pay", new StartPaymentRequest { Lang = lang }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StartPaymentResponse>(TestJson.Options))!;
    }

    public static async Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string orderNo, string transactionId)
        => await client.PostAsJsonAsync($"{Base}/donations/{orderNo}/confirm", new ConfirmPaymentRequest { TransactionId = transactionId }, TestJson.WriteOptions);

    public static async Task<string> LatestTransactionIdAsync(CharityApiFixture fx, string orderNo)
        => (await fx.ScalarAsync<string>(
            "SELECT TOP 1 p.transaction_id FROM donation_payments p JOIN donations d ON d.id = p.donation_id WHERE d.order_no = @o ORDER BY p.seq DESC",
            ("@o", orderNo)))!;

    public static async Task<PublicDonationResultDto> GetResultAsync(HttpClient client, string orderNo)
    {
        var response = await client.GetAsync($"{Base}/donations/{orderNo}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicDonationResultDto>(TestJson.Options))!;
    }

    /// <summary>建單 → 發起付款 → 確認，走完整個成功流程，回傳單號。</summary>
    public static async Task<string> CreatePaidDonationAsync(
        CharityApiFixture fx, HttpClient client, string projectSlug, int amount = 500, string? storeSlug = null, object? invoice = null, string? email = null)
    {
        var orderNo = await CreateDonationAsync(client, NewRequest(projectSlug, amount, storeSlug, invoice, email));
        var pay = await StartPaymentAsync(client, orderNo);
        Assert.False(string.IsNullOrEmpty(pay.PaymentUrl));
        var confirm = await ConfirmAsync(client, orderNo, await LatestTransactionIdAsync(fx, orderNo));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        return orderNo;
    }

    public static async Task<Guid> DonationIdAsync(CharityApiFixture fx, string orderNo)
        => (await fx.ScalarAsync<Guid>("SELECT id FROM donations WHERE order_no = @o", ("@o", orderNo)))!;

    /// <summary>測試用的合法統一編號（通過檢核碼）：04595257（財政部公開範例之一）。</summary>
    public const string ValidTaxId = "04595257";
}
