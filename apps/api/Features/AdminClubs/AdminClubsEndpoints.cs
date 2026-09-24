using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminClubs;

/// <summary>J4「俱樂部品牌與法人資料」。全域端點（管的是「有哪些俱樂部」本身），
/// 用 <see cref="IAdminSystemAuthorizer"/>。</summary>
public static class AdminClubsEndpoints
{
    private const string PermissionView = "system.club.view";
    private const string PermissionUpdate = "system.club.update";

    public static void MapAdminClubsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/clubs")
            .WithTags("AdminClubs")
            .WithDescription("J4 俱樂部品牌與法人資料，需要登入且為系統管理員（system.club.* 皆 sysadmin_only）。");

        group.MapGet("", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer, AdminClubsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var clubs = await repository.ListAsync(cancellationToken);
            return Results.Ok(clubs);
        })
        .WithName("AdminListClubs")
        .Produces<IReadOnlyList<AdminClubListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminClubsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var club = await repository.GetByIdAsync(id, cancellationToken);
            return club is null ? Results.NotFound() : Results.Ok(club);
        })
        .WithName("AdminGetClub")
        .Produces<AdminClubDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateAdminClubRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminClubsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var created = await repository.CreateAsync(request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/clubs/{created.Id}", created);
        })
        .WithName("AdminCreateClub")
        .Produces<AdminClubDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateAdminClubRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminClubsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateClub")
        .Produces<AdminClubDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
