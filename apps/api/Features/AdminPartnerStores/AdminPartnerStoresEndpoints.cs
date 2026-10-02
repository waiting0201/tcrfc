using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPartnerStores;

/// <summary>K4 特約店家後台端點（主站規劃書 §4.11 K4）。權限碼 <c>member.store.*</c>（module=K、submodule=K4、domain=member）。
/// 建立／更新為 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋選填檔案欄位 <c>image</c>。</summary>
public static class AdminPartnerStoresEndpoints
{
    private const string PermissionView = "member.store.view";
    private const string PermissionCreate = "member.store.create";
    private const string PermissionUpdate = "member.store.update";
    private const string PermissionDelete = "member.store.delete";

    public static void MapAdminPartnerStoresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/partner-stores")
            .WithTags("AdminPartnerStores")
            .WithDescription("K4 特約店家維護，需要登入與俱樂部授權。兩隊共同的店家唯讀（僅系統管理員可編輯）。");

        // GET /partner-stores?category=&region=&status=&tier=&keyword=
        group.MapGet("", async (
            string club, string? category, string? region, string? status, string? tier, string? keyword, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPartnerStoresRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, category, region, status, tier, keyword, cancellationToken));
        })
        .WithName("AdminListPartnerStores").Produces<IReadOnlyList<AdminPartnerStoreListItemDto>>();

        group.MapGet("/filters", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListFiltersAsync(scope, cancellationToken));
        })
        .WithName("AdminPartnerStoreFilters").Produces<AdminPartnerStoreFiltersDto>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var store = await repository.GetByIdAsync(scope, id, cancellationToken);
            return store is null ? Results.NotFound() : Results.Ok(store);
        })
        .WithName("AdminGetPartnerStore").Produces<AdminPartnerStoreDetailDto>().Produces(StatusCodes.Status404NotFound);

        // POST /partner-stores/locate —— 「由地址定位」輔助按鈕（S2-5）。只回候選座標，不寫入任何資料；
        // 規劃書：人工確認後才隨店家資料儲存。未設定定位服務回 503，查無此地址回 404（訊息為日常中文）。
        group.MapPost("/locate", async (
            string club, LocatePartnerStoreRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, [PermissionCreate, PermissionUpdate], cancellationToken);
            var result = await repository.LocateAsync(request.Address, cancellationToken);
            return result is null
                ? Results.Json(new { message = "查無此地址的座標，請確認地址是否正確，或直接輸入緯度與經度。" }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new LocatePartnerStoreResponse { Lat = result.Lat, Lng = result.Lng });
        })
        .WithName("AdminLocatePartnerStore").Produces<LocatePartnerStoreResponse>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPartnerStoreRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveImage)
            {
                throw new AdminValidationException("建立店家時不能選擇移除照片。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["image"];
                var image = file is null
                    ? null
                    : await tx.AddImageAsync("partner_stores", "image", file, $"{(request.IsShared ? "shared" : scope.ClubCode)}/partner-stores/{id}", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, image, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/partner-stores/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminCreatePartnerStore").Produces<AdminPartnerStoreDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPartnerStoreRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("partner_stores", "image", "店家照片", form.Files["image"], request.RemoveImage,
                    $"{scope.ClubCode}/partner-stores/{id}", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, image, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        .WithName("AdminUpdatePartnerStore").Produces<AdminPartnerStoreDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeletePartnerStore").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPartnerStoresRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderPartnerStores").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }
}
