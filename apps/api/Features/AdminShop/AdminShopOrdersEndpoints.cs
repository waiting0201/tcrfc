using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S2 庫存管理、S3 訂單管理、S4 出貨與物流後台端點（主站規劃書 §4.13）。權限碼：<c>shop.inventory.view／update</c>；
/// <c>shop.order.view／create／update／reveal／export</c>（reveal 於 repository 判斷收件人遮罩；export 為 is_restricted）；
/// <c>shop.shipment.view／update</c>。module=S、submodule=S2／S3／S4、domain=shop。全部 JSON（匯入物流單號為 CSV multipart）。
/// 訂單一律限定 <c>selling_club_id</c> 為目前俱樂部；跨俱樂部 id 一律 404。
/// </summary>
public static class AdminShopOrdersEndpoints
{
    public static void MapAdminShopOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        MapInventory(app);
        MapOrders(app);
        MapShipments(app);
    }

    // ═════════════ S2 庫存 ═════════════

    private static void MapInventory(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/inventory")
            .WithTags("AdminShopInventory")
            .WithDescription("S2 庫存管理（可售量、低庫存提醒、進貨／盤點／報損／調整與異動紀錄），需要登入與俱樂部授權。");

        // GET …/inventory?keyword=&productId=&lowStockOnly=&status=&page=&pageSize=
        group.MapGet("", async (
            string club, string? keyword, Guid? productId, bool? lowStockOnly, string? status, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminShopInventoryRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.inventory.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, keyword, productId, lowStockOnly, status, p, ps, cancellationToken));
        }).WithName("AdminListShopInventory").Produces<PagedResult<AdminInventoryItemDto>>();

        // GET …/inventory/movements?variantId=&type=&from=&to=&orderId=&page=&pageSize=
        group.MapGet("/movements", async (
            string club, Guid? variantId, string? type, DateOnly? from, DateOnly? to, Guid? orderId, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminShopInventoryRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.inventory.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListMovementsAsync(scope, variantId, type, from, to, orderId, p, ps, cancellationToken));
        }).WithName("AdminListShopInventoryMovements").Produces<PagedResult<AdminInventoryMovementDto>>();

        group.MapPost("/movements", async (
            string club, CreateAdminInventoryMovementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopInventoryRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.inventory.update", cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/shop/inventory/movements", await repository.CreateMovementAsync(scope, request, cancellationToken));
        }).WithName("AdminCreateShopInventoryMovement").Produces<AdminInventoryMovementResultDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);
    }

    // ═════════════ S3 訂單 ═════════════

    private static void MapOrders(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/orders")
            .WithTags("AdminShopOrders")
            .WithDescription("S3 訂單管理（含狀態動作、人工建立、分帳標記與匯出），需要登入與俱樂部授權。收件人資料依權限遮罩。");

        group.MapGet("", async (
            string club, [AsParameters] OrderListQuery query, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(query.Page, query.PageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, query.ToFilter(), p, ps, cancellationToken));
        }).WithName("AdminListShopOrders").Produces<PagedResult<AdminOrderListItemDto>>();

        // GET …/orders/export?purpose=…（同清單的篩選參數）→ CSV。需要 shop.order.export，須填用途。
        group.MapGet("/export", async (
            string club, [AsParameters] OrderListQuery query, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.export", cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, query.ToFilter(), query.Purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"orders-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).WithName("AdminExportShopOrders").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.view", cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopOrder").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminOrderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.create", cancellationToken);
            var created = await repository.CreateManualAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/shop/orders/{created.Id}", created);
        }).WithName("AdminCreateShopOrder").Produces<AdminOrderDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/notes", async (
            string club, Guid id, UpdateAdminOrderNotesRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            var row = await repository.UpdateNotesAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminUpdateShopOrderNotes").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/settlement", async (
            string club, Guid id, UpdateAdminOrderSettlementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            var row = await repository.UpdateSettlementAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminUpdateShopOrderSettlement").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/batch/settlement", async (
            string club, BatchAdminOrderSettlementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            return Results.Ok(await repository.BatchSettlementAsync(scope, request, cancellationToken));
        }).WithName("AdminBatchShopOrderSettlement").Produces<BatchOperationResultDto>().Produces(StatusCodes.Status400BadRequest);

        // POST …/orders/release-expired —— 把逾時未付款的訂單釋回庫存（目前沒有背景排程，由畫面按鈕或日後排程呼叫）。
        group.MapPost("/release-expired", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopOrdersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            return Results.Ok(await repository.ReleaseExpiredAsync(scope, cancellationToken));
        }).WithName("AdminReleaseExpiredShopOrders").Produces<ReleaseExpiredOrdersResultDto>();

        // ── 狀態動作（每個動作都是帶前置狀態的單句更新，並行重送只會有一個成功）──
        group.MapPost("/{id:guid}/prepare", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken))
            {
                return Results.NotFound();
            }

            await lifecycle.PrepareAsync(id, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminPrepareShopOrder").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (
            string club, Guid id, CancelAdminOrderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.order.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken))
            {
                return Results.NotFound();
            }

            var reason = AdminInput.RequireText(request.Reason, "取消原因", 255);
            await lifecycle.CancelAsync(id, reason, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminCancelShopOrder").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/ship", async (
            string club, Guid id, ShipAdminOrderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken))
            {
                return Results.NotFound();
            }

            await lifecycle.ShipAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminShipShopOrder").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/shipment", async (
            string club, Guid id, UpdateAdminShipmentRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken) || !await lifecycle.UpdateShipmentAsync(id, request, scope.Identity.AdminUserId, cancellationToken))
            {
                return Results.NotFound();
            }

            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminUpdateShopShipment").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/arrival-notified", async (
            string club, Guid id, ArrivalNotifiedRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken))
            {
                return Results.NotFound();
            }

            await lifecycle.ArrivalNotifiedAsync(id, request.PickupDeadlineOn, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminShopOrderArrivalNotified").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopOrdersRepository repository, ShopOrderLifecycle lifecycle, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            if (!await repository.ExistsAsync(scope, id, cancellationToken))
            {
                return Results.NotFound();
            }

            await lifecycle.CompleteAsync(id, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(await repository.DetailAfterActionAsync(scope, id, cancellationToken));
        }).WithName("AdminCompleteShopOrder").Produces<AdminOrderDetailDto>().Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);
    }

    // ═════════════ S4 出貨與物流 ═════════════

    private static void MapShipments(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/shipments")
            .WithTags("AdminShopShipments")
            .WithDescription("S4 出貨與物流（揀貨單、出貨單、批次出貨、物流單號回填），需要登入與俱樂部授權。不串接物流商 API。");

        // GET …/shipments?orderStatus=&deliveryMethod=&pickupStatus=&keyword=&page=&pageSize=
        group.MapGet("", async (
            string club, string? orderStatus, string? deliveryMethod, string? pickupStatus, string? keyword, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminShopShipmentsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, orderStatus, deliveryMethod, pickupStatus, keyword, p, ps, cancellationToken));
        }).WithName("AdminListShopShipments").Produces<PagedResult<AdminShipmentListItemDto>>();

        // GET …/shipments/picking-list?orderIds=<id>&orderIds=<id>（省略＝全部待出貨訂單）
        group.MapGet("/picking-list", async (
            string club, Guid[]? orderIds, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopShipmentsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.view", cancellationToken);
            return Results.Ok(await repository.PickingListAsync(scope, orderIds, cancellationToken));
        }).WithName("AdminShopPickingList").Produces<AdminPickingListDto>();

        // GET …/shipments/dispatch-slips?orderIds=<id>&orderIds=<id>
        group.MapGet("/dispatch-slips", async (
            string club, Guid[]? orderIds, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopShipmentsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.view", cancellationToken);
            return Results.Ok(await repository.DispatchSlipsAsync(scope, orderIds ?? [], cancellationToken));
        }).WithName("AdminShopDispatchSlips").Produces<IReadOnlyList<AdminDispatchSlipDto>>().Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/batch/ship", async (
            string club, BatchShipAdminOrdersRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopShipmentsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            return Results.Ok(await repository.BatchShipAsync(scope, request, cancellationToken));
        }).WithName("AdminBatchShipShopOrders").Produces<BatchOperationResultDto>().Produces(StatusCodes.Status400BadRequest);

        // POST …/shipments/import —— multipart，檔案欄位 file（CSV：訂單編號、物流商、物流單號、門市代碼）。
        group.MapPost("/import", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopShipmentsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.shipment.update", cancellationToken);
            var file = await Features.Uploads.AdminMultipartForm.ReadFileAsync(httpRequest, "file", cancellationToken);
            if (file.Length is 0 or > 2 * 1024 * 1024)
            {
                throw new AdminValidationException("CSV 檔案不可為空，且不可超過 2 MB。");
            }

            using var reader = new StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return Results.Ok(await repository.ImportTrackingAsync(scope, text, cancellationToken));
        }).WithName("AdminImportShopTracking").Produces<AdminShipmentImportResultDto>().Produces(StatusCodes.Status400BadRequest).DisableAntiforgery();
    }
}

/// <summary>訂單清單與匯出共用的查詢參數。</summary>
public sealed record OrderListQuery(
    string? PaymentStatus, string? OrderStatus, string? DeliveryMethod, bool? IsMember, DateOnly? From, DateOnly? To, string? Keyword,
    string? SettlementStatus, string? PaymentMethod, int? Page, int? PageSize, string? Purpose)
{
    public OrderFilter ToFilter() => new(PaymentStatus, OrderStatus, DeliveryMethod, IsMember, From, To, Keyword, SettlementStatus, PaymentMethod);
}
