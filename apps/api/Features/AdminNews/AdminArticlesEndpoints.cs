using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 後台新聞寫入與後台讀取端點。
///
/// 每個端點一律先呼叫 <see cref="IAdminClubAuthorizer.AuthorizeAsync"/>，同時證明
/// 「有登入」「這個俱樂部存在」「對這個俱樂部有授權」「有這個操作的權限碼」四件事，
/// 缺一即擲例外（401／403，見 <c>Common/ApiExceptionHandler.cs</c>）。`created_by`／
/// `updated_by` 一律取自 <see cref="AdminClubScope.Identity"/>，不讀任何呼叫端可自報的標頭。
/// 舊有的開發模式開關（環境旗標決定路由存不存在）已於 2026-09-23 整支移除，見
/// apps/api/README.md「S1」整節。
///
/// 權限碼對應 docs/12b-database-tables.md §7.3 命名慣例（<c>&lt;domain&gt;.&lt;object&gt;.&lt;action&gt;</c>），
/// module_code=B、submodule_code=B2（新聞與故事）。
///
/// 🔴🔴🔴 **S0-8 修正（2026-09-22）**：建立／更新這兩個端點是 <c>multipart/form-data</c>
/// 單一請求——封面圖片跟其餘欄位一起送出，逐字對應規劃書 §4.0 與第 53 行「選檔不上傳、儲存才
/// 上傳」，細節見 apps/api/README.md「圖片上傳共用元件」整節。
/// </summary>
public static class AdminArticlesEndpoints
{
    private const string PermissionView = "content.article.view";
    private const string PermissionCreate = "content.article.create";
    private const string PermissionUpdate = "content.article.update";
    private const string PermissionPublish = "content.article.publish";
    private const string PermissionDelete = "content.article.delete";

    public static void MapAdminNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/news")
            .WithTags("AdminNews")
            .WithDescription("後台新聞讀寫，需要登入與俱樂部授權，見 Security/AdminClubAuthorizer.cs。");

        // GET /api/v1/admin/{club}/news?status=&category=&keyword=&page=&pageSize=
        // 補 STATUS.md S0-12 的缺口：公開 API 只回 published，後台要看得到草稿／排程中的文章。
        group.MapGet("", async (
            string club, string? status, string? category, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminArticlesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListAsync(adminScope, status, category, keyword, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListNews")
        .Produces<PagedResult<AdminArticleListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/news/{id}
        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminArticlesRepository repository, CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var article = await repository.GetByIdAsync(adminScope, id, cancellationToken);
            return article is null ? Results.NotFound() : Results.Ok(article);
        })
        .WithName("AdminGetNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/news  → 一律建立成草稿，狀態轉換是獨立端點。
        // 🔴🔴🔴 S0-8 修正：multipart/form-data，固定兩個欄位——`payload`（JSON 文字，其餘欄位）
        // 與可選的 `file`（封面圖片）。契約細節見 apps/api/README.md。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminArticlesRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var operatorId = adminScope.Identity.AdminUserId;

            var (request, file) = await AdminArticleRequestForm.ReadAsync<CreateArticleRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            // 🔴 id 必須在上傳之前就決定：物件鍵路徑（{club}/articles/{articleId}/cover/...）要指到
            // 「這張圖屬於哪一筆將要建立的資料列」，見 AdminArticlesRepository.CreateAsync 上的說明。
            var articleId = Guid.NewGuid();
            string? coverKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("articles", "cover");
                var uploaded = await UploadCoverAsync(adminScope, articleId, file, imageStorage, cancellationToken);
                coverKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(adminScope, articleId, request, coverKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/news/{created.Id}", created);
            }
            catch
            {
                // 🔴 失敗時不得留下半套（CLAUDE.md 任務指示）：圖片已經成功寫進物件儲存，
                // 但這篇文章的資料列沒有寫成功（分類不存在、slug 重複、標題空白……）。
                // 物件儲存跟 SQL 是兩個系統，做不到真正跨系統的 atomic transaction，
                // 這裡是補償交易（compensating transaction）：刪掉剛剛上傳的物件，
                // 不留下沒有任何資料列指著它的孤兒物件，再把原例外原樣往上丟。
                if (coverKey is not null)
                {
                    await imageStorage.DeleteAsync(coverKey, cancellationToken);
                }

                throw;
            }
        })
        .WithName("AdminCreateNewsArticle")
        .Produces<AdminArticleDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        // PUT /api/v1/admin/{club}/news/{id}  → 整份取代可編輯內容，不改狀態。
        // 🔴🔴🔴 S0-8 修正：跟 POST 一樣改成 multipart/form-data。封面圖片三態見
        // UpdateArticleRequest.RemoveCover／CoverKeyUpdate 上的說明。
        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminArticlesRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var operatorId = adminScope.Identity.AdminUserId;

            var (request, file) = await AdminArticleRequestForm.ReadAsync<UpdateArticleRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemoveCover)
            {
                throw new AdminArticleValidationException("不能同時上傳新的封面圖片與移除封面圖片，請擇一。");
            }

            string? uploadedKey = null;
            CoverKeyUpdate coverUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("articles", "cover");
                var uploaded = await UploadCoverAsync(adminScope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                coverUpdate = CoverKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemoveCover)
            {
                coverUpdate = CoverKeyUpdate.Set(null);
            }
            else
            {
                coverUpdate = CoverKeyUpdate.Keep;
            }

            try
            {
                var updated = await repository.UpdateAsync(adminScope, id, request, coverUpdate, operatorId, cancellationToken);
                if (updated is null)
                {
                    // 找不到這篇文章（跨俱樂部或真的不存在）：圖片已經上傳成功，但不會有任何資料列
                    // 指到它——同樣是補償交易，刪掉剛剛上傳的物件（見 POST 端點同一段說明）。
                    if (uploadedKey is not null)
                    {
                        await imageStorage.DeleteAsync(uploadedKey, cancellationToken);
                    }

                    return Results.NotFound();
                }

                return Results.Ok(updated);
            }
            catch
            {
                if (uploadedKey is not null)
                {
                    await imageStorage.DeleteAsync(uploadedKey, cancellationToken);
                }

                throw;
            }
        })
        .WithName("AdminUpdateNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        // POST /api/v1/admin/{club}/news/{id}/publish  → draft／scheduled → published，立即生效。
        group.MapPost("/{id:guid}/publish", async (
            string club, Guid id, PublishArticleRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionPublish, cancellationToken);
            var published = await repository.PublishAsync(adminScope, id, request, adminScope.Identity.AdminUserId, cancellationToken);
            return published is null ? Results.NotFound() : Results.Ok(published);
        })
        .WithName("AdminPublishNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/{club}/news/{id}/schedule  → draft／scheduled → scheduled（未來時間）。
        // 🔴 「排程時間到了誰把狀態改成 published」目前沒有排程器，見 README「排程發布：誰改狀態」。
        group.MapPost("/{id:guid}/schedule", async (
            string club, Guid id, ScheduleArticleRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionPublish, cancellationToken);
            var scheduled = await repository.ScheduleAsync(adminScope, id, request, adminScope.Identity.AdminUserId, cancellationToken);
            return scheduled is null ? Results.NotFound() : Results.Ok(scheduled);
        })
        .WithName("AdminScheduleNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE /api/v1/admin/{club}/news/{id}?expectedUpdatedAt=2026-09-22T03:00:00Z
        group.MapDelete("/{id:guid}", async (
            string club, Guid id, DateTime expectedUpdatedAt, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var adminScope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(adminScope, id, expectedUpdatedAt, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteNewsArticle")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// 🔴🔴🔴 S0-8 修正：把封面圖片讀進記憶體並交給 <see cref="IImageStorageService"/> 處理＋上傳，
    /// 邏輯跟已經停用的 <c>Features/Uploads/UploadsEndpoints.cs</c> 完全相同（原地搬過來，不重寫
    /// <see cref="ImageProcessor"/> 的轉檔邏輯）——差別只在這裡是被建立／更新端點在同一次請求裡
    /// 直接呼叫，不再是一個獨立的 HTTP 往返。
    /// </summary>
    private static async Task<UploadedImageInfo> UploadCoverAsync(
        AdminClubScope scope, Guid articleId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }

        // 🔴 宣告長度先擋一次超過上限的檔案，避免白白花時間讀進記憶體；實際位元組數在
        // IImageStorageService.UploadAsync 內部還會再檢查一次，兩層都不信任呼叫端。
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

        // 物件鍵前綴含俱樂部代碼＋實體型別＋實體 id＋欄位名，確保「一張圖只屬於一筆資料列」
        // （docs/14-invariants.md）；用 scope.ClubCode（已驗證）而不是路由原始字串。
        var objectKeyPrefix = $"{scope.ClubCode}/articles/{articleId}/cover";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
