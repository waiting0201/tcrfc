using System.Net;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S1 商品與規格、S2 庫存管理（S3-3）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminShopCatalogTests(AdminWriteApiFixture fixture)
{
    private static object ProductPayload(string slug, string status = "draft", string name = "【測試】商品", object? extra = null) => new
    {
        slug, status, isNewArrival = true,
        content = new { zh = new { name, narrative = "敘事", seoTitle = "SEO 標題", tags = "測試,示範" }, en = new { name = "Test product" } },
    };

    // ═════════════ 權限 ═════════════

    [Fact]
    public async Task 商店_權限矩陣_檢視者看不到規格與售價_內容編輯不能刪除_成本只有系統管理員與商務_合作球隊沒有成本權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "perm", ("M", 1000, 5));
        try
        {
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/shop/products")).StatusCode);

            // 檢視者：看得到商品與系列，但看不到規格與售價
            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            var list = await BizTest.ReadAsync<PagedResult<AdminProductListItemDto>>(await viewer.GetAsync("/api/v1/admin/tcrfc/shop/products?keyword=" + made.Slug));
            var item = Assert.Single(list.Items);
            Assert.Null(item.PriceMin);
            Assert.Null(item.AvailableTotal);
            var detail = await BizTest.ReadAsync<AdminProductDetailDto>(await viewer.GetAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}"));
            Assert.False(detail.CanViewVariants);
            Assert.Empty(detail.Variants);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/tcrfc/shop/inventory")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/tcrfc/shop/orders")).StatusCode);

            // 內容編輯（S1 文案／圖）：可建立與編輯商品、看得到規格（含售價），但不能新增規格、不能刪商品、看不到成本
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            var editorDetail = await BizTest.ReadAsync<AdminProductDetailDto>(await editor.GetAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}"));
            Assert.True(editorDetail.CanViewVariants);
            Assert.False(editorDetail.CanViewCost);
            Assert.All(editorDetail.Variants, v => Assert.Null(v.Cost));
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants",
                BizTest.Json(new { sku = "X-NOPE", price = 1 }))).StatusCode);

            // 商務／贊助：S1 全權、有成本權限（docs/12b §7）——看得到、改得動、可清除
            using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
            var sku = made.Variants[0].Sku;
            var bizVariant = await ShopTest.VariantAsync(business, "tcrfc", made.ProductId, made.Variants[0].Id);
            Assert.Equal(500, bizVariant.Cost);
            Assert.Equal(700, (await BizTest.ReadAsync<AdminVariantDto>(await business.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{made.Variants[0].Id}",
                BizTest.Json(new { sku, price = 1000, cost = 700 })))).Cost);
            // 沒有帶成本欄位的更新：成本維持不變
            Assert.Equal(HttpStatusCode.OK, (await business.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{made.Variants[0].Id}",
                BizTest.Json(new { sku, price = 1100 }))).StatusCode);
            Assert.Equal(700, (await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, made.Variants[0].Id)).Cost);
            var cleared = await BizTest.ReadAsync<AdminVariantDto>(await business.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{made.Variants[0].Id}",
                BizTest.Json(new { sku, price = 1100, clearCost = true })));
            Assert.Null(cleared.Cost);

            // 合作球隊管理（僅藍鯨）：打磐石 403、打藍鯨 200
            using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/tcrfc/shop/products")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await partner.GetAsync("/api/v1/admin/bw/shop/products")).StatusCode);
            // 合作球隊管理有規格權限、沒有成本權限：看不到成本、帶成本 403、沒帶成本可正常建立與改價
            var bwMade = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PostJsonAsync(partner, "/api/v1/admin/bw/shop/products", ProductPayload(BizTest.Unique("bwc"))));
            try
            {
                var bwVariantsUrl = $"/api/v1/admin/bw/shop/products/{bwMade.Id}/variants";
                var withCost = await C1Test.PostJsonAsync(partner, bwVariantsUrl, new { sku = "T-BWC-" + Guid.NewGuid().ToString("N")[..6], price = 100, cost = 10 });
                Assert.Equal(HttpStatusCode.Forbidden, withCost.StatusCode);
                var bwVariant = await BizTest.ReadAsync<AdminVariantDto>(await C1Test.PostJsonAsync(partner, bwVariantsUrl, new { sku = "T-BWC-" + Guid.NewGuid().ToString("N")[..6], price = 100 }));
                Assert.Null(bwVariant.Cost);
                Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PutJsonAsync(partner, $"{bwVariantsUrl}/{bwVariant.Id}", new { sku = bwVariant.Sku, price = 100, clearCost = true })).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await C1Test.PutJsonAsync(partner, $"{bwVariantsUrl}/{bwVariant.Id}", new { sku = bwVariant.Sku, price = 120 })).StatusCode);
            }
            finally
            {
                await ShopTest.CleanupAsync(bwMade.Id);
            }

            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/bw/shop/products/{made.ProductId}")).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ═════════════ S1 ═════════════

    [Fact]
    public async Task 商品系列_建立更新排序_有商品不能刪_網址名稱重複()
    {
        using var admin = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var slug1 = BizTest.Unique("col");
        var slug2 = BizTest.Unique("col");
        var ids = new List<Guid>();
        Guid productId = Guid.Empty;
        try
        {
            var c1 = await BizTest.ReadAsync<AdminCollectionDetailDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/collections", new
            {
                slug = slug1, status = "published", content = new { zh = new { name = "【測試】系列一", narrative = "品牌敘事" }, en = new { name = "Test one" } },
            }));
            ids.Add(c1.Id);
            var c2 = await BizTest.ReadAsync<AdminCollectionDetailDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/collections", new
            {
                slug = slug2, status = "draft", content = new { zh = new { name = "【測試】系列二" } },
            }));
            ids.Add(c2.Id);
            Assert.Equal("品牌敘事", c1.Zh.Narrative);
            Assert.Equal("草稿", c2.StatusLabel);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/collections", new
            {
                slug = slug1, status = "draft", content = new { zh = new { name = "重複" } },
            })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/collections", new
            {
                slug = "Bad Slug", status = "draft", content = new { zh = new { name = "x" } },
            })).StatusCode);

            // 排序
            Assert.Equal(HttpStatusCode.NoContent, (await C1Test.PutJsonAsync(admin, "/api/v1/admin/tcrfc/shop/collections/order", new { ids = new[] { c2.Id, c1.Id } })).StatusCode);
            var list = await BizTest.ReadAsync<List<AdminCollectionListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/collections"));
            Assert.True(list.FindIndex(c => c.Id == c2.Id) < list.FindIndex(c => c.Id == c1.Id));

            // 商品掛系列 → 系列有商品不能刪
            var product = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", new
            {
                slug = BizTest.Unique("p"), collectionId = c1.Id, status = "draft", content = new { zh = new { name = "【測試】掛系列商品" } },
            }));
            productId = product.Id;
            Assert.Equal(1, (await BizTest.ReadAsync<AdminCollectionDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/collections/{c1.Id}"))).ProductCount);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/collections/{c1.Id}")).StatusCode);
            // 找不到系列 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", new
            {
                slug = BizTest.Unique("p"), collectionId = Guid.NewGuid(), status = "draft", content = new { zh = new { name = "x" } },
            })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/collections/{c2.Id}")).StatusCode);
            ids.Remove(c2.Id);
        }
        finally
        {
            if (productId != Guid.Empty)
            {
                await ShopTest.CleanupAsync(productId);
            }

            foreach (var id in ids)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/collections/{id}");
            }
        }
    }

    [Fact]
    public async Task 商品與規格_生命週期_貨號全站唯一_價格驗證_上架規則_缺貨自動判定_刪除規則()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var slug = BizTest.Unique("prod");
        Guid productId = Guid.Empty;
        try
        {
            // 新商品不能直接上架（還沒有規格）
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", ProductPayload(slug, "published"))).StatusCode);
            var created = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", new
            {
                slug, status = "draft", isNewArrival = true, outOfStockBehavior = "hide",
                sizeChart = new { columns = new[] { "尺寸", "胸圍" }, rows = new[] { new[] { "M", "96" } } },
                content = new { zh = new { name = "【測試】商品", narrative = "敘事", seoTitle = "SEO 標題", seoDescription = "描述", tags = "新款,主場" }, en = new { name = "Test product" } },
            }));
            productId = created.Id;
            Assert.Equal("下架（草稿）", created.DisplayStatusLabel);
            Assert.Equal("自動隱藏", created.OutOfStockBehaviorLabel);
            Assert.Equal("新款,主場", created.Zh.Tags);
            Assert.Equal(JsonValueKind.Object, created.SizeChart?.ValueKind);
            Assert.Equal("Test product", created.En?.Name);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", ProductPayload(slug))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products", new
            {
                slug = BizTest.Unique("p"), status = "draft", outOfStockBehavior = "oops", content = new { zh = new { name = "x" } },
            })).StatusCode);

            var url = $"/api/v1/admin/tcrfc/shop/products/{productId}/variants";
            var sku = "T-LIFE-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            // 上架前必須有販售中規格
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, $"/api/v1/admin/tcrfc/shop/products/{productId}", ProductPayload(slug, "published"))).StatusCode);

            // 規格驗證：促銷價不可高於售價、負數、空白貨號
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, url, new { sku, price = 100, salePrice = 200 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, url, new { sku, price = -1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(admin, url, new { sku = "a b", price = 1 })).StatusCode);
            var variant = await BizTest.ReadAsync<AdminVariantDto>(await C1Test.PostJsonAsync(admin, url, new
            {
                sku, size = "M", colour = "藍", price = 1000, salePrice = 900, cost = 400, initialStock = 8, lowStockThreshold = 2,
            }));
            Assert.Equal(900, variant.EffectivePrice);
            Assert.Equal("M／藍", variant.Label);
            Assert.Equal(8, variant.StockQty);
            Assert.Equal(8, variant.AvailableQty);
            Assert.False(variant.IsLowStock);
            // 貨號全站唯一（跨俱樂部也一樣）
            var bwProduct = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/bw/shop/products", ProductPayload(BizTest.Unique("bwp"))));
            try
            {
                var sameClub = await C1Test.PostJsonAsync(admin, url, new { sku, price = 1 });
                Assert.Equal(HttpStatusCode.Conflict, sameClub.StatusCode);
                Assert.Contains("這個俱樂部的另一個規格", await sameClub.Content.ReadAsStringAsync());
                var crossClub = await C1Test.PostJsonAsync(admin, $"/api/v1/admin/bw/shop/products/{bwProduct.Id}/variants", new { sku, price = 1 });
                Assert.Equal(HttpStatusCode.Conflict, crossClub.StatusCode);
                // 🔴 撞到「別的俱樂部」的貨號：訊息不得透露它已被使用、更不得說在全站或哪一隊（合作球隊帳號會拿貨號探測對方的商品）
                var crossText = await crossClub.Content.ReadAsStringAsync();
                Assert.Contains("無法使用", crossText);
                Assert.DoesNotContain("已經被", crossText);
                Assert.DoesNotContain("全站", crossText);
                Assert.DoesNotContain("俱樂部", crossText);
            }
            finally
            {
                await ShopTest.CleanupAsync(bwProduct.Id);
            }

            // 初始庫存記成一筆進貨異動
            var movements = await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/inventory/movements?variantId={variant.Id}"));
            var initial = Assert.Single(movements.Items);
            Assert.Equal("stock_in", initial.MovementType);
            Assert.Equal(8, initial.Quantity);
            Assert.Equal(8, initial.StockAfter);

            // 上架 → 已上架；庫存全部扣完 → 缺貨（自動判定）
            var published = await BizTest.ReadAsync<AdminProductDetailDto>(await C1Test.PutJsonAsync(admin, $"/api/v1/admin/tcrfc/shop/products/{productId}", new
            {
                slug, status = "published", outOfStockBehavior = "hide", content = new { zh = new { name = "【測試】商品" } },
            }));
            Assert.Equal("published", published.DisplayStatus);
            Assert.Null(published.SizeChart); // PUT 整份取代：省略尺碼表＝清除
            await BizTest.ReadAsync<AdminInventoryMovementResultDto>(await C1Test.PostJsonAsync(admin, "/api/v1/admin/tcrfc/shop/inventory/movements", new { variantId = variant.Id, type = "damage", quantity = 8, reason = "測試報損" }));
            var soldOut = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/products/{productId}"));
            Assert.Equal("sold_out", soldOut.DisplayStatus);
            Assert.Equal("缺貨", soldOut.DisplayStatusLabel);
            var listItem = (await BizTest.ReadAsync<PagedResult<AdminProductListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/products?status=published&keyword=" + slug))).Items.Single();
            Assert.Equal("sold_out", listItem.DisplayStatus);
            Assert.Equal(0, listItem.AvailableTotal);
            Assert.Equal(900, listItem.PriceMin);

            // 停售的規格不參與缺貨判定；規格排序
            var second = await BizTest.ReadAsync<AdminVariantDto>(await C1Test.PostJsonAsync(admin, url, new { sku = sku + "-B", size = "L", price = 1000, initialStock = 3 }));
            Assert.Equal("published", (await BizTest.ReadAsync<AdminProductDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/products/{productId}"))).DisplayStatus);
            Assert.Equal(HttpStatusCode.NoContent, (await C1Test.PutJsonAsync(admin, url + "/order", new { ids = new[] { second.Id, variant.Id } })).StatusCode);
            var reordered = await BizTest.ReadAsync<List<AdminVariantDto>>(await admin.GetAsync(url));
            Assert.Equal(second.Id, reordered[0].Id);
            var inactive = await BizTest.ReadAsync<AdminVariantDto>(await C1Test.PutJsonAsync(admin, $"{url}/{second.Id}", new { sku = second.Sku, size = "L", price = 1000, status = "inactive" }));
            Assert.Equal("停售", inactive.StatusLabel);
            Assert.Equal("sold_out", (await BizTest.ReadAsync<AdminProductDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/products/{productId}"))).DisplayStatus);

            // 沒有訂單的規格可以刪；商品有草稿以外的狀態切回下架
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{url}/{second.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{url}/{second.Id}")).StatusCode);
            Assert.Equal(0, await ShopTest.CountAsync("SELECT COUNT(*) FROM inventory_movements WHERE product_variant_id = @V", ("@V", second.Id)));
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{productId}")).StatusCode);
            productId = Guid.Empty;
        }
        finally
        {
            if (productId != Guid.Empty)
            {
                await ShopTest.CleanupAsync(productId);
            }
        }
    }

    [Fact]
    public async Task 商品_有訂單的規格與商品不能刪除_商品排序()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var a = await ShopTest.CreateProductAsync(admin, "tcrfc", "ordA", ("M", 500, 5));
        var b = await ShopTest.CreateProductAsync(admin, "tcrfc", "ordB", ("M", 500, 5));
        try
        {
            await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (a.Variants[0].Id, 1));
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{a.ProductId}/variants/{a.Variants[0].Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/shop/products/{a.ProductId}")).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await C1Test.PutJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products/order", new { ids = new[] { b.ProductId, a.ProductId } })).StatusCode);
            var list = await BizTest.ReadAsync<PagedResult<AdminProductListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/shop/products?pageSize=100"));
            Assert.True(list.Items.ToList().FindIndex(p => p.Id == b.ProductId) < list.Items.ToList().FindIndex(p => p.Id == a.ProductId));
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(admin, "/api/v1/admin/tcrfc/shop/products/order", new { ids = new[] { Guid.NewGuid() } })).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(a.ProductId, b.ProductId);
        }
    }

    // ═════════════ S2 ═════════════

    [Fact]
    public async Task 庫存_進貨盤點報損調整_原因規則_低庫存提醒_異動可追溯()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "inv", ("M", 300, 10));
        var variant = made.Variants[0];
        var url = "/api/v1/admin/tcrfc/shop/inventory/movements";
        try
        {
            Task<HttpResponseMessage> Move(string type, int quantity, string? reason = null)
                => service.PostAsync(url, BizTest.Json(new { variantId = variant.Id, type, quantity, reason }));

            var stockIn = await BizTest.ReadAsync<AdminInventoryMovementResultDto>(await Move("stock_in", 5));
            Assert.Equal(15, stockIn.Item.StockQty);
            Assert.Equal("進貨", stockIn.Movement.MovementTypeLabel);
            Assert.Equal(5, stockIn.Movement.Quantity);
            Assert.Equal(15, stockIn.Movement.StockAfter);
            Assert.NotNull(stockIn.Movement.HandledByName);

            // 報損與調整必填原因；數量規則
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("damage", 1)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("adjust", 1)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("stock_in", 0)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("adjust", 0, "x")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("stocktake", -1)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Move("sale", 1, "x")).StatusCode);

            var damage = await BizTest.ReadAsync<AdminInventoryMovementResultDto>(await Move("damage", 3, "測試報損"));
            Assert.Equal(12, damage.Item.StockQty);
            Assert.Equal(-3, damage.Movement.Quantity);
            var adjust = await BizTest.ReadAsync<AdminInventoryMovementResultDto>(await Move("adjust", -2, "測試盤差"));
            Assert.Equal(10, adjust.Item.StockQty);

            // 盤點：以實際盤點數為準，異動量是差額
            var count = await BizTest.ReadAsync<AdminInventoryMovementResultDto>(await Move("stocktake", 7));
            Assert.Equal(7, count.Item.StockQty);
            Assert.Equal(-3, count.Movement.Quantity);
            Assert.Equal("盤點", count.Movement.Reason);

            // 扣到負數 → 409 庫存不足，庫存不變
            var over = await Move("damage", 8, "超量");
            Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
            Assert.Contains("可售量不足", await C1Test.BodyAsync(over));

            // 低庫存：門檻預設 5（tcrfc 種子值），可售量 7 → 不算；調到 4 → 算
            await Move("stocktake", 4);
            var low = await BizTest.ReadAsync<PagedResult<AdminInventoryItemDto>>(await service.GetAsync($"/api/v1/admin/tcrfc/shop/inventory?lowStockOnly=true&keyword={variant.Sku}"));
            var lowItem = Assert.Single(low.Items);
            Assert.True(lowItem.IsLowStock);
            Assert.Equal(5, lowItem.LowStockThreshold);
            // 規格自己的門檻覆寫：門檻 3 → 可售量 4 不算低庫存
            await admin.PutAsync($"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/variants/{variant.Id}", BizTest.Json(new { sku = variant.Sku, size = "M", price = 300, lowStockThreshold = 3 }));
            Assert.Empty((await BizTest.ReadAsync<PagedResult<AdminInventoryItemDto>>(await service.GetAsync($"/api/v1/admin/tcrfc/shop/inventory?lowStockOnly=true&keyword={variant.Sku}"))).Items);

            // 異動紀錄：依規格、類型、期間篩選；跨俱樂部規格 → 400
            var all = await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await service.GetAsync($"{url}?variantId={variant.Id}"));
            Assert.Equal(6, all.TotalCount); // 初始進貨＋進貨＋報損＋調整＋兩次盤點（超量失敗的不留紀錄）
            Assert.Equal("stocktake", all.Items[0].MovementType);
            Assert.Equal(2, (await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await service.GetAsync($"{url}?variantId={variant.Id}&type=stocktake"))).TotalCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{url}?type=oops")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/bw/shop/inventory/movements", BizTest.Json(new { variantId = variant.Id, type = "stock_in", quantity = 1 }))).StatusCode);
            var today = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd");
            Assert.Equal(6, (await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await service.GetAsync($"{url}?variantId={variant.Id}&from={today}&to={today}"))).TotalCount);

            // 商務／贊助只能看庫存，不能異動
            using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
            Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/admin/tcrfc/shop/inventory")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await business.PostAsync(url, BizTest.Json(new { variantId = variant.Id, type = "stock_in", quantity = 1 }))).StatusCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Fact]
    public async Task 庫存_並行扣減不會超賣_十個同時報損只有庫存量那麼多個成功()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "race", ("M", 100, 5));
        var variant = made.Variants[0];
        try
        {
            var tasks = Enumerable.Range(0, 10).Select(i => admin.PostAsync("/api/v1/admin/tcrfc/shop/inventory/movements",
                BizTest.Json(new { variantId = variant.Id, type = "damage", quantity = 1, reason = $"並行測試 {i}" }))).ToList();
            var results = await Task.WhenAll(tasks);
            Assert.Equal(5, results.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(5, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            var final = await ShopTest.VariantAsync(admin, "tcrfc", made.ProductId, variant.Id);
            Assert.Equal(0, final.StockQty);
            // 每一次成功的扣減都有一筆異動，異動後水位遞減不重複
            var afters = (await BizTest.ReadAsync<PagedResult<AdminInventoryMovementDto>>(await admin.GetAsync($"/api/v1/admin/tcrfc/shop/inventory/movements?variantId={variant.Id}&type=damage")))
                .Items.Select(m => m.StockAfter).OrderBy(x => x).ToArray();
            Assert.Equal(new int?[] { 0, 1, 2, 3, 4 }, afters);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }
}
