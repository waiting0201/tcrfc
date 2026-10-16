using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.MembershipPayments;
using Tcrfc.Api.Features.Shop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 站內商店「會員折扣」（主站規劃書 v3.29 §3.8 8.3、§4.13 S1／S3）：持有賣方俱樂部有效會籍的會員，在勾選「適用會員折扣」的商品上享有會籍方案設定的折扣比例；
/// 🔴 與促銷價不疊加、取較低價；購物車、結帳、訂單快照、LINE Pay 請款金額、電子發票明細與退款金額必須一致。
/// 純計價函式用 Theory 逐案驗證；整合測試用真的購物車／結帳／後台現場建單／退款端點，並以記錄用金流與發票替身核對金額。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ShopMemberDiscountTests(AdminWriteApiFixture fixture)
{
    private const string Club = "tcrfc";

    // ═════════════ 純計價 ═════════════

    [Theory]
    // 原價, 促銷價, 適用商品, 折扣率 → 單價, 是否促銷價生效, 每件因會員折扣少收
    [InlineData(1000, null, true, 10, 900, false, 100)] // 沒有促銷價：原價打九折
    [InlineData(1000, 950, true, 10, 900, false, 50)] // 促銷價 950 > 會員折扣價 900：取 900（不是 950 再打折）；相對本來要付的 950 少收 50
    [InlineData(1000, 850, true, 10, 850, true, 0)] // 促銷價 850 < 會員折扣價 900：取促銷價，會員折扣不疊加
    [InlineData(1000, 900, true, 10, 900, true, 0)] // 兩者相同：不算會員折扣（維持促銷價）
    [InlineData(1000, null, false, 10, 1000, false, 0)] // 商品沒勾選「適用會員折扣」
    [InlineData(1000, 950, false, 10, 950, true, 0)] // 沒勾選但有促銷價：促銷價照舊
    [InlineData(1000, null, true, 0, 1000, false, 0)] // 方案折扣 0%
    [InlineData(999, null, true, 10, 899, false, 100)] // 899.1 → 四捨五入到整數元
    [InlineData(995, null, true, 10, 896, false, 99)] // 895.5 → 四捨五入進位為 896（五入）
    [InlineData(1000, 1000, true, 10, 900, false, 100)] // 促銷價不低於原價不算促銷
    [InlineData(1000, 1200, true, 10, 900, false, 100)] // 促銷價高於原價（資料錯誤）也不算促銷
    public void 單價_會員折扣與促銷價不疊加取低(int list, int? sale, bool eligible, int percent, int expectedUnit, bool expectedOnSale, int expectedDiscountPerUnit)
    {
        var result = MemberDiscountService.Compute(list, sale, eligible, percent > 0 || eligible ? new MemberDiscount(Guid.NewGuid(), percent) : null);
        Assert.Equal(expectedUnit, result.UnitPrice);
        Assert.Equal(expectedOnSale, result.OnSale);
        Assert.Equal(expectedDiscountPerUnit, result.MemberDiscountPerUnit);
        Assert.Equal(list, result.ListPrice);
        Assert.Equal(expectedDiscountPerUnit > 0, result.MemberDiscountApplied);
    }

    [Fact]
    public void 單價_沒有有效會籍_訪客或僅註冊帳號_付原價或促銷價()
    {
        var none = MemberDiscountService.Compute(1000, 950, true, null);
        Assert.Equal(950, none.UnitPrice);
        Assert.True(none.OnSale);
        Assert.Equal(0, none.MemberDiscountPerUnit);
    }

    // ═════════════ 整合 ═════════════

    private sealed class RecordingGateway : IPaymentGateway
    {
        public List<int> Reserved { get; } = [];
        public List<int> Confirmed { get; } = [];
        public string Method => "linepay";
        public bool IsConfigured => true;

        public Task<PaymentReservation> ReserveAsync(PaymentReserveRequest request, CancellationToken cancellationToken)
        {
            lock (Reserved) { Reserved.Add(request.Amount); }
            return Task.FromResult(new PaymentReservation($"FAKE-{request.OrderNo}", $"https://fake-linepay.invalid/pay/{request.OrderNo}"));
        }

        public Task<PaymentConfirmation> ConfirmAsync(string transactionId, string orderNo, int amount, CancellationToken cancellationToken)
        {
            lock (Confirmed) { Confirmed.Add(amount); }
            return Task.FromResult(new PaymentConfirmation(true, null));
        }
    }

    private sealed class RecordingInvoiceIssuer : IInvoiceIssuer
    {
        public List<InvoiceIssueRequest> Issued { get; } = [];
        public bool IsConfigured => true;

        public Task<InvoiceIssueResult> IssueAsync(InvoiceIssueRequest request, CancellationToken cancellationToken)
        {
            lock (Issued) { Issued.Add(request); }
            return Task.FromResult(new InvoiceIssueResult(true, "ZZ" + request.OrderNo.GetHashCode().ToString("X").PadLeft(8, '0')[..8], null));
        }
    }

    private static async Task<JsonElement> CartAsync(HttpClient client, string? cartToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/{Club}/shop/cart");
        if (cartToken is not null)
        {
            request.Headers.Add(ShopCartService.CartTokenHeader, cartToken);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> AddAsync(HttpClient client, Guid variantId, int quantity, string? cartToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{Club}/shop/cart/items") { Content = BizTest.Json(new { variantId, quantity }) };
        if (cartToken is not null)
        {
            request.Headers.Add(ShopCartService.CartTokenHeader, cartToken);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("cartToken", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : cartToken;
    }

    private static JsonElement Line(JsonElement cart, Guid variantId) => cart.GetProperty("items").EnumerateArray().First(i => i.GetProperty("variantId").GetGuid() == variantId);

    [Fact]
    public async Task 會員折扣_購物車_結帳_LINEPay金額_發票明細_訂單快照_後台現場建單_退款_全部一致()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var members = new MemberTestScope(fixture);
        var products = new List<Guid>();
        var memberIds = new List<Guid>();
        var cartHashes = new List<string>();
        try
        {
            var eligible = await ShopTest.CreateProductAsync(admin, Club, "mdisc", ("A", 1000, 30), ("B", 1000, 30), ("C", 1000, 30));
            var plain = await ShopTest.CreateProductAsync(admin, Club, "mplain", ("D", 1000, 30));
            products.AddRange([eligible.ProductId, plain.ProductId]);
            var (a, b, c) = (eligible.Variants[0].Id, eligible.Variants[1].Id, eligible.Variants[2].Id);
            var d = plain.Variants[0].Id;
            await BizTest.ExecuteSqlAsync("UPDATE product_variants SET sale_price = 950 WHERE id = @V", ("@V", b));
            await BizTest.ExecuteSqlAsync("UPDATE product_variants SET sale_price = 850 WHERE id = @V", ("@V", c));

            // 後台 S1：商品「適用會員折扣」勾選（整份取代語意，省略＝不適用）
            var detail = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.GetAsync($"/api/v1/admin/{Club}/shop/products/{eligible.ProductId}"));
            Assert.False(detail.MemberDiscountEligible);
            var put = await admin.PutAsync($"/api/v1/admin/{Club}/shop/products/{eligible.ProductId}", BizTest.Json(new
            {
                slug = eligible.Slug, status = "published", memberDiscountEligible = true,
                content = new { zh = new { name = "【測試】mdisc" }, en = new { name = "Test mdisc" } },
            }));
            Assert.True((await BizTest.ReadAsync<AdminProductDetailDto>(put)).MemberDiscountEligible);
            var publicProduct = await (await fixture.CreateClient().GetAsync($"/api/v1/{Club}/shop/products/{eligible.Slug}")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(publicProduct.GetProperty("memberDiscountEligible").GetBoolean());

            var member = await members.CreateVerifiedMemberAsync("mdisc-standard");
            var registered = await members.CreateVerifiedMemberAsync("mdisc-registered");
            memberIds.AddRange([member.MemberId, registered.MemberId]);
            await MemberTestScope.BuyMembershipAsync(member, "standard"); // 磐石會員：商店折扣 10%（種子）

            // ── 購物車：會員 ──
            foreach (var (variant, qty) in new[] { (a, 2), (b, 1), (c, 1), (d, 1) })
            {
                await AddAsync(member.Client, variant, qty);
            }

            var cart = await CartAsync(member.Client);
            Assert.Equal(10, cart.GetProperty("memberDiscountPercent").GetInt32());
            var la = Line(cart, a);
            Assert.Equal(1000, la.GetProperty("listPrice").GetInt32());
            Assert.Equal(900, la.GetProperty("unitPrice").GetInt32());
            Assert.True(la.GetProperty("memberDiscountApplied").GetBoolean());
            Assert.Equal(200, la.GetProperty("memberDiscountAmount").GetInt32());
            Assert.Equal(1800, la.GetProperty("lineTotal").GetInt32());
            var lb = Line(cart, b); // 促銷價 950，會員折扣價 900 → 取 900
            Assert.Equal(900, lb.GetProperty("unitPrice").GetInt32());
            Assert.False(lb.GetProperty("onSale").GetBoolean());
            Assert.True(lb.GetProperty("memberDiscountApplied").GetBoolean());
            Assert.Equal(50, lb.GetProperty("memberDiscountAmount").GetInt32());
            var lc = Line(cart, c); // 促銷價 850 < 900 → 取促銷價，不疊加
            Assert.Equal(850, lc.GetProperty("unitPrice").GetInt32());
            Assert.True(lc.GetProperty("onSale").GetBoolean());
            Assert.False(lc.GetProperty("memberDiscountApplied").GetBoolean());
            var ld = Line(cart, d); // 沒勾選的商品
            Assert.Equal(1000, ld.GetProperty("unitPrice").GetInt32());
            Assert.False(ld.GetProperty("memberDiscountApplied").GetBoolean());
            Assert.Equal(1800 + 900 + 850 + 1000, cart.GetProperty("subtotal").GetInt32());
            Assert.Equal(250, cart.GetProperty("memberDiscountTotal").GetInt32());

            // ── 僅註冊帳號、訪客：付原價或促銷價 ──
            await AddAsync(registered.Client, a, 1);
            var regCart = await CartAsync(registered.Client);
            Assert.Equal(JsonValueKind.Null, regCart.GetProperty("memberDiscountPercent").ValueKind);
            Assert.Equal(1000, Line(regCart, a).GetProperty("unitPrice").GetInt32());
            Assert.Equal(0, regCart.GetProperty("memberDiscountTotal").GetInt32());
            using var guest = fixture.CreateClient();
            var guestToken = await AddAsync(guest, b, 1);
            cartHashes.Add(ShopCartService.Hash(guestToken!));
            var guestLine = Line(await CartAsync(guest, guestToken), b);
            Assert.Equal(950, guestLine.GetProperty("unitPrice").GetInt32());
            Assert.True(guestLine.GetProperty("onSale").GetBoolean());

            // ── 會籍到期 → 不再享有折扣（直接查庫，不讀快取）；還原後恢復 ──
            await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", member.MemberId));
            Assert.Equal(1000, Line(await CartAsync(member.Client), a).GetProperty("unitPrice").GetInt32());
            await BizTest.ExecuteSqlAsync("UPDATE memberships SET membership_end_on = DATEADD(DAY, 30, CAST(SYSUTCDATETIME() AS date)) WHERE member_id = @M", ("@M", member.MemberId));
            Assert.Equal(900, Line(await CartAsync(member.Client), a).GetProperty("unitPrice").GetInt32());

            // ── 結帳：用記錄用金流與發票替身核對金額 ──
            var gateway = new RecordingGateway();
            var invoices = new RecordingInvoiceIssuer();
            using var factory = fixture.WithWebHostBuilder(builder => builder.ConfigureServices(s =>
            {
                s.RemoveAll<IPaymentGateway>();
                s.AddSingleton<IPaymentGateway>(gateway);
                s.RemoveAll<IInvoiceIssuer>();
                s.AddSingleton<IInvoiceIssuer>(invoices);
            }));
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = member.Client.DefaultRequestHeaders.Authorization;
            var prefix = await EnsureInvoicePrefixAsync();
            try
            {
                using var checkout = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{Club}/shop/checkout")
                {
                    Content = BizTest.Json(new { deliveryMethod = "onsite_pickup", invoice = new { type = "donation", donationCode = await EnsureDonationCodeAsync() } }),
                };
                checkout.Headers.Add("Idempotency-Key", "md-" + Guid.NewGuid().ToString("N"));
                using var created = await client.SendAsync(checkout);
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                var order = await created.Content.ReadFromJsonAsync<JsonElement>();
                var orderNo = order.GetProperty("orderNo").GetString()!;
                Assert.Equal(4550, order.GetProperty("subtotal").GetInt32()); // 小計已是折後金額
                Assert.Equal(250, order.GetProperty("memberDiscountAmount").GetInt32());
                Assert.Equal(4550, order.GetProperty("total").GetInt32());
                var orderLine = order.GetProperty("items").EnumerateArray().First(i => i.GetProperty("unitPrice").GetInt32() == 900 && i.GetProperty("quantity").GetInt32() == 2);
                Assert.Equal(1800, orderLine.GetProperty("lineTotal").GetInt32());
                Assert.Equal(200, orderLine.GetProperty("memberDiscountAmount").GetInt32());

                // 資料庫快照：訂單與品項的會員折扣金額、套用的方案
                var planId = await B1Test.PlanIdAsync(Club, "2026-27", "standard");
                Assert.Equal(1, await ShopTest.CountAsync("SELECT COUNT(*) FROM orders WHERE order_no = @N AND member_discount_amount = 250 AND member_discount_plan_id = @P AND subtotal = 4550", ("@N", orderNo), ("@P", planId)));
                Assert.Equal(250, await ShopTest.CountAsync("SELECT SUM(i.member_discount_amount) FROM order_items i JOIN orders o ON o.id = i.order_id WHERE o.order_no = @N", ("@N", orderNo)));
                Assert.Equal(1, await ShopTest.CountAsync("SELECT COUNT(*) FROM order_items i JOIN orders o ON o.id = i.order_id WHERE o.order_no = @N AND i.unit_price_snapshot = 850 AND i.member_discount_amount = 0", ("@N", orderNo)));

                // LINE Pay 請款金額＝訂單總額；確認金額＝訂單總額；電子發票金額與明細加總＝訂單總額
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/{Club}/shop/orders/{orderNo}/pay", null)).StatusCode);
                Assert.Equal([4550], gateway.Reserved);
                var confirm = await client.PostAsync($"/api/v1/{Club}/shop/orders/{orderNo}/confirm", BizTest.Json(new { transactionId = $"FAKE-{orderNo}" }));
                Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
                Assert.Equal([4550], gateway.Confirmed);
                var issued = Assert.Single(invoices.Issued);
                Assert.Equal(4550, issued.Total);
                Assert.Equal(4550, issued.Items.Sum(i => i.UnitPrice * i.Quantity));
                Assert.Equal(prefix, issued.InvoicePrefix);
            }
            finally
            {
                // 發票字軌通道要等訂單與發票都清掉之後才能還原（外鍵），還原放在最外層 finally
            }

            // ── 後台現場建單指定會員：同一個計價函式（與促銷價不疊加、取低）；退款預設金額＝折後品項小計 ──
            var manual = await BizTest.ReadAsync<AdminOrderDetailDto>(await admin.PostAsync($"/api/v1/admin/{Club}/shop/orders", BizTest.Json(new
            {
                memberId = member.MemberId,
                items = new[] { new { variantId = a, quantity = 1 }, new { variantId = c, quantity = 1 } },
                deliveryMethod = "onsite_pickup", recipientName = "【測試】現場", recipientPhone = "0900-000-777",
            })));
            Assert.Equal(900 + 850, manual.Subtotal);
            Assert.Equal(100, manual.MemberDiscountAmount);
            Assert.Equal(900, manual.Items.First(i => i.VariantId == a).UnitPrice);
            Assert.Equal(850, manual.Items.First(i => i.VariantId == c).UnitPrice);
            var noMember = await BizTest.ReadAsync<AdminOrderDetailDto>(await admin.PostAsync($"/api/v1/admin/{Club}/shop/orders", BizTest.Json(new
            {
                items = new[] { new { variantId = a, quantity = 1 } }, deliveryMethod = "onsite_pickup", recipientName = "【測試】現場", recipientPhone = "0900-000-777",
            })));
            Assert.Equal(1000, noMember.Subtotal); // 沒指定會員不套用折扣
            Assert.Equal(0, noMember.MemberDiscountAmount);

            await C1Test.PostJsonAsync(admin, $"/api/v1/admin/{Club}/shop/orders/{manual.Id}/ship", new { pickupDeadlineOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd") });
            await C1Test.PostEmptyAsync(admin, $"/api/v1/admin/{Club}/shop/orders/{manual.Id}/complete");
            var refund = await BizTest.ReadAsync<AdminRefundDetailDto>(await admin.PostAsync($"/api/v1/admin/{Club}/shop/refunds", BizTest.Json(new
            {
                orderId = manual.Id, reason = "【測試】退一件", items = new[] { new { orderItemId = manual.Items.First(i => i.VariantId == a).Id, quantity = 1 } },
            })));
            Assert.Equal(900, refund.RefundAmount); // 按折後金額退，不是原價 1000
        }
        finally
        {
            await ShopTest.CleanupAsync([.. products]);
            await RestoreInvoicePrefixAsync();
            foreach (var hash in cartHashes)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM cart_items WHERE cart_id IN (SELECT id FROM carts WHERE anonymous_token = @H); DELETE FROM carts WHERE anonymous_token = @H;", ("@H", hash));
            }

            foreach (var id in memberIds)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM cart_items WHERE cart_id IN (SELECT id FROM carts WHERE member_id = @M); DELETE FROM carts WHERE member_id = @M;", ("@M", id));
            }
        }
    }

    // ── 發票字軌與捐贈碼（結帳需要；沿用既有測試的作法，測完還原）──
    private string? _invoiceRestoreSql;
    private Guid _invoiceRestoreId;

    private async Task<string> EnsureInvoicePrefixAsync()
    {
        var owner = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM clubs WHERE is_collecting_subject = 1 ORDER BY sort_order");
        var env = await C1Test.ScalarAsync<string>("SELECT TOP 1 setting_value FROM settings WHERE club_id = @O AND setting_key = 'shop.payment_environment'", ("@O", owner)) is "production" ? "production" : "sandbox";
        var existing = await C1Test.ScalarAsync<string>("SELECT TOP 1 CONCAT(CAST(id AS nvarchar(40)), '|', ISNULL(invoice_prefix, '')) FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'einvoice' AND environment = @E", ("@O", owner), ("@E", env));
        if (existing is null)
        {
            await BizTest.ExecuteSqlAsync("INSERT INTO payment_channels (owner_club_id, channel_type, environment, credential_encrypted, invoice_prefix) VALUES (@O, 'einvoice', @E, 'test-only-md', 'ZZ')", ("@O", owner), ("@E", env));
            _invoiceRestoreSql = "DELETE FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'einvoice' AND credential_encrypted = 'test-only-md'";
            _invoiceRestoreId = owner;
            return "ZZ";
        }

        var parts = existing.Split('|');
        if (parts[1].Length == 2)
        {
            return parts[1];
        }

        _invoiceRestoreId = Guid.Parse(parts[0]);
        _invoiceRestoreSql = $"UPDATE payment_channels SET invoice_prefix = NULLIF('{parts[1]}', '') WHERE id = @O";
        await BizTest.ExecuteSqlAsync("UPDATE payment_channels SET invoice_prefix = 'ZZ' WHERE id = @I", ("@I", _invoiceRestoreId));
        return "ZZ";
    }

    private async Task RestoreInvoicePrefixAsync()
    {
        if (_invoiceRestoreSql is not null)
        {
            await BizTest.ExecuteSqlAsync(_invoiceRestoreSql, ("@O", _invoiceRestoreId));
        }
    }

    private static async Task<string> EnsureDonationCodeAsync()
    {
        var code = await C1Test.ScalarAsync<string>("SELECT TOP 1 code FROM invoice_donation_codes WHERE is_active = 1 ORDER BY sort_order, row_seq");
        if (code is not null)
        {
            return code;
        }

        await BizTest.ExecuteSqlAsync("INSERT INTO invoice_donation_codes (code, org_name, is_active) VALUES ('9990001', N'【測試】捐贈團體', 1)");
        return "9990001";
    }
}
