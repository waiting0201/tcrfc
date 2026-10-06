using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.AdminPartnerStores;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 場地管理端點（I5，規劃書 §4.9）。掛在 <c>{club}</c> 路由段下只是為了沿用既有 <see cref="IAdminClubAuthorizer"/> 的授權管線
/// （帳號狀態、俱樂部存在、俱樂部授權、權限碼四步一次到齊）——回傳內容本身**與俱樂部無關**（<c>Venue</c> 不帶 <c>club_id</c>，
/// 見 <see cref="AdminVenuesRepository"/> 檔頭），任何俱樂部呼叫都會拿到同一份全站清單。
///
/// 🔴 **清單 <c>GET</c> 採 <see cref="IAdminClubAuthorizer.AuthorizeAnyAsync"/>，允許三組既有權限碼任一通過**：
/// <c>site.fact.view</c>（網站設定挑選主場）、<c>team.match.view</c>（賽程挑選比賽地點）、<c>site.venue.view</c>（場地管理）。
/// 詳情、新增、修改、刪除、定位一律要 <c>site.venue.*</c>（module=I、submodule=I5，sysadmin_only）。
/// 新增與修改是 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋選填檔案欄位 <c>photo</c>。
/// </summary>
public static class AdminVenuesEndpoints
{
    private static readonly string[] ViewCandidateCodes = ["site.fact.view", "team.match.view", "site.venue.view"];
    private static readonly string[] LocateCodes = ["site.venue.create", "site.venue.update"];

    public static void MapAdminVenuesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/{club}/venues", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCandidateCodes, cancellationToken);
            var result = await repository.ListAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListVenues")
        .WithTags("AdminVenues")
        .WithDescription("全站共用場地主檔的清單，供後台下拉選單挑選既有場地（例如網站設定挑主場、賽程挑比賽地點）與場地管理畫面使用。")
        .Produces<IReadOnlyList<AdminVenueListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/v1/admin/{club}/venues")
            .WithTags("AdminVenues")
            .WithDescription("I5 場地管理（新增、修改、刪除、照片、經緯度、交通說明），需要登入與系統管理員權限。");

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, "site.venue.view", cancellationToken);
            var venue = await repository.GetAsync(id, cancellationToken);
            return venue is null ? Results.NotFound() : Results.Ok(venue);
        })
        .WithName("AdminGetVenue").Produces<AdminVenueDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        // POST /venues/locate —— 「由地址定位」預覽按鈕，只回候選座標，不寫入任何資料。
        group.MapPost("/locate", async (
            string club, LocatePartnerStoreRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, LocateCodes, cancellationToken);
            var result = await repository.LocateAsync(request.Address, cancellationToken);
            return result is null
                ? Results.Json(new { message = "查無此地址的座標，請確認地址是否正確，或直接輸入緯度與經度。" }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new LocatePartnerStoreResponse { Lat = result.Lat, Lng = result.Lng });
        })
        .WithName("AdminLocateVenue").Produces<LocatePartnerStoreResponse>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "site.venue.create", cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminVenueRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            if (request.RemovePhoto)
            {
                throw new Common.AdminValidationException("建立場地時不能選擇移除照片。", "photo");
            }

            var id = Guid.NewGuid();
            var tx = new UploadTransaction(images, documents);
            try
            {
                var photo = await tx.ResolveImageAsync("venues", "photo", "場地照片", form.Files["photo"], remove: false, $"venues/{id}/photo", cancellationToken);
                var created = await repository.CreateAsync(id, request, photo, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/venues/{created.Id}", created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminCreateVenue").Produces<AdminVenueDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "site.venue.update", cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpsertAdminVenueRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var photo = await tx.ResolveImageAsync("venues", "photo", "場地照片", form.Files["photo"], request.RemovePhoto, $"venues/{id}/photo", cancellationToken);
                var updated = await repository.UpdateAsync(id, request, photo, orphans, scope.Identity.AdminUserId, cancellationToken);
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
        })
        .WithName("AdminUpdateVenue").Produces<AdminVenueDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, IImageStorageService images, IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, "site.venue.delete", cancellationToken);
            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            var deleted = await repository.DeleteAsync(id, orphans, cancellationToken);
            if (!deleted)
            {
                return Results.NotFound();
            }

            await tx.CommitAsync(orphans); // 資料列刪除成功後才清照片物件
            return Results.NoContent();
        })
        .WithName("AdminDeleteVenue").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }
}
