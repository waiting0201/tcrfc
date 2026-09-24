using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// B4「主題分類管理」，全域端點（不含 <c>{club}</c> 路由段——<c>faq_categories</c> 沒有
/// <c>club_id</c>），比照 J 模組用 <see cref="IAdminSystemAuthorizer"/>。⚠️ 這幾個權限碼
/// **不是** <c>sysadmin_only</c>（跟真正的 J 模組不同）：規劃書 §6 矩陣「FAQ」欄把內容編輯／
/// 客服等角色也放進來，見 <c>db/seed/generate-club-seed-sql.py</c> §18.2 的權限碼定義與
/// apps/api/README.md 的角色指派說明。
/// </summary>
public static class AdminFaqCategoriesEndpoints
{
    private const string PermissionView = "content.faq_category.view";
    private const string PermissionCreate = "content.faq_category.create";
    private const string PermissionUpdate = "content.faq_category.update";
    private const string PermissionDelete = "content.faq_category.delete";

    public static void MapAdminFaqCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/faq-categories")
            .WithTags("AdminFaqCategories")
            .WithDescription("B4 常見問題主題分類，全站共用主檔，需要登入與對應權限碼（不分俱樂部）。");

        group.MapGet("", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminFaqCategoriesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var categories = await repository.ListAsync(cancellationToken);
            return Results.Ok(categories);
        })
        .WithName("AdminListFaqCategories")
        .Produces<IReadOnlyList<AdminFaqCategoryListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminFaqCategoriesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var category = await repository.GetByIdAsync(id, cancellationToken);
            return category is null ? Results.NotFound() : Results.Ok(category);
        })
        .WithName("AdminGetFaqCategory")
        .Produces<AdminFaqCategoryDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            CreateAdminFaqCategoryRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminFaqCategoriesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/faq-categories/{created.Id}", created);
        })
        .WithName("AdminCreateFaqCategory")
        .Produces<AdminFaqCategoryDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateAdminFaqCategoryRequest request, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminFaqCategoriesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateFaqCategory")
        .Produces<AdminFaqCategoryDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // DELETE＝規劃書「停用分類」的實作方式，見 AdminFaqCategoriesRepository 檔頭說明。
        group.MapDelete("/{id:guid}", async (
            Guid id, HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminFaqCategoriesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteFaqCategory")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
