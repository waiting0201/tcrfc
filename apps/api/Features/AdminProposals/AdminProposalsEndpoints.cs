using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminProposals;

/// <summary>E3 提案簡介與 Lead 名單後台端點。權限碼：<c>business.proposal.*</c>（提案與檔案）、
/// <c>business.lead.view／update／export</c>（Lead 名單；export 為受限碼）。module=E、submodule=E3、domain=business。</summary>
public static class AdminProposalsEndpoints
{
    private const string ProposalView = "business.proposal.view";
    private const string ProposalCreate = "business.proposal.create";
    private const string ProposalUpdate = "business.proposal.update";
    private const string ProposalDelete = "business.proposal.delete";
    private const string LeadView = "business.lead.view";
    private const string LeadUpdate = "business.lead.update";
    private const string LeadExport = "business.lead.export";

    public static void MapAdminProposalsEndpoints(this IEndpointRouteBuilder app)
    {
        MapProposals(app);
        MapLeads(app);
    }

    private static void MapProposals(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/proposals")
            .WithTags("AdminProposals")
            .WithDescription("E3 提案簡介後台讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, cancellationToken));
        })
        .WithName("AdminListProposals").Produces<IReadOnlyList<AdminProposalListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalView, cancellationToken);
            var proposal = await repository.GetByIdAsync(scope, id, cancellationToken);
            return proposal is null ? Results.NotFound() : Results.Ok(proposal);
        })
        .WithName("AdminGetProposal").Produces<AdminProposalDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminProposalRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/proposals/{created.Id}", created);
        })
        .WithName("AdminCreateProposal").Produces<AdminProposalDetailDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminProposalRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateProposal").Produces<AdminProposalDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalDelete, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        })
        .WithName("AdminDeleteProposal").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        // POST /proposals/{id}/files —— multipart：payload（locale／versionNo）＋ file（PDF 或 ZIP）。
        group.MapPost("/{id:guid}/files", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<AddAdminProposalFileRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            AdminProposalsRepository.ToDbLocale(request.Locale); // fail fast：先驗證再上傳。
            var file = form.Files["file"] ?? throw new AdminValidationException("請選擇要上傳的提案檔案。", "file");

            var tx = new UploadTransaction(images, documents);
            try
            {
                var uploaded = await tx.AddDocumentAsync(DocumentBucket.Private, file, $"{scope.ClubCode}/proposals/{id}", cancellationToken);
                var result = await repository.AddFileAsync(scope, id, request, uploaded, scope.Identity.AdminUserId, cancellationToken);
                if (result is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                return Results.Created($"/api/v1/admin/{club}/proposals/{id}", result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminAddProposalFile").Produces<AdminProposalDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}/files/{fileId:guid}", async (
            string club, Guid id, Guid fileId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalUpdate, cancellationToken);
            var orphans = new OrphanedObjects();
            var result = await repository.DeleteFileAsync(scope, id, fileId, orphans, scope.Identity.AdminUserId, cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.Ok(result);
        })
        .WithName("AdminDeleteProposalFile").Produces<AdminProposalDetailDto>().Produces(StatusCodes.Status404NotFound);

        // 後台預覽／下載自己上傳的檔案（不寫 Lead、不受前台表單關卡限制）。
        group.MapGet("/{id:guid}/files/{fileId:guid}/download", async (
            string club, Guid id, Guid fileId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProposalsRepository repository, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, ProposalView, cancellationToken);
            var file = await repository.FindFileAsync(scope, id, fileId, cancellationToken);
            if (file is null)
            {
                return Results.NotFound();
            }

            var read = await documents.OpenReadAsync(DocumentBucket.Private, file.FileKey, cancellationToken);
            if (read is null)
            {
                return Results.NotFound();
            }

            var extension = Path.GetExtension(file.FileKey);
            return Results.Stream(read.Content, read.ContentType, $"proposal-{fileId:N}{extension}");
        })
        .WithName("AdminDownloadProposalFile").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound);
    }

    private static void MapLeads(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/proposal-leads")
            .WithTags("AdminProposalLeads")
            .WithDescription("E3 提案下載 Lead 名單，需要登入與俱樂部授權。含個資（公司、姓名、Email）。");

        // GET ?proposalId=&status=&keyword=&dateFrom=&dateTo=&page=&pageSize=
        group.MapGet("", async (
            string club, Guid? proposalId, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminLeadsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, LeadView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, proposalId, status, keyword, dateFrom, dateTo, p, ps, cancellationToken));
        })
        .WithName("AdminListProposalLeads").Produces<PagedResult<AdminLeadListItemDto>>();

        group.MapGet("/export", async (
            string club, Guid? proposalId, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminLeadsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, LeadExport, cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, proposalId, status, keyword, dateFrom, dateTo, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"proposal-leads-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportProposalLeads").Produces(StatusCodes.Status200OK);

        group.MapGet("/assignable-users", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminLeadsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, LeadUpdate, cancellationToken);
            return Results.Ok(await repository.ListAssignableUsersAsync(scope, cancellationToken));
        })
        .WithName("AdminListLeadAssignableUsers").Produces<IReadOnlyList<AdminLeadAssigneeDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminLeadsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, LeadView, cancellationToken);
            var lead = await repository.GetByIdAsync(scope, id, cancellationToken);
            return lead is null ? Results.NotFound() : Results.Ok(lead);
        })
        .WithName("AdminGetProposalLead").Produces<AdminLeadDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminLeadRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminLeadsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, LeadUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateProposalLead").Produces<AdminLeadDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}
