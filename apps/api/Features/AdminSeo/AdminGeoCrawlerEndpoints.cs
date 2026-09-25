using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// `GEO-02` AI 爬蟲授權（S1-12b）。權限碼 <c>sysadmin_only</c>，理由同
/// <see cref="AdminSeoSettingsEndpoints"/> 檔頭說明。純 JSON body（沒有檔案欄位）。
/// </summary>
public static class AdminGeoCrawlerEndpoints
{
    private const string PermissionView = "seo.crawler.view";
    private const string PermissionUpdate = "seo.crawler.update";

    public static void MapAdminGeoCrawlerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/crawler-settings")
            .WithTags("AdminSeoCrawler")
            .WithDescription("後台 AI 爬蟲授權維護（H 模組 GEO-02），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminGeoCrawlerRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetCrawlerSettings")
        .Produces<AdminCrawlerSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, UpdateCrawlerSettingsRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminGeoCrawlerRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminUpdateCrawlerSettings")
        .Produces<AdminCrawlerSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
