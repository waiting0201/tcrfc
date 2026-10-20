using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminOpponentTeams;

/// <summary>
/// C4 對手球隊庫後台端點（主站規劃書 v3.31）。權限同賽程與賽果：<c>team.match.view／create／update／delete</c>；
/// 俱樂部範圍（<c>AdminUserClub</c>）；球隊列級授權不適用（對手球隊不是本方球隊）。
/// 新增與修改是 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋選填檔案欄位 <c>crest</c>（隊徽）。
/// </summary>
public static class AdminOpponentTeamsEndpoints
{
    private const string PermissionView = "team.match.view";
    private const string PermissionCreate = "team.match.create";
    private const string PermissionUpdate = "team.match.update";
    private const string PermissionDelete = "team.match.delete";

    public static void MapAdminOpponentTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/opponent-teams")
            .WithTags("AdminOpponentTeams")
            .WithDescription("C4 對手球隊庫（隊名中英、隊徽），俱樂部範圍，權限同賽程與賽果。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminOpponentTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, cancellationToken));
        })
        .WithName("AdminListOpponentTeams")
        .Produces<IReadOnlyList<AdminOpponentTeamListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminOpponentTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var team = await repository.GetAsync(scope, id, cancellationToken);
            return team is null ? Results.NotFound() : Results.Ok(team);
        })
        .WithName("AdminGetOpponentTeam")
        .Produces<AdminOpponentTeamDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminOpponentTeamsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminOpponentTeamRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveCrest)
            {
                throw new Common.AdminValidationException("建立球隊時不能選擇移除隊徽。", "crest");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var crest = await tx.ResolveImageAsync("opponent_teams", "crest", "隊徽", form.Files["crest"], remove: false, $"{scope.ClubCode}/opponent-teams/{id}/crest", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, crest, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/opponent-teams/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminCreateOpponentTeam")
        .Produces<AdminOpponentTeamDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminOpponentTeamsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminOpponentTeamRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var crest = await tx.ResolveImageAsync("opponent_teams", "crest", "隊徽", form.Files["crest"], request.RemoveCrest, $"{scope.ClubCode}/opponent-teams/{id}/crest", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, crest, orphans, scope.Identity.AdminUserId, cancellationToken);
                if (updated is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                await tx.CommitAsync(orphans);
                return Results.Ok(updated);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminUpdateOpponentTeam")
        .Produces<AdminOpponentTeamDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminOpponentTeamsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            var deleted = await repository.DeleteAsync(scope, id, orphans, cancellationToken);
            if (!deleted)
            {
                return Results.NotFound();
            }

            await tx.CommitAsync(orphans); // 資料列刪除成功後才清隊徽物件
            return Results.NoContent();
        })
        .WithName("AdminDeleteOpponentTeam")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }
}
