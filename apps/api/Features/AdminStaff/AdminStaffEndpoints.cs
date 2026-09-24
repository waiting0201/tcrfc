using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminStaff;

/// <summary>C3「教練與團隊成員」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=C、
/// submodule_code=C3、domain=team。</summary>
public static class AdminStaffEndpoints
{
    private const string PermissionView = "team.staff.view";
    private const string PermissionCreate = "team.staff.create";
    private const string PermissionUpdate = "team.staff.update";

    public static void MapAdminStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/staff")
            .WithTags("AdminStaff")
            .WithDescription("C3 俱樂部範圍的教練與團隊成員維護，需要登入與俱樂部授權。共同資料（IsShared=true）僅供檢視，不可編輯。");

        // GET /api/v1/admin/{club}/staff?teamId=
        group.MapGet("", async (
            string club, Guid? teamId, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStaffRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, teamId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListStaff")
        .Produces<IReadOnlyList<AdminStaffListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminStaffRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var staff = await repository.GetByIdAsync(scope, id, cancellationToken);
            return staff is null ? Results.NotFound() : Results.Ok(staff);
        })
        .WithName("AdminGetStaff")
        .Produces<AdminStaffDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/staff —— multipart/form-data（payload ＋ 選填 file 照片）。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStaffRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminStaffRequestForm.ReadAsync<CreateAdminStaffRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var staffId = Guid.NewGuid();
            string? photoKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("staff", "photo");
                var uploaded = await UploadPhotoAsync(scope, staffId, file, imageStorage, cancellationToken);
                photoKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(scope, staffId, request, photoKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/staff/{created.Id}", created);
            }
            catch
            {
                if (photoKey is not null)
                {
                    await imageStorage.DeleteAsync(photoKey, CancellationToken.None); // E-47：請求已取消也要清掉
                }

                throw;
            }
        })
        .WithName("AdminCreateStaff")
        .Produces<AdminStaffDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminStaffRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminStaffRequestForm.ReadAsync<UpdateAdminStaffRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemovePhoto)
            {
                throw new AdminStaffValidationException("不能同時上傳新的照片與移除照片，請擇一。");
            }

            string? uploadedKey = null;
            StaffPhotoKeyUpdate photoUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("staff", "photo");
                var uploaded = await UploadPhotoAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                photoUpdate = StaffPhotoKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemovePhoto)
            {
                photoUpdate = StaffPhotoKeyUpdate.Set(null);
            }
            else
            {
                photoUpdate = StaffPhotoKeyUpdate.Keep;
            }

            try
            {
                var updated = await repository.UpdateAsync(scope, id, request, photoUpdate, operatorId, cancellationToken);
                if (updated is null)
                {
                    if (uploadedKey is not null)
                    {
                        await imageStorage.DeleteAsync(uploadedKey, CancellationToken.None);
                    }

                    return Results.NotFound();
                }

                return Results.Ok(updated);
            }
            catch
            {
                if (uploadedKey is not null)
                {
                    await imageStorage.DeleteAsync(uploadedKey, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminUpdateStaff")
        .Produces<AdminStaffDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }

    private static async Task<UploadedImageInfo> UploadPhotoAsync(
        AdminClubScope scope, Guid staffId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
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

        var objectKeyPrefix = $"{scope.ClubCode}/staff/{staffId}/photo";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
