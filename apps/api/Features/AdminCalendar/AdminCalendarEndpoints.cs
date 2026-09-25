using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>L1 行事曆總覽／L2 自建事件後台端點（主站規劃書 §4.12）。權限碼命名照 docs/12b §7.3：
/// module_code=L、domain=calendar。</summary>
public static class AdminCalendarEndpoints
{
    private const string PermissionView = "calendar.view";
    private const string CustomEventView = "calendar.custom_event.view";
    private const string CustomEventCreate = "calendar.custom_event.create";
    private const string CustomEventUpdate = "calendar.custom_event.update";
    private const string CustomEventDelete = "calendar.custom_event.delete";

    public static void MapAdminCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/calendar")
            .WithTags("AdminCalendar")
            .WithDescription("L1 行事曆總覽／L2 自建事件維護，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/calendar/events?from=2026-10-01&to=2026-11-01&team=D1&sourceType=match
        group.MapGet("/events", async (
            string club, DateOnly? from, DateOnly? to, string? team, string? sourceType,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminCalendarOverviewRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (fromDate, toDateExclusive) = NormalizeRange(from, to);
            var result = await repository.ListAsync(scope, fromDate, toDateExclusive, team, sourceType, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListCalendarEvents")
        .Produces<IReadOnlyList<AdminCalendarEventDto>>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/calendar/event-types —— L2 建立／編輯事件時的分類下拉選單。
        // L3 正式的 CRUD 管理畫面留給 S2-6，本輪只開唯讀端點。
        group.MapGet("/event-types", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarCustomEventsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, CustomEventView, cancellationToken);
            var result = await repository.ListEventTypesAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListCalendarEventTypes")
        .Produces<IReadOnlyList<AdminEventTypeDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/calendar/custom-events?team=D1
        group.MapGet("/custom-events", async (
            string club, string? team, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarCustomEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventView, cancellationToken);
            var result = await repository.ListAsync(scope, team, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListCalendarCustomEvents")
        .Produces<IReadOnlyList<AdminCalendarCustomEventListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/custom-events/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarCustomEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventView, cancellationToken);
            var item = await repository.GetByIdAsync(scope, id, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .WithName("AdminGetCalendarCustomEvent")
        .Produces<AdminCalendarCustomEventDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/calendar/custom-events —— multipart/form-data（payload ＋ 選填 file 封面圖）。
        group.MapPost("/custom-events", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCalendarCustomEventsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventCreate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminCalendarRequestForm.ReadAsync<CreateAdminCalendarCustomEventRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var eventId = Guid.NewGuid();
            string? coverKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("calendar_custom_events", "cover");
                var uploaded = await UploadCoverAsync(scope, eventId, file, imageStorage, cancellationToken);
                coverKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(scope, eventId, request, coverKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/calendar/custom-events/{created.Id}", created);
            }
            catch
            {
                if (coverKey is not null)
                {
                    await imageStorage.DeleteAsync(coverKey, CancellationToken.None); // E-47：請求已取消也要清掉
                }

                throw;
            }
        })
        .WithName("AdminCreateCalendarCustomEvent")
        .Produces<AdminCalendarCustomEventDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapPut("/custom-events/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCalendarCustomEventsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventUpdate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminCalendarRequestForm.ReadAsync<UpdateAdminCalendarCustomEventRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemoveCover)
            {
                throw new AdminCalendarValidationException("不能同時上傳新的封面圖與移除封面圖，請擇一。");
            }

            string? uploadedKey = null;
            CalendarEventCoverKeyUpdate coverUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("calendar_custom_events", "cover");
                var uploaded = await UploadCoverAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                coverUpdate = CalendarEventCoverKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemoveCover)
            {
                coverUpdate = CalendarEventCoverKeyUpdate.Set(null);
            }
            else
            {
                coverUpdate = CalendarEventCoverKeyUpdate.Keep;
            }

            try
            {
                var updated = await repository.UpdateAsync(scope, id, request, coverUpdate, operatorId, cancellationToken);
                if (updated is null)
                {
                    if (uploadedKey is not null)
                    {
                        await imageStorage.DeleteAsync(uploadedKey, CancellationToken.None);
                    }

                    return Results.NotFound();
                }

                return Results.Ok(updated);
            }
            catch
            {
                if (uploadedKey is not null)
                {
                    await imageStorage.DeleteAsync(uploadedKey, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminUpdateCalendarCustomEvent")
        .Produces<AdminCalendarCustomEventDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapDelete("/custom-events/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCalendarCustomEventsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, CustomEventDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteCalendarCustomEvent")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    /// <summary>預設一個月範圍（本月 1 日～下月 1 日），與月曆檢視的自然使用情境一致；呼叫端可用
    /// <c>from</c>／<c>to</c> 明確指定其他範圍（例如列表檢視要看更長區間）。上限 366 天，避免一次
    /// 查詢整個資料庫的賽事與活動（合併讀取沒有分頁，範圍越大回應越大）。</summary>
    private static (DateOnly From, DateOnly ToExclusive) NormalizeRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var normalizedTo = to ?? normalizedFrom.AddMonths(1);

        if (normalizedTo <= normalizedFrom)
        {
            throw new AdminCalendarValidationException("結束日期必須晚於起始日期。");
        }
        if (normalizedTo.DayNumber - normalizedFrom.DayNumber > 366)
        {
            throw new AdminCalendarValidationException("查詢範圍最長 366 天，請縮小 from／to 區間。");
        }

        return (normalizedFrom, normalizedTo);
    }

    private static async Task<UploadedImageInfo> UploadCoverAsync(
        AdminClubScope scope, Guid eventId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }
        if (file.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        byte[] rawBytes;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            rawBytes = buffer.ToArray();
        }

        var objectKeyPrefix = $"{scope.ClubCode}/calendar/{eventId}/cover";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
