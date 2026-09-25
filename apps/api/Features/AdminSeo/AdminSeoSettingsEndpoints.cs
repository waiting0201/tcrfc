using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 全站 SEO 預設＋追蹤碼（S1-12）。權限碼 <c>sysadmin_only</c>——矩陣「SEO／設定」欄除了
/// 內容編輯的「單頁 SEO」外，十個角色裡只有系統管理員打勾，見 docs/12b §7.4「S1-12 新增」附註。
/// <c>PUT</c> 是 <c>multipart/form-data</c>（規劃書 §4.0「選檔不上傳、儲存才上傳」）：
/// <c>payload</c> 文字欄位＋選填的 <c>ogImage</c> 圖片檔案。
/// </summary>
public static class AdminSeoSettingsEndpoints
{
    private const string PermissionView = "seo.setting.view";
    private const string PermissionUpdate = "seo.setting.update";

    public static void MapAdminSeoSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/settings")
            .WithTags("AdminSeoSettings")
            .WithDescription("後台全站 SEO 預設與追蹤碼（H 模組），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeoSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.GetAsync(scope, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminGetSeoSettings")
        .Produces<AdminSeoSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminSeoSettingsRepository repository, IImageStorageService imageStorage,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);

            var (request, ogImageFile) = await AdminSeoSettingsRequestForm.ReadAsync(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (ogImageFile is not null && request.RemoveOgImage)
            {
                throw new AdminSeoValidationException("上傳新圖片與勾選「移除全站預設 OG 圖片」不能同時發生，請擇一。");
            }

            var previousOgImageKey = await repository.GetCurrentOgImageKeyAsync(scope.ClubId, cancellationToken);

            ImageFieldUpdate ogImageUpdate;
            if (ogImageFile is not null)
            {
                UploadSlotPolicy.Validate("clubs", "ogImage");
                var uploaded = await UploadImageAsync(scope, ogImageFile, imageStorage, cancellationToken);
                ogImageUpdate = ImageFieldUpdate.Set(uploaded.Key, uploaded.Width, uploaded.Height);
            }
            else if (request.RemoveOgImage)
            {
                ogImageUpdate = ImageFieldUpdate.Remove;
            }
            else
            {
                ogImageUpdate = ImageFieldUpdate.Keep;
            }

            try
            {
                var result = await repository.UpdateAsync(scope, request, ogImageUpdate, scope.Identity.AdminUserId, cancellationToken);

                // 換圖或清空成功後才刪舊物件（規劃書 §4.0「換圖與刪除」）。fail-open：舊物件清理
                // 失敗不影響這次請求已經成功的結果，比照 Features/AdminNews 既有慣例。
                if (ogImageUpdate.Change && !string.IsNullOrEmpty(previousOgImageKey))
                {
                    await imageStorage.DeleteAsync(previousOgImageKey, cancellationToken);
                }

                return Results.Ok(result);
            }
            catch
            {
                // 補償交易：圖片已寫入物件儲存，但資料列沒有寫成功。E-47 教訓：一律用
                // CancellationToken.None，不沿用可能已取消的請求 token。
                if (ogImageUpdate.Change && ogImageUpdate.Key is not null)
                {
                    await imageStorage.DeleteAsync(ogImageUpdate.Key, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminUpdateSeoSettings")
        .Produces<AdminSeoSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }

    private static async Task<UploadedImageInfo> UploadImageAsync(
        AdminClubScope scope, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }

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

        var objectKeyPrefix = $"{scope.ClubCode}/seo/og-image";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
