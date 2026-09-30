using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminNewsletter;

/// <summary>G3 電子報訂閱名單後台端點。權限碼：<c>form.newsletter.view／update／export</c>（module=G、submodule=G3、domain=enquiry；
/// 匯出為 is_restricted）。全部走俱樂部範圍（<c>{club}</c>），名單兩站各自獨立。</summary>
public static class AdminNewsletterEndpoints
{
    private const string PermissionView = "form.newsletter.view";
    private const string PermissionUpdate = "form.newsletter.update";
    private const string PermissionExport = "form.newsletter.export";

    public static void MapAdminNewsletterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/newsletter")
            .WithTags("AdminNewsletter")
            .WithDescription("G3 電子報訂閱名單，需要登入與俱樂部授權。名單視同個資，匯出須額外授權並填用途。");

        group.MapGet("/subscribers", async (
            string club, [AsParameters] AdminNewsletterListQuery query, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, query, cancellationToken));
        })
        .WithName("AdminListNewsletterSubscribers").Produces<PagedResult<AdminNewsletterSubscriberDto>>();

        group.MapGet("/summary", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.SummaryAsync(scope, cancellationToken));
        })
        .WithName("AdminNewsletterSummary").Produces<AdminNewsletterSummaryDto>();

        group.MapPost("/subscribers", async (
            string club, CreateAdminNewsletterSubscriberRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/newsletter/subscribers/{created.Id}", created);
        })
        .WithName("AdminCreateNewsletterSubscriber").Produces<AdminNewsletterSubscriberDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/subscribers/{id:guid}/status", async (
            string club, Guid id, UpdateAdminNewsletterStatusRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateStatusAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateNewsletterSubscriberStatus").Produces<AdminNewsletterSubscriberDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/subscribers/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteNewsletterSubscriber").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        // GET /subscribers/export?purpose=…（同名單的篩選參數）→ CSV。需要 form.newsletter.export，並須填用途。
        group.MapGet("/export", async (
            string club, [AsParameters] AdminNewsletterListQuery query, string? purpose, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminNewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionExport, cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, query, purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"newsletter-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportNewsletterSubscribers").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/edm", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, INewsletterEdmSync edm, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(new AdminNewsletterEdmStatusDto
            {
                Configured = edm.ProviderName is not null,
                Provider = edm.ProviderName,
                Message = edm.ProviderName is null ? "EDM 平台尚未串接（供應商尚未確定）。" : $"已串接 {edm.ProviderName}。",
            });
        })
        .WithName("AdminNewsletterEdmStatus").Produces<AdminNewsletterEdmStatusDto>();

        group.MapPost("/edm/sync", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminNewsletterRepository repository,
            INewsletterEdmSync edm, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.SyncToEdmAsync(scope, edm, cancellationToken));
        })
        .WithName("AdminNewsletterEdmSync").Produces<AdminNewsletterEdmSyncResultDto>();
    }
}
