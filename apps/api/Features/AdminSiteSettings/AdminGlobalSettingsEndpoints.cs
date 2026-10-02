using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I3 全域設定後台端點（Logo、品牌色、Favicon、政策頁、維護模式）。權限碼 <c>site.global.view／update</c>（sysadmin_only）。
/// <c>PUT</c> 是 <c>multipart/form-data</c>：<c>payload</c>（JSON）＋選填檔案欄位 <c>logoLight</c>／<c>logoDark</c>／<c>favicon</c>（規劃書 §4.0 上傳通則）。</summary>
public static class AdminGlobalSettingsEndpoints
{
    private const string PermissionView = "site.global.view";
    private const string PermissionUpdate = "site.global.update";

    public static void MapAdminGlobalSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/global-settings")
            .WithTags("AdminGlobalSettings")
            .WithDescription("I3 全域設定（Logo、品牌色、Favicon、Cookie 政策、隱私權政策、會員條款、維護模式），需要登入與系統管理員權限。");

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
            AdminGlobalSettingsRepository repository, IImageStorageService images, IDocumentStorageService documents,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var (request, form) = await AdminMultipartForm.ReadAsync<UpdateAdminGlobalSettingsRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var tx = new UploadTransaction(images, documents);
            var orphans = new OrphanedObjects();
            try
            {
                var prefix = $"{scope.ClubCode}/brand";
                var light = await tx.ResolveImageAsync("clubs", "logoLight", "淺色底 Logo", form.Files["logoLight"], request.RemoveLogoLight, $"{prefix}/logo-light", cancellationToken);
                var dark = await tx.ResolveImageAsync("clubs", "logoDark", "深色底 Logo", form.Files["logoDark"], request.RemoveLogoDark, $"{prefix}/logo-dark", cancellationToken);
                var favicon = await tx.ResolveImageAsync("clubs", "favicon", "Favicon", form.Files["favicon"], request.RemoveFavicon, $"{prefix}/favicon", cancellationToken);
                var result = await repository.UpdateAsync(scope, request, light, dark, favicon, orphans, scope.Identity.AdminUserId, cancellationToken);
                await tx.CommitAsync(orphans); // 儲存成功後才刪舊圖
                return Results.Ok(result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        })
        .WithName("AdminUpdateGlobalSettings").Produces<AdminGlobalSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
