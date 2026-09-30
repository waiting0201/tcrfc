using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminHonors;

/// <summary>C5 榮譽與里程碑後台端點。權限碼 <c>team.achievement.*</c>（榮譽，帶球隊列級授權）與
/// <c>team.milestone.*</c>（里程碑），module=C、submodule=C5、domain=team。里程碑建立／更新為 multipart（<c>payload</c>＋選填 <c>image</c>）。</summary>
public static class AdminHonorsEndpoints
{
    private const string AchievementView = "team.achievement.view";
    private const string AchievementCreate = "team.achievement.create";
    private const string AchievementUpdate = "team.achievement.update";
    private const string AchievementDelete = "team.achievement.delete";
    private const string MilestoneView = "team.milestone.view";
    private const string MilestoneCreate = "team.milestone.create";
    private const string MilestoneUpdate = "team.milestone.update";
    private const string MilestoneDelete = "team.milestone.delete";

    public static void MapAdminHonorsEndpoints(this IEndpointRouteBuilder app)
    {
        var achievements = app.MapGroup("/api/v1/admin/{club}/achievements").WithTags("AdminAchievements")
            .WithDescription("C5 榮譽後台讀寫，需要登入、俱樂部授權與球隊列級授權。");

        achievements.MapGet("", async (
            string club, Guid? teamId, Guid? seasonId, int? year, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AchievementView, cancellationToken);
            return Results.Ok(await repository.ListAchievementsAsync(scope, teamId, seasonId, year, cancellationToken));
        }).WithName("AdminListAchievements").Produces<IReadOnlyList<AdminAchievementDto>>();

        achievements.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AchievementView, cancellationToken);
            var item = await repository.GetAchievementAsync(scope, id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).WithName("AdminGetAchievement").Produces<AdminAchievementDto>().Produces(StatusCodes.Status404NotFound);

        achievements.MapPost("", async (
            string club, UpsertAdminAchievementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AchievementCreate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, AchievementCreate, cancellationToken);
            var created = await repository.CreateAchievementAsync(scope, rowScope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/achievements/{created.Id}", created);
        }).WithName("AdminCreateAchievement").Produces<AdminAchievementDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden);

        achievements.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminAchievementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AchievementUpdate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, AchievementUpdate, cancellationToken);
            var updated = await repository.UpdateAchievementAsync(scope, rowScope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateAchievement").Produces<AdminAchievementDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        achievements.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AchievementDelete, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, AchievementDelete, cancellationToken);
            return await repository.DeleteAchievementAsync(scope, rowScope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAchievement").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status403Forbidden);

        var milestones = app.MapGroup("/api/v1/admin/{club}/milestones").WithTags("AdminMilestones")
            .WithDescription("C5 里程碑後台讀寫，需要登入與俱樂部授權。");

        milestones.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MilestoneView, cancellationToken);
            return Results.Ok(await repository.ListMilestonesAsync(scope, cancellationToken));
        }).WithName("AdminListMilestones").Produces<IReadOnlyList<AdminMilestoneDto>>();

        milestones.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MilestoneView, cancellationToken);
            var item = await repository.GetMilestoneAsync(scope, id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).WithName("AdminGetMilestone").Produces<AdminMilestoneDto>().Produces(StatusCodes.Status404NotFound);

        milestones.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MilestoneCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminMilestoneRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["image"];
                var image = file is null ? null : await tx.AddImageAsync("milestones", "image", file, $"{scope.ClubCode}/milestones/{id}/image", cancellationToken);
                var created = await repository.CreateMilestoneAsync(scope, id, request, image, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/milestones/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateMilestone").Produces<AdminMilestoneDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).DisableAntiforgery();

        milestones.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MilestoneUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminMilestoneRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("milestones", "image", "圖片", form.Files["image"], request.RemoveImage, $"{scope.ClubCode}/milestones/{id}/image", cancellationToken);
                var updated = await repository.UpdateMilestoneAsync(scope, id, request, image, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        }).WithName("AdminUpdateMilestone").Produces<AdminMilestoneDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        milestones.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHonorsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MilestoneDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteMilestoneAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteMilestone").Produces(StatusCodes.Status204NoContent);
    }
}
