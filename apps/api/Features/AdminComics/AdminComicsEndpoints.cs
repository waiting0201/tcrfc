using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminComics;

/// <summary>F1 漫畫管理後台端點（主站規劃書 §4.6 F1）。權限碼：<c>culture.comic.view／create／update／delete</c>。
/// module=F、submodule=F1、domain=culture。🔴 台中藍鯨不設漫畫：授權通過後一律 403（<see cref="FeatureNotAvailableException"/>）。
/// 角色與集數封面是 multipart（<c>payload</c>＋檔案欄位），內頁一次可上傳多張。</summary>
public static class AdminComicsEndpoints
{
    private const string View = "culture.comic.view";
    private const string Create = "culture.comic.create";
    private const string Update = "culture.comic.update";
    private const string Delete = "culture.comic.delete";

    public static void MapAdminComicsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/comic")
            .WithTags("AdminComics")
            .WithDescription("F1 漫畫管理（企劃設定、角色、集數與內頁），需要登入與俱樂部授權；台中藍鯨不設漫畫。");

        // ── 企劃設定 ──
        group.MapGet("/about", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            return Results.Ok(await repository.GetAboutAsync(scope, cancellationToken));
        }).WithName("AdminGetComicAbout").Produces<AdminComicAboutDto>().Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/about", async (
            string club, UpdateAdminComicAboutRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            return Results.Ok(await repository.UpdateAboutAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateComicAbout").Produces<AdminComicAboutDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden);

        MapCharacters(group);
        MapEpisodes(group);
    }

    private static void MapCharacters(RouteGroupBuilder group)
    {
        group.MapGet("/characters", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            return Results.Ok(await repository.ListCharactersAsync(scope, cancellationToken));
        }).WithName("AdminListComicCharacters").Produces<IReadOnlyList<AdminComicCharacterDto>>();

        group.MapGet("/characters/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var row = await repository.GetCharacterAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetComicCharacter").Produces<AdminComicCharacterDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/characters", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Create, cancellationToken);
            AdminComicsRepository.EnsureSupported(scope);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminComicCharacterRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveImage)
            {
                throw new AdminValidationException("新增角色時不能選擇移除圖片。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["image"];
                var image = file is null
                    ? ImageFieldUpdate.Keep
                    : ToUpdate(await tx.AddImageAsync("comic_characters", "image", file, $"{scope.ClubCode}/comic/characters/{id}", cancellationToken));
                var created = await repository.CreateCharacterAsync(scope, id, request, image, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/comic/characters/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateComicCharacter").Produces<AdminComicCharacterDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).DisableAntiforgery();

        group.MapPut("/characters/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            AdminComicsRepository.EnsureSupported(scope);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminComicCharacterRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("comic_characters", "image", "角色圖片", form.Files["image"], request.RemoveImage, $"{scope.ClubCode}/comic/characters/{id}", cancellationToken);
                var updated = await repository.UpdateCharacterAsync(scope, id, request, image, orphans, cancellationToken);
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
        }).WithName("AdminUpdateComicCharacter").Produces<AdminComicCharacterDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/characters/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Delete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteCharacterAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteComicCharacter").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/characters/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            await repository.ReorderCharactersAsync(scope, request.Ids, cancellationToken);
            return Results.NoContent();
        }).WithName("AdminReorderComicCharacters").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapEpisodes(RouteGroupBuilder group)
    {
        group.MapGet("/episodes", async (
            string club, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            return Results.Ok(await repository.ListEpisodesAsync(scope, status, cancellationToken));
        }).WithName("AdminListComicEpisodes").Produces<IReadOnlyList<AdminComicEpisodeListItemDto>>();

        group.MapGet("/episodes/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var row = await repository.GetEpisodeAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetComicEpisode").Produces<AdminComicEpisodeDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/episodes", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Create, cancellationToken);
            AdminComicsRepository.EnsureSupported(scope);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminComicEpisodeRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveCover)
            {
                throw new AdminValidationException("新增集數時不能選擇移除封面。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["cover"];
                var cover = file is null
                    ? ImageFieldUpdate.Keep
                    : ToUpdate(await tx.AddImageAsync("comic_episodes", "cover", file, $"{scope.ClubCode}/comic/episodes/{id}/cover", cancellationToken));
                var created = await repository.CreateEpisodeAsync(scope, id, request, cover, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/comic/episodes/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateComicEpisode").Produces<AdminComicEpisodeDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/episodes/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            AdminComicsRepository.EnsureSupported(scope);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminComicEpisodeRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var cover = await tx.ResolveImageAsync("comic_episodes", "cover", "集數封面", form.Files["cover"], request.RemoveCover, $"{scope.ClubCode}/comic/episodes/{id}/cover", cancellationToken);
                var updated = await repository.UpdateEpisodeAsync(scope, id, request, cover, orphans, cancellationToken);
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
        }).WithName("AdminUpdateComicEpisode").Produces<AdminComicEpisodeDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/episodes/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Delete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteEpisodeAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteComicEpisode").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        // POST …/episodes/{id}/pages —— multipart，檔案欄位 files（可多張，依上傳順序接在既有內頁之後）。
        group.MapPost("/episodes/{id:guid}/pages", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            AdminComicsRepository.EnsureSupported(scope);
            if (!httpRequest.HasFormContentType)
            {
                throw new AdminValidationException("請求格式錯誤，需要 multipart/form-data（檔案欄位 files）。");
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var files = form.Files.GetFiles("files");
            if (files.Count == 0)
            {
                throw new AdminValidationException("請選擇要上傳的內頁圖片。");
            }

            if (files.Count > 60)
            {
                throw new AdminValidationException("一次最多上傳 60 張內頁。");
            }

            var tx = new UploadTransaction(images, documents);
            try
            {
                var uploaded = new List<UploadedImageInfo>();
                foreach (var file in files)
                {
                    uploaded.Add(await tx.AddImageAsync("comic_pages", "image", file, $"{scope.ClubCode}/comic/episodes/{id}/pages", cancellationToken));
                }

                var result = await repository.AddPagesAsync(scope, id, uploaded, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/comic/episodes/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminAddComicPages").Produces<AdminComicEpisodeDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/episodes/{id:guid}/pages/{pageId:guid}", async (
            string club, Guid id, Guid pageId, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeletePageAsync(scope, id, pageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteComicPage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/episodes/{id:guid}/pages/order", async (
            string club, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminComicsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var result = await repository.ReorderPagesAsync(scope, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminReorderComicPages").Produces<AdminComicEpisodeDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound);
    }

    private static ImageFieldUpdate ToUpdate(UploadedImageInfo info) => ImageFieldUpdate.Set(info.Key, info.Width, info.Height);
}
