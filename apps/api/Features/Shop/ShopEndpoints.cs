using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Shop;

/// <summary>
/// 站內商店 8.3 的公開端點（主站規劃書 §3.8 8.3、藍鯨規劃書 §5.2／§5.3）：目錄、購物車、結帳、付款、訂單查詢。
/// 路徑 <c>/api/v1/{club}/shop/…</c>，兩個站台各自一份資料（<c>club_id</c> 硬過濾）。目錄讀取不需要登入；購物車與訂單以「會員權杖」或「訪客權杖」識別擁有者。
/// 🔴 全部回應 <c>Cache-Control: no-store</c>（庫存、購物車、訂單、付款狀態不得被 CDN 或瀏覽器快取，docs/14）。
/// 🔴 寫入端點一律依 IP 限流：購物車與訂單動作用 <c>public-member-write</c>（每 IP 每分鐘 60 次），訪客查單用較嚴的 <c>public-member-auth</c>（每 IP 5 分鐘 30 次，防訂單編號枚舉）。
/// </summary>
public static class ShopEndpoints
{
    public static void MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        MapCatalog(app);
        MapCart(app);
        MapOrders(app);
    }

    private static string Lang(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";

    private static void NoStore(HttpContext http) => http.Response.Headers.CacheControl = "no-store";

    // ═══════════════════════════ 目錄 ═══════════════════════════

    private static void MapCatalog(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/{club}/shop/info", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, ShopCatalogRepository repository, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.GetInfoAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("GetShopInfo").WithTags("Shop")
        .Produces<ShopInfoDto>().Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/{club}/shop/collections", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, ShopCatalogRepository repository, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListCollectionsAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListShopCollections").WithTags("Shop")
        .Produces<IReadOnlyList<ShopCollectionDto>>().Produces(StatusCodes.Status404NotFound);

        // GET /shop/products?collection=<slug>&tag=&minPrice=&maxPrice=&size=&colour=&sort=newest|price_asc|price_desc&isNew=true&page=&pageSize=&lang=
        app.MapGet("/api/v1/{club}/shop/products", async (
            string club, string? collection, string? tag, int? minPrice, int? maxPrice, string? size, string? colour, string? sort, bool? isNew,
            string? lang, int? page, int? pageSize, HttpContext http, IClubResolver clubs, ShopCatalogRepository repository, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 24, maxPageSize: 60);
            var filter = new ShopCatalogRepository.ListFilter(collection, tag, minPrice, maxPrice, size, colour, sort, isNew == true);
            return Results.Ok(await repository.ListProductsAsync(scope, filter, RequestLocale.ToDbLocale(lang), p, ps, ct));
        })
        .WithName("ListShopProducts").WithTags("Shop")
        .Produces<PagedResult<ShopProductListItemDto>>().Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/{club}/shop/products/{slug}", async (
            string club, string slug, string? lang, HttpContext http, IClubResolver clubs, ShopCatalogRepository repository, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            var product = await repository.GetProductAsync(scope, slug, RequestLocale.ToDbLocale(lang), ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        })
        .WithName("GetShopProduct").WithTags("Shop")
        .Produces<ShopProductDetailDto>().Produces(StatusCodes.Status404NotFound);
    }

    // ═══════════════════════════ 購物車 ═══════════════════════════

    private static void MapCart(IEndpointRouteBuilder app)
    {
        // 擁有者：有效的會員權杖 → 會員購物車；否則標頭 X-Cart-Token → 訪客購物車。
        app.MapGet("/api/v1/{club}/shop/cart", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.GetAsync(scope, await carts.ResolveOwnerAsync(http, ct), RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("GetShopCart").WithTags("Shop")
        .Produces<ShopCartDto>();

        // POST /shop/cart/items { variantId, quantity } —— 加入（數量累加）。訪客第一次加入時回應帶 cartToken。
        app.MapPost("/api/v1/{club}/shop/cart/items", async (
            string club, AddCartItemRequest request, string? lang, HttpContext http, IClubResolver clubs, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.AddAsync(scope, await carts.ResolveOwnerAsync(http, ct), request, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("AddShopCartItem").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopCartDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status429TooManyRequests);

        // PUT /shop/cart/items/{variantId} { quantity } —— 設定數量（0＝移除）。
        app.MapPut("/api/v1/{club}/shop/cart/items/{variantId:guid}", async (
            string club, Guid variantId, SetCartItemRequest request, string? lang, HttpContext http, IClubResolver clubs, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.SetQuantityAsync(scope, await carts.ResolveOwnerAsync(http, ct), variantId, request.Quantity, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("SetShopCartItem").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopCartDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status429TooManyRequests);

        app.MapDelete("/api/v1/{club}/shop/cart/items/{variantId:guid}", async (
            string club, Guid variantId, string? lang, HttpContext http, IClubResolver clubs, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.RemoveAsync(scope, await carts.ResolveOwnerAsync(http, ct), variantId, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("RemoveShopCartItem").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopCartDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status429TooManyRequests);

        app.MapDelete("/api/v1/{club}/shop/cart", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.ClearAsync(scope, await carts.ResolveOwnerAsync(http, ct), RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ClearShopCart").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopCartDto>().Produces(StatusCodes.Status429TooManyRequests);

        // POST /shop/cart/merge —— 登入後呼叫：會員權杖 ＋ X-Cart-Token（登入前的訪客購物車）→ 併入會員購物車。
        app.MapPost("/api/v1/{club}/shop/cart/merge", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopCartService carts, CancellationToken ct) =>
        {
            NoStore(http);
            var me = await members.RequireAsync(http, ct);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await carts.MergeGuestIntoMemberAsync(scope, http, me, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("MergeShopCart").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopCartDto>().Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status429TooManyRequests);
    }

    // ═══════════════════════════ 結帳與訂單 ═══════════════════════════

    private static OrderAccess Access(HttpContext http, MemberIdentity? me)
        => new(me?.MemberId, http.Request.Headers[ShopOrderService.OrderTokenHeader].ToString() is { Length: > 0 } t ? t.Trim() : null);

    private static void MapOrders(IEndpointRouteBuilder app)
    {
        // POST /shop/checkout —— 標頭 Idempotency-Key（必填）＋會員權杖或 X-Cart-Token。本文沒有金額欄位。成立 201、冪等重送 200。
        app.MapPost("/api/v1/{club}/shop/checkout", async (
            string club, CheckoutRequest request, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopCartService carts,
            ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            var me = await members.TryAsync(http, ct);
            var owner = await carts.ResolveOwnerAsync(http, ct);
            var (order, created) = await orders.CheckoutAsync(scope, owner, me, http.Request.Headers["Idempotency-Key"].ToString(), request, ct);
            return created ? Results.Created($"/api/v1/{club}/shop/orders/{order.OrderNo}", order) : Results.Ok(order);
        })
        .WithName("ShopCheckout").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopOrderDto>(StatusCodes.Status201Created).Produces<ShopOrderDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status429TooManyRequests);

        // GET /shop/orders/{orderNo} —— 會員本人（Bearer）或持有訂單權杖（X-Order-Token）。
        app.MapGet("/api/v1/{club}/shop/orders/{orderNo}", async (
            string club, string orderNo, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.GetAsync(scope, Access(http, await members.TryAsync(http, ct)), orderNo, Lang(lang), ct));
        })
        .WithName("GetShopOrder").WithTags("Shop")
        .Produces<ShopOrderDto>().Produces(StatusCodes.Status404NotFound);

        // POST /shop/orders/{orderNo}/pay —— 向 LINE Pay 請款，回付款網址（冪等）。
        app.MapPost("/api/v1/{club}/shop/orders/{orderNo}/pay", async (
            string club, string orderNo, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.PayAsync(scope, Access(http, await members.TryAsync(http, ct)), orderNo, Lang(lang), ct));
        })
        .WithName("PayShopOrder").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopOrderDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable).Produces(StatusCodes.Status429TooManyRequests);

        // POST /shop/orders/{orderNo}/confirm { transactionId } —— LINE Pay 付款完成導回後呼叫；伺服器向金流方確認，不信用戶端回報。
        app.MapPost("/api/v1/{club}/shop/orders/{orderNo}/confirm", async (
            string club, string orderNo, ConfirmShopOrderRequest request, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members,
            ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.ConfirmAsync(scope, Access(http, await members.TryAsync(http, ct)), orderNo, request.TransactionId, Lang(lang), ct));
        })
        .WithName("ConfirmShopOrder").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopOrderDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status429TooManyRequests);

        // POST /shop/orders/{orderNo}/cancel —— 只能取消「待付款」（LINE Pay 取消導回頁呼叫）；釋回庫存。
        app.MapPost("/api/v1/{club}/shop/orders/{orderNo}/cancel", async (
            string club, string orderNo, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.CancelAsync(scope, Access(http, await members.TryAsync(http, ct)), orderNo, Lang(lang), ct));
        })
        .WithName("CancelShopOrder").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<ShopOrderDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status429TooManyRequests);

        // POST /shop/orders/lookup —— 前台 /order/lookup：{ orderNo, email } 或 { token }。找不到／Email 不符一律 404。
        app.MapPost("/api/v1/{club}/shop/orders/lookup", async (
            string club, LookupShopOrderRequest request, string? lang, HttpContext http, IClubResolver clubs, ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.LookupAsync(scope, request, Lang(lang), ct));
        })
        .WithName("LookupShopOrder").WithTags("Shop").RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<ShopOrderDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status429TooManyRequests);

        // GET /shop/my-orders —— 會員中心「我的訂單」（需登入；只回本人在這個俱樂部的訂單）。
        app.MapGet("/api/v1/{club}/shop/my-orders", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator members, ShopOrderService orders, CancellationToken ct) =>
        {
            NoStore(http);
            var me = await members.RequireAsync(http, ct);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await orders.ListMineAsync(scope, me.MemberId, Lang(lang), ct));
        })
        .WithName("ListMyShopOrders").WithTags("Shop")
        .Produces<IReadOnlyList<ShopOrderListItemDto>>().Produces(StatusCodes.Status401Unauthorized);
    }
}
