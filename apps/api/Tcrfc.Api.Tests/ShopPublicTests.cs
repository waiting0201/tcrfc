using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.Shop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// F 批（S3-5）站內商店前台公開端點：目錄、購物車、結帳、付款、訂單查詢，以及庫存與冪等的<b>並行</b>驗證（E-97 的教訓：冪等與防超賣必須用真正的並行請求測，不能只靠循序呼叫）。
/// 金流與發票走本機假實作（Development 註冊）；測試資料一律在 finally 清除（商品、訂單、購物車、字軌通道），不動種子。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ShopPublicTests(AdminWriteApiFixture fixture)
{
    private const string Club = "tcrfc";

    // ═══════════════════════════ 小工具 ═══════════════════════════

    private sealed class Scope : IAsyncDisposable
    {
        public List<Guid> Products { get; } = [];
        public List<string> CartHashes { get; } = [];
        public List<Guid> Members { get; } = [];
        public Func<Task>? RestoreChannel { get; set; }

        public async ValueTask DisposeAsync()
        {
            await ShopTest.CleanupAsync([.. Products]);
            foreach (var hash in CartHashes)
            {
                await BizTest.ExecuteSqlAsync(
                    "DELETE FROM cart_items WHERE cart_id IN (SELECT id FROM carts WHERE anonymous_token = @H); DELETE FROM carts WHERE anonymous_token = @H;", ("@H", hash));
            }

            foreach (var member in Members)
            {
                await BizTest.ExecuteSqlAsync(
                    """
                    DELETE FROM cart_items WHERE cart_id IN (SELECT id FROM carts WHERE member_id = @M);
                    DELETE FROM carts WHERE member_id = @M;
                    DECLARE @o TABLE (id uniqueidentifier);
                    INSERT INTO @o SELECT id FROM orders WHERE member_id = @M;
                    DELETE FROM store_invoices WHERE order_id IN (SELECT id FROM @o);
                    DELETE FROM inventory_movements WHERE order_id IN (SELECT id FROM @o);
                    DELETE FROM order_items WHERE order_id IN (SELECT id FROM @o);
                    DELETE FROM orders WHERE id IN (SELECT id FROM @o);
                    """, ("@M", member));
            }

            if (RestoreChannel is not null)
            {
                await RestoreChannel();
            }
        }
    }

    private static string NewEmail() => $"shop-{Guid.NewGuid():N}@example.test";

    private HttpClient Client() => fixture.CreateClient();

    private static async Task<(HttpStatusCode Status, JsonElement Json)> SendAsync(
        HttpClient client, HttpMethod method, string url, object? body = null, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = BizTest.Json(body);
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static string Url(string path, string club = Club) => $"/api/v1/{club}/shop/{path}";

    private async Task<string> AddToCartAsync(Scope scope, Guid variantId, int quantity, string? token = null, string club = Club)
    {
        var (status, json) = await SendAsync(Client(), HttpMethod.Post, Url("cart/items", club), new { variantId, quantity },
            token is null ? [] : [(ShopCartService.CartTokenHeader, token)]);
        Assert.Equal(HttpStatusCode.OK, status);
        var returned = json.TryGetProperty("cartToken", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var effective = returned ?? token ?? throw new InvalidOperationException("沒有購物車權杖");
        if (returned is not null)
        {
            scope.CartHashes.Add(ShopCartService.Hash(returned));
        }

        return effective;
    }

    private static object Checkout(string email, string delivery = "home_delivery", object? invoice = null, string? note = null) => new
    {
        email, recipientName = "【測試】買家", recipientPhone = "0912-345-678", deliveryMethod = delivery,
        recipientAddress = delivery == "home_delivery" ? "【測試】台中市西屯區測試路 1 號" : null, pickupStore = delivery == "cvs_pickup" ? "【測試】測試門市 123456" : null,
        customerNote = note, invoice = invoice ?? new { type = "mobile_barcode", carrierId = "/ABC+123" },
    };

    private async Task<(HttpStatusCode Status, JsonElement Json)> CheckoutAsync(string cartToken, object body, string? key = null, string club = Club, string? bearer = null)
    {
        var headers = new List<(string, string)> { ("Idempotency-Key", key ?? "t-" + Guid.NewGuid().ToString("N")) };
        if (cartToken.Length > 0)
        {
            headers.Add((ShopCartService.CartTokenHeader, cartToken));
        }

        using var client = Client();
        if (bearer is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await SendAsync(client, HttpMethod.Post, Url("checkout", club), body, [.. headers]);
    }

    private static async Task<(int Stock, int Reserved)> StockAsync(Guid variantId)
        => (await C1Test.ScalarAsync<int>("SELECT stock_qty FROM product_variants WHERE id = @V", ("@V", variantId)),
            await C1Test.ScalarAsync<int>("SELECT reserved_qty FROM product_variants WHERE id = @V", ("@V", variantId)));

    /// <summary>確保收款主體（俱樂部）有一個電子發票字軌通道，回傳「還原」委派。已有就沿用（沒有字軌則暫時補上），測完還原。</summary>
    private static async Task<(string Prefix, Func<Task> Restore)> EnsureInvoiceChannelAsync()
    {
        var owner = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM clubs WHERE is_collecting_subject = 1 ORDER BY sort_order");
        var env = await ChannelEnvironmentAsync(owner);
        var existing = await C1Test.ScalarAsync<string>("SELECT TOP 1 CONCAT(CAST(id AS nvarchar(40)), '|', ISNULL(invoice_prefix, '')) FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'einvoice' AND environment = @E", ("@O", owner), ("@E", env));
        if (existing is null)
        {
            await BizTest.ExecuteSqlAsync(
                "INSERT INTO payment_channels (owner_club_id, channel_type, environment, credential_encrypted, invoice_prefix) VALUES (@O, 'einvoice', @E, 'test-only', 'ZZ')", ("@O", owner), ("@E", env));
            return ("ZZ", () => BizTest.ExecuteSqlAsync("DELETE FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'einvoice' AND environment = @E AND credential_encrypted = 'test-only'", ("@O", owner), ("@E", env)));
        }

        var parts = existing.Split('|');
        if (parts[1].Length == 2)
        {
            return (parts[1], () => Task.CompletedTask);
        }

        var id = Guid.Parse(parts[0]);
        await BizTest.ExecuteSqlAsync("UPDATE payment_channels SET invoice_prefix = 'ZZ' WHERE id = @I", ("@I", id));
        return ("ZZ", () => BizTest.ExecuteSqlAsync("UPDATE payment_channels SET invoice_prefix = NULLIF(@P, '') WHERE id = @I", ("@I", id), ("@P", parts[1])));
    }

    /// <summary>商店目前啟用的金流／發票環境（<c>shop.payment_environment</c>，預設 sandbox）——發票字軌取這個環境的通道。</summary>
    private static async Task<string> ChannelEnvironmentAsync(Guid owner)
        => await C1Test.ScalarAsync<string>("SELECT TOP 1 setting_value FROM settings WHERE club_id = @O AND setting_key = 'shop.payment_environment'", ("@O", owner)) is "production" ? "production" : "sandbox";

    private async Task<ShopTest.Made> NewProductAsync(Scope scope, HttpClient admin, string tag, string club, params (string Size, int Price, int Stock)[] variants)
    {
        var made = await ShopTest.CreateProductAsync(admin, club, tag, variants);
        scope.Products.Add(made.ProductId);
        return made;
    }

    // ═══════════════════════════ 目錄 ═══════════════════════════

    [Fact]
    public async Task 目錄_列表詳情篩選_缺貨判定_隱藏_跨俱樂部互不可見_不得快取()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "catalog", Club, ("S", 800, 20), ("M", 1000, 0));
        var bwMade = await NewProductAsync(scope, admin, "bwcat", "bw", ("M", 700, 3));
        var hidden = await NewProductAsync(scope, admin, "hiddenoos", Club, ("M", 900, 0));
        await BizTest.ExecuteSqlAsync("UPDATE product_variants SET sale_price = 600 WHERE id = @V", ("@V", made.Variants[0].Id));
        await BizTest.ExecuteSqlAsync("UPDATE products SET is_new_arrival = 1 WHERE id = @P", ("@P", made.ProductId));
        await BizTest.ExecuteSqlAsync("UPDATE products SET out_of_stock_behavior = 'hide' WHERE id = @P", ("@P", hidden.ProductId));
        using var client = Client();

        // 詳情：規格、促銷價、庫存狀態；回應不得被快取。
        using var raw = await client.GetAsync(Url($"products/{made.Slug}"));
        Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
        Assert.Equal("no-store", raw.Headers.CacheControl?.ToString());
        var detail = await raw.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, detail.GetProperty("variants").GetArrayLength());
        var s = detail.GetProperty("variants").EnumerateArray().First(v => v.GetProperty("size").GetString() == "S");
        var m = detail.GetProperty("variants").EnumerateArray().First(v => v.GetProperty("size").GetString() == "M");
        Assert.Equal(600, s.GetProperty("price").GetInt32());
        Assert.Equal(800, s.GetProperty("listPrice").GetInt32());
        Assert.True(s.GetProperty("onSale").GetBoolean());
        Assert.True(s.GetProperty("purchasable").GetBoolean());
        Assert.False(m.GetProperty("purchasable").GetBoolean());
        Assert.Equal(0, m.GetProperty("availableQty").GetInt32());
        Assert.True(detail.GetProperty("isNewArrival").GetBoolean());
        Assert.Equal("in_stock", detail.GetProperty("stockStatus").GetString());

        // 列表：篩選（價格、尺寸、新上市）與排序。
        var list = await client.GetFromJsonAsync<JsonElement>(Url($"products?size=S&minPrice=500&maxPrice=700&isNew=true&pageSize=60"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("slug").GetString() == made.Slug);
        var item = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("slug").GetString() == made.Slug);
        Assert.Equal(600, item.GetProperty("priceMin").GetInt32());
        Assert.Equal(800, item.GetProperty("listPriceMin").GetInt32());
        var none = await client.GetFromJsonAsync<JsonElement>(Url("products?size=XXL&pageSize=60"));
        Assert.DoesNotContain(none.GetProperty("items").EnumerateArray(), i => i.GetProperty("slug").GetString() == made.Slug);

        // 缺貨且設定「自動隱藏」：列表不出現，詳情網址仍可開（標示缺貨）。
        var all = await client.GetFromJsonAsync<JsonElement>(Url("products?pageSize=60"));
        Assert.DoesNotContain(all.GetProperty("items").EnumerateArray(), i => i.GetProperty("slug").GetString() == hidden.Slug);
        var hiddenDetail = await client.GetFromJsonAsync<JsonElement>(Url($"products/{hidden.Slug}"));
        Assert.Equal("sold_out", hiddenDetail.GetProperty("stockStatus").GetString());

        // 缺貨但「顯示但不可購買」：仍在列表。
        await BizTest.ExecuteSqlAsync("UPDATE products SET out_of_stock_behavior = 'show_unavailable' WHERE id = @P", ("@P", hidden.ProductId));
        var again = await client.GetFromJsonAsync<JsonElement>(Url("products?pageSize=60"));
        Assert.Contains(again.GetProperty("items").EnumerateArray(), i => i.GetProperty("slug").GetString() == hidden.Slug && i.GetProperty("stockStatus").GetString() == "sold_out");

        // 草稿商品不可見；別隊商品不可見（club_id 硬過濾）。
        await BizTest.ExecuteSqlAsync("UPDATE products SET status = 'draft' WHERE id = @P", ("@P", hidden.ProductId));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url($"products/{hidden.Slug}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url($"products/{bwMade.Slug}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url($"products/{bwMade.Slug}", "bw"))).StatusCode);
        var bwList = await client.GetFromJsonAsync<JsonElement>(Url("products?pageSize=60", "bw"));
        Assert.DoesNotContain(bwList.GetProperty("items").EnumerateArray(), i => i.GetProperty("slug").GetString() == made.Slug);

        // 庫存即時（不讀快取）：後台改庫存後前台立刻看到。
        await BizTest.ExecuteSqlAsync("UPDATE product_variants SET stock_qty = 9 WHERE id = @V", ("@V", made.Variants[1].Id));
        var refreshed = await client.GetFromJsonAsync<JsonElement>(Url($"products/{made.Slug}"));
        Assert.Equal(9, refreshed.GetProperty("variants").EnumerateArray().First(v => v.GetProperty("size").GetString() == "M").GetProperty("availableQty").GetInt32());
    }

    [Fact]
    public async Task 商店入口_運費發票選項_收款主體_付款可用狀態()
    {
        using var client = Client();
        var info = await client.GetFromJsonAsync<JsonElement>(Url("info"));
        Assert.Equal(3, info.GetProperty("deliveryMethods").GetArrayLength());
        Assert.True(info.GetProperty("paymentAvailable").GetBoolean()); // 測試主機註冊本機假金流
        Assert.Equal(JsonValueKind.String, info.GetProperty("collectingSubjectName").ValueKind);
        Assert.Equal(JsonValueKind.Array, info.GetProperty("donationCodes").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url("info", "no-such-club"))).StatusCode);
    }

    // ═══════════════════════════ 購物車 ═══════════════════════════

    [Fact]
    public async Task 購物車_訪客權杖_累加_設定數量_庫存上限_不跨俱樂部混買()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "cart", Club, ("M", 500, 3));
        var bwMade = await NewProductAsync(scope, admin, "bwcart", "bw", ("M", 400, 5));
        var v = made.Variants[0].Id;

        // 第一次加入：發權杖；之後帶權杖累加。
        var token = await AddToCartAsync(scope, v, 1);
        await AddToCartAsync(scope, v, 1, token);
        var cart = (await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, token))).Json;
        Assert.Equal(2, cart.GetProperty("itemCount").GetInt32());
        Assert.Equal(1000, cart.GetProperty("subtotal").GetInt32());
        Assert.True(cart.GetProperty("canCheckout").GetBoolean());
        Assert.False(cart.TryGetProperty("cartToken", out var again) && again.ValueKind == JsonValueKind.String); // 權杖只在新發出時回傳
        Assert.Equal("no-store", (await Client().GetAsync(Url("cart"))).Headers.CacheControl?.ToString());

        // 超過可售量 → 409；數量上限；設定數量與移除。
        var over = await SendAsync(Client(), HttpMethod.Post, Url("cart/items"), new { variantId = v, quantity = 2 }, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.Conflict, over.Status);
        Assert.Equal("insufficient_stock", over.Json.GetProperty("code").GetString());
        var set = await SendAsync(Client(), HttpMethod.Put, Url($"cart/items/{v}"), new { quantity = 3 }, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.OK, set.Status);
        Assert.Equal(3, set.Json.GetProperty("itemCount").GetInt32());
        var tooMany = await SendAsync(Client(), HttpMethod.Put, Url($"cart/items/{v}"), new { quantity = 4 }, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.Conflict, tooMany.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(Client(), HttpMethod.Put, Url($"cart/items/{v}"), new { quantity = 1000 }, (ShopCartService.CartTokenHeader, token))).Status);
        var removed = await SendAsync(Client(), HttpMethod.Put, Url($"cart/items/{v}"), new { quantity = 0 }, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(0, removed.Json.GetProperty("itemCount").GetInt32());

        // 🔴 不跨俱樂部混買：別隊的規格 id 加入這隊購物車 → 404；同一個權杖換站台 → 看到的是空車（切換站台即切換購物車）。
        var cross = await SendAsync(Client(), HttpMethod.Post, Url("cart/items"), new { variantId = bwMade.Variants[0].Id, quantity = 1 }, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.NotFound, cross.Status);
        await AddToCartAsync(scope, v, 1, token);
        var otherSite = (await SendAsync(Client(), HttpMethod.Get, Url("cart", "bw"), null, (ShopCartService.CartTokenHeader, token))).Json;
        Assert.Equal(0, otherSite.GetProperty("itemCount").GetInt32());

        // 未知權杖＝空車；不存在的規格 404；數量非法 400。
        var unknown = await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, "no-such-token"));
        Assert.Equal(0, unknown.Json.GetProperty("itemCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url("cart/items"), new { variantId = Guid.NewGuid(), quantity = 1 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(Client(), HttpMethod.Post, Url("cart/items"), new { variantId = v, quantity = 0 })).Status);

        // 商品下架後購物車標示、不可結帳。
        await BizTest.ExecuteSqlAsync("UPDATE products SET status = 'draft' WHERE id = @P", ("@P", made.ProductId));
        var flagged = (await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, token))).Json;
        Assert.False(flagged.GetProperty("canCheckout").GetBoolean());
        Assert.Equal("unavailable", flagged.GetProperty("items")[0].GetProperty("issue").GetString());
    }

    [Fact]
    public async Task 購物車_會員購物車存帳號_登入後合併訪客購物車()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var members = new MemberTestScope(fixture); // 先宣告、後釋放：商品／訂單／購物車（Scope）要先清，會員才刪得掉
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "merge", Club, ("M", 500, 20), ("L", 520, 20));
        var member = await members.CreateVerifiedMemberAsync("cartmerge");
        scope.Members.Add(member.MemberId);

        var memberAdd = await SendAsync(member.Client, HttpMethod.Post, Url("cart/items"), new { variantId = made.Variants[0].Id, quantity = 2 });
        Assert.Equal(HttpStatusCode.OK, memberAdd.Status);
        Assert.False(memberAdd.Json.TryGetProperty("cartToken", out var t) && t.ValueKind == JsonValueKind.String); // 會員購物車不發訪客權杖

        var token = await AddToCartAsync(scope, made.Variants[0].Id, 1);
        await AddToCartAsync(scope, made.Variants[1].Id, 1, token);
        var merged = await SendAsync(member.Client, HttpMethod.Post, Url("cart/merge"), null, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.OK, merged.Status);
        Assert.Equal(4, merged.Json.GetProperty("itemCount").GetInt32()); // 2 + 1 + 1
        // 合併後訪客購物車已不存在；重複合併安全。
        Assert.Equal(0, (await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, token))).Json.GetProperty("itemCount").GetInt32());
        var twice = await SendAsync(member.Client, HttpMethod.Post, Url("cart/merge"), null, (ShopCartService.CartTokenHeader, token));
        Assert.Equal(4, twice.Json.GetProperty("itemCount").GetInt32());
        // 沒登入不能合併。
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(Client(), HttpMethod.Post, Url("cart/merge"))).Status);
    }

    // ═══════════════════════════ 結帳／付款／發票／通知 ═══════════════════════════

    [Fact]
    public async Task 訪客完整流程_結帳金額重算_保留庫存_付款確認扣庫存_開發票_兩封信_確認冪等_查詢遮罩()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var (prefix, restore) = await EnsureInvoiceChannelAsync();
        scope.RestoreChannel = restore;
        var made = await NewProductAsync(scope, admin, "flow", Club, ("M", 500, 10));
        var variant = made.Variants[0].Id;
        await BizTest.ExecuteSqlAsync("UPDATE product_variants SET sale_price = 450 WHERE id = @V", ("@V", variant)); // 促銷價優先
        var info = await Client().GetFromJsonAsync<JsonElement>(Url("info"));
        var fee = info.GetProperty("shippingFee").GetInt32();
        var threshold = info.TryGetProperty("freeShippingThreshold", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int?)null;
        var token = await AddToCartAsync(scope, variant, 2);
        var email = NewEmail();
        var sentBefore = MemberTestDoubles.Email.Sent.Count;

        var created = await CheckoutAsync(token, Checkout(email, note: "【測試】請按門鈴"));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var order = created.Json;
        var orderNo = order.GetProperty("orderNo").GetString()!;
        var orderToken = order.GetProperty("accessToken").GetString()!;
        Assert.StartsWith("TR-", orderNo);
        Assert.Equal("待付款", order.GetProperty("status").GetString());
        Assert.Equal(900, order.GetProperty("subtotal").GetInt32()); // 450 × 2：金額由伺服器重算，請求本文沒有金額欄位
        var expectedFee = threshold is int th && 900 >= th ? 0 : fee;
        Assert.Equal(expectedFee, order.GetProperty("shippingFee").GetInt32());
        Assert.Equal(900 + expectedFee, order.GetProperty("total").GetInt32());
        Assert.True(order.GetProperty("canPay").GetBoolean());
        Assert.Equal("mobile_barcode", order.GetProperty("invoice").GetProperty("type").GetString());
        Assert.False(order.GetProperty("isMasked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, order.GetProperty("paymentUrl").ValueKind);

        // 下單即保留（庫存量不變、保留量 +2）；購物車已清空；成立信已寄出；資料庫分帳欄位正確。
        Assert.Equal((10, 2), await StockAsync(variant));
        Assert.Equal(0, (await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, token))).Json.GetProperty("itemCount").GetInt32());
        var createdMail = MemberTestDoubles.Email.LastTo(email, "shop_order_created");
        Assert.NotNull(createdMail);
        Assert.Contains(orderNo, createdMail!.Subject);
        Assert.Contains("order/lookup/?token=", createdMail.TextBody);
        Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM orders WHERE order_no = @N AND selling_club_id = club_id AND collecting_club_id = (SELECT TOP 1 id FROM clubs WHERE is_collecting_subject = 1 ORDER BY sort_order) AND payment_status = 'pending' AND is_manual = 0", ("@N", orderNo)));

        // 付款前不能被別人操作：沒帶權杖 404；帶錯權杖 404。
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"), null, (ShopOrderService.OrderTokenHeader, "wrong"))).Status);
        // 別的俱樂部路由看不到這張訂單。
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Get, Url($"orders/{orderNo}", "bw"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Status);

        // 請款：回付款網址；重複請款回同一個（冪等）。
        var pay = await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"), null, (ShopOrderService.OrderTokenHeader, orderToken));
        Assert.Equal(HttpStatusCode.OK, pay.Status);
        Assert.Equal($"https://fake-linepay.invalid/pay/{orderNo}", pay.Json.GetProperty("paymentUrl").GetString());
        var pay2 = await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"), null, (ShopOrderService.OrderTokenHeader, orderToken));
        Assert.Equal(pay.Json.GetProperty("paymentUrl").GetString(), pay2.Json.GetProperty("paymentUrl").GetString());

        // 確認：交易識別不符 400；用戶端不能自己宣稱付款成功。
        var bad = await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/confirm"), new { transactionId = "FAKE-OTHER" }, (ShopOrderService.OrderTokenHeader, orderToken));
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Equal("transaction_mismatch", bad.Json.GetProperty("code").GetString());
        Assert.Equal("待付款", (await SendAsync(Client(), HttpMethod.Get, Url($"orders/{orderNo}"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Json.GetProperty("status").GetString());

        // 並行 6 個確認：只成立一次——庫存只扣一次、發票只開一張、付款完成信只寄一封。
        var confirms = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/confirm"), new { transactionId = $"FAKE-{orderNo}" }, (ShopOrderService.OrderTokenHeader, orderToken))));
        Assert.All(confirms, c => Assert.Equal(HttpStatusCode.OK, c.Status));
        Assert.All(confirms, c => Assert.Equal("已付款", c.Json.GetProperty("status").GetString()));
        Assert.Equal((8, 0), await StockAsync(variant));
        Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM inventory_movements WHERE order_id = (SELECT id FROM orders WHERE order_no = @N) AND movement_type = 'sale'", ("@N", orderNo)));
        Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N) AND issue_status = 'issued'", ("@N", orderNo)));
        Assert.Equal(1, MemberTestDoubles.Email.Sent.Count(m => m.To == email && m.Kind == "shop_payment_completed"));
        // 並行的 6 個請求只有「第一個」會開發票；其餘走冪等分支、在贏家開完發票之前就回應，那時發票狀態可能還是 pending。
        // 所以不能拿 confirms[0]（不一定是贏家）斷言發票：改為「至少一個回應是 issued（贏家）」，並以全部確認完成後重讀的訂單為準。
        Assert.Contains(confirms, c => c.Json.GetProperty("invoice").GetProperty("status").GetString() == "issued");
        var paid = (await SendAsync(Client(), HttpMethod.Get, Url($"orders/{orderNo}"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Json;
        Assert.Equal("issued", paid.GetProperty("invoice").GetProperty("status").GetString());
        Assert.StartsWith(prefix, paid.GetProperty("invoice").GetProperty("invoiceNo").GetString());
        Assert.Matches("^[A-Z]{2}[0-9]{8}$", paid.GetProperty("invoice").GetProperty("invoiceNo").GetString()!);
        Assert.False(paid.GetProperty("canPay").GetBoolean());
        Assert.Equal(JsonValueKind.Null, paid.GetProperty("paymentUrl").ValueKind);
        Assert.Contains(MemberTestDoubles.Email.Sent, m => m.To == email && m.Kind == "shop_payment_completed" && m.TextBody.Contains(paid.GetProperty("invoice").GetProperty("invoiceNo").GetString()!));
        // 載具號碼不得以明文存放。
        Assert.DoesNotContain("ABC+123", await C1Test.ScalarAsync<string>("SELECT carrier_id_encrypted FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo)) ?? "");
        Assert.True(MemberTestDoubles.Email.Sent.Count >= sentBefore + 2);

        // 已付款不能取消、不能再請款。
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/cancel"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Status);

        // 訪客查詢：訂單編號＋Email → 遮罩；Email 不符 → 404；信件連結權杖 → 完整；訂單編號大小寫不拘。
        var lookup = await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { orderNo = orderNo.ToLowerInvariant(), email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.OK, lookup.Status);
        Assert.True(lookup.Json.GetProperty("isMasked").GetBoolean());
        Assert.DoesNotContain("0912-345-678", lookup.Json.GetProperty("recipientPhone").GetString());
        Assert.Equal(JsonValueKind.Null, lookup.Json.GetProperty("accessToken").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { orderNo, email = "other@example.test" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { orderNo = "TR-00000000-XXXXXX", email })).Status);
        var byToken = await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { token = orderToken });
        Assert.Equal(HttpStatusCode.OK, byToken.Status);
        Assert.False(byToken.Json.GetProperty("isMasked").GetBoolean());
        Assert.Equal("0912-345-678", byToken.Json.GetProperty("recipientPhone").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup", "bw"), new { orderNo, email })).Status); // 別隊站台查不到
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { })).Status);

        // 權杖過期（成立超過 30 天）：權杖失效，但「訂單編號＋Email」仍可查。
        await BizTest.ExecuteSqlAsync("UPDATE orders SET created_at = DATEADD(day, -31, SYSUTCDATETIME()) WHERE order_no = @N", ("@N", orderNo));
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { token = orderToken })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(Client(), HttpMethod.Get, Url($"orders/{orderNo}"), null, (ShopOrderService.OrderTokenHeader, orderToken))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(Client(), HttpMethod.Post, Url("orders/lookup"), new { orderNo, email })).Status);
    }

    [Fact]
    public async Task 會員結帳_歸戶_我的訂單_別人看不到_會員本人可付款()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var members = new MemberTestScope(fixture); // 先宣告、後釋放：商品／訂單／購物車（Scope）要先清，會員才刪得掉
        await using var scope = new Scope();
        var (_, restore) = await EnsureInvoiceChannelAsync();
        scope.RestoreChannel = restore;
        var made = await NewProductAsync(scope, admin, "member", Club, ("M", 300, 5));
        var me = await members.CreateVerifiedMemberAsync("shopper");
        var other = await members.CreateVerifiedMemberAsync("shopper2");
        scope.Members.Add(me.MemberId);
        scope.Members.Add(other.MemberId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(me.Client, HttpMethod.Post, Url("cart/items"), new { variantId = made.Variants[0].Id, quantity = 1 })).Status);

        // 會員省略 Email 與收件資料：用帳號資料帶入；會員訂單回應不帶訪客權杖。
        var created = await SendAsync(me.Client, HttpMethod.Post, Url("checkout"),
            new { deliveryMethod = "onsite_pickup", invoice = new { type = "donation", donationCode = await EnsureDonationCodeAsync() } }, ("Idempotency-Key", "m-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var orderNo = created.Json.GetProperty("orderNo").GetString()!;
        Assert.Equal(JsonValueKind.Null, created.Json.GetProperty("accessToken").ValueKind);
        Assert.Equal(0, created.Json.GetProperty("shippingFee").GetInt32()); // 現場自取免運
        Assert.Equal(me.MemberId, await BizTest.ScalarGuidAsync("SELECT member_id FROM orders WHERE order_no = @N", ("@N", orderNo)));
        Assert.Equal("donation", created.Json.GetProperty("invoice").GetProperty("type").GetString());

        var mine = await SendAsync(me.Client, HttpMethod.Get, Url("my-orders"));
        Assert.Equal(orderNo, mine.Json[0].GetProperty("orderNo").GetString());
        Assert.Equal(0, (await SendAsync(other.Client, HttpMethod.Get, Url("my-orders"))).Json.GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(Client(), HttpMethod.Get, Url("my-orders"))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(other.Client, HttpMethod.Get, Url($"orders/{orderNo}"))).Status); // 別人的訂單 404
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(other.Client, HttpMethod.Post, Url($"orders/{orderNo}/pay"))).Status);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(me.Client, HttpMethod.Post, Url($"orders/{orderNo}/pay"))).Status);
        var done = await SendAsync(me.Client, HttpMethod.Post, Url($"orders/{orderNo}/confirm"), new { transactionId = $"FAKE-{orderNo}" });
        Assert.Equal("已付款", done.Json.GetProperty("status").GetString());
        Assert.Equal("issued", done.Json.GetProperty("invoice").GetProperty("status").GetString());
        Assert.Equal("已付款", (await SendAsync(me.Client, HttpMethod.Get, Url($"orders/{orderNo}"))).Json.GetProperty("status").GetString());
        // 英文介面標籤。
        Assert.Equal("Paid", (await SendAsync(me.Client, HttpMethod.Get, Url($"orders/{orderNo}?lang=en"))).Json.GetProperty("status").GetString());
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

    [Fact]
    public async Task 結帳驗證_發票三選一格式_統編檢核碼_捐贈碼_配送欄位_必填標頭_空購物車()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "validate", Club, ("M", 100, 50));
        var token = await AddToCartAsync(scope, made.Variants[0].Id, 1);
        var email = NewEmail();

        async Task<string?> CodeOfAsync(object body, string? key = "k-" + "valid-key-1")
        {
            var r = await CheckoutAsync(token, body, key);
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            return r.Json.GetProperty("code").GetString();
        }

        Assert.Equal("invalid_carrier", await CodeOfAsync(Checkout(email, invoice: new { type = "mobile_barcode", carrierId = "ABC1234" })));
        Assert.Equal("invalid_carrier", await CodeOfAsync(Checkout(email, invoice: new { type = "citizen_cert", carrierId = "AB123" })));
        Assert.Equal("invalid_tax_id", await CodeOfAsync(Checkout(email, invoice: new { type = "tax_id", taxId = "12345678" })));
        Assert.Equal("invalid_donation_code", await CodeOfAsync(Checkout(email, invoice: new { type = "donation", donationCode = "0000000" })));
        Assert.Equal("invoice_required", await CodeOfAsync(Checkout(email, invoice: new { type = "none" })));
        Assert.Equal("invoice_required", await CodeOfAsync(new { email, recipientName = "x", recipientPhone = "0912345678", deliveryMethod = "home_delivery", recipientAddress = "台中市" }));
        Assert.Equal("invalid_email", await CodeOfAsync(Checkout("not-an-email")));
        Assert.Equal("invalid_delivery_method", await CodeOfAsync(new { email, recipientName = "x", recipientPhone = "0912345678", deliveryMethod = "drone", invoice = new { type = "mobile_barcode", carrierId = "/ABC+123" } }));
        Assert.Equal("invalid_address", await CodeOfAsync(new { email, recipientName = "x", recipientPhone = "0912345678", deliveryMethod = "home_delivery", invoice = new { type = "mobile_barcode", carrierId = "/ABC+123" } }));
        Assert.Equal("invalid_pickup_store", await CodeOfAsync(new { email, recipientName = "x", recipientPhone = "0912345678", deliveryMethod = "cvs_pickup", invoice = new { type = "mobile_barcode", carrierId = "/ABC+123" } }));
        Assert.Equal("invalid_phone", await CodeOfAsync(new { email, recipientName = "x", recipientPhone = "abc", deliveryMethod = "onsite_pickup", invoice = new { type = "mobile_barcode", carrierId = "/ABC+123" } }));

        // 統編檢核碼（04595257 是合法統編；金額欄位根本不存在於請求本文）。
        Assert.True(ShopOrderService.IsValidTaxId("04595257"));
        Assert.False(ShopOrderService.IsValidTaxId("04595258"));

        // 冪等鍵必填且格式正確。
        var noKey = await SendAsync(Client(), HttpMethod.Post, Url("checkout"), Checkout(email), (ShopCartService.CartTokenHeader, token));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.Status);
        Assert.Equal("idempotency_key_required", noKey.Json.GetProperty("code").GetString());

        // 沒有購物車、空購物車：409。
        var empty = await CheckoutAsync("", Checkout(email));
        Assert.Equal(HttpStatusCode.Conflict, empty.Status);
        Assert.Equal("cart_empty", empty.Json.GetProperty("code").GetString());

        // 以上全部驗證失敗都不能留下訂單、不能動庫存。
        Assert.Equal((50, 0), await StockAsync(made.Variants[0].Id));
        Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM order_items WHERE product_variant_id = @V", ("@V", made.Variants[0].Id)));
    }

    [Fact]
    public async Task 超商取貨與統編發票_合法下單成功()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "cvs", Club, ("M", 100, 5));
        var token = await AddToCartAsync(scope, made.Variants[0].Id, 1);
        var created = await CheckoutAsync(token, Checkout(NewEmail(), "cvs_pickup", new { type = "tax_id", taxId = "04595257" }));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("cvs_pickup", created.Json.GetProperty("deliveryMethod").GetString());
        Assert.Contains("【測試】測試門市 123456", created.Json.GetProperty("recipientAddress").GetString());
        Assert.Equal("04595257", created.Json.GetProperty("invoice").GetProperty("taxId").GetString());
    }

    // ═══════════════════════════ 冪等 ═══════════════════════════

    [Fact]
    public async Task 結帳冪等_同鍵重送回原訂單_不同內容409_不同擁有者409_並行同鍵只成立一張()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "idem", Club, ("M", 100, 10));
        var v = made.Variants[0].Id;
        var token = await AddToCartAsync(scope, v, 2);
        var email = NewEmail();
        var key = "idem-" + Guid.NewGuid().ToString("N");

        var first = await CheckoutAsync(token, Checkout(email), key);
        Assert.Equal(HttpStatusCode.Created, first.Status);
        var orderNo = first.Json.GetProperty("orderNo").GetString();

        // 購物車此時已清空：同鍵同內容重送仍回原訂單（200，不是 409 購物車是空的），庫存只保留一次。
        var replay = await CheckoutAsync(token, Checkout(email), key);
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(orderNo, replay.Json.GetProperty("orderNo").GetString());
        Assert.Equal(first.Json.GetProperty("accessToken").GetString(), replay.Json.GetProperty("accessToken").GetString());
        Assert.Equal((10, 2), await StockAsync(v));

        // 同鍵不同內容 → 409；別的訪客（不同權杖）拿同一把鍵 → 409，不洩漏原訂單。
        var different = await CheckoutAsync(token, Checkout(email, "onsite_pickup"), key);
        Assert.Equal(HttpStatusCode.Conflict, different.Status);
        Assert.Equal("idempotency_key_reused", different.Json.GetProperty("code").GetString());
        var token2 = await AddToCartAsync(scope, v, 1);
        var stolen = await CheckoutAsync(token2, Checkout(email), key);
        Assert.Equal(HttpStatusCode.Conflict, stolen.Status);
        Assert.Equal((10, 2), await StockAsync(v));

        // 🔴 並行：同一台購物車、同一把冪等鍵，同時送 8 個——只成立 1 張訂單，其餘回同一張；庫存只保留一次。
        var token3 = await AddToCartAsync(scope, v, 3);
        var raceKey = "race-" + Guid.NewGuid().ToString("N");
        var raceEmail = NewEmail();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CheckoutAsync(token3, Checkout(raceEmail), raceKey)));
        Assert.All(results, r => Assert.True(r.Status is HttpStatusCode.Created or HttpStatusCode.OK, $"非預期狀態 {r.Status}：{r.Json}"));
        Assert.Single(results.Select(r => r.Json.GetProperty("orderNo").GetString()).Distinct());
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM orders WHERE idempotency_key = @K", ("@K", raceKey)));
        Assert.Equal((10, 5), await StockAsync(v)); // 2（第一張）＋ 3（並行那張）
    }

    // ═══════════════════════════ 防超賣（並行）═══════════════════════════

    [Fact]
    public async Task 防超賣_庫存3件_10個訪客同時各買1件_恰好3張成立_其餘409_保留量等於庫存()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "oversell", Club, ("M", 100, 3));
        var v = made.Variants[0].Id;
        var tokens = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            tokens.Add(await AddToCartAsync(scope, v, 1)); // 加入購物車時庫存都還夠，真正的競爭發生在結帳
        }

        var results = await Task.WhenAll(tokens.Select(t => CheckoutAsync(t, Checkout(NewEmail()))));
        Assert.Equal(3, results.Count(r => r.Status == HttpStatusCode.Created));
        var rejected = results.Where(r => r.Status != HttpStatusCode.Created).ToList();
        Assert.Equal(7, rejected.Count);
        Assert.All(rejected, r =>
        {
            Assert.Equal(HttpStatusCode.Conflict, r.Status);
            Assert.Equal("insufficient_stock", r.Json.GetProperty("code").GetString());
        });
        Assert.Equal((3, 3), await StockAsync(v)); // 保留量剛好等於庫存，沒有超賣、沒有負數
        Assert.Equal(3, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM order_items WHERE product_variant_id = @V", ("@V", v)));
        // 失敗的請求不留下訂單、不留下保留異動；失敗者的購物車仍在（可以改數量重試）。
        Assert.Equal(3, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM inventory_movements WHERE product_variant_id = @V AND movement_type = 'reserve'", ("@V", v)));
        var failedToken = tokens[Array.FindIndex(results, r => r.Status != HttpStatusCode.Created)];
        Assert.Equal(1, (await SendAsync(Client(), HttpMethod.Get, Url("cart"), null, (ShopCartService.CartTokenHeader, failedToken))).Json.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task 防超賣_多規格互相交錯下單_不死結_不超賣()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "deadlock", Club, ("S", 100, 4), ("M", 100, 4));
        var (a, b) = (made.Variants[0].Id, made.Variants[1].Id);
        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            // 一半先加 S 再加 M、一半相反：若取鎖順序依購物車加入順序就會互相死結。
            var t = await AddToCartAsync(scope, i % 2 == 0 ? a : b, 1);
            await AddToCartAsync(scope, i % 2 == 0 ? b : a, 1, t);
            tokens.Add(t);
        }

        var results = await Task.WhenAll(tokens.Select(t => CheckoutAsync(t, Checkout(NewEmail()))));
        Assert.All(results, r => Assert.True(r.Status is HttpStatusCode.Created or HttpStatusCode.Conflict, $"非預期狀態 {r.Status}：{r.Json}"));
        Assert.Equal(4, results.Count(r => r.Status == HttpStatusCode.Created)); // 每個規格 4 件、每單各 1 件 → 恰好 4 單
        Assert.Equal((4, 4), await StockAsync(a));
        Assert.Equal((4, 4), await StockAsync(b));
    }

    // ═══════════════════════════ 取消／逾時 ═══════════════════════════

    [Fact]
    public async Task 取消待付款釋回庫存_逾時讀到時換算_庫存被逾時訂單占住時結帳會先清掃再成立()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "expire", Club, ("M", 100, 1));
        var v = made.Variants[0].Id;

        // 買家取消待付款訂單：釋回保留、狀態已取消；重複取消冪等。
        var t1 = await AddToCartAsync(scope, v, 1);
        var o1 = await CheckoutAsync(t1, Checkout(NewEmail()));
        var no1 = o1.Json.GetProperty("orderNo").GetString()!;
        var tok1 = o1.Json.GetProperty("accessToken").GetString()!;
        Assert.Equal((1, 1), await StockAsync(v));
        var cancelled = await SendAsync(Client(), HttpMethod.Post, Url($"orders/{no1}/cancel"), null, (ShopOrderService.OrderTokenHeader, tok1));
        Assert.Equal("已取消", cancelled.Json.GetProperty("status").GetString());
        Assert.Equal((1, 0), await StockAsync(v));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{no1}/cancel"), null, (ShopOrderService.OrderTokenHeader, tok1))).Status);
        // 已取消的訂單不能再請款或確認。
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{no1}/pay"), null, (ShopOrderService.OrderTokenHeader, tok1))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{no1}/confirm"), new { transactionId = $"FAKE-{no1}" }, (ShopOrderService.OrderTokenHeader, tok1))).Status);

        // 逾時：待付款超過保留時間，被讀到時自動取消並釋回庫存。
        var t2 = await AddToCartAsync(scope, v, 1);
        var o2 = await CheckoutAsync(t2, Checkout(NewEmail()));
        var no2 = o2.Json.GetProperty("orderNo").GetString()!;
        var tok2 = o2.Json.GetProperty("accessToken").GetString()!;
        Assert.Equal((1, 1), await StockAsync(v));
        await BizTest.ExecuteSqlAsync("UPDATE orders SET created_at = DATEADD(hour, -3, SYSUTCDATETIME()) WHERE order_no = @N", ("@N", no2));
        var expired = await SendAsync(Client(), HttpMethod.Get, Url($"orders/{no2}"), null, (ShopOrderService.OrderTokenHeader, tok2));
        Assert.Equal("已取消", expired.Json.GetProperty("status").GetString());
        Assert.Equal("expired", expired.Json.GetProperty("paymentStatus").GetString());
        Assert.Equal((1, 0), await StockAsync(v));
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(Client(), HttpMethod.Post, Url($"orders/{no2}/confirm"), new { transactionId = $"FAKE-{no2}" }, (ShopOrderService.OrderTokenHeader, tok2))).Status);

        // 庫存被「已逾時、但還沒有人讀到」的訂單占住：新的結帳會先清掃一輪再成立（不會誤報庫存不足）。
        var t3 = await AddToCartAsync(scope, v, 1);
        var t4 = await AddToCartAsync(scope, v, 1); // 兩台購物車都在庫存還有 1 件時加入
        var o3 = await CheckoutAsync(t3, Checkout(NewEmail()));
        Assert.Equal(HttpStatusCode.Created, o3.Status);
        var no3 = o3.Json.GetProperty("orderNo").GetString()!;
        Assert.Equal((1, 1), await StockAsync(v)); // 唯一的一件被 no3 保留
        await BizTest.ExecuteSqlAsync("UPDATE orders SET created_at = DATEADD(hour, -3, SYSUTCDATETIME()) WHERE order_no = @N", ("@N", no3)); // no3 已逾時但沒人讀它
        var o4 = await CheckoutAsync(t4, Checkout(NewEmail()));
        Assert.Equal(HttpStatusCode.Created, o4.Status);
        Assert.Equal((1, 1), await StockAsync(v)); // 保留量換成 o4 的
        Assert.Equal("已取消", await C1Test.ScalarAsync<string>("SELECT order_status FROM orders WHERE order_no = @N", ("@N", no3)));
    }

    [Fact]
    public async Task 定時維護_逾時訂單釋回_待開立發票資料列清理_發票失敗後重試補開_訪客舊購物車清理()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        await using var scope = new Scope();
        var made = await NewProductAsync(scope, admin, "maint", Club, ("M", 100, 5));
        var v = made.Variants[0].Id;

        // 沒有字軌通道時付款成功 → 發票開立失敗（訂單照常進入備貨）；補上字軌後由重試補開。
        // 先確保「沒有字軌」：記下現況、暫時把字軌清空，結束還原。
        var owner = await BizTest.ScalarGuidAsync("SELECT TOP 1 id FROM clubs WHERE is_collecting_subject = 1 ORDER BY sort_order");
        var snapshot = await C1Test.ScalarAsync<string>("SELECT TOP 1 CONCAT(CAST(id AS nvarchar(40)), '|', ISNULL(invoice_prefix, '')) FROM payment_channels WHERE owner_club_id = @O AND channel_type = 'einvoice' AND environment = @E", ("@O", owner), ("@E", await ChannelEnvironmentAsync(owner)));
        if (snapshot is not null)
        {
            var id = Guid.Parse(snapshot.Split('|')[0]);
            await BizTest.ExecuteSqlAsync("UPDATE payment_channels SET invoice_prefix = NULL WHERE id = @I", ("@I", id));
            scope.RestoreChannel = () => BizTest.ExecuteSqlAsync("UPDATE payment_channels SET invoice_prefix = NULLIF(@P, '') WHERE id = @I", ("@I", id), ("@P", snapshot.Split('|')[1]));
        }

        var token = await AddToCartAsync(scope, v, 1);
        var created = await CheckoutAsync(token, Checkout(NewEmail()));
        var orderNo = created.Json.GetProperty("orderNo").GetString()!;
        var orderToken = created.Json.GetProperty("accessToken").GetString()!;
        await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/pay"), null, (ShopOrderService.OrderTokenHeader, orderToken));
        var paid = await SendAsync(Client(), HttpMethod.Post, Url($"orders/{orderNo}/confirm"), new { transactionId = $"FAKE-{orderNo}" }, (ShopOrderService.OrderTokenHeader, orderToken));
        Assert.Equal("已付款", paid.Json.GetProperty("status").GetString()); // 發票失敗不影響付款與訂單
        Assert.Equal("pending", paid.Json.GetProperty("invoice").GetProperty("status").GetString()); // 對顧客一律顯示「處理中」
        Assert.Equal(JsonValueKind.Null, paid.Json.GetProperty("invoice").GetProperty("invoiceNo").ValueKind);
        Assert.Equal("failed", await C1Test.ScalarAsync<string>("SELECT issue_status FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo)));
        Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT retry_count FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo)));

        // 另一張待付款訂單逾時；一張被取消的訂單；一台超過 30 天的訪客購物車。
        var token2 = await AddToCartAsync(scope, v, 1);
        var o2 = await CheckoutAsync(token2, Checkout(NewEmail()));
        var no2 = o2.Json.GetProperty("orderNo").GetString()!;
        await BizTest.ExecuteSqlAsync("UPDATE orders SET created_at = DATEADD(hour, -5, SYSUTCDATETIME()) WHERE order_no = @N", ("@N", no2));
        var oldToken = await AddToCartAsync(scope, v, 1);
        await BizTest.ExecuteSqlAsync("UPDATE carts SET updated_at = DATEADD(day, -40, SYSUTCDATETIME()) WHERE anonymous_token = @H", ("@H", ShopCartService.Hash(oldToken)));
        // 補上字軌，並讓重試間隔已過。
        var (prefix, restore) = await EnsureInvoiceChannelAsync();
        var previousRestore = scope.RestoreChannel;
        scope.RestoreChannel = async () =>
        {
            await restore();
            if (previousRestore is not null)
            {
                await previousRestore();
            }
        };
        await BizTest.ExecuteSqlAsync("UPDATE store_invoices SET updated_at = DATEADD(hour, -2, SYSUTCDATETIME()) WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo));

        using var serviceScope = fixture.Services.CreateScope();
        var maintenance = serviceScope.ServiceProvider.GetRequiredService<ShopMaintenanceService>();
        var result = await maintenance.RunAsync(CancellationToken.None);
        Assert.True(result.ExpiredOrders >= 1);
        Assert.True(result.IssuedInvoices >= 1);
        Assert.True(result.RemovedCarts >= 1);
        Assert.Equal("已取消", await C1Test.ScalarAsync<string>("SELECT order_status FROM orders WHERE order_no = @N", ("@N", no2)));
        Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", no2))); // 沒付款的訂單不留發票資料列
        Assert.Equal("issued", await C1Test.ScalarAsync<string>("SELECT issue_status FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo)));
        Assert.StartsWith(prefix, await C1Test.ScalarAsync<string>("SELECT invoice_no FROM store_invoices WHERE order_id = (SELECT id FROM orders WHERE order_no = @N)", ("@N", orderNo)));
        Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM carts WHERE anonymous_token = @H", ("@H", ShopCartService.Hash(oldToken))));
        // 重跑一輪是冪等的。
        var second = await maintenance.RunAsync(CancellationToken.None);
        Assert.Equal(0, second.IssuedInvoices);
    }
}
