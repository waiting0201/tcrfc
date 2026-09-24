using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminRoles;

/// <summary>J2 角色與權限。全域端點（不含 <c>{club}</c> 路由段），比照 J1／J4 用
/// <see cref="IAdminSystemAuthorizer"/>。</summary>
public static class AdminRolesEndpoints
{
    private const string PermissionView = "system.role.view";
    private const string PermissionUpdate = "system.role.update";

    public static void MapAdminRolesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/roles")
            .WithTags("AdminRoles")
            .WithDescription("J2 角色與權限，需要登入且為系統管理員（system.role.* 皆 sysadmin_only）。");

        // 權限碼字典——放在角色群組底下（/api/v1/admin/roles/permissions），供角色編輯畫面
        // 組出可勾選的權限清單，不獨立開一個頂層路由前綴。
        group.MapGet("/permissions", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var permissions = await repository.ListPermissionsAsync(cancellationToken);
            return Results.Ok(permissions);
        })
        .WithName("AdminListPermissions")
        .Produces<IReadOnlyList<AdminPermissionDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var roles = await repository.ListRolesAsync(cancellationToken);
            return Results.Ok(roles);
        })
        .WithName("AdminListRoles")
        .Produces<IReadOnlyList<AdminRoleListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var role = await repository.GetByIdAsync(id, cancellationToken);
            return role is null ? Results.NotFound() : Results.Ok(role);
        })
        .WithName("AdminGetRole")
        .Produces<AdminRoleDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateAdminRoleRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var created = await repository.CreateAsync(request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/roles/{created.Id}", created);
        })
        .WithName("AdminCreateRole")
        .Produces<AdminRoleDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateAdminRoleRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateRole")
        .Produces<AdminRoleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteRole")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/permissions", async (
            Guid id, ReplaceRolePermissionsRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminRolesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.ReplacePermissionsAsync(id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminReplaceRolePermissions")
        .Produces<AdminRoleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
