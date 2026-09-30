using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S5 退貨與退款、S6 商店設定與報表後台端點（主站規劃書 §4.13）。權限碼：<c>shop.refund.view／update</c>、
/// <c>shop.refund.execute</c>（<b>sysadmin_only</b>）；<c>shop.setting.view／update</c>、<c>shop.credential.view／update</c>
/// （<b>sysadmin_only</b>＋受限）、<c>shop.report.view</c>、<c>shop.report.export</c>（受限）；<c>shop.donation_code.*</c>（全系統共用，
/// 走全域端點）。module=S、submodule=S5／S6、domain=shop。
/// </summary>
public static class AdminShopBackofficeEndpoints
{
    public static void MapAdminShopBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        MapRefunds(app);
        MapSettings(app);
        MapCredentials(app);
        MapReports(app);
        MapDonationCodes(app);
    }

    // ═════════════ S5 退貨與退款 ═════════════

    private static void MapRefunds(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/refunds")
            .WithTags("AdminShopRefunds")
            .WithDescription("S5 退貨與退款案件（審核、驗收、退款執行），需要登入與俱樂部授權。退款執行僅系統管理員。");

        group.MapGet("", async (
            string club, string? status, string? keyword, int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, status, keyword, p, ps, cancellationToken));
        }).WithName("AdminListShopRefunds").Produces<PagedResult<AdminRefundListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.view", cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopRefund").Produces<AdminRefundDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminRefundRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.update", cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/shop/refunds/{created.Id}", created);
        }).WithName("AdminCreateShopRefund").Produces<AdminRefundDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/approve", async (
            string club, Guid id, ReviewAdminRefundRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.update", cancellationToken);
            var row = await repository.ApproveAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminApproveShopRefund").Produces<AdminRefundDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", async (
            string club, Guid id, ReviewAdminRefundRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.update", cancellationToken);
            var row = await repository.RejectAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminRejectShopRefund").Produces<AdminRefundDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/receive", async (
            string club, Guid id, ReceiveAdminRefundRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.update", cancellationToken);
            var row = await repository.ReceiveAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminReceiveShopRefund").Produces<AdminRefundDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // 退款執行：sysadmin_only。金流失敗或尚未串接時回 409，案件退回原本的狀態。
        group.MapPost("/{id:guid}/execute", async (
            string club, Guid id, ExecuteAdminRefundRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopRefundsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.refund.execute", cancellationToken);
            var row = await repository.ExecuteAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminExecuteShopRefund").Produces<AdminRefundExecuteResultDto>().Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    // ═════════════ S6 設定 ═════════════

    private static void MapSettings(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/settings")
            .WithTags("AdminShopSettings")
            .WithDescription("S6 商店設定（運費、庫存門檻、入口與政策內容），運費為俱樂部層級。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.setting.view", cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        }).WithName("AdminGetShopSettings").Produces<AdminShopSettingsDto>();

        group.MapPut("", async (
            string club, UpdateAdminShopSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.setting.update", cancellationToken);
            return Results.Ok(await repository.UpdateAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateShopSettings").Produces<AdminShopSettingsDto>().Produces(StatusCodes.Status400BadRequest);
    }

    // ═════════════ S6 憑證（僅系統管理員）═════════════

    private static void MapCredentials(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/credentials")
            .WithTags("AdminShopCredentials")
            .WithDescription("S6 金流與電子發票憑證，僅系統管理員。憑證屬於收款主體俱樂部；密鑰永遠不回傳。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopCredentialsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, "shop.credential.view", cancellationToken);
            return Results.Ok(await repository.GetAsync(cancellationToken));
        }).WithName("AdminGetShopCredentials").Produces<AdminShopCredentialsDto>().Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/linepay", async (
            string club, UpdateAdminLinePayCredentialRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCredentialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.credential.update", cancellationToken);
            return Results.Ok(await repository.UpdateLinePayAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateShopLinePayCredential").Produces<AdminShopCredentialsDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/einvoice", async (
            string club, UpdateAdminEInvoiceCredentialRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCredentialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.credential.update", cancellationToken);
            return Results.Ok(await repository.UpdateEInvoiceAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateShopEInvoiceCredential").Produces<AdminShopCredentialsDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/mode", async (
            string club, UpdateAdminPaymentModeRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCredentialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.credential.update", cancellationToken);
            return Results.Ok(await repository.UpdateModeAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateShopPaymentMode").Produces<AdminShopCredentialsDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/invoice-retry", async (
            string club, UpdateAdminInvoiceRetryRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCredentialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.credential.update", cancellationToken);
            return Results.Ok(await repository.UpdateInvoiceRetryAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateShopInvoiceRetry").Produces<AdminShopCredentialsDto>().Produces(StatusCodes.Status400BadRequest);
    }

    // ═════════════ S6 報表 ═════════════

    private static void MapReports(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/reports")
            .WithTags("AdminShopReports")
            .WithDescription("S6 商店報表（營收、訂單數、客單價、熱銷 SKU、庫存、退貨率）與依賣方俱樂部加總。");

        group.MapGet("/summary", async (
            string club, DateOnly? from, DateOnly? to, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopReportsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.report.view", cancellationToken);
            return Results.Ok(await repository.SummaryAsync(scope, from, to, cancellationToken));
        }).WithName("AdminShopReportSummary").Produces<AdminShopReportSummaryDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/by-selling-club", async (
            string club, DateOnly? from, DateOnly? to, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopReportsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.report.view", cancellationToken);
            return Results.Ok(await repository.BySellingClubAsync(scope, from, to, cancellationToken));
        }).WithName("AdminShopReportBySellingClub").Produces<IReadOnlyList<AdminSellingClubTotalDto>>().Produces(StatusCodes.Status400BadRequest);

        // GET …/reports/export?kind=summary｜by-selling-club&from=&to=&purpose=… → CSV。需要 shop.report.export，須填用途。
        group.MapGet("/export", async (
            string club, string? kind, DateOnly? from, DateOnly? to, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopReportsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.report.export", cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, kind, from, to, purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"shop-report-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).WithName("AdminExportShopReport").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);
    }

    // ═════════════ S6 發票捐贈碼（全系統共用，不分俱樂部）═════════════

    private static void MapDonationCodes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/shop/donation-codes")
            .WithTags("AdminShopDonationCodes")
            .WithDescription("S6 電子發票捐贈碼名單，全系統共用，需要登入與對應權限碼（不分俱樂部）。");

        group.MapGet("", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminShopDonationCodesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, "shop.donation_code.view", cancellationToken);
            return Results.Ok(await repository.ListAsync(cancellationToken));
        }).WithName("AdminListShopDonationCodes").Produces<IReadOnlyList<AdminDonationCodeDto>>();

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminShopDonationCodesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, "shop.donation_code.view", cancellationToken);
            var row = await repository.GetAsync(id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopDonationCode").Produces<AdminDonationCodeDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            UpsertAdminDonationCodeRequest request, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminShopDonationCodesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, "shop.donation_code.create", cancellationToken);
            var created = await repository.CreateAsync(request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/shop/donation-codes/{created.Id}", created);
        }).WithName("AdminCreateShopDonationCode").Produces<AdminDonationCodeDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpsertAdminDonationCodeRequest request, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminShopDonationCodesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, "shop.donation_code.update", cancellationToken);
            var row = await repository.UpdateAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminUpdateShopDonationCode").Produces<AdminDonationCodeDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminShopDonationCodesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, "shop.donation_code.delete", cancellationToken);
            return await repository.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteShopDonationCode").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);
    }
}
