using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminRegistrations;

/// <summary>P3「報名管理」後台端點。權限碼命名照 docs/12b §7.3：module_code=P、submodule_code=P3、
/// domain=program。<c>program.registration.export</c> 是 <c>is_restricted=1</c>（見
/// <c>db/seed/generate-club-seed-sql.py</c> 對應段落），本檔只做基本權限檢查——is_restricted
/// 文件承諾的「執行當下二次驗證」全系統目前都還沒有任何模組實作，本輪比照現狀，不另外發明，
/// 見任務回報。</summary>
public static class AdminRegistrationsEndpoints
{
    private const string PermissionView = "program.registration.view";
    private const string PermissionCreate = "program.registration.create";
    private const string PermissionUpdate = "program.registration.update";
    private const string PermissionExport = "program.registration.export";

    public static void MapAdminRegistrationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/registrations")
            .WithTags("AdminRegistrations")
            .WithDescription("P3 俱樂部範圍的課程報名管理，需要登入與俱樂部授權。只服務課程報名（session），不含 P4 試訓。");

        // GET /api/v1/admin/{club}/registrations?sessionId=&status=&programId=&keyword=&isMember=&dateFrom=&dateTo=
        group.MapGet("", async (
            string club, Guid? sessionId, string? status, Guid? programId, string? keyword, bool? isMember, DateOnly? dateFrom, DateOnly? dateTo,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var extra = new AdminRegistrationsRepository.ExtraFilter(programId, keyword, isMember, dateFrom, dateTo);
            var result = await repository.ListAsync(scope, sessionId, status, cancellationToken, extra);
            return Results.Ok(result);
        })
        .WithName("AdminListRegistrations")
        .Produces<IReadOnlyList<AdminRegistrationListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // S2-4 報名進階 ─────────────────────────────────────────────

        // GET /registrations/waitlist-reminders —— 有空位而且還有人候補的梯次（候補遞補提醒清單）。
        group.MapGet("/waitlist-reminders", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.WaitlistRemindersAsync(scope, cancellationToken));
        })
        .WithName("AdminWaitlistReminders")
        .Produces<IReadOnlyList<AdminWaitlistReminderDto>>();

        // GET /registrations/sign-in-sheet?sessionId= —— 簽到表資料（畫面直接列印）。
        group.MapGet("/sign-in-sheet", async (
            string club, Guid? sessionId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            if (sessionId is null)
            {
                throw new AdminRegistrationValidationException("請先選擇要列印簽到表的梯次。");
            }

            var sheet = await repository.SignInSheetAsync(scope, sessionId.Value, cancellationToken);
            return sheet is null ? Results.NotFound() : Results.Ok(sheet);
        })
        .WithName("AdminRegistrationSignInSheet")
        .Produces<AdminRegistrationSignInSheetDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // POST /registrations/batch/status —— 批次確認／取消／加入候補等。
        group.MapPost("/batch/status", async (
            string club, BatchRegistrationStatusRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.BatchStatusAsync(scope, request.Ids, request.Status, scope.Identity.AdminUserId, cancellationToken));
        })
        .WithName("AdminBatchRegistrationStatus")
        .Produces<Tcrfc.Api.Common.BatchOperationResultDto>()
        .Produces(StatusCodes.Status400BadRequest);

        // POST /registrations/{id}/promote —— 候補遞補（候補 → 已確認）。
        group.MapPost("/{id:guid}/promote", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var promoted = await repository.PromoteAsync(scope, id, scope.Identity.AdminUserId, cancellationToken);
            return promoted is null ? Results.NotFound() : Results.Ok(promoted);
        })
        .WithName("AdminPromoteRegistration")
        .Produces<AdminRegistrationDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var registration = await repository.GetByIdAsync(scope, id, cancellationToken);
            return registration is null ? Results.NotFound() : Results.Ok(registration);
        })
        .WithName("AdminGetRegistration")
        .Produces<AdminRegistrationDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/registrations/export?sessionId=&status=&programId=&keyword=&isMember=&dateFrom=&dateTo=&purpose=（必填）  → CSV（Excel 可直接開啟）。
        group.MapGet("/export", async (
            string club, Guid? sessionId, string? status, Guid? programId, string? keyword, bool? isMember, DateOnly? dateFrom, DateOnly? dateTo, string? purpose,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionExport, cancellationToken);
            var extra = new AdminRegistrationsRepository.ExtraFilter(programId, keyword, isMember, dateFrom, dateTo);
            var csv = await repository.ExportCsvAsync(scope, sessionId, status, purpose, cancellationToken, extra);
            var bytes = CsvUtils.ToUtf8BytesWithBom(csv);
            return Results.File(bytes, "text/csv; charset=utf-8", $"registrations-{club}-{TaiwanClock.Today:yyyyMMdd}.csv");
        })
        .WithName("AdminExportRegistrations")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminRegistrationRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/registrations/{created.Id}", created);
        })
        .WithName("AdminCreateRegistration")
        .Produces<AdminRegistrationDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminRegistrationRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminRegistrationsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateRegistration")
        .Produces<AdminRegistrationDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
