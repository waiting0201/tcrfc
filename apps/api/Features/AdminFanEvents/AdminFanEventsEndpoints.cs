using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFanEvents;

/// <summary>F2 球迷會活動後台端點（主站規劃書 §4.6 F2）。權限碼：<c>culture.fan_event.view／create／update／delete</c>
/// （module=F、submodule=F2、domain=culture）。活動含封面圖是 multipart（<c>payload</c>＋檔案欄位 <c>cover</c>）；
/// 活動回顧圖集一次可上傳多張。報名名單的個資依 <c>member.pii.reveal</c> 遮罩。</summary>
public static class AdminFanEventsEndpoints
{
    private const string View = "culture.fan_event.view";
    private const string Create = "culture.fan_event.create";
    private const string Update = "culture.fan_event.update";
    private const string Delete = "culture.fan_event.delete";

    public static void MapAdminFanEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/fan-events")
            .WithTags("AdminFanEvents")
            .WithDescription("F2 球迷會活動（含報名名單與活動回顧），需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? status, DateOnly? from, DateOnly? to, string? keyword, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFanEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, status, from, to, keyword, cancellationToken));
        }).WithName("AdminListFanEvents").Produces<IReadOnlyList<AdminFanEventListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetFanEvent").Produces<AdminFanEventDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Create, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminFanEventRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveCover)
            {
                throw new AdminValidationException("新增活動時不能選擇移除封面。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["cover"];
                var cover = file is null
                    ? ImageFieldUpdate.Keep
                    : ToUpdate(await tx.AddImageAsync("fan_events", "cover", file, $"{scope.ClubCode}/fan-events/{id}/cover", cancellationToken));
                var created = await repository.CreateAsync(scope, id, request, cover, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/fan-events/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateFanEvent").Produces<AdminFanEventDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminFanEventRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var cover = await tx.ResolveImageAsync("fan_events", "cover", "活動封面", form.Files["cover"], request.RemoveCover, $"{scope.ClubCode}/fan-events/{id}/cover", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, cover, orphans, cancellationToken);
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
        }).WithName("AdminUpdateFanEvent").Produces<AdminFanEventDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Delete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteFanEvent").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        // ── 活動回顧圖集 ──
        group.MapPost("/{id:guid}/images", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            if (!httpRequest.HasFormContentType)
            {
                throw new AdminValidationException("請求格式錯誤，需要 multipart/form-data（檔案欄位 files）。");
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var files = form.Files.GetFiles("files");
            if (files.Count == 0)
            {
                throw new AdminValidationException("請選擇要上傳的圖片。");
            }

            if (files.Count > 40)
            {
                throw new AdminValidationException("一次最多上傳 40 張圖片。");
            }

            var tx = new UploadTransaction(images, documents);
            try
            {
                var uploaded = new List<UploadedImageInfo>();
                foreach (var file in files)
                {
                    uploaded.Add(await tx.AddImageAsync("fan_event_images", "image", file, $"{scope.ClubCode}/fan-events/{id}/gallery", cancellationToken));
                }

                var result = await repository.AddImagesAsync(scope, id, uploaded, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/fan-events/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminAddFanEventImages").Produces<AdminFanEventDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid id, Guid imageId, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteImageAsync(scope, id, imageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteFanEventImage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/images/order", async (
            string club, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFanEventsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var result = await repository.ReorderImagesAsync(scope, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminReorderFanEventImages").Produces<AdminFanEventDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound);

        // ── 報名名單 ──
        group.MapGet("/{id:guid}/registrations", async (
            string club, Guid id, string? status, string? keyword, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFanEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var rows = await repository.ListRegistrationsAsync(scope, id, status, keyword, cancellationToken);
            return rows is null ? Results.NotFound() : Results.Ok(rows);
        }).WithName("AdminListFanEventRegistrations").Produces<IReadOnlyList<AdminFanEventRegistrationDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/registrations", async (
            string club, Guid id, CreateAdminFanEventRegistrationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFanEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var created = await repository.CreateRegistrationAsync(scope, id, request, cancellationToken);
            return created is null ? Results.NotFound() : Results.Created($"/api/v1/admin/{club}/fan-events/{id}/registrations", created);
        }).WithName("AdminCreateFanEventRegistration").Produces<AdminFanEventRegistrationDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/registrations/{registrationId:guid}", async (
            string club, Guid id, Guid registrationId, UpdateAdminFanEventRegistrationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFanEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var updated = await repository.UpdateRegistrationAsync(scope, id, registrationId, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateFanEventRegistration").Produces<AdminFanEventRegistrationDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    private static ImageFieldUpdate ToUpdate(UploadedImageInfo info) => ImageFieldUpdate.Set(info.Key, info.Width, info.Height);
}
