using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>孤立頁面偵測（S1-12）。唯讀報表，權限碼 <c>sysadmin_only</c>，理由同
/// <see cref="AdminSeoSettingsEndpoints"/> 檔頭說明。</summary>
public static class AdminSeoReportEndpoints
{
    private const string PermissionView = "seo.report.view";

    public static void MapAdminSeoReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/orphan-pages")
            .WithTags("AdminSeoReport")
            .WithDescription("後台孤立頁面偵測（H 模組），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeoReportRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetOrphanPagesAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetOrphanPages")
        .Produces<OrphanPageReportDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
