using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 🔴🔴🔴 後台新聞寫入與後台讀取端點。**這組端點在接上登入與權限之前不得在任何對外環境啟用**
/// （CLAUDE.md 任務指示、<see cref="DevWriteGate"/>）。<c>Program.cs</c> 只在
/// <see cref="DevWriteGate.IsEnabled"/> 回傳 <c>true</c> 時才呼叫 <see cref="MapAdminNewsEndpoints"/>——
/// 關閉時這些路由完全不存在（404，不是 403，不透露端點存在）。
///
/// `created_by`／`updated_by` 由 <see cref="IDevOperatorResolver"/> 從 <c>X-Dev-Operator-Id</c>
/// 標頭解析，🔴 這不是身分驗證，見該介面上的完整說明。
/// </summary>
public static class AdminArticlesEndpoints
{
    public static void MapAdminNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/news")
            .WithTags("AdminNews")
            .WithDescription("🔴 開發模式限定，接上登入與權限之前不得在對外環境啟用，見 Security/DevWriteGate.cs。");

        // GET /api/v1/admin/{club}/news?status=&category=&keyword=&page=&pageSize=
        // 補 STATUS.md S0-12 的缺口：公開 API 只回 published，後台要看得到草稿／排程中的文章。
        group.MapGet("", async (
            string club, string? status, string? category, string? keyword, int? page, int? pageSize,
            IClubResolver clubResolver, AdminArticlesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var result = await repository.ListAsync(scope, status, category, keyword, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListNews")
        .Produces<PagedResult<AdminArticleListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/news/{id}
        group.MapGet("/{id:guid}", async (
            string club, Guid id, IClubResolver clubResolver, AdminArticlesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var article = await repository.GetByIdAsync(scope, id, cancellationToken);
            return article is null ? Results.NotFound() : Results.Ok(article);
        })
        .WithName("AdminGetNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/news  → 一律建立成草稿，狀態轉換是獨立端點。
        group.MapPost("", async (
            string club, CreateArticleRequest request, HttpContext httpContext,
            IClubResolver clubResolver, IDevOperatorResolver operatorResolver, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var operatorId = await operatorResolver.ResolveAsync(httpContext, cancellationToken);
            var created = await repository.CreateAsync(scope, request, operatorId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/news/{created.Id}", created);
        })
        .WithName("AdminCreateNewsArticle")
        .Produces<AdminArticleDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // PUT /api/v1/admin/{club}/news/{id}  → 整份取代可編輯內容，不改狀態。
        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateArticleRequest request, HttpContext httpContext,
            IClubResolver clubResolver, IDevOperatorResolver operatorResolver, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var operatorId = await operatorResolver.ResolveAsync(httpContext, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, operatorId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/{club}/news/{id}/publish  → draft／scheduled → published，立即生效。
        group.MapPost("/{id:guid}/publish", async (
            string club, Guid id, PublishArticleRequest request, HttpContext httpContext,
            IClubResolver clubResolver, IDevOperatorResolver operatorResolver, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var operatorId = await operatorResolver.ResolveAsync(httpContext, cancellationToken);
            var published = await repository.PublishAsync(scope, id, request, operatorId, cancellationToken);
            return published is null ? Results.NotFound() : Results.Ok(published);
        })
        .WithName("AdminPublishNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/{club}/news/{id}/schedule  → draft／scheduled → scheduled（未來時間）。
        // 🔴 「排程時間到了誰把狀態改成 published」目前沒有排程器，見 README「排程發布：誰改狀態」。
        group.MapPost("/{id:guid}/schedule", async (
            string club, Guid id, ScheduleArticleRequest request, HttpContext httpContext,
            IClubResolver clubResolver, IDevOperatorResolver operatorResolver, AdminArticlesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var operatorId = await operatorResolver.ResolveAsync(httpContext, cancellationToken);
            var scheduled = await repository.ScheduleAsync(scope, id, request, operatorId, cancellationToken);
            return scheduled is null ? Results.NotFound() : Results.Ok(scheduled);
        })
        .WithName("AdminScheduleNewsArticle")
        .Produces<AdminArticleDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE /api/v1/admin/{club}/news/{id}?expectedUpdatedAt=2026-09-22T03:00:00Z
        group.MapDelete("/{id:guid}", async (
            string club, Guid id, DateTime expectedUpdatedAt,
            IClubResolver clubResolver, AdminArticlesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, expectedUpdatedAt, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteNewsArticle")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
