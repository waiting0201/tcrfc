using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// `GEO-01` <c>llms.txt</c> 內容維護（S1-12a）。權限碼 <c>sysadmin_only</c>，理由同
/// <see cref="AdminSeoSettingsEndpoints"/> 檔頭說明——矩陣「SEO／設定」欄十個角色只有系統管理員
/// 打勾。純文字內容，一般 JSON body（不需要 <c>multipart/form-data</c>，這五個區塊沒有圖片欄位）。
/// </summary>
public static class AdminGeoLlmsEndpoints
{
    private const string PermissionView = "seo.llms.view";
    private const string PermissionUpdate = "seo.llms.update";

    public static void MapAdminGeoLlmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/llms-content")
            .WithTags("AdminSeoLlms")
            .WithDescription("後台 llms.txt 內容維護（H 模組 GEO-01），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminGeoLlmsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetLlmsContent")
        .Produces<AdminLlmsContentDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, UpdateLlmsContentRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminGeoLlmsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminUpdateLlmsContent")
        .Produces<AdminLlmsContentDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
