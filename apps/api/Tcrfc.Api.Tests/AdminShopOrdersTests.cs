using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S3 訂單、S4 出貨與物流、S5 退貨與退款（S3-3／S3-4）。金流與發票不串接：LINE Pay 退款以測試用的假金流驗證「串接後」的流程。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminShopOrdersTests(AdminWriteApiFixture fixture)
{
    private sealed class FakeLinePay : ILinePayGateway
    {
        public static int Calls;

        public async Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            await Task.Delay(250, cancellationToken); // 拉長處理時間，讓並行的第二個請求一定撞上「退款處理中」
            return new GatewayRefundResult(GatewayOutcome.Succeeded, "FAKE-REF-" + request.OrderNo);
        }
    }

    private const string Orders = "/api/v1/admin/tcrfc/shop/orders";

    private static async Task<AdminOrderDetailDto> GetOrderAsync(HttpClient client, Guid id, string club = "tcrfc")
        => await BizTest.ReadAsync<AdminOrderDetailDto>(await client.GetAsync($"/api/v1/admin/{club}/shop/orders/{id}"));

    private static async Task<AdminRefundDetailDto> RefundAsync(HttpClient client, Guid id)
        => await BizTest.ReadAsync<AdminRefundDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/shop/refunds/{id}"));

    // ═════════════ S3：建立、流程、快照 ═════════════

    [Fact]
    public async Task 現場收款訂單_建立扣庫存_價格快照_流程備貨出貨完成_資料範圍()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "flow", ("M", 500, 10));
        var variant = made.Variants[0];
        try
        {
            var settings = await BizTest.ReadAsync<AdminShopSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/settings"));
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (variant.Id, 2));
            Assert.StartsWith("TR-", order.OrderNo);
            Assert.Equal("已付款", order.OrderStatus);
            Assert.Equal("paid", order.PaymentStatus);
            Assert.Equal("現場收款", order.PaymentMethodLabel);
            Assert.True(order.IsManual);
            Assert.False(order.IsMember);
            Assert.Equal(1000, order.Subtotal);
            var expectedFee = settings.FreeShippingThreshold is int t && 1000 >= t ? 0 : settings.ShippingFee;
            Assert.Equal(expectedFee, order.ShippingFee);
            Assert.Equal(1000 + expectedFee, order.Total);
            Assert.Equal("tcrfc", order.SellingClubCode);
            Assert.Equal(order.SellingClubId, order.CollectingClubId);
            Assert.Equal("待結算", order.SettlementStatusLabel);
            Assert.Equal("未出貨", order.ShipmentStatusLabel);
            Assert.False(order.IsMasked); // 客服／行政看得到完整收件人資料
            Assert.Equal("0900-000-777", order.RecipientPhone);
            var item = Assert.Single(order.Items);
            Assert.Equal("【測試】flow", item.ProductName);
            Assert.Equal(500, item.UnitPrice);
            Assert.Equal(new[] { "prepare", "ship", "cancel" }, order.AvailableActions.ToArray());

            // 庫存已扣減（售出），異動紀錄關聯到訂單
            var after = await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id);
            Assert.Equal(8, after.StockQty);
            var sale = (await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/inventory/movements?orderId={order.Id}"))).Items.Single();
            Assert.Equal("sale", sale.MovementType);
            Assert.Equal(-2, sale.Quantity);
            Assert.Equal(order.OrderNo, sale.OrderNo);

            // 價格快照：之後改價不影響歷史訂單
            await admin.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{variant.Id}", BizTest.Json(new { sku = variant.Sku, size = "M", price = 999 }));
            Assert.Equal(500, (await GetOrderAsync(admin, order.Id)).Items[0].UnitPrice);

            // 資料範圍：別的俱樂部路由看不到這張訂單
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/bw/shop/orders/{order.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await C1Test.PostEmptyAsync(admin, $"/api/v1/admin/bw/shop/orders/{order.Id}/prepare")).StatusCode);

            // 備貨中 → 出貨（物流）→ 補填單號 → 完成
            var preparing = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/prepare"));
            Assert.Equal("備貨中", preparing.OrderStatus);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/prepare")).StatusCode);
            var shipped = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/ship", new { carrier = "【測試】物流", trackingNo = "TRK-001" }));
            Assert.Equal("已出貨", shipped.OrderStatus);
            Assert.Equal("已出貨", shipped.ShipmentStatusLabel);
            Assert.Equal("TRK-001", shipped.Shipment?.TrackingNo);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/ship", new { carrier = "x" })).StatusCode);
            var tracked = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PutJsonAsync(service, $"{Orders}/{order.Id}/shipment", new { carrier = "【測試】物流", trackingNo = "TRK-002" }));
            Assert.Equal("TRK-002", tracked.Shipment?.TrackingNo);
            Assert.Equal(new[] { "complete", "request_refund" }, tracked.AvailableActions.ToArray());
            var done = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/complete"));
            Assert.Equal("已完成", done.OrderStatus);
            Assert.Equal("已送達", done.ShipmentStatusLabel);
            Assert.NotNull(done.CompletedAt);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/complete")).StatusCode);

            // 內部註記、分帳標記（人工旗標，不是狀態機）
            var noted = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PutJsonAsync(service, $"{Orders}/{order.Id}/notes", new { internalNote = "【測試】內部註記" }));
            Assert.Equal("【測試】內部註記", noted.InternalNote);
            var settled = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PutJsonAsync(service, $"{Orders}/{order.Id}/settlement", new { status = "settled", note = "【測試】已匯款" }));
            Assert.Equal("已結算", settled.SettlementStatusLabel);
            Assert.NotNull(settled.SettledOn);
            var reverted = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PutJsonAsync(service, $"{Orders}/{order.Id}/settlement", new { status = "pending" }));
            Assert.Null(reverted.SettledOn);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, $"{Orders}/{order.Id}/settlement", new { status = "oops" })).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task 建立訂單_驗證規則_停售與跨俱樂部規格_可售量不足整張訂單回復()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "create", ("M", 400, 3), ("L", 400, 3));
        try
        {
            Task<HttpResponseMessage> Post(object body) => service.PostAsync(Orders, BizTest.Json(body));
            var m = made.Variants[0];
            var l = made.Variants[1];
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = Array.Empty<object>(), deliveryMethod = "onsite_pickup" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 0 } }, deliveryMethod = "onsite_pickup" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "teleport" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "home_delivery" })).StatusCode); // 宅配須收件資料
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "cvs_pickup", recipientName = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "home_delivery", completeImmediately = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = Guid.NewGuid(), quantity = 1 } }, deliveryMethod = "onsite_pickup" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup", memberId = Guid.NewGuid() })).StatusCode);
            // 別的俱樂部路由不能用這個規格
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/bw/shop/orders", BizTest.Json(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup" }))).StatusCode);

            // 第二個品項庫存不足：整張訂單不成立，第一個品項的庫存也沒被扣
            var over = await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 }, new { variantId = l.Id, quantity = 4 } }, deliveryMethod = "onsite_pickup" });
            Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
            Assert.Contains(l.Sku, await C1Test.BodyAsync(over));
            Assert.Equal(3, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, m.Id)).StockQty);
            Assert.Equal(0, await ShopTest.CountAsync("SELECT COUNT(*) FROM order_items WHERE product_variant_id IN (@A, @B)", ("@A", m.Id), ("@B", l.Id)));

            // 停售的規格不能下單
            await admin.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{l.Id}", BizTest.Json(new { sku = l.Sku, size = "L", price = 400, status = "inactive" }));
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = l.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup" })).StatusCode);

            // 現場自取免運、可當場完成；同一規格重複列的品項會合併
            var done = await BizTest.ReadAsync<AdminOrderDetailDto>(await Post(new
            {
                items = new[] { new { variantId = m.Id, quantity = 1 }, new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup", completeImmediately = true,
                recipientName = "【測試】現場客", customerNote = "【測試】備註",
            }));
            Assert.Equal("已完成", done.OrderStatus);
            Assert.Equal(0, done.ShippingFee);
            Assert.Equal(800, done.Total);
            Assert.Equal(2, Assert.Single(done.Items).Quantity);
            Assert.Equal("已領取", done.ShipmentStatusLabel);
            Assert.Equal("【測試】備註", done.CustomerNote);
            Assert.Equal(1, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, m.Id)).StockQty);
            // 運費覆寫
            var custom = await BizTest.ReadAsync<AdminOrderDetailDto>(await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup", shippingFee = 25 }));
            Assert.Equal(25, custom.ShippingFee);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { items = new[] { new { variantId = m.Id, quantity = 1 } }, deliveryMethod = "onsite_pickup", shippingFee = -1 })).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task 建立訂單_並行搶最後庫存_不會超賣()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "oversell", ("M", 100, 3));
        try
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.PostAsync(Orders, BizTest.Json(new
            {
                items = new[] { new { variantId = made.Variants[0].Id, quantity = 1 } }, deliveryMethod = "onsite_pickup",
            }))));
            Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(5, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(0, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, made.Variants[0].Id)).StockQty);
            Assert.Equal(3, await ShopTest.CountAsync("SELECT COUNT(*) FROM order_items WHERE product_variant_id = @V", ("@V", made.Variants[0].Id)));
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ 取消 ═════════════

    [Fact]
    public async Task 取消訂單_已付款回補庫存並自動建立全額退款案件_退款執行只有系統管理員_不重複退款()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "cancel", ("M", 300, 6));
        var variant = made.Variants[0];
        try
        {
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (variant.Id, 2));
            Assert.Equal(4, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty);
            // 取消原因必填
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/cancel", new { reason = " " })).StatusCode);
            var cancelled = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/cancel", new { reason = "【測試】顧客反悔" }));
            Assert.Equal("已取消", cancelled.OrderStatus);
            Assert.Equal("【測試】顧客反悔", cancelled.CancelReason);
            Assert.NotNull(cancelled.CancelledAt);
            Assert.Equal(6, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty); // 回補
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/cancel", new { reason = "再取消一次" })).StatusCode);
            Assert.Equal(6, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty); // 沒有重複回補
            var refundSummary = Assert.Single(cancelled.Refunds);
            Assert.Equal("已核准", refundSummary.StatusLabel);
            Assert.Equal(cancelled.Total, refundSummary.RefundAmount);

            // 客服／行政不能執行退款（sysadmin_only）；系統管理員以人工退款登錄（現場收款）
            var url = $"/api/v1/admin/tcrfc/shop/refunds/{refundSummary.Id}/execute";
            Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PostJsonAsync(service, url, new { })).StatusCode);
            var executed = await BizTest.ReadAsync<AdminRefundExecuteResultDto>(await C1Test.PostJsonAsync(admin, url, new { note = "【測試】現場退現金" }));
            Assert.Equal("已退款", executed.Refund.StatusLabel);
            Assert.Equal("人工退款", executed.Refund.RefundMethodLabel);
            Assert.NotNull(executed.Refund.RefundedByName);
            Assert.Null(executed.InvoiceAction);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, url, new { })).StatusCode);
            var final = await GetOrderAsync(admin, order.Id);
            Assert.Equal("refunded", final.PaymentStatus);
            Assert.Equal("已取消", final.OrderStatus); // 已取消的訂單退款後仍是「已取消」
            // 尚未出貨的訂單不能手動申請退貨（請直接取消）
            var second = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (variant.Id, 1));
            var early = await C1Test.PostJsonAsync(service, "/api/v1/admin/tcrfc/shop/refunds", new { orderId = second.Id, reason = "x", items = new[] { new { orderItemId = second.Items[0].Id, quantity = 1 } } });
            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
            Assert.Contains("直接取消訂單", await C1Test.BodyAsync(early));
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ 退貨退款 ═════════════

    [Fact]
    public async Task 退貨退款_部分退款_審核驗收回補庫存_駁回還原訂單狀態_全額後訂單已退款()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "refund", ("M", 500, 10));
        var variant = made.Variants[0];
        var refunds = "/api/v1/admin/tcrfc/shop/refunds";
        try
        {
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (variant.Id, 2));
            Assert.Equal(8, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty);
            // 出貨前：onsite_pickup 備妥待領 → 領取完成，才能申請退貨
            await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/ship", new { pickupDeadlineOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd") });
            await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/complete");
            var item = order.Items[0];

            Task<HttpResponseMessage> Create(object body) => service.PostAsync(refunds, BizTest.Json(body));
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(new { orderId = order.Id, reason = "尺寸不合", items = new[] { new { orderItemId = item.Id, quantity = 3 } } })).StatusCode); // 超過購買數量
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(new { orderId = order.Id, reason = "尺寸不合", items = new[] { new { orderItemId = Guid.NewGuid(), quantity = 1 } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(new { orderId = order.Id, reason = "尺寸不合", items = new[] { new { orderItemId = item.Id, quantity = 1 } }, refundAmount = order.Total + 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(new { orderId = order.Id, reason = " ", items = new[] { new { orderItemId = item.Id, quantity = 1 } } })).StatusCode);

            // ① 先駁回一次：訂單回到「已完成」
            var first = await BizTest.ReadAsync<AdminRefundDetailDto>(await Create(new { orderId = order.Id, reason = "【測試】尺寸不合", items = new[] { new { orderItemId = item.Id, quantity = 1 } } }));
            Assert.Equal("申請中", first.StatusLabel);
            Assert.Equal(500, first.RefundAmount);
            Assert.True(first.NeedsReturn);
            Assert.Equal("退貨處理中", (await GetOrderAsync(admin, order.Id)).OrderStatus);
            Assert.Equal(HttpStatusCode.Conflict, (await Create(new { orderId = order.Id, reason = "重複", items = new[] { new { orderItemId = item.Id, quantity = 1 } } })).StatusCode); // 已有處理中案件
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{refunds}/{first.Id}/reject", new { })).StatusCode); // 駁回必填原因
            var rejected = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, $"{refunds}/{first.Id}/reject", new { note = "【測試】超過鑑賞期" }));
            Assert.Equal("已駁回", rejected.StatusLabel);
            Assert.Equal("已完成", (await GetOrderAsync(admin, order.Id)).OrderStatus);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{refunds}/{first.Id}/approve", new { })).StatusCode);

            // ② 再申請一次（部分退款：1 件）→ 核准 → 驗收退回品（回補庫存）→ 系統管理員執行退款
            var second = await BizTest.ReadAsync<AdminRefundDetailDto>(await Create(new { orderId = order.Id, reason = "【測試】瑕疵", items = new[] { new { orderItemId = item.Id, quantity = 1 } } }));
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, $"{refunds}/{second.Id}/execute", new { })).StatusCode); // 還沒核准不能退款
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{refunds}/{second.Id}/receive", new { })).StatusCode); // 還沒核准不能驗收
            var approved = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, $"{refunds}/{second.Id}/approve", new { note = "【測試】同意退貨" }));
            Assert.Equal("已核准", approved.StatusLabel);
            Assert.NotNull(approved.ApprovedByName);
            Assert.Equal(new[] { "receive", "reject" }, approved.AvailableActions.ToArray());
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, $"{refunds}/{second.Id}/execute", new { })).StatusCode); // 需退回商品：驗收前不能退款
            var received = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, $"{refunds}/{second.Id}/receive", new { restock = true }));
            Assert.Equal("已驗收退回品", received.StatusLabel);
            Assert.Equal(9, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty); // 退貨回補
            Assert.Equal(new[] { "execute" }, received.AvailableActions.ToArray());
            var partial = await BizTest.ReadAsync<AdminRefundExecuteResultDto>(await C1Test.PostJsonAsync(admin, $"{refunds}/{second.Id}/execute", new { }));
            Assert.Equal("已退款", partial.Refund.StatusLabel);
            Assert.Equal(500, partial.Refund.OrderRefundedTotal);
            var afterPartial = await GetOrderAsync(admin, order.Id);
            Assert.Equal("已完成", afterPartial.OrderStatus); // 部分退款：訂單回到已完成，付款狀態維持已付款
            Assert.Equal("paid", afterPartial.PaymentStatus);
            Assert.Equal(1, afterPartial.Items[0].RefundedQuantity);

            // ③ 退掉剩下的 1 件（含運費金額）：不需退回商品 → 核准後直接退款 → 訂單已退款
            var remaining = order.Total - 500;
            var third = await BizTest.ReadAsync<AdminRefundDetailDto>(await Create(new
            {
                orderId = order.Id, reason = "【測試】全額退", items = new[] { new { orderItemId = item.Id, quantity = 1 } }, refundAmount = remaining, needsReturn = false,
            }));
            await C1Test.PostJsonAsync(service, $"{refunds}/{third.Id}/approve", new { });
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{refunds}/{third.Id}/receive", new { })).StatusCode); // 不需退回商品 → 不必驗收
            await BizTest.ReadAsync<AdminRefundExecuteResultDto>(await C1Test.PostJsonAsync(admin, $"{refunds}/{third.Id}/execute", new { }));
            var full = await GetOrderAsync(admin, order.Id);
            Assert.Equal("已退款", full.OrderStatus);
            Assert.Equal("refunded", full.PaymentStatus);
            Assert.Equal(3, full.Refunds.Count);
            Assert.Equal(9, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).StockQty); // 不需退回的案件不回補

            // 案件列表與篩選；跨俱樂部 404
            var list = await BizTest.ReadAsync<PagedResult<AdminRefundListItemDto>>(await service.GetAsync($"{refunds}?keyword={order.OrderNo}"));
            Assert.Equal(3, list.TotalCount);
            Assert.Equal(2, (await BizTest.ReadAsync<PagedResult<AdminRefundListItemDto>>(await service.GetAsync($"{refunds}?status=refunded&keyword={order.OrderNo}"))).TotalCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{refunds}?status=oops")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/bw/shop/refunds/{third.Id}")).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ 金流：冪等、逾時釋回、LINE Pay 不串接與串接後 ═════════════

    [Fact]
    public async Task 待付款訂單_付款成立冪等_並行只扣一次_逾時釋回_取消釋回保留()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "pay", ("M", 200, 10));
        var variant = made.Variants[0];
        try
        {
            // ① 下單保留：庫存量不變、保留量增加、可售量減少
            var orderId = await ShopTest.CreatePendingOrderAsync(fixture, "tcrfc", null, (variant.Id, 3));
            var reserved = await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id);
            Assert.Equal((10, 3, 7), (reserved.StockQty, reserved.ReservedQty, reserved.AvailableQty));
            var pending = await GetOrderAsync(admin, orderId);
            Assert.Equal("待付款", pending.OrderStatus);
            Assert.Equal(new[] { "cancel" }, pending.AvailableActions.ToArray());
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{Orders}/{orderId}/prepare")).StatusCode);

            // ② 金流回呼重送：同時兩次付款成立，只有一次真的扣庫存
            using var scopeA = fixture.Services.CreateScope();
            using var scopeB = fixture.Services.CreateScope();
            var results = await Task.WhenAll(
                scopeA.ServiceProvider.GetRequiredService<ShopOrderLifecycle>().ConfirmPaymentAsync(orderId, "LP-TXN-1", null, CancellationToken.None),
                scopeB.ServiceProvider.GetRequiredService<ShopOrderLifecycle>().ConfirmPaymentAsync(orderId, "LP-TXN-1", null, CancellationToken.None));
            Assert.Equal(1, results.Count(r => r == PaymentConfirmResult.Confirmed));
            Assert.Equal(1, results.Count(r => r == PaymentConfirmResult.AlreadyPaid));
            var paid = await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id);
            Assert.Equal((7, 0), (paid.StockQty, paid.ReservedQty)); // 只扣一次：10 → 7，保留量歸零
            var order = await GetOrderAsync(admin, orderId);
            Assert.Equal("已付款", order.OrderStatus);
            Assert.Equal("LINE Pay", order.PaymentMethodLabel);
            Assert.Equal("LP-TXN-1", order.LinepayTransactionId);
            Assert.NotNull(order.PaidAt);
            using (var scope = fixture.Services.CreateScope())
            {
                Assert.Equal(PaymentConfirmResult.AlreadyPaid, await scope.ServiceProvider.GetRequiredService<ShopOrderLifecycle>().ConfirmPaymentAsync(orderId, "LP-TXN-1", null, CancellationToken.None));
            }

            Assert.Equal(1, await ShopTest.CountAsync("SELECT COUNT(*) FROM inventory_movements WHERE order_id = @O AND movement_type = 'sale'", ("@O", orderId)));

            // ③ 逾時：建立一張 2 小時前的待付款訂單 → 「釋回逾時訂單」釋回保留、訂單已取消（逾時）；第二次沒有東西可釋回
            var staleId = await ShopTest.CreatePendingOrderAsync(fixture, "tcrfc", DateTime.UtcNow.AddHours(-2), (variant.Id, 2));
            Assert.Equal(2, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).ReservedQty);
            var release = await BizTest.ReadAsync<ReleaseExpiredOrdersResultDto>(await C1Test.PostEmptyAsync(service, $"{Orders}/release-expired"));
            Assert.True(release.ExpiredCount >= 1);
            Assert.True(release.TimeoutMinutes >= 5);
            var stale = await GetOrderAsync(admin, staleId);
            Assert.Equal("已取消", stale.OrderStatus);
            Assert.Equal("expired", stale.PaymentStatus);
            Assert.Equal(0, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id)).ReservedQty);
            Assert.Equal(0, (await BizTest.ReadAsync<ReleaseExpiredOrdersResultDto>(await C1Test.PostEmptyAsync(service, $"{Orders}/release-expired"))).ExpiredCount);
            // 已逾時的訂單，遲到的付款回呼不能再成立
            using (var scope = fixture.Services.CreateScope())
            {
                await Assert.ThrowsAsync<AdminConflictException>(() => scope.ServiceProvider.GetRequiredService<ShopOrderLifecycle>().ConfirmPaymentAsync(staleId, "LATE", null, CancellationToken.None));
            }

            // ④ 後台取消待付款訂單：釋回保留，不建立退款案件
            var cancelId = await ShopTest.CreatePendingOrderAsync(fixture, "tcrfc", null, (variant.Id, 1));
            var cancelled = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{cancelId}/cancel", new { reason = "【測試】顧客不買了" }));
            Assert.Equal("已取消", cancelled.OrderStatus);
            Assert.Empty(cancelled.Refunds);
            var last = await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id);
            Assert.Equal((7, 0), (last.StockQty, last.ReservedQty));
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task LINE_Pay退款_未串接時409並退回原狀態_串接後成功且並行只退一次()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "linepay", ("M", 700, 10));
        var variant = made.Variants[0];
        try
        {
            var orderId = await ShopTest.CreatePendingOrderAsync(fixture, "tcrfc", null, (variant.Id, 1));
            using (var scope = fixture.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ShopOrderLifecycle>().ConfirmPaymentAsync(orderId, "LP-TXN-9", null, CancellationToken.None);
            }

            await C1Test.PostJsonAsync(service, $"{Orders}/{orderId}/ship", new { carrier = "【測試】物流", trackingNo = "T1" });
            await C1Test.PostEmptyAsync(service, $"{Orders}/{orderId}/complete");
            var order = await GetOrderAsync(admin, orderId);
            var refund = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, "/api/v1/admin/tcrfc/shop/refunds", new
            {
                orderId, reason = "【測試】不喜歡", items = new[] { new { orderItemId = order.Items[0].Id, quantity = 1 } }, needsReturn = false,
            }));
            await C1Test.PostJsonAsync(service, $"/api/v1/admin/tcrfc/shop/refunds/{refund.Id}/approve", new { });
            var url = $"/api/v1/admin/tcrfc/shop/refunds/{refund.Id}/execute";

            // 尚未串接：409，案件仍是「已核准」，訂單仍是「退貨處理中」，沒有留下已退款的痕跡
            var notConnected = await C1Test.PostJsonAsync(admin, url, new { });
            Assert.Equal(HttpStatusCode.Conflict, notConnected.StatusCode);
            Assert.Contains("LINE Pay 尚未串接", await C1Test.BodyAsync(notConnected));
            Assert.Equal("已核准", (await RefundAsync(admin, refund.Id)).StatusLabel);
            Assert.Equal("退貨處理中", (await GetOrderAsync(admin, orderId)).OrderStatus);

            // 串接後（測試用假金流）：兩個請求同時執行，只有一個成功、只呼叫金流一次
            FakeLinePay.Calls = 0;
            using var connected = fixture.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<ILinePayGateway>();
                s.AddScoped<ILinePayGateway, FakeLinePay>();
            }));
            using var adminConnected = await BizTest.ClientAsync(connected, "super.admin@tcrfc.test");
            var results = await Task.WhenAll(C1Test.PostJsonAsync(adminConnected, url, new { }), C1Test.PostJsonAsync(adminConnected, url, new { }));
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(1, FakeLinePay.Calls);
            var done = await RefundAsync(admin, refund.Id);
            Assert.Equal("已退款", done.StatusLabel);
            Assert.Equal("原路退回 LINE Pay", done.RefundMethodLabel);
            Assert.Equal("FAKE-REF-" + order.OrderNo, done.RefundReference);
            var final = await GetOrderAsync(admin, orderId);
            Assert.Equal("已退款", final.OrderStatus);
            Assert.Equal("refunded", final.PaymentStatus);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task 退款_已開立發票時同步登記作廢或折讓_未串接發票服務註明人工處理()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "invoice", ("M", 300, 10));
        try
        {
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (made.Variants[0].Id, 2));
            await C1Test.PostJsonAsync(service, $"{Orders}/{order.Id}/ship", new { });
            await C1Test.PostEmptyAsync(service, $"{Orders}/{order.Id}/complete");
            // 發票服務未串接，資料庫裡沒有發票；這裡直接種一筆「已開立」的發票，驗證退款會同步登記
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO store_invoices (id, club_id, order_id, invoice_no, issued_at, issue_status, void_status) SELECT NEWID(), club_id, id, 'AB12345678', SYSUTCDATETIME(), 'issued', 'none' FROM orders WHERE id = @O",
                ("@O", order.Id));
            Assert.Equal("AB12345678", (await GetOrderAsync(admin, order.Id)).Invoice?.InvoiceNo);

            var partial = await BizTest.ReadAsync<AdminRefundDetailDto>(await C1Test.PostJsonAsync(service, "/api/v1/admin/tcrfc/shop/refunds", new
            {
                orderId = order.Id, reason = "【測試】退一件", items = new[] { new { orderItemId = order.Items[0].Id, quantity = 1 } }, needsReturn = false,
            }));
            await C1Test.PostJsonAsync(service, $"/api/v1/admin/tcrfc/shop/refunds/{partial.Id}/approve", new { });
            var result = await BizTest.ReadAsync<AdminRefundExecuteResultDto>(await C1Test.PostJsonAsync(admin, $"/api/v1/admin/tcrfc/shop/refunds/{partial.Id}/execute", new { }));
            Assert.Contains("折讓", result.InvoiceAction);
            Assert.Contains("手動處理", result.InvoiceAction);
            Assert.Equal("allowance", (await GetOrderAsync(admin, order.Id)).Invoice?.VoidStatus);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ 個資遮罩、匯出、清單篩選 ═════════════

    [Fact]
    public async Task 訂單個資_未授權遮罩且搜尋不能探測_匯出兩道關卡_資料範圍不外洩()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "pii", ("M", 250, 10));
        try
        {
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (made.Variants[0].Id, 1));

            // 商務／贊助：看得到訂單但個資遮罩；搜尋收件人姓名探測不到，用訂單編號可以
            var masked = await GetOrderAsync(business, order.Id);
            Assert.True(masked.IsMasked);
            Assert.False(masked.CanReveal);
            Assert.NotEqual("【測試】收件人", masked.RecipientName);
            Assert.Contains('○', masked.RecipientName!);
            Assert.Equal("09******77", masked.RecipientPhone);
            Assert.EndsWith("***", masked.RecipientAddress);
            var byName = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await business.GetAsync($"{Orders}?keyword=" + Uri.EscapeDataString("收件人")));
            Assert.DoesNotContain(byName.Items, o => o.Id == order.Id);
            var byNo = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await business.GetAsync($"{Orders}?keyword={order.OrderNo}"));
            Assert.True(Assert.Single(byNo.Items).IsMasked);
            // 客服／行政：完整值，且可用姓名搜尋
            var byNameFull = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await service.GetAsync($"{Orders}?keyword=" + Uri.EscapeDataString("收件人")));
            Assert.Contains(byNameFull.Items, o => o.Id == order.Id && !o.IsMasked && o.RecipientName == "【測試】收件人");

            // 商務／贊助不能建立訂單、不能處理狀態、不能碰退款；沒有商店權限的內容編輯連清單都不行
            Assert.Equal(HttpStatusCode.Forbidden, (await business.PostAsync(Orders, BizTest.Json(new { items = new[] { new { variantId = made.Variants[0].Id, quantity = 1 } }, deliveryMethod = "onsite_pickup" }))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PostJsonAsync(business, $"{Orders}/{order.Id}/cancel", new { reason = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await business.GetAsync("/api/v1/admin/tcrfc/shop/refunds")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await business.GetAsync("/api/v1/admin/tcrfc/shop/shipments")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync(Orders)).StatusCode);

            // 匯出：需要 shop.order.export（商務 403）＋必填用途
            Assert.Equal(HttpStatusCode.Forbidden, (await business.GetAsync($"{Orders}/export?purpose=x")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{Orders}/export")).StatusCode);
            var export = await service.GetAsync($"{Orders}/export?purpose={Uri.EscapeDataString("測試對帳")}&keyword={order.OrderNo}");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            var csv = Encoding.UTF8.GetString(await export.Content.ReadAsByteArrayAsync());
            Assert.Contains("訂單編號", csv);
            Assert.Contains(order.OrderNo, csv);
            Assert.Contains("0900-000-777", csv); // 匯出含完整收件人資料（先套資料範圍再套受限欄位授權）
            Assert.Contains("現場收款", csv);
            // 資料範圍：磐石的匯出不含藍鯨種子訂單
            var full = Encoding.UTF8.GetString(await (await service.GetAsync($"{Orders}/export?purpose=test")).Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain("BW-SEED-0001", full);
            Assert.Contains("TR-SEED-0001", full);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task 訂單清單_篩選與分頁_藍鯨只看得到自己的訂單_分帳批次標記略過未付款()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "list", ("M", 100, 10));
        try
        {
            var paid = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (made.Variants[0].Id, 1));
            var pendingId = await ShopTest.CreatePendingOrderAsync(fixture, "tcrfc", null, (made.Variants[0].Id, 1));

            // 種子：磐石 8 張、藍鯨 1 張（藍鯨賣、磐石代收）
            var tc = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?pageSize=100"));
            Assert.All(tc.Items, o => Assert.Equal("tcrfc", o.SellingClubCode));
            Assert.DoesNotContain(tc.Items, o => o.OrderNo.StartsWith("BW-"));
            var bw = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await partner.GetAsync("/api/v1/admin/bw/shop/orders"));
            Assert.All(bw.Items, o => Assert.Equal("bw", o.SellingClubCode));
            Assert.Contains(bw.Items, o => o.OrderNo == "BW-SEED-0001");
            Assert.All(bw.Items, o => Assert.False(o.IsMasked)); // 合作球隊管理（自家商品與訂單）持有收件人完整資料權限

            Assert.Contains((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?orderStatus=待付款&pageSize=100"))).Items, o => o.Id == pendingId);
            Assert.Contains((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?paymentStatus=pending&pageSize=100"))).Items, o => o.Id == pendingId);
            Assert.DoesNotContain((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?paymentMethod=linepay&pageSize=100"))).Items, o => o.Id == paid.Id);
            Assert.Contains((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?deliveryMethod=onsite_pickup&isMember=false&pageSize=100"))).Items, o => o.Id == paid.Id);
            var today = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
            Assert.Contains((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?from={today}&to={today}&pageSize=100"))).Items, o => o.Id == paid.Id);
            Assert.Empty((await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?from=2000-01-01&to=2000-01-02"))).Items);
            foreach (var bad in new[] { "paymentStatus=oops", "orderStatus=oops", "deliveryMethod=oops", "settlementStatus=oops", "paymentMethod=oops" })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"{Orders}?{bad}")).StatusCode);
            }

            var page = await BizTest.ReadAsync<PagedResult<AdminOrderListItemDto>>(await admin.GetAsync($"{Orders}?pageSize=2&page=2"));
            Assert.Equal(2, page.Page);
            Assert.True(page.TotalCount >= 10);
            Assert.Equal(2, page.Items.Count);

            // 分帳批次標記：待付款的訂單略過並說明原因；已付款的成功
            var batch = await BizTest.ReadAsync<BatchOperationResultDto>(await C1Test.PostJsonAsync(service, $"{Orders}/batch/settlement", new { ids = new[] { paid.Id, pendingId, Guid.NewGuid() }, status = "settled", note = "【測試】批次" }));
            Assert.Equal(1, batch.UpdatedCount);
            Assert.Equal(2, batch.Skipped.Count);
            Assert.Equal("已結算", (await GetOrderAsync(admin, paid.Id)).SettlementStatusLabel);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{Orders}/batch/settlement", new { ids = Array.Empty<Guid>(), status = "settled" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(service, $"{Orders}/{pendingId}/settlement", new { status = "settled" })).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ S4 出貨與物流 ═════════════

    [Fact]
    public async Task 出貨作業_揀貨單_出貨單遮罩_超商取貨到店通知與逾期_自取_批次出貨_物流單號CSV回填()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "ship", ("M", 120, 20), ("L", 120, 20));
        var m = made.Variants[0];
        var l = made.Variants[1];
        var shipments = "/api/v1/admin/tcrfc/shop/shipments";
        try
        {
            var o1 = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (m.Id, 2), (l.Id, 1));
            var o2 = await ShopTest.CreateOrderAsync(service, "tcrfc", "home_delivery", (m.Id, 3));
            var cvs = await BizTest.ReadAsync<AdminOrderDetailDto>(await service.PostAsync(Orders, BizTest.Json(new
            {
                items = new[] { new { variantId = l.Id, quantity = 1 } }, deliveryMethod = "cvs_pickup", recipientName = "【測試】超商客", recipientPhone = "0900-000-888",
            })));
            var pickup = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (m.Id, 1));

            // 揀貨單：依 SKU 加總（m：2+3+1=6、l：1+1=2），可指定訂單
            var picking = await BizTest.ReadAsync<AdminPickingListDto>(await service.GetAsync($"{shipments}/picking-list?orderIds={o1.Id}&orderIds={o2.Id}&orderIds={cvs.Id}&orderIds={pickup.Id}"));
            Assert.Equal(4, picking.OrderCount);
            var lineM = picking.Lines.Single(x => x.Sku == m.Sku);
            Assert.Equal((6, 3), (lineM.Quantity, lineM.OrderCount));
            Assert.Equal(2, picking.Lines.Single(x => x.Sku == l.Sku).Quantity);
            Assert.Equal(8, picking.TotalQuantity);

            // 出貨單：客服看得到完整；合作球隊管理（沒有 reveal）拿到遮罩——對自家俱樂部才有效，這裡用商務證明「沒有 shipment 權限」
            var slips = await BizTest.ReadAsync<List<AdminDispatchSlipDto>>(await service.GetAsync($"{shipments}/dispatch-slips?orderIds={o1.Id}&orderIds={cvs.Id}"));
            Assert.Equal(2, slips.Count);
            Assert.Equal("【測試】收件人", slips[0].RecipientName);
            Assert.Equal(2, slips[0].Items.Count);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{shipments}/dispatch-slips")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync($"{shipments}/picking-list")).StatusCode);

            // 超商取貨：出貨必須有門市代碼 → 到店通知（期限已過）→ 逾期 → 領取
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{Orders}/{cvs.Id}/ship", new { carrier = "【測試】超商" })).StatusCode);
            var cvsShipped = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{cvs.Id}/ship", new { carrier = "【測試】超商", storeBranchCode = "S001", trackingNo = "CVS-1" }));
            Assert.Equal("S001", cvsShipped.Shipment?.StoreBranchCode);
            Assert.Null(cvsShipped.Shipment?.PickupStatus);
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8).AddDays(-1)).ToString("yyyy-MM-dd");
            var notified = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{cvs.Id}/arrival-notified", new { pickupDeadlineOn = yesterday }));
            Assert.Equal("overdue", notified.Shipment?.PickupStatus);
            Assert.Equal("逾期", notified.Shipment?.PickupStatusLabel);
            Assert.NotNull(notified.Shipment?.ArrivalNotifiedAt);
            var overdue = await BizTest.ReadAsync<PagedResult<AdminShipmentListItemDto>>(await service.GetAsync($"{shipments}?pickupStatus=overdue&pageSize=100"));
            Assert.Contains(overdue.Items, s => s.OrderId == cvs.Id);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{Orders}/{o1.Id}/arrival-notified", new { })).StatusCode); // 宅配沒有到店通知
            var picked = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostEmptyAsync(service, $"{Orders}/{cvs.Id}/complete"));
            Assert.Equal("已領取", picked.ShipmentStatusLabel);
            Assert.Equal("picked_up", picked.Shipment?.PickupStatus);
            Assert.DoesNotContain((await BizTest.ReadAsync<PagedResult<AdminShipmentListItemDto>>(await service.GetAsync($"{shipments}?pickupStatus=overdue&pageSize=100"))).Items, s => s.OrderId == cvs.Id);

            // 現場自取：出貨＝備妥待領（待領取）
            var ready = await BizTest.ReadAsync<AdminOrderDetailDto>(await C1Test.PostJsonAsync(service, $"{Orders}/{pickup.Id}/ship", new { pickupDeadlineOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8).AddDays(5)).ToString("yyyy-MM-dd") }));
            Assert.Equal("waiting", ready.Shipment?.PickupStatus);
            Assert.Equal("待領取", ready.Shipment?.PickupStatusLabel);

            // 批次標記出貨：不存在的 id 與已出貨的訂單略過
            var batch = await BizTest.ReadAsync<BatchOperationResultDto>(await C1Test.PostJsonAsync(service, $"{shipments}/batch/ship", new { ids = new[] { o1.Id, o2.Id, cvs.Id, Guid.NewGuid() }, carrier = "【測試】批次物流" }));
            Assert.Equal(2, batch.UpdatedCount);
            Assert.Equal(2, batch.Skipped.Count);
            Assert.Equal("已出貨", (await GetOrderAsync(admin, o1.Id)).OrderStatus);

            // CSV 回填物流單號：已出貨的更新單號；已完成的更新；未知訂單編號與缺欄位略過
            var csv = $"訂單編號,物流商,物流單號\r\n{o1.OrderNo},【測試】黑貓,TRK-A\r\n{o2.OrderNo},【測試】黑貓,TRK-B\r\n{cvs.OrderNo},【測試】超商,CVS-2\r\nNOPE-000,x,y\r\n,x,y\r\n";
            using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "tracking.csv" } };
            var imported = await BizTest.ReadAsync<AdminShipmentImportResultDto>(await service.PostAsync($"{shipments}/import", form));
            Assert.Equal(3, imported.UpdatedCount);
            Assert.Equal(2, imported.Skipped.Count);
            Assert.Contains(imported.Skipped, s => s.OrderNo == "NOPE-000");
            Assert.Equal("TRK-A", (await GetOrderAsync(admin, o1.Id)).Shipment?.TrackingNo);
            Assert.Equal("CVS-2", (await GetOrderAsync(admin, cvs.Id)).Shipment?.TrackingNo);
            using var badForm = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("a,b\r\n1,2\r\n")), "file", "bad.csv" } };
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync($"{shipments}/import", badForm)).StatusCode);

            // 出貨清單與篩選
            var list = await BizTest.ReadAsync<PagedResult<AdminShipmentListItemDto>>(await service.GetAsync($"{shipments}?orderStatus=已出貨&keyword=TRK-A"));
            Assert.Contains(list.Items, s => s.OrderId == o1.Id);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{shipments}?orderStatus=已完成")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{shipments}?pickupStatus=oops")).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }
}
