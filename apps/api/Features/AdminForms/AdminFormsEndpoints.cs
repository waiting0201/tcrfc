using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminForms;

/// <summary>G1「表單設計器」後台端點。權限碼命名照 docs/12b §7.3：module_code=G、submodule_code=G1、
/// domain=enquiry（表單與詢問共用同一個 domain，見 db/seed/generate-club-seed-sql.py 對應段落）。
/// **沒有建立／刪除表單本身的端點**——9 個 <c>form_code</c> 是固定目錄，見
/// <c>AdminFormsRepository</c> 檔頭。</summary>
public static class AdminFormsEndpoints
{
    private const string PermissionView = "form.view";
    private const string PermissionUpdate = "form.update";

    public static void MapAdminFormsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/forms")
            .WithTags("AdminForms")
            .WithDescription("G1 俱樂部範圍的表單設計器，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListForms")
        .Produces<IReadOnlyList<AdminFormListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var form = await repository.GetByIdAsync(scope, id, cancellationToken);
            return form is null ? Results.NotFound() : Results.Ok(form);
        })
        .WithName("AdminGetForm")
        .Produces<AdminFormDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminFormRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateForm")
        .Produces<AdminFormDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/fields", async (
            string club, Guid id, CreateAdminFormFieldRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var created = await repository.CreateFieldAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return created is null ? Results.NotFound() : Results.Created($"/api/v1/admin/{club}/forms/{id}/fields/{created.Id}", created);
        })
        .WithName("AdminCreateFormField")
        .Produces<AdminFormFieldDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/fields/{fieldId:guid}", async (
            string club, Guid id, Guid fieldId, UpdateAdminFormFieldRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateFieldAsync(scope, id, fieldId, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateFormField")
        .Produces<AdminFormFieldDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}/fields/{fieldId:guid}", async (
            string club, Guid id, Guid fieldId, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFormsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.DeleteFieldAsync(scope, id, fieldId, cancellationToken);
            return result is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteFormField")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
