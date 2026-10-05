using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminTrials;

/// <summary>P4 試訓場次與報名名單後台端點（主站規劃書 §4.4 P4）。權限碼（module=P、submodule=P4、domain=program）：
/// <c>program.trial.view／create／update／delete</c>（場次）、
/// <c>program.trial_registration.view／create／update／export</c>（報名名單；export 為 is_restricted）。</summary>
public static class AdminTrialsEndpoints
{
    private const string TrialView = "program.trial.view";
    private const string TrialCreate = "program.trial.create";
    private const string TrialUpdate = "program.trial.update";
    private const string TrialDelete = "program.trial.delete";
    private const string RegView = "program.trial_registration.view";
    private const string RegCreate = "program.trial_registration.create";
    private const string RegUpdate = "program.trial_registration.update";
    private const string RegExport = "program.trial_registration.export";

    public static void MapAdminTrialsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/trials")
            .WithTags("AdminTrials")
            .WithDescription("P4 試訓場次與報名名單管理，需要登入與俱樂部授權。");

        // GET /trials?teamId=&status=&from=&to=
        group.MapGet("", async (
            string club, Guid? teamId, string? status, DateOnly? from, DateOnly? to, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminTrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, TrialView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, teamId, status, from, to, cancellationToken));
        })
        .WithName("AdminListTrials").Produces<IReadOnlyList<AdminTrialListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, TrialView, cancellationToken);
            var trial = await repository.GetByIdAsync(scope, id, cancellationToken);
            return trial is null ? Results.NotFound() : Results.Ok(trial);
        })
        .WithName("AdminGetTrial").Produces<AdminTrialDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminTrialRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, TrialCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/trials/{created.Id}", created);
        })
        .WithName("AdminCreateTrial").Produces<AdminTrialDetailDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminTrialRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, TrialUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateTrial").Produces<AdminTrialDetailDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, TrialDelete, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteTrial").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // ── 報名名單 ────────────────────────────────

        // GET /trials/{id}/registrations?status=&keyword=&isMember=
        group.MapGet("/{id:guid}/registrations", async (
            string club, Guid id, string? status, string? keyword, bool? isMember, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegView, cancellationToken);
            var list = await repository.ListAsync(scope, id, status, keyword, isMember, cancellationToken);
            return list is null ? Results.NotFound() : Results.Ok(list);
        })
        .WithName("AdminListTrialRegistrations").Produces<IReadOnlyList<AdminTrialRegistrationListItemDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/registrations/export", async (
            string club, Guid id, string? status, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegExport, cancellationToken);
            var export = await repository.ExportCsvAsync(scope, id, status, purpose, cancellationToken);
            // 檔名含場次日期（可讀），再接匯出日期；不放場次 GUID。
            return export is null
                ? Results.NotFound()
                : Results.File(CsvUtils.ToUtf8BytesWithBom(export.Value.Csv), "text/csv; charset=utf-8",
                    $"trial-registrations-{club}-{export.Value.TrialOn:yyyyMMdd}-{TaiwanClock.Today:yyyyMMdd}.csv");
        })
        .WithName("AdminExportTrialRegistrations").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/sign-in-sheet", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegView, cancellationToken);
            var sheet = await repository.SignInSheetAsync(scope, id, cancellationToken);
            return sheet is null ? Results.NotFound() : Results.Ok(sheet);
        })
        .WithName("AdminTrialSignInSheet").Produces<AdminTrialSignInSheetDto>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/registrations/{regId:guid}", async (
            string club, Guid id, Guid regId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegView, cancellationToken);
            var reg = await repository.GetAsync(scope, id, regId, cancellationToken);
            return reg is null ? Results.NotFound() : Results.Ok(reg);
        })
        .WithName("AdminGetTrialRegistration").Produces<AdminTrialRegistrationDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/registrations", async (
            string club, Guid id, CreateAdminTrialRegistrationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, id, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/trials/{id}/registrations/{created.Id}", created);
        })
        .WithName("AdminCreateTrialRegistration").Produces<AdminTrialRegistrationDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/registrations/{regId:guid}", async (
            string club, Guid id, Guid regId, UpdateAdminTrialRegistrationRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, regId, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateTrialRegistration").Produces<AdminTrialRegistrationDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        // POST /trials/{id}/registrations/{regId}/promote —— 候補遞補
        group.MapPost("/{id:guid}/registrations/{regId:guid}/promote", async (
            string club, Guid id, Guid regId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTrialRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, RegUpdate, cancellationToken);
            var promoted = await repository.PromoteAsync(scope, id, regId, cancellationToken);
            return promoted is null ? Results.NotFound() : Results.Ok(promoted);
        })
        .WithName("AdminPromoteTrialRegistration").Produces<AdminTrialRegistrationDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
    }
}
