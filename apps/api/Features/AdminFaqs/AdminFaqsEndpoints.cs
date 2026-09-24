using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// B4 常見問題（題目本身）後台讀寫端點，俱樂部範圍（<c>faqs.club_id</c> 可為空之一），
/// 一律經 <see cref="IAdminClubAuthorizer"/>，形狀比照 <c>Features/AdminNews/AdminArticlesEndpoints.cs</c>。
/// </summary>
public static class AdminFaqsEndpoints
{
    private const string PermissionView = "content.faq.view";
    private const string PermissionCreate = "content.faq.create";
    private const string PermissionUpdate = "content.faq.update";
    private const string PermissionDelete = "content.faq.delete";

    public static void MapAdminFaqsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/faqs")
            .WithTags("AdminFaqs")
            .WithDescription("後台常見問題讀寫，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/faqs?status=&categoryId=&keyword=&sort=low_rating&page=&pageSize=
        group.MapGet("", async (
            string club, string? status, Guid? categoryId, string? keyword, string? sort, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListAsync(scope, status, categoryId, keyword, sort, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListFaqs")
        .Produces<PagedResult<AdminFaqListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var faq = await repository.GetByIdAsync(scope, id, cancellationToken);
            return faq is null ? Results.NotFound() : Results.Ok(faq);
        })
        .WithName("AdminGetFaq")
        .Produces<AdminFaqDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateFaqRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/faqs/{created.Id}", created);
        })
        .WithName("AdminCreateFaq")
        .Produces<AdminFaqDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateFaqRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateFaq")
        .Produces<AdminFaqDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteFaq")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ── 批次操作（規劃書 B4「批次操作：批次改分類、批次顯示／隱藏」，主站規劃書行 1033）──
        // 🔴 逐字比照 Features/AdminNews 既有的批次操作端點；FAQ 沒有獨立的 content.faq.publish
        // 權限碼（單篇的顯示／隱藏本來就走 content.faq.update，見 AdminFaqDtos.cs 「Status」欄位
        // 上的說明），批次顯示／隱藏因此也掛 content.faq.update，不是另外新增一組權限碼——
        // 「批次操作的權限碼要跟單筆對應動作一致」這條規則本身沿用 S1-5 的做法，只是 FAQ 這裡
        // 單筆動作原本就沒有拆出獨立的 publish 權限，見 apps/api/README.md 的說明。

        // POST /api/v1/admin/{club}/faqs/batch/category
        group.MapPost("/batch/category", async (
            string club, BatchChangeFaqCategoryRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.BatchChangeCategoryAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminBatchChangeFaqCategory")
        .Produces<BatchFaqOperationResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/faqs/batch/show  → 批次「顯示」（status = published）。
        group.MapPost("/batch/show", async (
            string club, BatchFaqIdsRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.BatchSetVisibilityAsync(scope, request, publish: true, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminBatchShowFaqs")
        .Produces<BatchFaqOperationResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/faqs/batch/hide  → 批次「隱藏」（status = draft）。
        group.MapPost("/batch/hide", async (
            string club, BatchFaqIdsRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.BatchSetVisibilityAsync(scope, request, publish: false, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminBatchHideFaqs")
        .Produces<BatchFaqOperationResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ── CSV 匯入／匯出（規劃書 B4，主站規劃書行 1033）───────────────────────────────
        // 格式定義見 AdminFaqsRepository.CsvHeader 上的說明；沒有共用元件（跟圖片上傳不同），
        // 直接讀＋寫 HTTP 請求／回應主體的原始位元組，不是 multipart/form-data
        // （這裡只有單一檔案、沒有其他欄位要跟著送，比照純文字檔上傳的最小形狀）。

        // GET /api/v1/admin/{club}/faqs/export → 下載這個俱樂部自己的常見問題 CSV（UTF-8 BOM）。
        group.MapGet("/export", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var csvText = await repository.ExportCsvAsync(scope, cancellationToken);
            var bytes = Common.CsvUtils.ToUtf8BytesWithBom(csvText);
            return Results.File(bytes, "text/csv; charset=utf-8", $"faqs-{club}.csv");
        })
        .WithName("AdminExportFaqsCsv")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/faqs/import  body：CSV 檔案原始位元組（Content-Type 不拘，
        // 一律以 UTF-8 解碼，開頭若有 BOM 由 CsvUtils.Parse 自動去除）。
        group.MapPost("/import", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminFaqsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);

            using var reader = new StreamReader(httpContext.Request.Body, System.Text.Encoding.UTF8);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);

            var result = await repository.ImportCsvAsync(scope, csvContent, scope.Identity.AdminUserId, cancellationToken);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        })
        .WithName("AdminImportFaqsCsv")
        .Produces<FaqCsvImportResultDto>()
        .Produces<FaqCsvImportResultDto>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
