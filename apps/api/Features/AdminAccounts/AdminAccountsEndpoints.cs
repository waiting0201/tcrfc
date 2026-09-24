using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAccounts;

/// <summary>
/// J1 帳號管理 ＋ J4 的「後台帳號的俱樂部授權」端點。全域端點（不含 <c>{club}</c> 路由段）——
/// 帳號、角色、俱樂部授權本身都不是「某一個俱樂部的資料」，改用 <see cref="IAdminSystemAuthorizer"/>，
/// 不是 <see cref="IAdminClubAuthorizer"/>（見該介面上的說明）。
///
/// 權限碼命名對應 docs/12b-database-tables.md §7.3，module_code=J，submodule J1（帳號）／
/// J4（俱樂部授權與球隊授權，掛在帳號底下維護），全部 <c>sysadmin_only=true</c>——docs/12b §7.2
/// 的十個角色矩陣（規劃書 §6）裡「系統」欄只有系統管理員打勾，其餘全部是「—」或「✗」，這些
/// 權限碼本來就只該落在超管身上（見 db/seed/generate-club-seed-sql.py「18.2 permissions」既有
/// 的種子資料）。
///
/// 🔴 球隊授權（<c>system.team_grant.*</c>）用**獨立於俱樂部授權（<c>system.club_grant.*</c>）
/// 之外的權限碼**，不是共用同一組——理由見 apps/api/README.md「球隊授權（AdminUserTeam）」整節：
/// 兩者是主站規劃書第 1223–1231 行 J4 表格裡並列的兩件事（「俱樂部與球隊授權」），資源本身也不同
/// （`admin_user_clubs` vs `admin_user_teams`），拆開才能在日後某個角色只需要其中一種時單獨授予。
/// </summary>
public static class AdminAccountsEndpoints
{
    private const string PermissionView = "system.account.view";
    private const string PermissionCreate = "system.account.create";
    private const string PermissionUpdate = "system.account.update";
    private const string PermissionGrantView = "system.club_grant.view";
    private const string PermissionGrantUpdate = "system.club_grant.update";
    private const string PermissionTeamGrantView = "system.team_grant.view";
    private const string PermissionTeamGrantUpdate = "system.team_grant.update";

    public static void MapAdminAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/accounts")
            .WithTags("AdminAccounts")
            .WithDescription("J1 後台帳號管理，需要登入且為系統管理員（system.account.* 全部 sysadmin_only）。");

        group.MapGet("", async (
            string? status, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListAsync(status, keyword, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListAccounts")
        .Produces<PagedResult<AdminAccountListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var account = await repository.GetByIdAsync(id, cancellationToken);
            return account is null ? Results.NotFound() : Results.Ok(account);
        })
        .WithName("AdminGetAccount")
        .Produces<AdminAccountDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateAdminAccountRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/accounts/{created.Id}", created);
        })
        .WithName("AdminCreateAccount")
        .Produces<AdminAccountDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateAdminAccountRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateAccount")
        .Produces<AdminAccountDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /accounts/{id}/status  { "status": "active" | "disabled" }
        group.MapPost("/{id:guid}/status", async (
            Guid id, SetAdminAccountStatusRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.SetStatusAsync(id, request.Status, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminSetAccountStatus")
        .Produces<AdminAccountDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reset-password", async (
            Guid id, ResetAdminAccountPasswordRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var result = await repository.ResetPasswordAsync(id, request.NewPassword, scope.Identity.AdminUserId, cancellationToken);
            return result is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminResetAccountPassword")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reset-totp", async (
            Guid id, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var result = await repository.ResetTwoFactorAsync(id, scope.Identity.AdminUserId, cancellationToken);
            return result is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminResetAccountTwoFactor")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ── J4：這個帳號的俱樂部授權（admin_user_clubs）───────────────────────────
        group.MapGet("/{id:guid}/club-grants", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionGrantView, cancellationToken);
            var grants = await repository.ListClubGrantsAsync(id, cancellationToken);
            return grants is null ? Results.NotFound() : Results.Ok(grants);
        })
        .WithName("AdminListAccountClubGrants")
        .Produces<IReadOnlyList<AdminAccountClubGrantDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/club-grants", async (
            Guid id, CreateAdminAccountClubGrantRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionGrantUpdate, cancellationToken);
            var grant = await repository.UpsertClubGrantAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return grant is null ? Results.NotFound() : Results.Ok(grant);
        })
        .WithName("AdminUpsertAccountClubGrant")
        .Produces<AdminAccountClubGrantDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /accounts/{id}/club-grants/{clubId} — 撤銷（is_active=false，立即生效）。
        group.MapDelete("/{id:guid}/club-grants/{clubId:guid}", async (
            Guid id, Guid clubId, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionGrantUpdate, cancellationToken);
            var revoked = await repository.RevokeClubGrantAsync(id, clubId, cancellationToken);
            return revoked is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminRevokeAccountClubGrant")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ── J4：這個帳號的球隊授權（admin_user_teams，主站規劃書第 1223–1231 行）───────────
        group.MapGet("/{id:guid}/team-grants", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionTeamGrantView, cancellationToken);
            var grants = await repository.ListTeamGrantsAsync(id, cancellationToken);
            return grants is null ? Results.NotFound() : Results.Ok(grants);
        })
        .WithName("AdminListAccountTeamGrants")
        .Produces<IReadOnlyList<AdminAccountTeamGrantDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/team-grants", async (
            Guid id, CreateAdminAccountTeamGrantRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionTeamGrantUpdate, cancellationToken);
            var grant = await repository.UpsertTeamGrantAsync(id, request, cancellationToken);
            return grant is null ? Results.NotFound() : Results.Ok(grant);
        })
        .WithName("AdminUpsertAccountTeamGrant")
        .Produces<AdminAccountTeamGrantDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /accounts/{id}/team-grants/{teamId} — 撤銷（is_active=false，立即生效）。
        group.MapDelete("/{id:guid}/team-grants/{teamId:guid}", async (
            Guid id, Guid teamId, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminAccountsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionTeamGrantUpdate, cancellationToken);
            var revoked = await repository.RevokeTeamGrantAsync(id, teamId, cancellationToken);
            return revoked is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminRevokeAccountTeamGrant")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
