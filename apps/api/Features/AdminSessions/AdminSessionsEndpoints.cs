using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSessions;

/// <summary>P2「梯次與場次」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=P、
/// submodule_code=P2、domain=program。沒有圖片欄位，一般 JSON 請求即可，不需要 multipart。</summary>
public static class AdminSessionsEndpoints
{
    private const string PermissionView = "program.session.view";
    private const string PermissionCreate = "program.session.create";
    private const string PermissionUpdate = "program.session.update";

    public static void MapAdminSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/program-sessions")
            .WithTags("AdminSessions")
            .WithDescription("P2 俱樂部範圍的梯次與場次維護，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/program-sessions?programId=
        group.MapGet("", async (
            string club, Guid? programId, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminSessionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, programId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListProgramSessions")
        .Produces<IReadOnlyList<AdminSessionListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSessionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var session = await repository.GetByIdAsync(scope, id, cancellationToken);
            return session is null ? Results.NotFound() : Results.Ok(session);
        })
        .WithName("AdminGetProgramSession")
        .Produces<AdminSessionDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminSessionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminSessionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, Guid.NewGuid(), request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/program-sessions/{created.Id}", created);
        })
        .WithName("AdminCreateProgramSession")
        .Produces<AdminSessionDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminSessionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminSessionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateProgramSession")
        .Produces<AdminSessionDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
