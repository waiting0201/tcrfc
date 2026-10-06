using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCharity;

/// <summary>
/// B5 慈善與社會影響後台端點（公益團體／慈善計畫／事蹟紀錄／影響力數據／捐款導流設定）。權限碼：
/// <c>charity.content.view／create／update／delete</c>（四個內容子項共用）、<c>charity.setting.view／update</c>
/// （導流與參與方式設定）。module=B、submodule=B5、domain=charity。路徑前綴 <c>/api/v1/admin/{club}/charity/</c>。
/// 建立與更新含圖片者為 <c>multipart/form-data</c>（<c>payload</c>＋檔案欄位），其餘為純 JSON。
/// </summary>
public static class AdminCharityEndpoints
{
    private const string ContentView = "charity.content.view";
    private const string ContentCreate = "charity.content.create";
    private const string ContentUpdate = "charity.content.update";
    private const string ContentDelete = "charity.content.delete";
    private const string SettingView = "charity.setting.view";
    private const string SettingUpdate = "charity.setting.update";

    public static void MapAdminCharityEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/v1/admin/{club}/charity").WithTags("AdminCharity")
            .WithDescription("B5 慈善與社會影響後台讀寫，需要登入與俱樂部授權。");
        MapOrganizations(root);
        MapPrograms(root);
        MapRecords(root);
        MapMetrics(root);
        MapSettings(root);
    }

    private static void MapOrganizations(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/organizations");

        group.MapGet("", async (
            string club, string? keyword, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityOrgsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, keyword, cancellationToken));
        }).WithName("AdminListCharityOrgs").Produces<IReadOnlyList<AdminCharityOrgListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityOrgsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var org = await repository.GetByIdAsync(scope, id, cancellationToken);
            return org is null ? Results.NotFound() : Results.Ok(org);
        }).WithName("AdminGetCharityOrg").Produces<AdminCharityOrgDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityOrgsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminCharityOrgRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var logoFile = form.Files["logo"];
                var logo = logoFile is null ? null : await tx.AddImageAsync("charities", "logo", logoFile, $"{scope.ClubCode}/charities/{id}/logo", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, logo, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/charity/organizations/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateCharityOrg").Produces<AdminCharityOrgDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityOrgsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminCharityOrgRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var logo = await tx.ResolveImageAsync("charities", "logo", "Logo", form.Files["logo"], request.RemoveLogo, $"{scope.ClubCode}/charities/{id}/logo", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, logo, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        }).WithName("AdminUpdateCharityOrg").Produces<AdminCharityOrgDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityOrgsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteCharityOrg").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);
    }

    private static void MapPrograms(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/programs");

        group.MapGet("", async (
            string club, string? status, string? keyword, int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, status, keyword, p, ps, cancellationToken));
        }).WithName("AdminListCharityPrograms").Produces<PagedResult<AdminCharityProgramListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var program = await repository.GetByIdAsync(scope, id, cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(program);
        }).WithName("AdminGetCharityProgram").Produces<AdminCharityProgramDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminCharityProgramRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var coverFile = form.Files["cover"];
                var cover = coverFile is null ? null : await tx.AddImageAsync("charity_programs", "cover", coverFile, $"{scope.ClubCode}/charity-programs/{id}/cover", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, cover, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/charity/programs/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateCharityProgram").Produces<AdminCharityProgramDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminCharityProgramRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var cover = await tx.ResolveImageAsync("charity_programs", "cover", "封面", form.Files["cover"], request.RemoveCover, $"{scope.ClubCode}/charity-programs/{id}/cover", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, cover, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        }).WithName("AdminUpdateCharityProgram").Produces<AdminCharityProgramDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteCharityProgram").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        // 圖集：POST（multipart，檔案欄位 file，一次一張）／DELETE／PUT order。
        group.MapPost("/{id:guid}/images", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var file = await AdminMultipartForm.ReadFileAsync(httpRequest, "file", cancellationToken);
            var tx = new UploadTransaction(images, documents);
            try
            {
                var info = await tx.AddImageAsync("charity_program_images", "image", file, $"{scope.ClubCode}/charity-programs/{id}/gallery", cancellationToken);
                var result = await repository.AddImageAsync(scope, id, info, scope.Identity.AdminUserId, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/charity/programs/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminAddCharityProgramImage").Produces<AdminCharityProgramDetailDto>(StatusCodes.Status201Created).DisableAntiforgery();

        group.MapDelete("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid id, Guid imageId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteImageAsync(scope, id, imageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteCharityProgramImage").Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{id:guid}/images/order", async (
            string club, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharityProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var result = await repository.ReorderImagesAsync(scope, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminReorderCharityProgramImages").Produces<AdminCharityProgramDetailDto>();
    }

    private static void MapRecords(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/records");

        group.MapGet("", async (
            string club, Guid? charityId, Guid? programId, int? year, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminImpactRecordsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, charityId, programId, year, keyword, p, ps, cancellationToken));
        }).WithName("AdminListImpactRecords").Produces<PagedResult<AdminImpactRecordListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var record = await repository.GetByIdAsync(scope, id, cancellationToken);
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).WithName("AdminGetImpactRecord").Produces<AdminImpactRecordDetailDto>().Produces(StatusCodes.Status404NotFound);

        // 建立：payload ＋ 必填的 image（活動圖片）。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentCreate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminImpactRecordRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var file = form.Files["image"] ?? throw new AdminValidationException("事蹟紀錄必須上傳活動圖片。", "image");
            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var info = await tx.AddImageAsync("impact_records", "image", file, $"{scope.ClubCode}/impact-records/{id}/image", cancellationToken);
                var created = await repository.CreateAsync(scope, id, request, info, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/charity/records/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateImpactRecord").Produces<AdminImpactRecordDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminImpactRecordRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("impact_records", "image", "活動圖片", form.Files["image"], false, $"{scope.ClubCode}/impact-records/{id}/image", cancellationToken);
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
        }).WithName("AdminUpdateImpactRecord").Produces<AdminImpactRecordDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteImpactRecord").Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/images", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var file = await AdminMultipartForm.ReadFileAsync(httpRequest, "file", cancellationToken);
            var tx = new UploadTransaction(images, documents);
            try
            {
                var info = await tx.AddImageAsync("impact_record_images", "image", file, $"{scope.ClubCode}/impact-records/{id}/gallery", cancellationToken);
                var result = await repository.AddImageAsync(scope, id, info, scope.Identity.AdminUserId, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/charity/records/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminAddImpactRecordImage").Produces<AdminImpactRecordDetailDto>(StatusCodes.Status201Created).DisableAntiforgery();

        group.MapDelete("/{id:guid}/images/{imageId:guid}", async (
            string club, Guid id, Guid imageId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteImageAsync(scope, id, imageId, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteImpactRecordImage").Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{id:guid}/images/order", async (
            string club, Guid id, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactRecordsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var result = await repository.ReorderImagesAsync(scope, id, request.Ids, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AdminReorderImpactRecordImages").Produces<AdminImpactRecordDetailDto>();
    }

    private static void MapMetrics(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/metrics");

        group.MapGet("", async (
            string club, Guid? programId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactMetricsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, programId, cancellationToken));
        }).WithName("AdminListImpactMetrics").Produces<IReadOnlyList<AdminImpactMetricDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactMetricsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentView, cancellationToken);
            var metric = await repository.GetByIdAsync(scope, id, cancellationToken);
            return metric is null ? Results.NotFound() : Results.Ok(metric);
        }).WithName("AdminGetImpactMetric").Produces<AdminImpactMetricDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminImpactMetricRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactMetricsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/charity/metrics/{created.Id}", created);
        }).WithName("AdminCreateImpactMetric").Produces<AdminImpactMetricDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminImpactMetricRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactMetricsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateImpactMetric").Produces<AdminImpactMetricDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminImpactMetricsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ContentDelete, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteImpactMetric").Produces(StatusCodes.Status204NoContent);
    }

    private static void MapSettings(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/settings");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharitySettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingView, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        }).WithName("AdminGetCharitySettings").Produces<AdminCharitySettingsDto>();

        group.MapPut("", async (
            string club, AdminCharitySettingsDto request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCharitySettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            return Results.Ok(await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken));
        }).WithName("AdminUpdateCharitySettings").Produces<AdminCharitySettingsDto>().Produces(StatusCodes.Status400BadRequest);
    }
}
