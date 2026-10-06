using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I3 全域設定後台端點（政策頁、維護模式；v3.20 起沒有 Logo、品牌色、Favicon）。權限碼 <c>site.global.view／update</c>（sysadmin_only）。
/// <c>PUT</c> 仍是 <c>multipart/form-data</c>（欄位 <c>payload</c>，後台畫面維持此送法），但<b>不再有任何檔案欄位</b>。</summary>
public static class AdminGlobalSettingsEndpoints
{
    private const string PermissionView = "site.global.view";
    private const string PermissionUpdate = "site.global.update";

    public static void MapAdminGlobalSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/global-settings")
            .WithTags("AdminGlobalSettings")
            .WithDescription("I3 全域設定（Cookie 政策、隱私權政策、會員條款、維護模式），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminGlobalSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        })
        .WithName("AdminGetGlobalSettings").Produces<AdminGlobalSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminGlobalSettingsRepository repository, IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, _) = await AdminMultipartForm.ReadAsync<UpdateAdminGlobalSettingsRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);
            return Results.Ok(await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken));
        })
        .WithName("AdminUpdateGlobalSettings").Produces<AdminGlobalSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
