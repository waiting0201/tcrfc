using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Tcrfc.Api.Videos;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E4 廣告主與版位、E5 投放檔期與素材、E6 成效報表（App 規劃書 §7、§8.7–8.9；主站 §4.5 E4–E6）。
/// 廣告不分俱樂部（App 是兩隊共用平台，App 規劃書 §10.1），所以走全域路由 <c>/api/v1/admin/ads/…</c> 與 <see cref="IAdminSystemAuthorizer"/>，
/// 權限碼 <c>ad.*</c>（module=E、submodule=E4／E5／E6、domain=ad、<c>is_club_scoped=0</c>）——合作球隊管理角色沒有任何 <c>ad.*</c>（規劃書 §11）。
/// 檔期與素材的審核／暫停是各自獨立的權限（<c>ad.campaign.review</c>／<c>ad.campaign.pause</c>），合約金額另有 <c>ad.contract.view／update</c>。
/// </summary>
public static class AdminAdsEndpoints
{
    public static void MapAdminAdsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ads")
            .WithTags("AdminAds")
            .WithDescription("E4–E6 App 廣告後台，需要登入與對應的 ad.* 權限（不分俱樂部）。");

        MapSlots(group);
        MapAdvertisers(group);
        MapCampaigns(group);
        MapCreatives(group);
        MapReports(group);
    }

    private static void MapSlots(RouteGroupBuilder group)
    {
        group.MapGet("/slots", async (HttpContext http, IAdminSystemAuthorizer auth, AdminAdSlotsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.slot.view", ct);
            return Results.Ok(await repo.ListAsync(ct));
        }).WithName("AdminListAdSlots").Produces<IReadOnlyList<AdminAdSlotDto>>();

        group.MapGet("/slots/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdSlotsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.slot.view", ct);
            var slot = await repo.GetAsync(id, ct);
            return slot is null ? Results.NotFound() : Results.Ok(slot);
        }).WithName("AdminGetAdSlot").Produces<AdminAdSlotDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/slots", async (
            HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdSlotsRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.slot.create", ct);
            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminAdSlotRequest>(request, json.Value.SerializerOptions, ct);
            if (payload.RemoveFallbackImage)
            {
                throw new AdminValidationException("建立版位時不能選擇移除備援素材。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["fallbackImage"];
                var image = file is null ? null : await tx.AddImageAsync("ad_slots", "fallbackImage", file, $"ads/slots/{id}/fallback", ct);
                var created = await repo.CreateAsync(id, payload, image, scope.Identity.AdminUserId, ct);
                return Results.Created($"/api/v1/admin/ads/slots/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateAdSlot").Produces<AdminAdSlotDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/slots/{id:guid}", async (
            Guid id, HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdSlotsRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.slot.update", ct);
            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminAdSlotRequest>(request, json.Value.SerializerOptions, ct);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var image = await tx.ResolveImageAsync("ad_slots", "fallbackImage", "備援素材", form.Files["fallbackImage"], payload.RemoveFallbackImage, $"ads/slots/{id}/fallback", ct);
                var updated = await repo.UpdateAsync(id, payload, image, orphans, scope.Identity.AdminUserId, ct);
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
        }).WithName("AdminUpdateAdSlot").Produces<AdminAdSlotDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).DisableAntiforgery();

        group.MapDelete("/slots/{id:guid}", async (
            Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdSlotsRepository repo,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.slot.delete", ct);
            var orphans = new OrphanedObjects();
            if (!await repo.DeleteAsync(id, orphans, ct))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteAdSlot").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        group.MapGet("/slots/{id:guid}/schedule", async (
            Guid id, DateTimeOffset? from, DateTimeOffset? to, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.campaign.view", ct);
            var schedule = await repo.ScheduleAsync(id, from?.UtcDateTime, to?.UtcDateTime, ct);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        }).WithName("AdminAdSlotSchedule").Produces<AdminAdScheduleDto>().Produces(StatusCodes.Status404NotFound);
    }

    private static void MapAdvertisers(RouteGroupBuilder group)
    {
        group.MapGet("/advertisers", async (string? status, string? keyword, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.advertiser.view", ct);
            return Results.Ok(await repo.ListAsync(status, keyword, ct));
        }).WithName("AdminListAdvertisers").Produces<IReadOnlyList<AdminAdvertiserDto>>();

        group.MapGet("/advertisers/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.advertiser.view", ct);
            var a = await repo.GetAsync(id, ct);
            return a is null ? Results.NotFound() : Results.Ok(a);
        }).WithName("AdminGetAdvertiser").Produces<AdminAdvertiserDto>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sponsor-options", async (string? keyword, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.advertiser.view", ct);
            return Results.Ok(await repo.SponsorOptionsAsync(keyword, ct));
        }).WithName("AdminAdSponsorOptions").Produces<IReadOnlyList<AdminSponsorOptionDto>>();

        group.MapPost("/advertisers", async (UpsertAdminAdvertiserRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.advertiser.create", ct);
            var created = await repo.CreateAsync(request, scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/ads/advertisers/{created.Id}", created);
        }).WithName("AdminCreateAdvertiser").Produces<AdminAdvertiserDto>(StatusCodes.Status201Created).Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/advertisers/{id:guid}", async (Guid id, UpsertAdminAdvertiserRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.advertiser.update", ct);
            var updated = await repo.UpdateAsync(id, request, scope.Identity.AdminUserId, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateAdvertiser").Produces<AdminAdvertiserDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/advertisers/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdvertisersRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.advertiser.delete", ct);
            return await repo.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAdvertiser").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);
    }

    private static void MapCampaigns(RouteGroupBuilder group)
    {
        group.MapGet("/campaigns", async (
            string? status, Guid? slotId, Guid? advertiserId, string? keyword, HttpContext http, IAdminSystemAuthorizer auth,
            AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.campaign.view", ct);
            return Results.Ok(await repo.ListAsync(status, slotId, advertiserId, keyword, ct));
        }).WithName("AdminListAdCampaigns").Produces<IReadOnlyList<AdminAdCampaignListItemDto>>();

        group.MapGet("/campaigns/{id:guid}", async (
            Guid id, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.view", ct);
            var c = await repo.GetAsync(id, await AdCaller.ResolveAsync(scope, checker, ct), ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminGetAdCampaign").Produces<AdminAdCampaignDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/campaigns", async (
            UpsertAdminAdCampaignRequest request, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.create", ct);
            var created = await repo.CreateAsync(request, await AdCaller.ResolveAsync(scope, checker, ct), scope.Identity.AdminUserId, ct);
            return Results.Created($"/api/v1/admin/ads/campaigns/{created.Id}", created);
        }).WithName("AdminCreateAdCampaign").Produces<AdminAdCampaignDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/campaigns/{id:guid}", async (
            Guid id, UpsertAdminAdCampaignRequest request, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.update", ct);
            var updated = await repo.UpdateAsync(id, request, await AdCaller.ResolveAsync(scope, checker, ct), scope.Identity.AdminUserId, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("AdminUpdateAdCampaign").Produces<AdminAdCampaignDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/campaigns/{id:guid}", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.campaign.delete", ct);
            return await repo.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("AdminDeleteAdCampaign").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        MapAction(group, "submit", "ad.campaign.update", (r, id, caller, actor, ct) => r.SubmitAsync(id, caller, actor, ct));
        MapAction(group, "approve", "ad.campaign.review", (r, id, caller, actor, ct) => r.ApproveAsync(id, caller, actor, ct));
        MapAction(group, "return", "ad.campaign.review", (r, id, caller, actor, ct) => r.ReturnAsync(id, caller, actor, ct));
        MapAction(group, "resume", "ad.campaign.pause", (r, id, caller, actor, ct) => r.ResumeAsync(id, caller, actor, ct));
        MapAction(group, "close", "ad.campaign.update", (r, id, caller, actor, ct) => r.CloseAsync(id, caller, actor, ct));
        MapReasonAction(group, "pause", "ad.campaign.pause", (r, id, reason, caller, actor, ct) => r.PauseAsync(id, reason, caller, actor, ct));
        MapReasonAction(group, "void", "ad.campaign.update", (r, id, reason, caller, actor, ct) => r.VoidAsync(id, reason, caller, actor, ct));
    }

    private static void MapAction(
        RouteGroupBuilder group, string name, string permission,
        Func<AdminAdCampaignsRepository, Guid, AdCaller, AdminIdentity, CancellationToken, Task<AdminAdCampaignDetailDto?>> run)
        => group.MapPost($"/campaigns/{{id:guid}}/{name}", async (
            Guid id, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, permission, ct);
            var result = await run(repo, id, await AdCaller.ResolveAsync(scope, checker, ct), scope.Identity, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName($"AdminAdCampaign_{name}").Produces<AdminAdCampaignDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

    private static void MapReasonAction(
        RouteGroupBuilder group, string name, string permission,
        Func<AdminAdCampaignsRepository, Guid, string?, AdCaller, AdminIdentity, CancellationToken, Task<AdminAdCampaignDetailDto?>> run)
        => group.MapPost($"/campaigns/{{id:guid}}/{name}", async (
            Guid id, AdCampaignReasonRequest? body, HttpContext http, IAdminSystemAuthorizer auth, IPermissionChecker checker, AdminAdCampaignsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, permission, ct);
            var result = await run(repo, id, body?.Reason, await AdCaller.ResolveAsync(scope, checker, ct), scope.Identity, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName($"AdminAdCampaign_{name}").Produces<AdminAdCampaignDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

    private static void MapCreatives(RouteGroupBuilder group)
    {
        group.MapGet("/campaigns/{id:guid}/creatives", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.campaign.view", ct);
            var list = await repo.ListAsync(id, ct);
            return list is null ? Results.NotFound() : Results.Ok(list);
        }).WithName("AdminListAdCreatives").Produces<IReadOnlyList<AdminAdCreativeDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/campaigns/{id:guid}/creatives", async (
            Guid id, HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IVideoStorageService videos, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.update", ct);
            var slot = await repo.GetSlotForCampaignAsync(id, ct);
            if (slot is null)
            {
                return Results.NotFound();
            }

            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminAdCreativeRequest>(request, json.Value.SerializerOptions, ct);
            var imageFile = form.Files["image"] ?? throw new AdminValidationException("請上傳素材圖片（影片素材的圖片是海報）。");
            var tx = new UploadTransaction(images, documents, videos);
            try
            {
                var prefix = $"ads/creatives/{id}/{Guid.NewGuid():N}";
                var image = await tx.AddImageAsync("ad_creatives", "image", imageFile, $"{prefix}/image", ct);
                AdminAdCreativesRepository.ValidateImageAgainstSlot(slot, image, imageFile.Length);
                var videoFile = form.Files["video"];
                if (videoFile is not null && !slot.AllowVideo)
                {
                    throw new AdminValidationException("這個版位不允許影片素材。");
                }

                var video = videoFile is null ? null : await tx.AddVideoAsync(videoFile, $"{prefix}/video", ct);
                var created = await repo.CreateAsync(id, slot, payload, image, video?.Key, scope.Identity.AdminUserId, ct);
                return Results.Created($"/api/v1/admin/ads/creatives/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateAdCreative").Produces<AdminAdCreativeDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/creatives/{id:guid}", async (
            Guid id, HttpRequest request, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IVideoStorageService videos, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.update", ct);
            var campaignId = await repo.GetCampaignIdAsync(id, ct);
            if (campaignId is null)
            {
                return Results.NotFound();
            }

            var slot = await repo.GetSlotForCampaignAsync(campaignId.Value, ct);
            var (payload, form) = await AdminMultipartForm.ReadAsync<UpsertAdminAdCreativeRequest>(request, json.Value.SerializerOptions, ct);
            var tx = new UploadTransaction(images, documents, videos);
            var orphans = new OrphanedObjects();
            try
            {
                var prefix = $"ads/creatives/{campaignId}/{Guid.NewGuid():N}";
                var imageFile = form.Files["image"];
                var image = await tx.ResolveImageAsync("ad_creatives", "image", "素材圖片", imageFile, false, $"{prefix}/image", ct);
                if (imageFile is not null)
                {
                    AdminAdCreativesRepository.ValidateImageAgainstSlot(slot!, new UploadedImageInfo(image.Key!, image.Width ?? 0, image.Height ?? 0, imageFile.Length), imageFile.Length);
                }

                var videoFile = form.Files["video"];
                var video = videoFile is null ? null : await tx.AddVideoAsync(videoFile, $"{prefix}/video", ct);
                var updated = await repo.UpdateAsync(id, slot!, payload, image, video?.Key, orphans, scope.Identity.AdminUserId, ct);
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
        }).WithName("AdminUpdateAdCreative").Produces<AdminAdCreativeDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/creatives/{id:guid}", async (
            Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo,
            IImageStorageService images, IDocumentStorageService documents, IVideoStorageService videos, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.campaign.update", ct);
            var orphans = new OrphanedObjects();
            if (!await repo.DeleteAsync(id, orphans, ct))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents, videos).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteAdCreative").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/creatives/{id:guid}/approve", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.review", ct);
            var c = await repo.ApproveAsync(id, scope.Identity, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminApproveAdCreative").Produces<AdminAdCreativeDto>().Produces(StatusCodes.Status409Conflict);

        group.MapPost("/creatives/{id:guid}/reject", async (Guid id, AdCampaignReasonRequest? body, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.review", ct);
            var c = await repo.RejectAsync(id, body?.Reason, scope.Identity, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminRejectAdCreative").Produces<AdminAdCreativeDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/creatives/{id:guid}/pause", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.pause", ct);
            var c = await repo.SetPausedAsync(id, true, scope.Identity, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminPauseAdCreative").Produces<AdminAdCreativeDto>();

        group.MapPost("/creatives/{id:guid}/resume", async (Guid id, HttpContext http, IAdminSystemAuthorizer auth, AdminAdCreativesRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.campaign.pause", ct);
            var c = await repo.SetPausedAsync(id, false, scope.Identity, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).WithName("AdminResumeAdCreative").Produces<AdminAdCreativeDto>();
    }

    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapGet("/reports", async ([AsParameters] AdReportQuery query, HttpContext http, IAdminSystemAuthorizer auth, AdminAdReportsRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "ad.report.view", ct);
            return Results.Ok(await repo.BuildAsync(query, ct));
        }).WithName("AdminAdReport").Produces<AdminAdReportDto>().Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/reports/export", async ([AsParameters] AdReportQuery query, HttpContext http, IAdminSystemAuthorizer auth, AdminAdReportsRepository repo, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.report.export", ct);
            var csv = await repo.ExportCsvAsync(query, scope, ct);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"ad-report-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).WithName("AdminExportAdReport").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/maintenance/run", async (HttpContext http, IAdminSystemAuthorizer auth, AdMaintenanceService service, SensitiveActionLogger audit, CancellationToken ct) =>
        {
            var scope = await auth.AuthorizeAsync(http, "ad.maintenance.run", ct);
            var result = await service.RunAsync(ct);
            audit.Record(scope, "手動執行廣告維護作業", null, result.EventsAggregated, "推進檔期、聚合、清除");
            return Results.Ok(result);
        }).WithName("AdminAdMaintenanceRun").Produces<AdMaintenanceResultDto>();
    }
}
