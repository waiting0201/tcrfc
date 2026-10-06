using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSponsors;

/// <summary>E2 贊助商／贊助方案／贊助活動後台端點。權限碼：<c>business.sponsor.*</c>（贊助商與其活動）、
/// <c>business.sponsor_package.*</c>（贊助方案）。module=E、submodule=E2、domain=business。
/// 贊助商與活動圖集的圖片走 multipart（<c>payload</c>＋檔案欄位），方案為純 JSON。</summary>
public static class AdminSponsorsEndpoints
{
    private const string SponsorView = "business.sponsor.view";
    private const string SponsorCreate = "business.sponsor.create";
    private const string SponsorUpdate = "business.sponsor.update";
    private const string SponsorDelete = "business.sponsor.delete";
    private const string PackageView = "business.sponsor_package.view";
    private const string PackageCreate = "business.sponsor_package.create";
    private const string PackageUpdate = "business.sponsor_package.update";
    private const string PackageDelete = "business.sponsor_package.delete";

    public static void MapAdminSponsorsEndpoints(this IEndpointRouteBuilder app)
    {
        MapSponsors(app);
        MapPackages(app);
        MapActivations(app);
    }

    private static void MapSponsors(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/sponsors")
            .WithTags("AdminSponsors")
            .WithDescription("E2 贊助商後台讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? tier, string? contractStatus, string? keyword, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminSponsorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, tier, contractStatus, keyword, cancellationToken));
        })
        .WithName("AdminListSponsors").Produces<IReadOnlyList<AdminSponsorListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorView, cancellationToken);
            var sponsor = await repository.GetByIdAsync(scope, id, cancellationToken);
            return sponsor is null ? Results.NotFound() : Results.Ok(sponsor);
        })
        .WithName("AdminGetSponsor").Produces<AdminSponsorDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminSponsorRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveLogoDark || request.RemoveLogoLight)
            {
                throw new AdminValidationException("建立贊助商時不能選擇移除 Logo。", "logoLight");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var prefix = $"{scope.ClubCode}/sponsors/{id}";
                var darkFile = form.Files["logoDark"];
                var lightFile = form.Files["logoLight"];
                var dark = darkFile is null ? null : await tx.AddImageAsync("sponsors", "logoDark", darkFile, $"{prefix}/logo-dark", cancellationToken);
                var light = lightFile is null ? null : await tx.AddImageAsync("sponsors", "logoLight", lightFile, $"{prefix}/logo-light", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, dark, light, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/sponsors/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminCreateSponsor").Produces<AdminSponsorDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminSponsorRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var prefix = $"{scope.ClubCode}/sponsors/{id}";
                var dark = await tx.ResolveImageAsync("sponsors", "logoDark", "深色底 Logo", form.Files["logoDark"], request.RemoveLogoDark, $"{prefix}/logo-dark", cancellationToken);
                var light = await tx.ResolveImageAsync("sponsors", "logoLight", "淺色底 Logo", form.Files["logoLight"], request.RemoveLogoLight, $"{prefix}/logo-light", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, dark, light, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        .WithName("AdminUpdateSponsor").Produces<AdminSponsorDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeleteSponsor").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderSponsors").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapPackages(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/sponsor-packages")
            .WithTags("AdminSponsorPackages")
            .WithDescription("E2 贊助方案後台讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, status, cancellationToken));
        })
        .WithName("AdminListSponsorPackages").Produces<IReadOnlyList<AdminSponsorPackageListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageView, cancellationToken);
            var package = await repository.GetByIdAsync(scope, id, cancellationToken);
            return package is null ? Results.NotFound() : Results.Ok(package);
        })
        .WithName("AdminGetSponsorPackage").Produces<AdminSponsorPackageDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminSponsorPackageRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/sponsor-packages/{created.Id}", created);
        })
        .WithName("AdminCreateSponsorPackage").Produces<AdminSponsorPackageDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminSponsorPackageRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateSponsorPackage").Produces<AdminSponsorPackageDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageDelete, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteSponsorPackage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorPackagesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PackageUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderSponsorPackages").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapActivations(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/sponsors/{sponsorId:guid}/activations")
            .WithTags("AdminSponsorActivations")
            .WithDescription("E2 贊助活動（含圖集）後台讀寫，權限碼沿用贊助商（business.sponsor.*）。");

        group.MapGet("", async (
            string club, Guid sponsorId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorView, cancellationToken);
            var list = await repository.ListAsync(scope, sponsorId, cancellationToken);
            return list is null ? Results.NotFound() : Results.Ok(list);
        })
        .WithName("AdminListSponsorActivations").Produces<IReadOnlyList<AdminActivationDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid sponsorId, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorView, cancellationToken);
            var item = await repository.GetAsync(scope, sponsorId, id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .WithName("AdminGetSponsorActivation").Produces<AdminActivationDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, Guid sponsorId, UpsertAdminActivationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var created = await repository.CreateAsync(scope, sponsorId, request, scope.Identity.AdminUserId, cancellationToken);
            return created is null ? Results.NotFound() : Results.Created($"/api/v1/admin/{club}/sponsors/{sponsorId}/activations/{created.Id}", created);
        })
        .WithName("AdminCreateSponsorActivation").Produces<AdminActivationDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid sponsorId, Guid id, UpsertAdminActivationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, sponsorId, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateSponsorActivation").Produces<AdminActivationDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid sponsorId, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, sponsorId, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeleteSponsorActivation").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        // POST .../{id}/images —— multipart，檔案欄位 file（單張，一次一張）。
        group.MapPost("/{id:guid}/images", async (
            string club, Guid sponsorId, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var file = await AdminMultipartForm.ReadFileAsync(httpRequest, "file", cancellationToken);
            var tx = new UploadTransaction(images, documents);
            try
            {
                var info = await tx.AddImageAsync("sponsor_activation_images", "image", file, $"{scope.ClubCode}/sponsor-activations/{id}", cancellationToken);
                var result = await repository.AddImageAsync(scope, sponsorId, id, info, scope.Identity.AdminUserId, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/sponsors/{sponsorId}/activations/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminAddSponsorActivationImage").Produces<AdminActivationDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid sponsorId, Guid id, Guid imageId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteImageAsync(scope, sponsorId, id, imageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeleteSponsorActivationImage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/images/order", async (
            string club, Guid sponsorId, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSponsorActivationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SponsorUpdate, cancellationToken);
            var result = await repository.ReorderImagesAsync(scope, sponsorId, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("AdminReorderSponsorActivationImages").Produces<AdminActivationDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
    }
}
