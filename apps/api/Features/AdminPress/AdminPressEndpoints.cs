using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPress;

/// <summary>B6 媒體專區後台端點。權限碼 <c>content.press.*</c>（module=B、submodule=B6、domain=content）。
/// 建立／更新為 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋ <c>file</c>（資源檔案）＋選填 <c>cover</c>（封面圖，
/// 高解析圖類別不需要）。新聞稿與品牌識別包的檔案為 PDF／ZIP；高解析圖為圖片（JPG／PNG／WebP）。</summary>
public static class AdminPressEndpoints
{
    private const string PermissionView = "content.press.view";
    private const string PermissionCreate = "content.press.create";
    private const string PermissionUpdate = "content.press.update";
    private const string PermissionDelete = "content.press.delete";

    public static void MapAdminPressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/press-resources")
            .WithTags("AdminPress")
            .WithDescription("B6 媒體專區後台讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? resourceType, string? status, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, resourceType, status, keyword, p, ps, cancellationToken));
        }).WithName("AdminListPressResources").Produces<PagedResult<AdminPressListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var resource = await repository.GetByIdAsync(scope, id, cancellationToken);
            return resource is null ? Results.NotFound() : Results.Ok(resource);
        }).WithName("AdminGetPressResource").Produces<AdminPressDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPressRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            AdminInput.OneOf(request.ResourceType, AdminPressRepository.ResourceTypes, "類別", "「press_release」（新聞稿）、「brand_kit」（品牌識別包）或「hires_image」（高解析圖）");
            var file = form.Files["file"] ?? throw new AdminValidationException("請選擇要上傳的資源檔案。");
            var coverFile = form.Files["cover"];
            if (coverFile is not null && AdminPressRepository.IsImageType(request.ResourceType))
            {
                throw new AdminValidationException("高解析圖不需要另外上傳封面，系統會直接用圖片本身產生縮圖。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var (fileKey, bytes) = await UploadFileAsync(tx, request.ResourceType, file, scope, id, cancellationToken);
                var cover = coverFile is null ? null : await tx.AddImageAsync("press_resources", "cover", coverFile, $"{scope.ClubCode}/press/{id}/cover", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, fileKey, bytes, cover, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/press-resources/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreatePressResource").Produces<AdminPressDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPressRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            AdminInput.OneOf(request.ResourceType, AdminPressRepository.ResourceTypes, "類別", "「press_release」（新聞稿）、「brand_kit」（品牌識別包）或「hires_image」（高解析圖）");
            var coverFile = form.Files["cover"];
            var isImage = AdminPressRepository.IsImageType(request.ResourceType);
            if (coverFile is not null && isImage)
            {
                throw new AdminValidationException("高解析圖不需要另外上傳封面，系統會直接用圖片本身產生縮圖。");
            }

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                (string Key, long Bytes)? newFile = null;
                var file = form.Files["file"];
                if (file is not null)
                {
                    newFile = await UploadFileAsync(tx, request.ResourceType, file, scope, id, cancellationToken);
                }

                // 高解析圖沒有獨立封面：一律清掉殘留的封面（例如由文件類改成高解析圖）。
                var cover = isImage
                    ? ImageFieldUpdate.Remove
                    : await tx.ResolveImageAsync("press_resources", "cover", "封面", coverFile, request.RemoveCover, $"{scope.ClubCode}/press/{id}/cover", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, newFile, cover, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        }).WithName("AdminUpdatePressResource").Produces<AdminPressDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeletePressResource").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        }).WithName("AdminReorderPressResources").Produces(StatusCodes.Status204NoContent);

        group.MapPost("/batch/show", async (
            string club, BatchIdsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.BatchSetStatusAsync(scope, request.Ids, publish: true, scope.Identity.AdminUserId, cancellationToken));
        }).WithName("AdminBatchShowPressResources").Produces<BatchOperationResultDto>();

        group.MapPost("/batch/hide", async (
            string club, BatchIdsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.BatchSetStatusAsync(scope, request.Ids, publish: false, scope.Identity.AdminUserId, cancellationToken));
        }).WithName("AdminBatchHidePressResources").Produces<BatchOperationResultDto>();

        group.MapPost("/batch/type", async (
            string club, BatchChangePressTypeRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPressRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.BatchChangeTypeAsync(scope, request, scope.Identity.AdminUserId, cancellationToken));
        }).WithName("AdminBatchChangePressResourceType").Produces<BatchOperationResultDto>();
    }

    /// <summary>依類別選擇上傳管線：高解析圖走圖片管線（重新編碼），其餘走檔案儲存（PDF／ZIP，公開容器）。</summary>
    private static async Task<(string Key, long Bytes)> UploadFileAsync(
        UploadTransaction tx, string resourceType, IFormFile file, AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        if (AdminPressRepository.IsImageType(resourceType))
        {
            var image = await tx.AddImageAsync("press_resources", "image", file, $"{scope.ClubCode}/press/{id}/image", cancellationToken);
            return (image.Key, image.SizeBytes);
        }

        var doc = await tx.AddDocumentAsync(DocumentBucket.Public, file, $"{scope.ClubCode}/press/{id}/file", cancellationToken);
        return (doc.Key, doc.SizeBytes);
    }
}
