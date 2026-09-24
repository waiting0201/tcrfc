using System.Text;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminStandings;

/// <summary>C4「積分榜」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=C、
/// submodule_code=C4、domain=team。⚠️ **不套用 <see cref="TeamRowScope"/>**——見
/// <see cref="AdminStandingsRepository"/> 檔頭「為什麼不套列級授權」的完整說明。</summary>
public static class AdminStandingsEndpoints
{
    private const string PermissionView = "team.standing.view";
    private const string PermissionCreate = "team.standing.create";
    private const string PermissionUpdate = "team.standing.update";
    private const string PermissionDelete = "team.standing.delete";

    public static void MapAdminStandingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/standings")
            .WithTags("AdminStandings")
            .WithDescription("C4 俱樂部範圍的積分榜維護，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, Guid? seasonId, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, seasonId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListStandings")
        .Produces<IReadOnlyList<AdminStandingListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var standing = await repository.GetByIdAsync(scope, id, cancellationToken);
            return standing is null ? Results.NotFound() : Results.Ok(standing);
        })
        .WithName("AdminGetStanding")
        .Produces<AdminStandingDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminStandingRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/standings/{created.Id}", created);
        })
        .WithName("AdminCreateStanding")
        .Produces<AdminStandingDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminStandingRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateStanding")
        .Produces<AdminStandingDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteStanding")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/standings/import  body：CSV 檔案原始位元組（整季替換，
        // 見 AdminStandingsRepository.ImportCsvAsync 檔頭說明）。權限碼用 PermissionUpdate——
        // 語意上是「把整季積分榜換成新內容」，比較貼近更新既有維護中的資料，跟
        // AdminMatchesEndpoints 匯入賽程（純新建賽季賽程）刻意不同。
        group.MapPost("/import", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminStandingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);

            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);

            var result = await repository.ImportCsvAsync(scope, csvContent, scope.Identity.AdminUserId, cancellationToken);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        })
        .WithName("AdminImportStandingsCsv")
        .Produces<StandingCsvImportResultDto>()
        .Produces<StandingCsvImportResultDto>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
