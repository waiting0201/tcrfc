using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>S1 商品與規格後台端點（主站規劃書 §4.13 S1）。權限碼：<c>shop.collection.*</c>（系列）、<c>shop.product.*</c>（商品與圖集）、
/// <c>shop.variant.*</c>（規格與售價）、<c>shop.cost.view／update</c>（成本，is_restricted，於 repository 內判斷）。
/// module=S、submodule=S1、domain=shop。商品圖集一次可上傳多張（multipart 欄位 <c>files</c>）；其餘皆為 JSON。</summary>
public static class AdminShopCatalogEndpoints
{
    public static void MapAdminShopCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        MapCollections(app);
        MapProducts(app);
        MapVariants(app);
    }

    private static void MapCollections(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/collections")
            .WithTags("AdminShopCollections")
            .WithDescription("S1 商品系列（含系列介紹文），需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.view", cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, status, cancellationToken));
        }).WithName("AdminListShopCollections").Produces<IReadOnlyList<AdminCollectionListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.view", cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopCollection").Produces<AdminCollectionDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminCollectionRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.create", cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/shop/collections/{created.Id}", created);
        }).WithName("AdminCreateShopCollection").Produces<AdminCollectionDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminCollectionRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.update", cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateShopCollection").Produces<AdminCollectionDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.delete", cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteShopCollection").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopCollectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.collection.update", cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, cancellationToken);
            return Results.NoContent();
        }).WithName("AdminReorderShopCollections").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapProducts(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/products")
            .WithTags("AdminShopProducts")
            .WithDescription("S1 商品與圖集，需要登入與俱樂部授權。規格、售價與成本依權限回傳。");

        group.MapGet("", async (
            string club, string? status, Guid? collectionId, string? keyword, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.view", cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, status, collectionId, keyword, p, ps, cancellationToken));
        }).WithName("AdminListShopProducts").Produces<PagedResult<AdminProductListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.view", cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopProduct").Produces<AdminProductDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminProductRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.create", cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/shop/products/{created.Id}", created);
        }).WithName("AdminCreateShopProduct").Produces<AdminProductDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminProductRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateShopProduct").Produces<AdminProductDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopProductsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.delete", cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteShopProduct").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, cancellationToken);
            return Results.NoContent();
        }).WithName("AdminReorderShopProducts").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);

        // ── 圖集 ──
        group.MapPost("/{id:guid}/images", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopProductsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            if (!httpRequest.HasFormContentType)
            {
                throw new AdminValidationException("請求格式錯誤，需要 multipart/form-data（檔案欄位 files）。");
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var files = form.Files.GetFiles("files");
            if (files.Count is 0 or > 20)
            {
                throw new AdminValidationException("請選擇 1 到 20 張商品圖片。");
            }

            var tx = new UploadTransaction(images, documents);
            try
            {
                var uploaded = new List<UploadedImageInfo>();
                foreach (var file in files)
                {
                    uploaded.Add(await tx.AddImageAsync("product_images", "image", file, $"{scope.ClubCode}/shop/products/{id}", cancellationToken));
                }

                var result = await repository.AddImagesAsync(scope, id, uploaded, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/shop/products/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminAddShopProductImages").Produces<AdminProductDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapPut("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid id, Guid imageId, UpdateImageAltRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            var result = await repository.UpdateImageAltAsync(scope, id, imageId, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminUpdateShopProductImageAlt").Produces<AdminProductDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid id, Guid imageId, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminShopProductsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteImageAsync(scope, id, imageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteShopProductImage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/images/order", async (
            string club, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.product.update", cancellationToken);
            var result = await repository.ReorderImagesAsync(scope, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminReorderShopProductImages").Produces<AdminProductDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapVariants(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/shop/products/{productId:guid}/variants")
            .WithTags("AdminShopVariants")
            .WithDescription("S1 商品規格（SKU），需要登入與俱樂部授權。成本欄位依權限回傳。");

        group.MapGet("", async (
            string club, Guid productId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.view", cancellationToken);
            var rows = await repository.ListVariantsAsync(scope, productId, cancellationToken);
            return rows is null ? Results.NotFound() : Results.Ok(rows);
        }).WithName("AdminListShopVariants").Produces<IReadOnlyList<AdminVariantDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid productId, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.view", cancellationToken);
            var row = await repository.GetVariantAsync(scope, productId, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetShopVariant").Produces<AdminVariantDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, Guid productId, UpsertAdminVariantRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.create", cancellationToken);
            var created = await repository.CreateVariantAsync(scope, productId, request, cancellationToken);
            return created is null ? Results.NotFound() : Results.Created($"/api/v1/admin/{club}/shop/products/{productId}/variants/{created.Id}", created);
        }).WithName("AdminCreateShopVariant").Produces<AdminVariantDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid productId, Guid id, UpsertAdminVariantRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.update", cancellationToken);
            var updated = await repository.UpdateVariantAsync(scope, productId, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateShopVariant").Produces<AdminVariantDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid productId, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.delete", cancellationToken);
            return await repository.DeleteVariantAsync(scope, productId, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteShopVariant").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (
            string club, Guid productId, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminShopProductsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "shop.variant.update", cancellationToken);
            await repository.ReorderVariantsAsync(scope, productId, request.Ids, cancellationToken);
            return Results.NoContent();
        }).WithName("AdminReorderShopVariants").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }
}
