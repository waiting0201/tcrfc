using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// 行動 App 後台 M1–M5（主站規劃書 §4 M 模組、App 規劃書 §8）。App 是兩隊共用平台，不分俱樂部，所以走全域路由
/// <c>/api/v1/admin/app/…</c> 與 <see cref="IAdminSystemAuthorizer"/>。權限碼 <c>app.*</c>（module=M、domain=app、<c>is_club_scoped=0</c>）：
/// M1 版本 <c>app.release.*</c>（僅系統管理員可寫）、M2 編排 <c>app.layout.*</c>、M3 推播 <c>app.push.view／create／approve</c>（核可僅系統管理員且雙人覆核）、
/// M4 裝置 <c>app.device.view／reveal／update</c>（完整值與清理僅系統管理員）、M5 <c>app.config.*</c>／<c>app.credential.*</c>／<c>app.diagnostic.*</c>。
/// 合作球隊管理角色沒有任何 <c>app.*</c>（規劃書 §11）。
/// </summary>
public static class AdminAppEndpoints
{
    public static void MapAdminAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/app").WithTags("AdminApp")
            .WithDescription("M1–M5 行動 App 後台，需要登入與對應的 app.* 權限（不分俱樂部）。");
        MapReleases(group);
        MapLayout(group);
        MapPush(group);
        MapDevices(group);
        MapConfig(group);
    }

    // ═════════ M1 ═════════

    private static void MapReleases(RouteGroupBuilder g)
    {
        g.MapGet("/releases", async (string? platform, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.release.view", ct);
            return Results.Ok(await repo.ListAsync(platform, ct));
        }).WithName("AdminListAppReleases").Produces<IReadOnlyList<AdminAppReleaseDto>>();

        g.MapGet("/releases/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.release.view", ct);
            var r = await repo.GetAsync(id, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminGetAppRelease").Produces<AdminAppReleaseDto>().Produces(StatusCodes.Status404NotFound);

        g.MapPost("/releases", async (UpsertAdminAppReleaseRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.release.update", ct);
            var created = await repo.CreateAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/releases/{created.Id}", created);
        }).WithName("AdminCreateAppRelease").Produces<AdminAppReleaseDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        g.MapPut("/releases/{id:guid}", async (Guid id, UpsertAdminAppReleaseRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.release.update", ct);
            var r = await repo.UpdateAsync(id, request, scope.Identity.AdminUserId, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminUpdateAppRelease").Produces<AppConfigChangeDto<AdminAppReleaseDto>>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        g.MapDelete("/releases/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.release.update", ct);
            return await repo.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAppRelease").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        g.MapPut("/releases/{id:guid}/flags", async (Guid id, SetReleaseFlagsRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.release.update", ct);
            var r = await repo.SetFlagsAsync(id, request, scope, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminSetAppReleaseFlags").Produces<AppConfigChangeDto<AdminAppReleaseDto>>().Produces(StatusCodes.Status409Conflict);

        g.MapGet("/maintenance", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.release.view", ct);
            return Results.Ok(await repo.ListMaintenanceAsync(ct));
        }).WithName("AdminListAppMaintenance").Produces<IReadOnlyList<AdminAppMaintenanceDto>>();

        g.MapPut("/maintenance/{scope}", async (string scope, SetAppMaintenanceRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppReleasesRepository repo, CancellationToken ct) =>
        {
            var actor = await auth.AuthorizeAsync(http, "app.release.update", ct);
            return Results.Ok(await repo.SetMaintenanceAsync(scope, request, actor, ct));
        }).WithName("AdminSetAppMaintenance").Produces<AppConfigChangeDto<AdminAppMaintenanceDto>>().Produces(StatusCodes.Status400BadRequest);
    }

    // ═════════ M2 ═════════

    private static void MapLayout(RouteGroupBuilder g)
    {
        g.MapGet("/layout/items", async (string? kind, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.view", ct);
            return Results.Ok(await repo.ListItemsAsync(kind, ct));
        }).WithName("AdminListAppLayoutItems").Produces<IReadOnlyList<AdminAppLayoutItemDto>>();

        g.MapPost("/layout/items", async (UpsertAdminAppLayoutItemRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var created = await repo.CreateItemAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/layout/items/{created.Id}", created);
        }).WithName("AdminCreateAppLayoutItem").Produces<AdminAppLayoutItemDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status409Conflict);

        g.MapPut("/layout/items/{id:guid}", async (Guid id, UpsertAdminAppLayoutItemRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var r = await repo.UpdateItemAsync(id, request, scope.Identity.AdminUserId, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminUpdateAppLayoutItem").Produces<AdminAppLayoutItemDto>().Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/layout/items/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.update", ct);
            return await repo.DeleteItemAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAppLayoutItem").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        g.MapPost("/layout/items/reorder", async (ReorderAppLayoutRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            await repo.ReorderAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Ok(await repo.ListItemsAsync(request.Kind, ct));
        }).WithName("AdminReorderAppLayoutItems").Produces<IReadOnlyList<AdminAppLayoutItemDto>>();

        g.MapGet("/layout/deep-links", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.view", ct);
            return Results.Ok(await repo.ListDeepLinksAsync(ct));
        }).WithName("AdminListAppDeepLinks").Produces<IReadOnlyList<AdminAppDeepLinkDto>>();

        g.MapPost("/layout/deep-links", async (UpsertAdminAppDeepLinkRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var created = await repo.CreateDeepLinkAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/layout/deep-links/{created.Id}", created);
        }).WithName("AdminCreateAppDeepLink").Produces<AdminAppDeepLinkDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status409Conflict);

        g.MapPut("/layout/deep-links/{id:guid}", async (Guid id, UpsertAdminAppDeepLinkRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var r = await repo.UpdateDeepLinkAsync(id, request, scope.Identity.AdminUserId, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminUpdateAppDeepLink").Produces<AdminAppDeepLinkDto>().Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/layout/deep-links/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.update", ct);
            return await repo.DeleteDeepLinkAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAppDeepLink").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        g.MapGet("/layout/announcements", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.view", ct);
            return Results.Ok(await repo.ListAnnouncementsAsync(ct));
        }).WithName("AdminListAppAnnouncements").Produces<IReadOnlyList<AdminAppAnnouncementDto>>();

        g.MapPost("/layout/announcements", async (UpsertAdminAppAnnouncementRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var created = await repo.CreateAnnouncementAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/layout/announcements/{created.Id}", created);
        }).WithName("AdminCreateAppAnnouncement").Produces<AdminAppAnnouncementDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        g.MapPut("/layout/announcements/{id:guid}", async (Guid id, UpsertAdminAppAnnouncementRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.layout.update", ct);
            var r = await repo.UpdateAnnouncementAsync(id, request, scope.Identity.AdminUserId, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminUpdateAppAnnouncement").Produces<AdminAppAnnouncementDto>().Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/layout/announcements/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppLayoutRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.layout.update", ct);
            return await repo.DeleteAnnouncementAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAppAnnouncement").Produces(StatusCodes.Status204NoContent);
    }

    // ═════════ M3 ═════════

    private static void MapPush(RouteGroupBuilder g)
    {
        g.MapGet("/push/messages", async (string? status, HttpContext http, IAdminSystemAuthorizer auth, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.view", ct);
            return Results.Ok(await repo.ListAsync(status, ct));
        }).WithName("AdminListPushMessages").Produces<IReadOnlyList<AdminPushMessageListItemDto>>();

        g.MapGet("/push/messages/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.view", ct);
            var m = await repo.GetAsync(id, await PushCaller.ResolveAsync(scope, checker, ct), ct);
            return m is null ? Results.NotFound() : Results.Ok(m);
        }).WithName("AdminGetPushMessage").Produces<AdminPushMessageDto>().Produces(StatusCodes.Status404NotFound);

        g.MapPost("/push/messages", async (
            HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAppPushRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.create", ct);
            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPushMessageRequest>(request, json.Value.SerializerOptions, ct);
            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["image"];
                var image = file is null ? null : await tx.AddImageAsync("push_messages", "image", file, $"app/push/{id}/image", ct);
                var created = await repo.CreateAsync(id, payload, image, await PushCaller.ResolveAsync(scope, checker, ct), ct);
                return Results.Created($"/api/v1/admin/app/push/messages/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreatePushMessage").Produces<AdminPushMessageDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        g.MapPut("/push/messages/{id:guid}", async (
            Guid id, HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAppPushRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.create", ct);
            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminPushMessageRequest>(request, json.Value.SerializerOptions, ct);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("push_messages", "image", "推播圖片", form.Files["image"], payload.RemoveImage, $"app/push/{id}/image", ct);
                var updated = await repo.UpdateAsync(id, payload, image, orphans, await PushCaller.ResolveAsync(scope, checker, ct), ct);
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
        }).WithName("AdminUpdatePushMessage").Produces<AdminPushMessageDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        g.MapDelete("/push/messages/{id:guid}", async (
            Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppPushRepository repo, IImageStorageService images, IDocumentStorageService documents, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.create", ct);
            var orphans = new OrphanedObjects();
            if (!await repo.DeleteAsync(id, orphans, ct))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeletePushMessage").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        g.MapPost("/push/estimate", async (PushAudienceRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.view", ct);
            return Results.Ok(await repo.EstimateAsync(request, ct));
        }).WithName("AdminEstimatePushAudience").Produces<AdminPushAudienceEstimateDto>().Produces(StatusCodes.Status400BadRequest);

        g.MapGet("/push/messages/{id:guid}/preview", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.view", ct);
            var p = await repo.PreviewAsync(id, ct);
            return p is null ? Results.NotFound() : Results.Ok(p);
        }).WithName("AdminPreviewPushMessage").Produces<AdminPushPreviewDto>();

        g.MapPost("/push/messages/{id:guid}/test-send", async (Guid id, PushTestSendRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.create", ct);
            var r = await repo.TestSendAsync(id, request, scope, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminTestSendPushMessage").Produces<AdminPushTestSendResultDto>().Produces(StatusCodes.Status400BadRequest);

        Action("submit", "app.push.create", (repo, id, caller, scope, body, ct) => repo.SubmitAsync(id, caller, ct));
        Action("cancel", "app.push.create", (repo, id, caller, scope, body, ct) => repo.CancelAsync(id, caller, scope, ct));
        Action("return", "app.push.approve", (repo, id, caller, scope, body, ct) => repo.ReturnAsync(id, body?.Note, caller, ct));
        Action("retry", "app.push.approve", (repo, id, caller, scope, body, ct) => repo.RetryAsync(id, caller, scope, ct));

        g.MapPost("/push/messages/{id:guid}/approve", async (
            Guid id, ApprovePushMessageRequest request, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAppPushRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.approve", ct);
            var m = await repo.ApproveAsync(id, request, await PushCaller.ResolveAsync(scope, checker, ct), scope, ct);
            return m is null ? Results.NotFound() : Results.Ok(m);
        }).WithName("AdminApprovePushMessage").Produces<AdminPushMessageDto>().Produces(StatusCodes.Status409Conflict);

        g.MapGet("/push/rules", async (HttpContext http, IAdminSystemAuthorizer auth, PushRulesStore store, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.view", ct);
            return Results.Ok(await store.GetAsync(ct));
        }).WithName("AdminGetPushRules").Produces<AdminPushRulesDto>();

        g.MapPut("/push/rules", async (UpdateAdminPushRulesRequest request, HttpContext http, IAdminSystemAuthorizer auth, PushRulesStore store, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.push.approve", ct);
            return Results.Ok(await store.UpdateAsync(request, scope.Identity.AdminUserId, ct));
        }).WithName("AdminUpdatePushRules").Produces<AdminPushRulesDto>().Produces(StatusCodes.Status400BadRequest);

        g.MapPost("/push/dispatch-due", async (HttpContext http, IAdminSystemAuthorizer auth, PushDispatcher dispatcher, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.push.approve", ct);
            return Results.Ok(new { dispatched = await dispatcher.DispatchDueAsync(ct) });
        }).WithName("AdminDispatchDuePush").Produces(StatusCodes.Status200OK);

        void Action(string name, string permission,
            Func<AdminAppPushRepository, Guid, PushCaller, AdminSystemScope, PushNoteRequest?, CancellationToken, Task<AdminPushMessageDto?>> run)
            => g.MapPost($"/push/messages/{{id:guid}}/{name}", async (
                Guid id, PushNoteRequest? body, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAppPushRepository repo, CancellationToken ct) =>
            {
                var scope = await auth.AuthorizeAsync(http, permission, ct);
                var m = await run(repo, id, await PushCaller.ResolveAsync(scope, checker, ct), scope, body, ct);
                return m is null ? Results.NotFound() : Results.Ok(m);
            }).WithName($"AdminPushMessage_{name}").Produces<AdminPushMessageDto>().Produces(StatusCodes.Status409Conflict);
    }

    // ═════════ M4 ═════════

    private static void MapDevices(RouteGroupBuilder g)
    {
        g.MapGet("/devices", async (
            string? platform, string? appVersion, string? permission, string? tokenStatus, int? page, int? pageSize,
            HttpContext http, IAdminSystemAuthorizer auth, AdminAppDevicesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.device.view", ct);
            return Results.Ok(await repo.ListAsync(platform, appVersion, permission, tokenStatus, page, pageSize, ct));
        }).WithName("AdminListAppDevices").Produces<PagedResult<AdminAppDeviceListItemDto>>();

        g.MapGet("/devices/stats", async (string? platform, string? belowVersion, HttpContext http, IAdminSystemAuthorizer auth, AdminAppDevicesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.device.view", ct);
            return Results.Ok(await repo.StatsAsync(platform, belowVersion, ct));
        }).WithName("AdminAppDeviceStats").Produces<AdminAppDeviceStatsDto>();

        g.MapGet("/devices/{id:guid}", async (Guid id, bool? reveal, HttpContext http, IAdminSystemAuthorizer auth, AdminAppDevicesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, reveal == true ? "app.device.reveal" : "app.device.view", ct);
            var d = await repo.GetAsync(id, reveal == true, scope, ct);
            return d is null ? Results.NotFound() : Results.Ok(d);
        }).WithName("AdminGetAppDevice").Produces<AdminAppDeviceDetailDto>().Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        g.MapPost("/devices/cleanup-invalid-tokens", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppDevicesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.device.update", ct);
            return Results.Ok(await repo.CleanupInvalidTokensAsync(scope, ct));
        }).WithName("AdminCleanupInvalidTokens").Produces<AdminAppDeviceCleanupResultDto>();
    }

    // ═════════ M5 ═════════

    private static void MapConfig(RouteGroupBuilder g)
    {
        g.MapGet("/config/flags", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.config.view", ct);
            return Results.Ok(await repo.ListFlagsAsync(ct));
        }).WithName("AdminListAppFlags").Produces<IReadOnlyList<AdminAppFeatureFlagDto>>();

        g.MapPost("/config/flags", async (UpsertAdminAppFeatureFlagRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.config.update", ct);
            var r = await repo.CreateFlagAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/config/flags/{r.Value.Id}", r);
        }).WithName("AdminCreateAppFlag").Produces<AppConfigChangeDto<AdminAppFeatureFlagDto>>(StatusCodes.Status201Created).Produces(StatusCodes.Status409Conflict);

        g.MapPut("/config/flags/{id:guid}", async (Guid id, UpsertAdminAppFeatureFlagRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.config.update", ct);
            var r = await repo.UpdateFlagAsync(id, request, scope.Identity.AdminUserId, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminUpdateAppFlag").Produces<AppConfigChangeDto<AdminAppFeatureFlagDto>>().Produces(StatusCodes.Status409Conflict);

        g.MapDelete("/config/flags/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.config.update", ct);
            var r = await repo.DeleteFlagAsync(id, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        }).WithName("AdminDeleteAppFlag").Produces<AppConfigChangeDto<bool>>();

        g.MapGet("/config/credentials", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.credential.view", ct);
            return Results.Ok(await repo.ListCredentialsAsync(ct));
        }).WithName("AdminListAppCredentials").Produces<IReadOnlyList<AdminAppCredentialDto>>();

        g.MapPost("/config/credentials", async (UpsertAdminAppCredentialRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.credential.update", ct);
            var c = await repo.CreateCredentialAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/app/config/credentials/{c.Id}", c);
        }).WithName("AdminCreateAppCredential").Produces<AdminAppCredentialDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        g.MapPut("/config/credentials/{id:guid}", async (Guid id, UpsertAdminAppCredentialRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.credential.update", ct);
            var c = await repo.UpdateCredentialAsync(id, request, scope.Identity.AdminUserId, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminUpdateAppCredential").Produces<AdminAppCredentialDto>().Produces(StatusCodes.Status404NotFound);

        g.MapPost("/config/credentials/{id:guid}/rotate", async (Guid id, DateOnly? newExpiresOn, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.credential.update", ct);
            var c = await repo.RotateAsync(id, newExpiresOn, scope, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminRotateAppCredential").Produces<AdminAppCredentialDto>().Produces(StatusCodes.Status404NotFound);

        g.MapDelete("/config/credentials/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.credential.update", ct);
            return await repo.DeleteCredentialAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAppCredential").Produces(StatusCodes.Status204NoContent);

        g.MapGet("/config/connection-check", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.config.view", ct);
            return Results.Ok(await repo.ConnectionCheckAsync(ct));
        }).WithName("AdminAppConnectionCheck").Produces<AdminAppConnectionCheckDto>();

        g.MapGet("/diagnostics", async (
            string? type, string? status, string? platform, string? appVersion, int? page, int? pageSize,
            HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.diagnostic.view", ct);
            return Results.Ok(await repo.ListDiagnosticsAsync(type, status, platform, appVersion, page, pageSize, ct));
        }).WithName("AdminListAppDiagnostics").Produces<PagedResult<AdminAppDiagnosticListItemDto>>();

        g.MapGet("/diagnostics/summary", async (int? days, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.diagnostic.view", ct);
            return Results.Ok(await repo.DiagnosticSummaryAsync(days, ct));
        }).WithName("AdminAppDiagnosticSummary").Produces<AdminAppDiagnosticSummaryDto>();

        g.MapGet("/diagnostics/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "app.diagnostic.view", ct);
            var d = await repo.GetDiagnosticAsync(id, ct);
            return d is null ? Results.NotFound() : Results.Ok(d);
        }).WithName("AdminGetAppDiagnostic").Produces<AdminAppDiagnosticDetailDto>().Produces(StatusCodes.Status404NotFound);

        g.MapPut("/diagnostics/{id:guid}/status", async (Guid id, UpdateAdminAppDiagnosticStatusRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAppConfigRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "app.diagnostic.update", ct);
            var d = await repo.UpdateDiagnosticStatusAsync(id, request.Status, scope.Identity.AdminUserId, ct);
            return d is null ? Results.NotFound() : Results.Ok(d);
        }).WithName("AdminUpdateAppDiagnosticStatus").Produces<AdminAppDiagnosticListItemDto>().Produces(StatusCodes.Status404NotFound);
    }
}
