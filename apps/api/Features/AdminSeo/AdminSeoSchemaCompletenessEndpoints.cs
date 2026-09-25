using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>結構化資料完整性檢查（GEO-05，S1-12c）。唯讀報表，權限碼 <c>sysadmin_only</c>，
/// 理由同 <see cref="AdminSeoSettingsEndpoints"/>／<see cref="AdminSeoReportEndpoints"/> 檔頭說明——
/// 矩陣「SEO／設定」欄除了內容編輯的「單頁 SEO」外，十個角色裡只有系統管理員打勾。</summary>
public static class AdminSeoSchemaCompletenessEndpoints
{
    private const string PermissionView = "seo.schema.view";

    public static void MapAdminSeoSchemaCompletenessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/schema-completeness")
            .WithTags("AdminSeoSchemaCompleteness")
            .WithDescription("後台結構化資料完整性檢查（H 模組，GEO-05），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeoSchemaCompletenessRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetReportAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetSchemaCompletenessReport")
        .Produces<SchemaCompletenessReportDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
