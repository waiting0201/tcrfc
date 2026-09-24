using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// 後台頁面管理（B1）寫入與後台讀取端點。形狀與授權模式逐字比照
/// <c>Features/AdminNews/AdminArticlesEndpoints.cs</c>：每個端點一律先呼叫
/// <see cref="IAdminClubAuthorizer.AuthorizeAsync"/>，<c>created_by</c>／<c>updated_by</c> 一律取自
/// <see cref="AdminClubScope.Identity"/>。
///
/// 權限碼對應 docs/12b-database-tables.md §7.3 命名慣例（<c>&lt;domain&gt;.&lt;object&gt;.&lt;action&gt;</c>），
/// module_code=B、submodule_code=B1（頁面管理）。
///
/// 🔴 建立／更新是 <c>multipart/form-data</c>（規劃書 §4.0「選檔不上傳、儲存才上傳」），但檔案欄位
/// 命名慣例跟新聞不同——見 <see cref="AdminPageRequestForm"/> 與 <see cref="PageBlockContentProcessor"/>
/// 檔頭：頁面區塊可能同時有多張待上傳圖片，用 <c>file:{區塊索引}:{圖片路徑}</c> 命名，不是固定的
/// 單一 <c>file</c> 欄位。
/// </summary>
public static class AdminPagesEndpoints
{
    private const string PermissionView = "content.page.view";
    private const string PermissionCreate = "content.page.create";
    private const string PermissionUpdate = "content.page.update";
    private const string PermissionPublish = "content.page.publish";
    private const string PermissionDelete = "content.page.delete";

    public static void MapAdminPagesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/pages")
            .WithTags("AdminPages")
            .WithDescription("後台頁面管理（B1）讀寫，需要登入與俱樂部授權，見 Security/AdminClubAuthorizer.cs。");

        // GET /api/v1/admin/{club}/pages?status=&keyword=&page=&pageSize=
        group.MapGet("", async (
            string club, string? status, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListAsync(adminScope, status, keyword, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListPages")
        .Produces<PagedResult<AdminPageListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/pages/{id}
        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var pageDto = await repository.GetByIdAsync(adminScope, id, cancellationToken);
            return pageDto is null ? Results.NotFound() : Results.Ok(pageDto);
        })
        .WithName("AdminGetPage")
        .Produces<AdminPageDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/pages  → 一律建立成草稿，狀態轉換是獨立端點。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var operatorId = adminScope.Identity.AdminUserId;

            var (request, files) = await AdminPageRequestForm.ReadAsync<CreatePageRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            // 🔴 id 必須在上傳之前就決定：區塊圖片的物件鍵路徑要指到「這張圖屬於哪一筆將要建立的
            // 頁面」，見 AdminPagesRepository.CreateAsync 上的說明（同 AdminArticlesEndpoints 的既有慣例）。
            var pageId = Guid.NewGuid();
            var created = await repository.CreateAsync(adminScope, pageId, request, files, operatorId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/pages/{created.Id}", created);
        })
        .WithName("AdminCreatePage")
        .Produces<AdminPageDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        // PUT /api/v1/admin/{club}/pages/{id}  → 整份取代（SEO ＋ 全部區塊），不改狀態。
        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var operatorId = adminScope.Identity.AdminUserId;

            var (request, files) = await AdminPageRequestForm.ReadAsync<UpdatePageRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var updated = await repository.UpdateAsync(adminScope, id, request, files, operatorId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdatePage")
        .Produces<AdminPageDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        // POST /api/v1/admin/{club}/pages/{id}/publish  → draft／scheduled → published，立即生效。
        group.MapPost("/{id:guid}/publish", async (
            string club, Guid id, PublishPageRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionPublish, cancellationToken);
            var published = await repository.PublishAsync(adminScope, id, request, adminScope.Identity.AdminUserId, cancellationToken);
            return published is null ? Results.NotFound() : Results.Ok(published);
        })
        .WithName("AdminPublishPage")
        .Produces<AdminPageDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/{club}/pages/{id}/schedule  → draft／scheduled → scheduled（未來時間）。
        group.MapPost("/{id:guid}/schedule", async (
            string club, Guid id, SchedulePageRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionPublish, cancellationToken);
            var scheduled = await repository.ScheduleAsync(adminScope, id, request, adminScope.Identity.AdminUserId, cancellationToken);
            return scheduled is null ? Results.NotFound() : Results.Ok(scheduled);
        })
        .WithName("AdminSchedulePage")
        .Produces<AdminPageDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE /api/v1/admin/{club}/pages/{id}?expectedUpdatedAt=2026-09-24T03:00:00Z
        group.MapDelete("/{id:guid}", async (
            string club, Guid id, DateTime expectedUpdatedAt, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(adminScope, id, expectedUpdatedAt, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeletePage")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // GET /api/v1/admin/{club}/pages/{id}/versions?page=&pageSize=
        group.MapGet("/{id:guid}/versions", async (
            string club, Guid id, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListVersionsAsync(adminScope, id, normalizedPage, normalizedPageSize, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("AdminListPageVersions")
        .Produces<PagedResult<AdminPageVersionListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/pages/{id}/versions/{versionNo}
        group.MapGet("/{id:guid}/versions/{versionNo:int}", async (
            string club, Guid id, int versionNo, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var version = await repository.GetVersionAsync(adminScope, id, versionNo, cancellationToken);
            return version is null ? Results.NotFound() : Results.Ok(version);
        })
        .WithName("AdminGetPageVersion")
        .Produces<AdminPageVersionDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/pages/{id}/versions/{versionNo}/restore
        // 🔴 我的判斷（規劃書沒定義還原的行為）：還原＝以舊版內容產生一個新版本，不改變發布狀態。
        // 見 AdminPagesRepository.RestoreVersionAsync 上的完整說明。權限碼用 update（這是一次內容變更）。
        group.MapPost("/{id:guid}/versions/{versionNo:int}/restore", async (
            string club, Guid id, int versionNo, RestorePageVersionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPagesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var restored = await repository.RestoreVersionAsync(adminScope, id, versionNo, request, adminScope.Identity.AdminUserId, cancellationToken);
            return restored is null ? Results.NotFound() : Results.Ok(restored);
        })
        .WithName("AdminRestorePageVersion")
        .Produces<AdminPageDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
