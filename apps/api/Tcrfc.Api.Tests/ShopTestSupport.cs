using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminShop;

namespace Tcrfc.Api.Tests;

/// <summary>站內商店（S1–S6）測試共用小工具：建商品與規格、建現場收款訂單、清除測試資料（外鍵順序：退款→出貨→異動→品項→訂單→規格→商品）。</summary>
internal static class ShopTest
{
    public sealed record Made(Guid ProductId, string Slug, IReadOnlyList<AdminVariantDto> Variants);

    /// <summary>用系統管理員建立一件上架商品與 N 個規格（各含初始庫存）。SKU 以 <paramref name="tag"/> 加隨機字串避免撞名。</summary>
    public static async Task<Made> CreateProductAsync(HttpClient admin, string club, string tag, params (string Size, int Price, int Stock)[] variants)
    {
        var slug = BizTest.Unique("t-" + tag.ToLowerInvariant());
        var created = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.PostAsync($"/api/v1/admin/{club}/shop/products", BizTest.Json(new
        {
            slug, status = "draft", content = new { zh = new { name = $"【測試】{tag}" }, en = new { name = $"Test {tag}" } },
        })));
        var made = new List<AdminVariantDto>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        foreach (var (size, price, stock) in variants)
        {
            made.Add(await BizTest.ReadAsync<AdminVariantDto>(await admin.PostAsync($"/api/v1/admin/{club}/shop/products/{created.Id}/variants", BizTest.Json(new
            {
                sku = $"T-{tag.ToUpperInvariant()}-{size}-{suffix}", size, price, cost = price / 2, initialStock = stock,
            }))));
        }

        var published = await admin.PutAsync($"/api/v1/admin/{club}/shop/products/{created.Id}", BizTest.Json(new
        {
            slug, status = "published", content = new { zh = new { name = $"【測試】{tag}" }, en = new { name = $"Test {tag}" } },
        }));
        if (!published.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("上架測試商品失敗：" + await published.Content.ReadAsStringAsync());
        }

        return new Made(created.Id, slug, made);
    }

    public static async Task<AdminOrderDetailDto> CreateOrderAsync(HttpClient client, string club, string delivery, params (Guid VariantId, int Quantity)[] lines)
    {
        var response = await client.PostAsync($"/api/v1/admin/{club}/shop/orders", BizTest.Json(new
        {
            items = lines.Select(l => new { variantId = l.VariantId, quantity = l.Quantity }).ToArray(),
            deliveryMethod = delivery,
            recipientName = "【測試】收件人", recipientPhone = "0900-000-777", recipientAddress = "【測試】台中市西屯區測試路 99 號",
        }));
        return await BizTest.ReadAsync<AdminOrderDetailDto>(response);
    }

    /// <summary>直接在測試庫建一張「待付款」的 LINE Pay 訂單並保留庫存（模擬前台結帳，前台結帳尚未開發）。回傳訂單 id。</summary>
    public static async Task<Guid> CreatePendingOrderAsync(
        WebApplicationFactory<Program> factory, string clubCode, DateTime? createdAtUtc, params (Guid VariantId, int Quantity)[] lines)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Code == clubCode);
        var collecting = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder).Select(c => c.Id).FirstAsync();
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.Product).Where(v => lines.Select(l => l.VariantId).Contains(v.Id)).ToListAsync();
        var orderId = Guid.NewGuid();
        var now = createdAtUtc ?? DateTime.UtcNow;
        var items = lines.Select(l =>
        {
            var v = variants.First(x => x.Id == l.VariantId);
            var price = v.SalePrice ?? v.Price;
            return new OrderItem
            {
                Id = Guid.NewGuid(), ClubId = club.Id, OrderId = orderId, ProductVariantId = v.Id, ProductNameSnapshot = "【測試】待付款商品", SkuSnapshot = v.Sku,
                UnitPriceSnapshot = price, Quantity = l.Quantity, LineTotal = price * l.Quantity, CreatedAt = now, UpdatedAt = now,
            };
        }).ToList();
        var subtotal = items.Sum(i => i.LineTotal);
        var order = new Order
        {
            Id = orderId, OrderNo = RegistrationNumberGenerator.Generate("TR", now), ClubId = club.Id, SellingClubId = club.Id, CollectingClubId = collecting,
            LookupToken = SecureToken.Generate(), RecipientName = "【測試】待付款收件人", RecipientPhone = "0900-000-555", RecipientAddress = "【測試】台中市測試路 5 號",
            Subtotal = subtotal, ShippingFee = 0, Total = subtotal, PaymentStatus = "pending", PaymentMethod = "linepay", OrderStatus = "待付款",
            DeliveryMethod = "home_delivery", SettlementStatus = "pending", CreatedAt = now, UpdatedAt = now,
        };
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Orders.Add(order);
        db.OrderItems.AddRange(items);
        await db.SaveChangesAsync();
        foreach (var item in items)
        {
            await inventory.ApplyAsync(new InventoryChange(club.Id, item.ProductVariantId, "reserve", 0, item.Quantity, "測試下單保留", orderId, null), CancellationToken.None);
        }

        await tx.CommitAsync();
        return orderId;
    }

    public static async Task<AdminVariantDto> VariantAsync(HttpClient admin, string club, Guid productId, Guid variantId)
        => await BizTest.ReadAsync<AdminVariantDto>(await admin.GetAsync($"/api/v1/admin/{club}/shop/products/{productId}/variants/{variantId}"));

    /// <summary>清掉一批測試商品與其訂單、出貨、退款、庫存異動（測試資料一律在 finally 清除）。</summary>
    public static async Task CleanupAsync(params Guid[] productIds)
    {
        foreach (var productId in productIds)
        {
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @v TABLE (id uniqueidentifier);
                INSERT INTO @v SELECT id FROM product_variants WHERE product_id = @P;
                DECLARE @o TABLE (id uniqueidentifier);
                INSERT INTO @o SELECT DISTINCT order_id FROM order_items WHERE product_variant_id IN (SELECT id FROM @v);
                DELETE FROM refund_request_items WHERE refund_request_id IN (SELECT id FROM refund_requests WHERE order_id IN (SELECT id FROM @o));
                DELETE FROM refund_requests WHERE order_id IN (SELECT id FROM @o);
                DELETE FROM store_invoices WHERE order_id IN (SELECT id FROM @o);
                DELETE FROM shipments WHERE order_id IN (SELECT id FROM @o);
                DELETE FROM inventory_movements WHERE order_id IN (SELECT id FROM @o) OR product_variant_id IN (SELECT id FROM @v);
                DELETE FROM order_items WHERE order_id IN (SELECT id FROM @o);
                DELETE FROM orders WHERE id IN (SELECT id FROM @o);
                DELETE FROM cart_items WHERE product_variant_id IN (SELECT id FROM @v);
                DELETE FROM product_variants WHERE product_id = @P;
                DELETE FROM product_images WHERE product_id = @P;
                DELETE FROM products WHERE id = @P;
                """, ("@P", productId));
        }
    }

    public static async Task<int> CountAsync(string sql, params (string Name, object? Value)[] parameters)
        => await C1Test.ScalarAsync<int>(sql, parameters);
}
