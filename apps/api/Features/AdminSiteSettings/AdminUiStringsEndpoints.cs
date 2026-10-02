using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I4 字串翻譯表後台端點。權限碼 <c>site.string.view／update／translate</c>（sysadmin_only=0，翻譯人員被指派 view＋translate）。
/// 字串是全站共用主檔；路由仍掛 <c>{club}</c> 只是為了沿用俱樂部授權管線（同 <c>AdminVenues</c>）。</summary>
public static class AdminUiStringsEndpoints
{
    private const string PermissionView = "site.string.view";
    private static readonly string[] ViewCodes = [PermissionView, AdminUiStringsRepository.PermissionUpdate, AdminUiStringsRepository.PermissionTranslate];
    private static readonly string[] WriteCodes = [AdminUiStringsRepository.PermissionUpdate, AdminUiStringsRepository.PermissionTranslate];

    public static void MapAdminUiStringsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/i18n/strings")
            .WithTags("AdminUiStrings")
            .WithDescription("I4 介面字串翻譯表（按鈕、表單標籤、提示與錯誤訊息的雙語對照），需要登入。");

        group.MapGet("", async (
            string club, [AsParameters] AdminUiStringListQuery query, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminUiStringsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCodes, cancellationToken);
            return Results.Ok(await repository.ListAsync(query, cancellationToken));
        })
        .WithName("AdminListUiStrings").Produces<PagedResult<AdminUiStringDto>>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapGet("/groups", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminUiStringsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCodes, cancellationToken);
            return Results.Ok(await repository.ListGroupsAsync(cancellationToken));
        })
        .WithName("AdminListUiStringGroups").Produces<IReadOnlyList<string>>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminUiStringRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminUiStringsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, WriteCodes, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/i18n/strings/{created.Id}", created);
        })
        .WithName("AdminCreateUiString").Produces<AdminUiStringDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminUiStringRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminUiStringsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, WriteCodes, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateUiString").Produces<AdminUiStringDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminUiStringsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, WriteCodes, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteUiString").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
