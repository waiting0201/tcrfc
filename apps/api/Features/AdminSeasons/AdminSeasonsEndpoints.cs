using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeasons;

/// <summary>
/// 賽季管理端點（稽核 A-12）。權限碼比照 C4 賽程與賽果（<c>team.match.*</c>）。
/// 🔴 賽季是俱樂部層級設定，不屬於任何一支球隊：寫入（新增、修改、刪除）另外要求球隊列級授權為「整個俱樂部」
/// （<c>scope_type = all</c>），學院限定或個別球隊的帳號即使有 <c>team.match.*</c> 也不能動賽季。
/// 清單同時接受 <c>team.match.view</c> 與 <c>team.competition.view</c> 任一（賽事系列表單的賽季下拉原本掛在後者）。
/// 取代原本掛在 <c>AdminCompetitions</c> 底下的唯讀 <c>GET .../seasons</c>（網址與欄位 id／code／startOn／endOn 不變，另補 inUse／usage／updatedAt）。
/// </summary>
public static class AdminSeasonsEndpoints
{
    private const string PermissionView = "team.match.view";
    private const string PermissionCreate = "team.match.create";
    private const string PermissionUpdate = "team.match.update";
    private const string PermissionDelete = "team.match.delete";
    private static readonly string[] ViewCandidateCodes = [PermissionView, "team.competition.view"];

    public static void MapAdminSeasonsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seasons")
            .WithTags("AdminSeasons")
            .WithDescription("賽季管理（新增、修改、刪除），俱樂部範圍，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeasonsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCandidateCodes, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, cancellationToken));
        })
        .WithName("AdminListSeasons")
        .Produces<IReadOnlyList<AdminSeasonDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeasonsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCandidateCodes, cancellationToken);
            var season = await repository.GetAsync(scope, id, cancellationToken);
            return season is null ? Results.NotFound() : Results.Ok(season);
        })
        .WithName("AdminGetSeason")
        .Produces<AdminSeasonDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminSeasonRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminSeasonsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            await RequireWholeClubAsync(rowScopeResolver, scope, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/seasons/{created.Id}", created);
        })
        .WithName("AdminCreateSeason")
        .Produces<AdminSeasonDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminSeasonRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminSeasonsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            await RequireWholeClubAsync(rowScopeResolver, scope, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateSeason")
        .Produces<AdminSeasonDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminSeasonsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            await RequireWholeClubAsync(rowScopeResolver, scope, PermissionDelete, cancellationToken);
            var result = await repository.DeleteAsync(scope, id, cancellationToken);
            return result is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteSeason")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task RequireWholeClubAsync(
        IAdminTeamRowScopeResolver resolver, AdminClubScope scope, string permissionCode, CancellationToken cancellationToken)
    {
        var rowScope = await resolver.ResolveAsync(scope, permissionCode, cancellationToken);
        if (!rowScope.IsUnrestricted)
        {
            throw new AdminForbiddenException("賽季是整個俱樂部共用的設定，你的授權範圍只限部分球隊，無法新增、修改或刪除賽季。");
        }
    }
}
