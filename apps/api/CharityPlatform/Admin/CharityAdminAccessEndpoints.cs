using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 慈善後台帳號與角色管理端點（<c>/api/v1/donation-platform/admin/accounts</c>、<c>/roles</c>）。路徑與形狀比照主站
/// <c>/api/v1/admin/accounts</c>、<c>/api/v1/admin/roles</c>（去掉俱樂部授權端點，慈善沒有這個維度）。
/// 🔴 權限一律是 <c>sysadmin_only</c> 的 <c>n7.admin_account.*</c>／<c>n7.admin_role.*</c>（比照主站 <c>system.account.*</c>／<c>system.role.*</c>）。
/// </summary>
public static class CharityAdminAccessEndpoints
{
    public static void MapCharityAdminAccessEndpoints(this IEndpointRouteBuilder admin)
    {
        MapAccounts(admin.MapGroup("/accounts"));
        MapRoles(admin.MapGroup("/roles"));
    }

    private static void MapAccounts(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? status, string? keyword, int? page, int? pageSize, HttpContext http,
            ICharityAdminAuthorizer authorizer, CharityAdminAccountsService service, CancellationToken ct) =>
        {
            await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountView, ct);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await service.ListAsync(status, keyword, p, ps, ct));
        }).WithName("CharityAdminListAccounts").Produces<PagedResult<CharityAdminAccountListItemDto>>();

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminAccountsService service, CancellationToken ct) =>
        {
            await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountView, ct);
            var account = await service.GetByIdAsync(id, ct);
            return account is null ? Results.NotFound() : Results.Ok(account);
        }).WithName("CharityAdminGetAccount").Produces<CharityAdminAccountDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateCharityAdminAccountRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminAccountsService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountManage, ct);
            var created = await service.CreateAsync(scope, request, ClientIpResolver.Resolve(http), ct);
            return Results.Created($"/api/v1/donation-platform/admin/accounts/{created.Id}", created);
        }).WithName("CharityAdminCreateAccount").Produces<CharityAdminAccountDetailDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateCharityAdminAccountRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminAccountsService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountManage, ct);
            var updated = await service.UpdateAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminUpdateAccount").Produces<CharityAdminAccountDetailDto>();

        group.MapPost("/{id:guid}/status", async (
            Guid id, SetCharityAdminAccountStatusRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminAccountsService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountManage, ct);
            var updated = await service.SetStatusAsync(scope, id, request.Status, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminSetAccountStatus").Produces<CharityAdminAccountDetailDto>();

        group.MapPost("/{id:guid}/reset-password", async (
            Guid id, ResetCharityAdminAccountPasswordRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminAccountsService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountManage, ct);
            var result = await service.ResetPasswordAsync(scope, id, request.NewPassword, ClientIpResolver.Resolve(http), ct);
            return result is null ? Results.NotFound() : Results.NoContent();
        }).WithName("CharityAdminResetAccountPassword").Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/reset-totp", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminAccountsService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminAccountManage, ct);
            var result = await service.ResetTwoFactorAsync(scope, id, ClientIpResolver.Resolve(http), ct);
            return result is null ? Results.NotFound() : Results.NoContent();
        }).WithName("CharityAdminResetAccountTwoFactor").Produces(StatusCodes.Status204NoContent);
    }

    private static void MapRoles(RouteGroupBuilder group)
    {
        group.MapGet("/permissions", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminRolesService service, CancellationToken ct) =>
        {
            await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleView, ct);
            return Results.Ok(await service.ListPermissionsAsync(ct));
        }).WithName("CharityAdminListPermissions").Produces<IReadOnlyList<CharityAdminPermissionDto>>();

        group.MapGet("", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminRolesService service, CancellationToken ct) =>
        {
            await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleView, ct);
            return Results.Ok(await service.ListRolesAsync(ct));
        }).WithName("CharityAdminListRoles").Produces<IReadOnlyList<CharityAdminRoleListItemDto>>();

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminRolesService service, CancellationToken ct) =>
        {
            await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleView, ct);
            var role = await service.GetByIdAsync(id, ct);
            return role is null ? Results.NotFound() : Results.Ok(role);
        }).WithName("CharityAdminGetRole").Produces<CharityAdminRoleDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateCharityAdminRoleRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminRolesService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleManage, ct);
            var created = await service.CreateAsync(scope, request, ClientIpResolver.Resolve(http), ct);
            return Results.Created($"/api/v1/donation-platform/admin/roles/{created.Id}", created);
        }).WithName("CharityAdminCreateRole").Produces<CharityAdminRoleDetailDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateCharityAdminRoleRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminRolesService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleManage, ct);
            var updated = await service.UpdateAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminUpdateRole").Produces<CharityAdminRoleDetailDto>();

        group.MapDelete("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityAdminRolesService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleManage, ct);
            var deleted = await service.DeleteAsync(scope, id, ClientIpResolver.Resolve(http), ct);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        }).WithName("CharityAdminDeleteRole").Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{id:guid}/permissions", async (
            Guid id, ReplaceCharityRolePermissionsRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityAdminRolesService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AdminRoleManage, ct);
            var updated = await service.ReplacePermissionsAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminReplaceRolePermissions").Produces<CharityAdminRoleDetailDto>();
    }
}
