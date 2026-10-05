using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminDraws;

/// <summary>K5 抽獎名單管理後台端點（主站規劃書 §4.11 K5）。權限碼：<c>member.draw.view／create／update</c>、
/// <c>member.draw.announce</c>（產生公布稿——只取得遮罩名單，公關／媒體用）、<c>member.draw.export</c>（is_restricted：中獎人聯絡名單與獎品出貨清單）。
/// module=K、submodule=K5、domain=member。🔴 <b>系統不抽出</b>：沒有任何隨機抽出端點，中獎人一律由人工以序號回填。
/// 活動建立與更新含封面圖，是 multipart（<c>payload</c>＋檔案欄位 <c>cover</c>）；其餘皆為 JSON。</summary>
public static class AdminDrawsEndpoints
{
    private const string View = "member.draw.view";
    private const string Create = "member.draw.create";
    private const string Update = "member.draw.update";
    private const string Announce = "member.draw.announce";
    private const string Export = "member.draw.export";

    public static void MapAdminDrawsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/draws")
            .WithTags("AdminDraws")
            .WithDescription("K5 抽獎名單管理（球迷會員抽獎），需要登入與俱樂部授權。名單視同會員個資，預設遮罩姓名。");

        // ── 蒐集告知 ──
        group.MapGet("/notice", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            return Results.Ok(await repository.GetNoticeAsync(scope, cancellationToken));
        }).WithName("AdminGetDrawNotice").Produces<AdminDrawNoticeDto>();

        group.MapPut("/notice", async (
            string club, UpdateAdminDrawNoticeRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            return Results.Ok(await repository.UpdateNoticeAsync(scope, request, cancellationToken));
        }).WithName("AdminUpdateDrawNotice").Produces<AdminDrawNoticeDto>();

        // ── 活動 ──
        group.MapGet("", async (
            string club, string? status, string? keyword, int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, status, keyword, p, ps, cancellationToken));
        }).WithName("AdminListDraws").Produces<PagedResult<AdminDrawListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var row = await repository.GetAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGetDraw").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Create, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminDrawRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemoveCover)
            {
                throw new AdminValidationException("新增活動時不能選擇移除封面。");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var file = form.Files["cover"];
                var cover = file is null
                    ? ImageFieldUpdate.Keep
                    : ToUpdate(await tx.AddImageAsync("member_draws", "cover", file, $"{scope.ClubCode}/member-draws/{id}/cover", cancellationToken));
                var created = await repository.CreateAsync(scope, id, request, cover, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/draws/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }).WithName("AdminCreateDraw").Produces<AdminDrawDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminDrawRequest>(httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var cover = await tx.ResolveImageAsync("member_draws", "cover", "活動封面", form.Files["cover"], request.RemoveCover, $"{scope.ClubCode}/member-draws/{id}/cover", cancellationToken);
                var updated = await repository.UpdateAsync(scope, id, request, cover, orphans, cancellationToken);
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
        }).WithName("AdminUpdateDraw").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository,
            IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var orphans = new OrphanedObjects();
            if (!await repository.DeleteAsync(scope, id, orphans, cancellationToken))
            {
                return Results.NotFound();
            }

            await new UploadTransaction(images, documents).CommitAsync(orphans);
            return Results.NoContent();
        }).WithName("AdminDeleteDraw").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict);

        // ── 合格名單 ──
        group.MapPost("/{id:guid}/roster/preview", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.PreviewRosterAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminPreviewDrawRoster").Produces<AdminRosterPreviewDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/roster", async (
            string club, Guid id, GenerateAdminRosterRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.GenerateRosterAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminGenerateDrawRoster").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/roster", async (
            string club, Guid id, int? version, string? keyword, bool? winnersOnly, bool? reveal, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);
            var rows = await repository.ListRosterAsync(scope, id, version, keyword, winnersOnly, p, ps, cancellationToken, reveal ?? false);
            return rows is null ? Results.NotFound() : Results.Ok(rows);
        }).WithName("AdminListDrawRoster").Produces<PagedResult<AdminRosterEntryDto>>().Produces(StatusCodes.Status404NotFound);

        // ── 中獎人回填（以序號為準）──
        group.MapPut("/{id:guid}/winners", async (
            string club, Guid id, RecordAdminWinnersRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.RecordWinnersAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminRecordDrawWinners").Produces<AdminWinnerResultDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/winners/remove", async (
            string club, Guid id, RemoveAdminWinnersRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.RemoveWinnersAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminRemoveDrawWinners").Produces<AdminWinnerResultDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // ── 獎品發放（比照 K3）──
        group.MapGet("/{id:guid}/fulfilment", async (
            string club, Guid id, string? status, string? claimMethod, int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);
            var rows = await repository.ListFulfilmentAsync(scope, id, status, claimMethod, p, ps, cancellationToken);
            return rows is null ? Results.NotFound() : Results.Ok(rows);
        }).WithName("AdminListDrawFulfilment").Produces<PagedResult<AdminFulfilmentDto>>().Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/fulfilment/{serialNo:int}", async (
            string club, Guid id, int serialNo, UpdateAdminFulfilmentRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.UpdateFulfilmentAsync(scope, id, serialNo, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminUpdateDrawFulfilment").Produces<AdminFulfilmentDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/fulfilment/batch/status", async (
            string club, Guid id, BatchAdminFulfilmentRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.BatchFulfilmentAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminBatchDrawFulfilment").Produces<AdminBatchFulfilmentResultDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // ── 匯出（檔名依規劃書：draw-<活動代碼>-v<版本>-public.csv／-winners.csv）──
        group.MapGet("/{id:guid}/export/public", async (
            string club, Guid id, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, View, cancellationToken);
            var result = await repository.ExportPublicAsync(scope, id, purpose, cancellationToken);
            return result is null ? Results.NotFound() : Results.File(CsvUtils.ToUtf8BytesWithBom(result.Value.Csv), "text/csv; charset=utf-8", result.Value.FileName);
        }).WithName("AdminExportDrawPublic").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/export/winners", async (
            string club, Guid id, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Export, cancellationToken);
            var result = await repository.ExportWinnersAsync(scope, id, purpose, cancellationToken);
            return result is null ? Results.NotFound() : Results.File(CsvUtils.ToUtf8BytesWithBom(result.Value.Csv), "text/csv; charset=utf-8", result.Value.FileName);
        }).WithName("AdminExportDrawWinners").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/export/shipping", async (
            string club, Guid id, string? purpose, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Export, cancellationToken);
            var result = await repository.ExportShippingAsync(scope, id, purpose, cancellationToken);
            return result is null ? Results.NotFound() : Results.File(CsvUtils.ToUtf8BytesWithBom(result.Value.Csv), "text/csv; charset=utf-8", result.Value.FileName);
        }).WithName("AdminExportDrawShipping").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        // ── 公布稿交接 B2（只給遮罩版名單）──
        group.MapGet("/{id:guid}/announcement-preview", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Announce, cancellationToken);
            var row = await repository.AnnouncementPreviewAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminDrawAnnouncementPreview").Produces<AdminAnnouncementPreviewDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/announcement-draft", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Announce, cancellationToken);
            var row = await repository.CreateAnnouncementDraftAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Created($"/api/v1/admin/{club}/news/{row.ArticleId}", row);
        }).WithName("AdminCreateDrawAnnouncementDraft").Produces<AdminAnnouncementDraftDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/announcement-article", async (
            string club, Guid id, LinkAdminAnnouncementRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Announce, cancellationToken);
            var row = await repository.LinkAnnouncementAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminLinkDrawAnnouncement").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // ── 狀態 ──
        group.MapPost("/{id:guid}/mark-announced", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.MarkAnnouncedAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminMarkDrawAnnounced").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/close", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.CloseAsync(scope, id, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminCloseDraw").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/void", async (
            string club, Guid id, VoidAdminDrawRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDrawsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Update, cancellationToken);
            var row = await repository.VoidAsync(scope, id, request, cancellationToken);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).WithName("AdminVoidDraw").Produces<AdminDrawDetailDto>().Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    private static ImageFieldUpdate ToUpdate(UploadedImageInfo info) => ImageFieldUpdate.Set(info.Key, info.Width, info.Height);
}
