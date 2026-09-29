using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteFacts;

/// <summary>
/// `I` 網站設定——`GEO-03`／`GEO-04` 站台事實（S1-12d）。權限碼 <c>sysadmin_only</c>——規劃書
/// §6 權限矩陣沒有「網站設定」欄，本輪比照 <c>seo.*</c>／<c>system.*</c> 既有先例判斷十個角色
/// 只有系統管理員打勾，見 docs/12b-database-tables.md §7.4「S1-12d 新增」。JSON <c>PUT</c>
/// （沒有圖片欄位，跟 <c>Features/AdminSeo</c> 系列的 <c>multipart/form-data</c> 不同）。
/// </summary>
public static class AdminSiteFactsEndpoints
{
    private const string PermissionView = "site.fact.view";
    private const string PermissionUpdate = "site.fact.update";

    public static void MapAdminSiteFactsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/site-facts")
            .WithTags("AdminSiteFacts")
            .WithDescription("後台網站設定（I 模組）站台事實，需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSiteFactsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetSiteFacts")
        .Produces<AdminSiteFactsDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, UpdateSiteFactsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSiteFactsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminUpdateSiteFacts")
        .Produces<AdminSiteFactsDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
