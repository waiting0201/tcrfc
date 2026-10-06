using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPartners;

/// <summary>E1 合作夥伴後台端點。權限碼 <c>business.partner.*</c>（module=E、submodule=E1、domain=business）。
/// 建立／更新為 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋選填檔案欄位 <c>logoDark</c>／<c>logoLight</c>。</summary>
public static class AdminPartnersEndpoints
{
    private const string PermissionView = "business.partner.view";
    private const string PermissionCreate = "business.partner.create";
    private const string PermissionUpdate = "business.partner.update";
    private const string PermissionDelete = "business.partner.delete";

    public static void MapAdminPartnersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/partners")
            .WithTags("AdminPartners")
            .WithDescription("E1 合作夥伴後台讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? partnerType, string? keyword, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, partnerType, keyword, cancellationToken));
        })
        .WithName("AdminListPartners")
        .Produces<IReadOnlyList<AdminPartnerListItemDto>>();

        group.MapGet("/types", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListTypesAsync(scope, cancellationToken));
        })
        .WithName("AdminListPartnerTypes")
        .Produces<AdminPartnerTypesDto>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var partner = await repository.GetByIdAsync(scope, id, cancellationToken);
            return partner is null ? Results.NotFound() : Results.Ok(partner);
        })
        .WithName("AdminGetPartner")
        .Produces<AdminPartnerDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPartnerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveLogoDark || request.RemoveLogoLight)
            {
                throw new AdminValidationException("建立夥伴時不能選擇移除 Logo。", "logoLight");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var dark = await UploadAsync(tx, "logoDark", form.Files["logoDark"], scope, id, cancellationToken);
                var light = await UploadAsync(tx, "logoLight", form.Files["logoLight"], scope, id, cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, dark, light, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/partners/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminCreatePartner")
        .Produces<AdminPartnerDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPartnerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var prefix = $"{scope.ClubCode}/partners/{id}";
                var dark = await tx.ResolveImageAsync("partners", "logoDark", "深色底 Logo", form.Files["logoDark"], request.RemoveLogoDark, $"{prefix}/logo-dark", cancellationToken);
                var light = await tx.ResolveImageAsync("partners", "logoLight", "淺色底 Logo", form.Files["logoLight"], request.RemoveLogoLight, $"{prefix}/logo-light", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, dark, light, orphans, scope.Identity.AdminUserId, cancellationToken);
                if (updated is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                await tx.CommitAsync(orphans);
                return Results.Ok(updated);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminUpdatePartner")
        .Produces<AdminPartnerDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            var deleted = await repository.DeleteAsync(scope, id, orphans, cancellationToken);
            if (!deleted)
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeletePartner")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderPartners")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<UploadedImageInfo?> UploadAsync(
        UploadTransaction tx, string field, IFormFile? file, AdminClubScope scope, Guid id, CancellationToken cancellationToken)
        => file is null
            ? null
            : await tx.AddImageAsync("partners", field, file, $"{scope.ClubCode}/partners/{id}/{(field == "logoDark" ? "logo-dark" : "logo-light")}", cancellationToken);
}
