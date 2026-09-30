using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminJerseys;

/// <summary>K3 球衣發放後台端點（主站規劃書 §4.11 K3）。權限碼：<c>member.jersey.view／create／update</c>、
/// <c>member.jersey.export</c>（is_restricted）。module=K、submodule=K3、domain=member。</summary>
public static class AdminJerseysEndpoints
{
    private const string PermissionView = "member.jersey.view";
    private const string PermissionCreate = "member.jersey.create";
    private const string PermissionUpdate = "member.jersey.update";
    private const string PermissionExport = "member.jersey.export";

    public static void MapAdminJerseysEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/jerseys")
            .WithTags("AdminJerseys")
            .WithDescription("K3 球衣發放，需要登入與俱樂部授權。收件資訊依權限遮罩。");

        // GET /jerseys?status=&size=&deliveryMethod=&memberId=&membershipId=&keyword=&page=&pageSize=
        group.MapGet("", async (
            string club, string? status, string? size, string? deliveryMethod, Guid? memberId, Guid? membershipId, string? keyword,
            int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, status, size, deliveryMethod, memberId, membershipId, keyword, p, ps, cancellationToken));
        })
        .WithName("AdminListJerseys").Produces<PagedResult<AdminJerseyDto>>();

        group.MapGet("/size-summary", async (
            string club, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.SizeSummaryAsync(scope, status, cancellationToken));
        })
        .WithName("AdminJerseySizeSummary").Produces<IReadOnlyList<AdminJerseySizeSummaryDto>>();

        group.MapGet("/export", async (
            string club, string? status, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionExport, cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, status, purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"jerseys-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportJerseys").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/batch/status", async (
            string club, BatchJerseyStatusRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.BatchStatusAsync(scope, request, cancellationToken));
        })
        .WithName("AdminBatchJerseyStatus").Produces<BatchOperationResultDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var jersey = await repository.GetByIdAsync(scope, id, cancellationToken);
            return jersey is null ? Results.NotFound() : Results.Ok(jersey);
        })
        .WithName("AdminGetJersey").Produces<AdminJerseyDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminJerseyRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/jerseys/{created.Id}", created);
        })
        .WithName("AdminCreateJersey").Produces<AdminJerseyDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminJerseyRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminJerseysRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateJersey").Produces<AdminJerseyDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
