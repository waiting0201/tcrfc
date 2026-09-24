using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Tcrfc.Api.Videos;

namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>
/// B3 Hero 輪播後台讀寫，俱樂部範圍，一律經 <see cref="IAdminClubAuthorizer"/>。
/// <c>multipart/form-data</c> 契約與補償刪除邏輯逐字比照 <c>Features/AdminNews/AdminArticlesEndpoints.cs</c>：
/// 建立時 <c>file</c>（海報格／圖片）為必填（<c>banners.image_key</c> 是 <c>NOT NULL</c>），
/// 更新時省略＝維持原圖。<c>video</c> 欄位（v3.14）僅 <c>mediaType="video"</c> 時使用。
/// </summary>
public static class AdminBannersEndpoints
{
    private const string PermissionView = "content.banner.view";
    private const string PermissionCreate = "content.banner.create";
    private const string PermissionUpdate = "content.banner.update";
    private const string PermissionDelete = "content.banner.delete";

    public static void MapAdminBannersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/banners")
            .WithTags("AdminBanners")
            .WithDescription("後台首頁輪播讀寫，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBannersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var banners = await repository.ListAsync(scope, cancellationToken);
            return Results.Ok(banners);
        })
        .WithName("AdminListBanners")
        .Produces<IReadOnlyList<AdminBannerListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBannersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var banner = await repository.GetByIdAsync(scope, id, cancellationToken);
            return banner is null ? Results.NotFound() : Results.Ok(banner);
        })
        .WithName("AdminGetBanner")
        .Produces<AdminBannerDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminBannersRepository repository,
            IImageStorageService imageStorage, IVideoStorageService videoStorage,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);

            var (request, file, videoFile) = await AdminBannerRequestForm.ReadAsync<CreateBannerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is null)
            {
                throw new AdminBannerValidationException("請選擇輪播圖片，這個欄位是必填的（影片模式下這張圖作為海報格）。");
            }

            // 🔴 fail fast：先驗證 mediaType／檔案欄位互斥，再上傳，避免對一個註定會被拒絕的
            // 請求白白做了圖片／影片上傳（E-47 系譜同一種「先驗證再碰外部資源」原則）。
            var mediaType = AdminBannersRepository.ValidateMediaType(request.MediaType);
            if (mediaType == "video" && videoFile is null)
            {
                throw new AdminBannerValidationException("素材種類為「影片」時，必須上傳影片檔案。");
            }
            if (mediaType == "image" && videoFile is not null)
            {
                throw new AdminBannerValidationException("素材種類為「圖片」時，不可上傳影片檔案。");
            }

            UploadSlotPolicy.Validate("banners", "image");

            var bannerId = Guid.NewGuid();
            var uploadedImage = await UploadImageAsync(scope, bannerId, file, imageStorage, cancellationToken);

            string? uploadedVideoKey = null;
            if (videoFile is not null)
            {
                UploadSlotPolicy.Validate("banners", "video");
                var uploadedVideo = await UploadVideoAsync(scope, bannerId, videoFile, videoStorage, cancellationToken);
                uploadedVideoKey = uploadedVideo.Key;
            }

            try
            {
                var created = await repository.CreateAsync(
                    scope, bannerId, request, uploadedImage.Key, uploadedImage.Width, uploadedImage.Height,
                    uploadedVideoKey, scope.Identity.AdminUserId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/banners/{created.Id}", created);
            }
            catch
            {
                // 補償交易：圖片／影片已寫入物件儲存，但資料列沒有寫成功（例如上架時間早於下架時間）。
                // 🔴 E-47 教訓：一律用 CancellationToken.None，不沿用可能已取消的請求 token。
                await imageStorage.DeleteAsync(uploadedImage.Key, CancellationToken.None);
                if (uploadedVideoKey is not null)
                {
                    await videoStorage.DeleteAsync(uploadedVideoKey, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminCreateBanner")
        .Produces<AdminBannerDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminBannersRepository repository,
            IImageStorageService imageStorage, IVideoStorageService videoStorage,
            IOptions<JsonOptions> jsonOptions, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);

            var (request, file, videoFile) = await AdminBannerRequestForm.ReadAsync<UpdateBannerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var mediaType = AdminBannersRepository.ValidateMediaType(request.MediaType);
            if (mediaType == "image" && videoFile is not null)
            {
                throw new AdminBannerValidationException("素材種類為「圖片」時，不可上傳影片檔案。");
            }

            string? uploadedKey = null;
            int? uploadedWidth = null;
            int? uploadedHeight = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("banners", "image");
                var uploaded = await UploadImageAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                uploadedWidth = uploaded.Width;
                uploadedHeight = uploaded.Height;
            }

            string? uploadedVideoKey = null;
            if (videoFile is not null)
            {
                UploadSlotPolicy.Validate("banners", "video");
                var uploadedVideo = await UploadVideoAsync(scope, id, videoFile, videoStorage, cancellationToken);
                uploadedVideoKey = uploadedVideo.Key;
            }

            try
            {
                var updated = await repository.UpdateAsync(
                    scope, id, request, uploadedKey, uploadedWidth, uploadedHeight, uploadedVideoKey,
                    scope.Identity.AdminUserId, cancellationToken);
                if (updated is null)
                {
                    if (uploadedKey is not null)
                    {
                        await imageStorage.DeleteAsync(uploadedKey, cancellationToken);
                    }
                    if (uploadedVideoKey is not null)
                    {
                        await videoStorage.DeleteAsync(uploadedVideoKey, cancellationToken);
                    }

                    return Results.NotFound();
                }

                return Results.Ok(updated);
            }
            catch
            {
                if (uploadedKey is not null)
                {
                    await imageStorage.DeleteAsync(uploadedKey, CancellationToken.None); // E-47：不沿用已取消的 token
                }
                if (uploadedVideoKey is not null)
                {
                    await videoStorage.DeleteAsync(uploadedVideoKey, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminUpdateBanner")
        .Produces<AdminBannerDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminBannersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, cancellationToken);
            return deleted is null ? Results.NotFound() : Results.NoContent();
        })
        .WithName("AdminDeleteBanner")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // ── v3.14：草稿／發布（沿用既有 content.banner.update 權限碼，不開新權限碼） ──────────
        group.MapPost("/{id:guid}/publish", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBannersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.PublishAsync(scope, id, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminPublishBanner")
        .Produces<AdminBannerDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/unpublish", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBannersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UnpublishAsync(scope, id, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUnpublishBanner")
        .Produces<AdminBannerDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<UploadedImageInfo> UploadImageAsync(
        AdminClubScope scope, Guid bannerId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
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

        var objectKeyPrefix = $"{scope.ClubCode}/banners/{bannerId}/image";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }

    /// <summary>v3.14 新增。長度檢查（<see cref="IFormFile.Length"/>，只讀 metadata 不讀檔案本體）
    /// 先擋大檔案，格式（magic bytes）驗證留給 <see cref="IVideoStorageService.UploadAsync"/>
    /// 內部的 <c>VideoValidator</c>，逐字比照 <see cref="UploadImageAsync"/> 的既有分工。</summary>
    private static async Task<UploadedVideoInfo> UploadVideoAsync(
        AdminClubScope scope, Guid bannerId, IFormFile file, IVideoStorageService videoStorage, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new EmptyVideoException();
        }

        if (file.Length > VideoUploadOptions.MaxUploadBytes)
        {
            throw new VideoTooLargeException();
        }

        byte[] rawBytes;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            rawBytes = buffer.ToArray();
        }

        var objectKeyPrefix = $"{scope.ClubCode}/banners/{bannerId}/video";
        return await videoStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
