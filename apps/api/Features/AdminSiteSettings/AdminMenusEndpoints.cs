using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I2 選單管理後台端點。權限碼 <c>site.menu.view／update</c>（module=I、submodule=I2、domain=site、sysadmin_only）。
/// 路由掛在 <c>{club}</c> 下：兩個俱樂部各有一份選單。</summary>
public static class AdminMenusEndpoints
{
    private const string PermissionView = "site.menu.view";
    private const string PermissionUpdate = "site.menu.update";

    public static void MapAdminMenusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/menus")
            .WithTags("AdminMenus")
            .WithDescription("I2 選單管理（主選單、Mega Menu、頁尾選單），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMenusRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        })
        .WithName("AdminGetMenus").Produces<AdminMenusDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        // PUT /menus/{location}  location：main／mega／footer；一次取代該位置的整棵樹。
        group.MapPut("/{location}", async (
            string club, string location, UpdateAdminMenuRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMenusRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.ReplaceAsync(scope, location.Trim().ToLowerInvariant(), request, scope.Identity.AdminUserId, cancellationToken));
        })
        .WithName("AdminReplaceMenu").Produces<AdminMenuLocationDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
