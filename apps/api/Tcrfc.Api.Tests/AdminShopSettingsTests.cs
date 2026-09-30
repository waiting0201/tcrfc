using System.Net;
using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S6 商店設定、金流與發票憑證、發票捐贈碼、報表（S3-4）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminShopSettingsTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 商店設定_運費層級與政策_雙語_驗證_影響建單運費_收款主體提醒()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "shop.%");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "setting", ("M", 400, 10));
        try
        {
            var seeded = await BizTest.ReadAsync<AdminShopSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/settings"));
            Assert.Contains("收款主體", seeded.CollectingSubject.Notice);
            Assert.Contains("台中磐石", seeded.CollectingSubject.Name);

            Task<HttpResponseMessage> Put(object body) => admin.PutAsync("/api/v1/admin/tcrfc/shop/settings", BizTest.Json(body));
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(new { shippingFee = -1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(new { shippingFee = 60, freeShippingThreshold = -5 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(new { shippingFee = 60, lowStockThreshold = -1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(new { shippingFee = 60, pendingTimeoutMinutes = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(new { shippingFee = 60, excludedRegions = new[] { new string('x', 40) } })).StatusCode);

            var saved = await BizTest.ReadAsync<AdminShopSettingsDto>(await Put(new
            {
                shippingFee = 55, freeShippingThreshold = 1000, excludedRegions = new[] { "【測試】離島", "【測試】山區", "【測試】離島" }, lowStockThreshold = 7, pendingTimeoutMinutes = 45,
                entryTitle = new { zh = "【測試】商店", en = "Test shop" }, policyReturns = new { zh = "【測試】退換貨政策" }, policyTerms = new { zh = "條款", en = "Terms" },
            }));
            Assert.Equal(55, saved.ShippingFee);
            Assert.Equal(1000, saved.FreeShippingThreshold);
            Assert.Equal(new[] { "【測試】離島", "【測試】山區" }, saved.ExcludedRegions.ToArray()); // 去除重複
            Assert.Equal((7, 45), (saved.LowStockThreshold, saved.PendingTimeoutMinutes));
            Assert.Equal("Test shop", saved.EntryTitle.En);
            Assert.Null(saved.PolicyReturns.En);
            Assert.Null(saved.PolicyNotice.Zh); // PUT 整份取代：沒帶的政策就是清空

            // 運費規則：小計未達免運門檻收固定運費；達門檻免運；現場自取免運
            var low = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (made.Variants[0].Id, 1));
            Assert.Equal(55, low.ShippingFee);
            var high = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (made.Variants[0].Id, 3));
            Assert.Equal(0, high.ShippingFee);
            Assert.Equal(1200, high.Total);
            var pickup = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (made.Variants[0].Id, 1));
            Assert.Equal(0, pickup.ShippingFee);

            // 低庫存門檻用新設定：可售量 5 ≤ 7
            var inventory = await BizTest.ReadAsync<PagedResult<AdminInventoryItemDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/inventory?lowStockOnly=true&keyword={made.Variants[0].Sku}"));
            Assert.Equal(7, Assert.Single(inventory.Items).LowStockThreshold);

            // 不設免運門檻＝一律收運費；藍鯨的設定是獨立的一份（俱樂部層級）
            var noThreshold = await BizTest.ReadAsync<AdminShopSettingsDto>(await Put(new { shippingFee = 30 }));
            Assert.Null(noThreshold.FreeShippingThreshold);
            Assert.Equal(5, noThreshold.LowStockThreshold);
            Assert.Equal(30, (await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (made.Variants[0].Id, 3))).ShippingFee);
            Assert.NotEqual(30, (await BizTest.ReadAsync<AdminShopSettingsDto>(await admin.GetAsync("/api/v1/admin/bw/shop/settings"))).ShippingFee);

            // 權限：客服／行政沒有商店設定；商務／贊助可看可改；檢視者不行
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/tcrfc/shop/settings")).StatusCode);
            using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
            Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/admin/tcrfc/shop/settings")).StatusCode);
            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/tcrfc/shop/settings")).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
            await restore();
        }
    }

    [Fact]
    public async Task 金流與發票憑證_只有系統管理員_密鑰加密且永不回傳_輪替時間_字軌驗證_模式與重試設定()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "shop.%");
        var owner = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM clubs WHERE is_collecting_subject = 1 ORDER BY sort_order");
        var before = await ShopTest.CountAsync("SELECT COUNT(*) FROM payment_channels WHERE owner_club_id = @O", ("@O", owner));
        var url = "/api/v1/admin/tcrfc/shop/credentials";
        try
        {
            // 商務／贊助雖有 S6，但憑證是 sysadmin_only；客服／行政更不行
            foreach (var client in new[] { business, service })
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PutJsonAsync(client, url + "/linepay", new { environment = "sandbox", channelId = "1", channelSecret = "s" })).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PutJsonAsync(client, url + "/mode", new { environment = "production" })).StatusCode);
            }

            var initial = await BizTest.ReadAsync<AdminShopCredentialsDto>(await admin.GetAsync(url));
            Assert.False(initial.IntegrationConnected);
            Assert.Contains("收款主體", initial.CollectingSubject.Notice);
            Assert.False(initial.LinePay.Sandbox.Configured);

            // 驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/linepay", new { environment = "staging", channelId = "1", channelSecret = "s" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/linepay", new { environment = "sandbox", channelId = "1234567890" })).StatusCode); // 第一次必須有密鑰

            const string secret = "SECRET-DO-NOT-LEAK-9f8e7d";
            var response = await C1Test.PutJsonAsync(admin, url + "/linepay", new { environment = "sandbox", channelId = "1234567890", channelSecret = secret });
            var body = await C1Test.BodyAsync(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(secret, body);
            Assert.DoesNotContain("1234567890", body); // 識別碼只回遮罩值
            var saved = await BizTest.ReadAsync<AdminShopCredentialsDto>(await admin.GetAsync(url));
            Assert.True(saved.LinePay.Sandbox.Configured);
            Assert.Equal("******7890", saved.LinePay.Sandbox.IdentifierMasked);
            Assert.False(saved.LinePay.Production.Configured);
            var rotatedAt = saved.LinePay.Sandbox.RotatedAt;
            Assert.NotNull(rotatedAt);

            // 密鑰加密存放：資料庫裡看不到明文
            var stored = await C1Test.ScalarAsync<string>("SELECT credential_encrypted FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'linepay' AND environment = 'sandbox'", ("@O", owner));
            Assert.DoesNotContain(secret, stored);
            Assert.DoesNotContain("1234567890", stored);

            // 沒帶密鑰＝沿用；輪替時間不變。換密鑰＝輪替時間更新
            await Task.Delay(30);
            await C1Test.PutJsonAsync(admin, url + "/linepay", new { environment = "sandbox", channelId = "1234567890" });
            Assert.Equal(rotatedAt, (await BizTest.ReadAsync<AdminShopCredentialsDto>(await admin.GetAsync(url))).LinePay.Sandbox.RotatedAt);
            await C1Test.PutJsonAsync(admin, url + "/linepay", new { environment = "sandbox", channelId = "1234567890", channelSecret = secret + "-2" });
            Assert.True((await BizTest.ReadAsync<AdminShopCredentialsDto>(await admin.GetAsync(url))).LinePay.Sandbox.RotatedAt > rotatedAt);

            // 電子發票：字軌兩位大寫英文字母
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/einvoice", new { environment = "sandbox", invoicePrefix = "A1" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/einvoice", new { environment = "sandbox", invoicePrefix = "ABC" })).StatusCode);
            var invoice = await BizTest.ReadAsync<AdminShopCredentialsDto>(await C1Test.PutJsonAsync(admin, url + "/einvoice", new { environment = "sandbox", merchantId = "M-0001", apiKey = "KEY-SECRET", invoicePrefix = "ab" }));
            Assert.Equal("AB", invoice.EInvoice.Sandbox.InvoicePrefix);
            Assert.True(invoice.EInvoice.Sandbox.Configured);

            // 環境開關與重試設定
            Assert.Equal("sandbox", initial.Environment);
            Assert.Equal("production", (await BizTest.ReadAsync<AdminShopCredentialsDto>(await C1Test.PutJsonAsync(admin, url + "/mode", new { environment = "production" }))).Environment);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/mode", new { environment = "x" })).StatusCode);
            var retry = await BizTest.ReadAsync<AdminShopCredentialsDto>(await C1Test.PutJsonAsync(admin, url + "/invoice-retry", new { maxRetries = 5, intervalMinutes = 15 }));
            Assert.Equal((5, 15), (retry.InvoiceRetry.MaxRetries, retry.InvoiceRetry.IntervalMinutes));
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/invoice-retry", new { maxRetries = 11, intervalMinutes = 15 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, url + "/invoice-retry", new { maxRetries = 1, intervalMinutes = 0 })).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM payment_channels WHERE owner_club_id = @O AND created_at > DATEADD(hour, -1, SYSUTCDATETIME())", ("@O", owner));
            await restore();
            Assert.Equal(before, await ShopTest.CountAsync("SELECT COUNT(*) FROM payment_channels WHERE owner_club_id = @O", ("@O", owner)));
        }
    }

    [Fact]
    public async Task 發票捐贈碼_全系統共用_權限_格式與重複()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var url = "/api/v1/admin/shop/donation-codes";
        var code = "88" + Random.Shared.Next(10000, 99999);
        var created = Guid.Empty;
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(url)).StatusCode);
            var list = await BizTest.ReadAsync<List<AdminDonationCodeDto>>(await business.GetAsync(url));
            Assert.Contains(list, c => c.Code == "9990001");
            Assert.Contains(list, c => c.Code == "9990002" && !c.IsActive);

            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(business, url, new { code = "12", orgName = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(business, url, new { code = "ABC123", orgName = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(business, url, new { code, orgName = " " })).StatusCode);
            var made = await BizTest.ReadAsync<AdminDonationCodeDto>(await C1Test.PostJsonAsync(business, url, new { code, orgName = "【測試】捐贈機構", isActive = true }));
            created = made.Id;
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(business, url, new { code, orgName = "重複" })).StatusCode);
            var updated = await BizTest.ReadAsync<AdminDonationCodeDto>(await C1Test.PutJsonAsync(business, $"{url}/{created}", new { code, orgName = "【測試】改名", isActive = false }));
            Assert.Equal("【測試】改名", updated.OrgName);
            Assert.False(updated.IsActive);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(business, $"{url}/{created}", new { code = "9990001", orgName = "撞名" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await business.DeleteAsync($"{url}/{created}")).StatusCode);
            created = Guid.Empty;
            Assert.Equal(HttpStatusCode.NotFound, (await business.DeleteAsync($"{url}/{made.Id}")).StatusCode);
        }
        finally
        {
            if (created != Guid.Empty)
            {
                await business.DeleteAsync($"{url}/{created}");
            }
        }
    }

    [Fact]
    public async Task 報表_摘要口徑_依賣方俱樂部加總_資料範圍_匯出權限與用途()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "report", ("M", 800, 10));
        var reports = "/api/v1/admin/tcrfc/shop/reports";
        try
        {
            var before = await BizTest.ReadAsync<AdminShopReportSummaryDto>(await business.GetAsync($"{reports}/summary"));
            var beforeClubs = await BizTest.ReadAsync<List<AdminSellingClubTotalDto>>(await admin.GetAsync($"{reports}/by-selling-club"));
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (made.Variants[0].Id, 2));
            await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/ship", new { });
            await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/complete");

            var after = await BizTest.ReadAsync<AdminShopReportSummaryDto>(await business.GetAsync($"{reports}/summary"));
            Assert.Equal(before.OrderCount + 1, after.OrderCount);
            Assert.Equal(before.GrossRevenue + 1600, after.GrossRevenue);
            Assert.Equal(before.NetRevenue + 1600, after.NetRevenue);
            var top = Assert.Single(after.TopSkus, t => t.Sku == made.Variants[0].Sku);
            Assert.Equal((2, 1600), (top.Quantity, top.Revenue));
            Assert.InRange(after.AverageOrderValue, after.GrossRevenue / after.OrderCount - 1, after.GrossRevenue / after.OrderCount + 1);

            // 退款：淨營收扣掉、退貨率上升
            var refund = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, "/api/v1/admin/tcrfc/shop/refunds", new
            {
                orderId = order.Id, reason = "【測試】退一件", items = new[] { new { orderItemId = order.Items[0].Id, quantity = 1 } }, needsReturn = false,
            }));
            await C1Test.PostJsonAsync(service, $"/api/v1/admin/tcrfc/shop/refunds/{refund.Id}/approve", new { });
            await C1Test.PostJsonAsync(admin, $"/api/v1/admin/tcrfc/shop/refunds/{refund.Id}/execute", new { });
            var refunded = await BizTest.ReadAsync<AdminShopReportSummaryDto>(await business.GetAsync($"{reports}/summary"));
            Assert.Equal(before.RefundedAmount + 800, refunded.RefundedAmount);
            Assert.Equal(before.NetRevenue + 800, refunded.NetRevenue);
            Assert.Equal(before.ReturnOrderCount + 1, refunded.ReturnOrderCount);
            Assert.True(refunded.ReturnRatePercent > 0);

            // 依賣方俱樂部加總：系統管理員看得到兩隊；合作球隊管理（僅藍鯨）只有藍鯨；磐石的訂單不算進藍鯨
            var clubs = await BizTest.ReadAsync<List<AdminSellingClubTotalDto>>(await admin.GetAsync($"{reports}/by-selling-club"));
            Assert.Equal(new[] { "tcrfc", "bw" }, clubs.Select(c => c.SellingClubCode).ToArray());
            var tc = clubs.Single(c => c.SellingClubCode == "tcrfc");
            Assert.Equal(beforeClubs.Single(c => c.SellingClubCode == "tcrfc").GrossRevenue + 1600, tc.GrossRevenue);
            Assert.Equal(beforeClubs.Single(c => c.SellingClubCode == "bw").GrossRevenue, clubs.Single(c => c.SellingClubCode == "bw").GrossRevenue);
            Assert.True(clubs.Single(c => c.SellingClubCode == "bw").PendingSettlementAmount > 0); // 藍鯨種子訂單待結算
            var bwOnly = await BizTest.ReadAsync<List<AdminSellingClubTotalDto>>(await partner.GetAsync("/api/v1/admin/bw/shop/reports/by-selling-club"));
            Assert.Equal("bw", Assert.Single(bwOnly).SellingClubCode);
            var bwSummary = await BizTest.ReadAsync<AdminShopReportSummaryDto>(await partner.GetAsync("/api/v1/admin/bw/shop/reports/summary"));
            Assert.DoesNotContain(bwSummary.TopSkus, t => t.Sku == made.Variants[0].Sku);

            // 權限與驗證
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{reports}/summary")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync($"{reports}/summary")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.GetAsync($"{reports}/summary?from=2026-09-30&to=2026-09-01")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.GetAsync($"{reports}/summary?from=2020-01-01&to=2026-09-01")).StatusCode);

            // 匯出：需要 shop.report.export（客服 403）、必填用途、種類檢查
            Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync($"{reports}/export?purpose=x")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.GetAsync($"{reports}/export")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.GetAsync($"{reports}/export?purpose=x&kind=oops")).StatusCode);
            var summaryCsv = Encoding.UTF8.GetString(await (await business.GetAsync($"{reports}/export?purpose={Uri.EscapeDataString("月結")}")).Content.ReadAsByteArrayAsync());
            Assert.Contains("淨營收", summaryCsv);
            Assert.Contains(made.Variants[0].Sku, summaryCsv);
            var clubCsv = Encoding.UTF8.GetString(await (await business.GetAsync($"{reports}/export?purpose=x&kind=by-selling-club")).Content.ReadAsByteArrayAsync());
            Assert.Contains("已結算金額", clubCsv);
            Assert.Contains("待結算金額", clubCsv);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    private const string Orders = "/api/v1/admin/tcrfc/shop/orders";
}
