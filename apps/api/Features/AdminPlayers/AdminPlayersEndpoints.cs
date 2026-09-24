using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPlayers;

/// <summary>C2「球員」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=C、submodule_code=C2、
/// domain=team（球隊管理底下的球員子模組，不是另立新 domain——跟 <c>team.competition.*</c>／
/// <c>team.team.*</c> 同一個 domain 值，方便權限查詢時整組 <c>domain = 'team'</c> 一次撈）。</summary>
public static class AdminPlayersEndpoints
{
    private const string PermissionView = "team.player.view";
    private const string PermissionCreate = "team.player.create";
    private const string PermissionUpdate = "team.player.update";

    public static void MapAdminPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/players")
            .WithTags("AdminPlayers")
            .WithDescription("C2 俱樂部範圍的球員維護，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/players?teamId=&status=
        group.MapGet("", async (
            string club, Guid? teamId, string? status, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPlayersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, teamId, status, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListPlayers")
        .Produces<IReadOnlyList<AdminPlayerListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminPlayersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var player = await repository.GetByIdAsync(scope, id, cancellationToken);
            return player is null ? Results.NotFound() : Results.Ok(player);
        })
        .WithName("AdminGetPlayer")
        .Produces<AdminPlayerDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/players —— multipart/form-data（payload ＋ 選填 file 照片）。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPlayersRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminPlayerRequestForm.ReadAsync<CreateAdminPlayerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var playerId = Guid.NewGuid();
            string? photoKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("players", "photo");
                var uploaded = await UploadPhotoAsync(scope, playerId, file, imageStorage, cancellationToken);
                photoKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(scope, playerId, request, photoKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/players/{created.Id}", created);
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
        .WithName("AdminCreatePlayer")
        .Produces<AdminPlayerDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminPlayersRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminPlayerRequestForm.ReadAsync<UpdateAdminPlayerRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemovePhoto)
            {
                throw new AdminPlayerValidationException("不能同時上傳新的照片與移除照片，請擇一。");
            }

            string? uploadedKey = null;
            PhotoKeyUpdate photoUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("players", "photo");
                var uploaded = await UploadPhotoAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                photoUpdate = PhotoKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemovePhoto)
            {
                photoUpdate = PhotoKeyUpdate.Set(null);
            }
            else
            {
                photoUpdate = PhotoKeyUpdate.Keep;
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
        .WithName("AdminUpdatePlayer")
        .Produces<AdminPlayerDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }

    private static async Task<UploadedImageInfo> UploadPhotoAsync(
        AdminClubScope scope, Guid playerId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
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

        var objectKeyPrefix = $"{scope.ClubCode}/players/{playerId}/photo";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
