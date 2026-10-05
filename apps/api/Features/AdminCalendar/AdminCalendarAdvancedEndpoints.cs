using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// S2-6 行事曆進階後台端點（主站規劃書 §4.12）：L1 分軌檢視／衝突偵測／拖曳改期、L3 分類與顯示設定、L4 訂閱與匯出。
/// 權限碼：<c>calendar.view</c>（分軌與衝突）、<c>team.match.update</c>＋球隊列級授權（賽事改期——行事曆權限跟隨來源模組）、
/// <c>calendar.custom_event.update</c>（自建活動改期）、<c>calendar.setting.view／update</c>（L3）、
/// <c>calendar.subscription.view</c>、<c>calendar.export</c>（L4）、<c>team.match.create</c>（整季賽程 CSV 匯入，與 C4 共用機制）。
/// </summary>
public static class AdminCalendarAdvancedEndpoints
{
    private const string PermissionView = "calendar.view";
    private const string MatchUpdate = "team.match.update";
    private const string MatchCreate = "team.match.create";
    private const string CustomEventUpdate = "calendar.custom_event.update";
    private const string SettingView = "calendar.setting.view";
    private const string SettingUpdate = "calendar.setting.update";
    private const string SubscriptionView = "calendar.subscription.view";
    private const string Export = "calendar.export";

    public static void MapAdminCalendarAdvancedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/calendar")
            .WithTags("AdminCalendarAdvanced")
            .WithDescription("S2-6 行事曆進階：分軌、衝突偵測、拖曳改期、分類與顯示設定、訂閱與匯出，需要登入與俱樂部授權。");

        MapTracksAndReschedule(group);
        MapSettings(group);
        MapSubscriptionsAndExport(group);
    }

    private static void MapTracksAndReschedule(RouteGroupBuilder group)
    {
        // GET /tracks?from=2026-10-01&to=2026-11-01 —— 預設本月；上限 366 天。
        group.MapGet("/tracks", async (
            string club, DateOnly? from, DateOnly? to, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarTracksRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (f, t) = AdminCalendarEndpoints.NormalizeRange(from, to);
            return Results.Ok(await repository.TracksAsync(scope, f, t, cancellationToken));
        })
        .WithName("AdminCalendarTracks").Produces<AdminCalendarTracksDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/conflicts", async (
            string club, DateOnly? from, DateOnly? to, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarTracksRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (f, t) = AdminCalendarEndpoints.NormalizeRange(from, to);
            return Results.Ok(await repository.ConflictsAsync(scope, f, t, cancellationToken));
        })
        .WithName("AdminCalendarConflicts").Produces<IReadOnlyList<AdminCalendarConflictDto>>().Produces(StatusCodes.Status400BadRequest);

        // POST /matches/{id}/reschedule —— 賽事拖曳改期。有衝突且未確認：409，回應本文含 conflicts，不寫入。
        group.MapPost("/matches/{id:guid}/reschedule", async (
            string club, Guid id, RescheduleMatchRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminCalendarTracksRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MatchUpdate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, MatchUpdate, cancellationToken);
            var result = await repository.RescheduleMatchAsync(scope, rowScope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return ToResponse(result);
        })
        .WithName("AdminRescheduleMatch").Produces<AdminCalendarRescheduleResultDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // POST /custom-events/{id}/move —— 自建活動拖曳改期（重複活動改的是整個系列的起始時間）。
        group.MapPost("/custom-events/{id:guid}/move", async (
            string club, Guid id, MoveCustomEventRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarTracksRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventUpdate, cancellationToken);
            var result = await repository.MoveCustomEventAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return ToResponse(result);
        })
        .WithName("AdminMoveCustomEvent").Produces<AdminCalendarRescheduleResultDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    private static IResult ToResponse(AdminCalendarRescheduleResultDto? result)
    {
        if (result is null)
        {
            return Results.NotFound();
        }

        if (result.Saved)
        {
            return Results.Ok(result);
        }

        var summary = string.Join("；", result.Conflicts.Take(3).Select(c => c.Description));
        var detail = $"新的時段和 {result.Conflicts.Count} 件事件衝突：{summary}。確定要改期的話，請確認後再送出一次。";
        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "排程衝突",
            detail: detail,
            extensions: Tcrfc.Api.Common.ApiErrorEnvelope.Extensions(
                StatusCodes.Status409Conflict, "schedule_conflict", detail,
                new Dictionary<string, object?> { ["conflicts"] = result.Conflicts, ["saved"] = false }));
    }

    private static void MapSettings(RouteGroupBuilder group)
    {
        group.MapGet("/settings", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingView, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        })
        .WithName("AdminGetCalendarSettings").Produces<AdminCalendarSettingsDto>();

        group.MapPut("/settings", async (
            string club, UpdateCalendarSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            return Results.Ok(await repository.UpdateAsync(scope, request, cancellationToken));
        })
        .WithName("AdminUpdateCalendarSettings").Produces<AdminCalendarSettingsDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/settings/teams", async (
            string club, UpdateCalendarTeamSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            return Results.Ok(await repository.UpdateTeamsAsync(scope, request, cancellationToken));
        })
        .WithName("AdminUpdateCalendarTeamSettings").Produces<IReadOnlyList<AdminCalendarTeamSettingDto>>().Produces(StatusCodes.Status400BadRequest);

        // ── 賽事／活動類型（兩隊共用：只有系統管理員能寫）──
        group.MapGet("/event-types/icons", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, SettingView, cancellationToken);
            return Results.Ok(AdminCalendarSettingsRepository.Icons);
        })
        .WithName("AdminListEventTypeIcons").Produces<IReadOnlyList<AdminEventTypeIconDto>>();

        group.MapPost("/event-types", async (
            string club, UpsertEventTypeRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            var created = await repository.CreateEventTypeAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/calendar/event-types", created);
        })
        .WithName("AdminCreateEventType").Produces<AdminEventTypeDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/event-types/{id:guid}", async (
            string club, Guid id, UpsertEventTypeRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            var updated = await repository.UpdateEventTypeAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateEventType").Produces<AdminEventTypeDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/event-types/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            return await repository.DeleteEventTypeAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteEventType").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/event-types/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            await repository.ReorderEventTypesAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderEventTypes").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden);
    }

    private static void MapSubscriptionsAndExport(RouteGroupBuilder group)
    {
        // GET /subscriptions —— 全站與各隊別的 https／webcal 訂閱網址與訂閱數。
        group.MapGet("/subscriptions", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarSubscriptionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SubscriptionView, cancellationToken);
            var request = httpContext.Request;
            return Results.Ok(await repository.ListAsync(scope, $"{request.Scheme}://{request.Host}", cancellationToken));
        })
        .WithName("AdminCalendarSubscriptions").Produces<AdminCalendarSubscriptionsDto>();

        // GET /export?format=csv|ics&from=&to=&team=&sourceType=&venueId=&status=&type= —— 指定期間匯出（上限 366 天）。
        group.MapGet("/export", async (
            string club, string? format, DateOnly? from, DateOnly? to, string? team, string? sourceType, Guid? venueId, string? status, string? type,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminCalendarExportRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Export, cancellationToken);
            var (f, t) = AdminCalendarEndpoints.NormalizeRange(from, to);
            var isIcs = string.Equals(format, "ics", StringComparison.OrdinalIgnoreCase);
            if (!isIcs && !string.IsNullOrWhiteSpace(format) && !string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new AdminCalendarValidationException("匯出格式只能是 CSV 或 ICS。");
            }

            if (isIcs)
            {
                var ics = await repository.ExportIcsAsync(scope, f, t, team, sourceType, venueId, status, type, cancellationToken);
                return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"calendar-{club}-{f:yyyyMMdd}.ics");
            }

            var csv = await repository.ExportCsvAsync(scope, f, t, team, sourceType, venueId, status, type, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"calendar-{club}-{f:yyyyMMdd}.csv");
        })
        .WithName("AdminCalendarExport").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        // POST /matches/import —— 整季賽程 CSV 匯入，與 C4 賽程匯入共用同一套機制（同一個 repository、同一份 CSV 格式、同一組權限）。
        group.MapPost("/matches/import", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver,
            AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MatchCreate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, MatchCreate, cancellationToken);
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);
            var result = await repository.ImportCsvAsync(scope, rowScope, csvContent, scope.Identity.AdminUserId, cancellationToken);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        })
        .WithName("AdminCalendarImportMatchesCsv").Produces<MatchCsvImportResultDto>()
        .Produces<MatchCsvImportResultDto>(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
        .DisableAntiforgery();
    }
}
