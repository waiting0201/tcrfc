using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminAds;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// 行動 App 的公開端點（App 規劃書 §9.2；官網與 App 共用同一套 API，B-14）。全部匿名、不分俱樂部，路徑 <c>/api/v1/app/…</c>。
/// 本批做的是後台 M／E4–E6 所對應、由 App 呼叫的那一半：裝置註冊、追蹤／推播訂閱、設定、內容編排、通知中心、廣告投放與事件、診斷回報。
/// 賽事／新聞／球隊等內容端點是既有的（AP-2 起 App 直接使用），會員相關端點屬 AP-3，不在這裡。
/// 🔴 寫入端點一律掛 <c>public-app</c> 限流政策（架構測試強制）；讀取端點的快取標頭見各端點註解——
/// 帶了裝置識別的回應一律 <c>private, no-store</c>（內容因裝置而異，不得被邊緣快取誤送給別人）。
/// </summary>
public static class AppPublicEndpoints
{
    private const string EdgeCacheable = "public, max-age=60, stale-while-revalidate=60, stale-if-error=86400";
    private const string NoStore = "private, no-store";

    public static void MapAppPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/app").WithTags("AppPublic")
            .WithDescription("行動 App 公開端點（匿名）：裝置、訂閱、設定、內容編排、通知、廣告、診斷。");

        group.MapPut("/devices/{deviceInstallId}", async (
            string deviceInstallId, RegisterAppDeviceRequest request, AppDevicesService service, CancellationToken ct) =>
            Results.Ok(await service.RegisterAsync(deviceInstallId, request, ct)))
        .RequireRateLimiting(PublicRateLimitPolicies.App)
        .WithName("AppRegisterDevice").Produces<AppDeviceRegisteredDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/devices/{deviceInstallId}/subscriptions", async (
            string deviceInstallId, HttpContext http, AppDevicesService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = NoStore;
            var list = await service.ListSubscriptionsAsync(deviceInstallId, ct);
            return list is null ? Results.NotFound() : Results.Ok(list);
        }).WithName("AppListSubscriptions").Produces<IReadOnlyList<AppSubscriptionDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapPut("/devices/{deviceInstallId}/subscriptions", async (
            string deviceInstallId, UpdateAppSubscriptionsRequest request, AppDevicesService service, CancellationToken ct) =>
        {
            var list = await service.UpdateSubscriptionsAsync(deviceInstallId, request, ct);
            return list is null ? Results.NotFound() : Results.Ok(list);
        })
        .RequireRateLimiting(PublicRateLimitPolicies.App)
        .WithName("AppUpdateSubscriptions").Produces<IReadOnlyList<AppSubscriptionDto>>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        // GET /config?platform=ios&appVersion=1.2.0 —— 第 2 層設定來源（docs/19 §7）。內容對所有裝置相同，可邊緣快取。
        group.MapGet("/config", async (string? platform, string? appVersion, HttpContext http, AppConfigComposer composer, CancellationToken ct) =>
        {
            var doc = await composer.ComposeAsync(ct);
            AppConfigEvaluation? evaluation = null;
            if (platform is not null)
            {
                var node = AppInput.RequirePlatform(platform) == "ios" ? doc.Ios : doc.Android;
                evaluation = AppConfigComposer.Evaluate(node, AppInput.OptionalVersion(appVersion));
            }

            http.Response.Headers.CacheControl = EdgeCacheable;
            return Results.Ok(new AppConfigResponse { GeneratedAt = doc.GeneratedAt, Ios = doc.Ios, Android = doc.Android, Evaluation = evaluation });
        }).WithName("AppGetConfig").Produces<AppConfigResponse>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/layout", async (
            string? lang, string? deviceInstallId, [Microsoft.AspNetCore.Mvc.FromHeader(Name = AppInput.DeviceHeaderName)] string? deviceHeader,
            HttpContext http, AppLayoutReader reader, CancellationToken ct) =>
        {
            deviceInstallId = AppInput.ResolveOptionalDeviceId(http, deviceInstallId); // 標頭 X-Device-Install-Id 優先（§9.3）；deviceHeader 參數只為了讓 OpenAPI 記載這個標頭
            http.Response.Headers.CacheControl = string.IsNullOrEmpty(deviceInstallId) ? EdgeCacheable : NoStore;
            return Results.Ok(await reader.ReadAsync(lang, deviceInstallId, ct));
        }).WithName("AppGetLayout").Produces<AppLayoutResponse>();

        group.MapGet("/notifications", async (
            string? lang, string? deviceInstallId, [Microsoft.AspNetCore.Mvc.FromHeader(Name = AppInput.DeviceHeaderName)] string? deviceHeader,
            HttpContext http, AppLayoutReader reader, CancellationToken ct) =>
        {
            deviceInstallId = AppInput.ResolveOptionalDeviceId(http, deviceInstallId);
            http.Response.Headers.CacheControl = string.IsNullOrEmpty(deviceInstallId) ? EdgeCacheable : NoStore;
            return Results.Ok(await reader.ListNotificationsAsync(lang, deviceInstallId, ct));
        }).WithName("AppListNotifications").Produces<IReadOnlyList<AppNotificationDto>>();

        group.MapPost("/push/{messageId:guid}/opened", async (
            Guid messageId, AppPushOpenedRequest request, AppLayoutReader reader, CancellationToken ct) =>
            await reader.RecordOpenedAsync(messageId, request.DeviceInstallId, ct) ? Results.NoContent() : Results.NotFound())
        .RequireRateLimiting(PublicRateLimitPolicies.App)
        .WithName("AppPushOpened").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        // GET /ads/{slotCode}?lang=zh&deviceInstallId=…&theme=dark —— 帶裝置識別才套用「每人頻次上限」，此時不可快取；沒帶則可短暫邊緣快取。
        // 裝置識別（§9.3）：標頭 X-Device-Install-Id 優先，查詢參數 deviceInstallId 相容保留；每人頻次上限依解析出的識別計算。
        // deviceHeader 參數只為了讓 OpenAPI 記載這個標頭，實際讀取在 AppInput.ResolveOptionalDeviceId。
        group.MapGet("/ads/{slotCode}", async (
            string slotCode, string? lang, string? deviceInstallId, [Microsoft.AspNetCore.Mvc.FromHeader(Name = AppInput.DeviceHeaderName)] string? deviceHeader,
            string? theme, HttpContext http, AdServingService service, CancellationToken ct) =>
        {
            deviceInstallId = AppInput.ResolveOptionalDeviceId(http, deviceInstallId);
            var result = await service.ServeAsync(slotCode, lang, deviceInstallId, theme, ct);
            http.Response.Headers.CacheControl = string.IsNullOrEmpty(deviceInstallId) ? "public, max-age=60" : NoStore;
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("AppServeAd").Produces<AppAdResponse>().Produces(StatusCodes.Status404NotFound);

        // GET /ads/prefetch?lang=zh&theme=dark —— 當日檔期素材預載目錄（App 規劃書 §2.4）。對所有裝置相同、不套頻次判斷，可短暫邊緣快取。
        // 路由 /ads/prefetch 與 /ads/{slotCode} 相鄰：字面路由優先於參數路由，所以版位代碼不得叫 prefetch（後台建立版位時另有代碼格式限制）。
        group.MapGet("/ads/prefetch", async (string? lang, string? theme, HttpContext http, AdServingService service, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(await service.PrefetchAsync(lang, theme, ct));
        }).WithName("AppPrefetchAds").Produces<AppAdPrefetchResponse>();

        group.MapPost("/ads/events", async (AppAdEventBatchRequest request, AdEventIngestService service, CancellationToken ct) =>
            Results.Ok(await service.IngestAsync(request, ct)))
        .RequireRateLimiting(PublicRateLimitPolicies.App)
        .WithName("AppIngestAdEvents").Produces<AppAdEventBatchResult>().Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/diagnostics", async (AppDiagnosticBatchRequest request, AppDiagnosticsIntake intake, CancellationToken ct) =>
            Results.Accepted(value: new { accepted = await intake.IngestAsync(request, ct) }))
        .RequireRateLimiting(PublicRateLimitPolicies.App)
        .WithName("AppIngestDiagnostics").Produces(StatusCodes.Status202Accepted).Produces(StatusCodes.Status400BadRequest);
    }
}
