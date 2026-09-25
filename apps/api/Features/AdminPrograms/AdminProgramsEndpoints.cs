using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPrograms;

/// <summary>P1「課程／營隊項目」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=P、
/// submodule_code=P1、domain=program。</summary>
public static class AdminProgramsEndpoints
{
    private const string PermissionView = "program.item.view";
    private const string PermissionCreate = "program.item.create";
    private const string PermissionUpdate = "program.item.update";

    public static void MapAdminProgramsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/programs")
            .WithTags("AdminPrograms")
            .WithDescription("P1 俱樂部範圍的課程／營隊項目維護，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/programs?programType=
        group.MapGet("", async (
            string club, string? programType, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, programType, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListPrograms")
        .Produces<IReadOnlyList<AdminProgramListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var program = await repository.GetByIdAsync(scope, id, cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(program);
        })
        .WithName("AdminGetProgram")
        .Produces<AdminProgramDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/programs —— multipart/form-data（payload ＋ 選填 file 封面圖）。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminProgramsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminProgramRequestForm.ReadAsync<CreateAdminProgramRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var programId = Guid.NewGuid();
            string? coverKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("programs", "cover");
                var uploaded = await UploadCoverAsync(scope, programId, file, imageStorage, cancellationToken);
                coverKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(scope, programId, request, coverKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/programs/{created.Id}", created);
            }
            catch
            {
                if (coverKey is not null)
                {
                    await imageStorage.DeleteAsync(coverKey, CancellationToken.None); // E-47：請求已取消也要清掉
                }

                throw;
            }
        })
        .WithName("AdminCreateProgram")
        .Produces<AdminProgramDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminProgramsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminProgramRequestForm.ReadAsync<UpdateAdminProgramRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemoveCover)
            {
                throw new AdminProgramValidationException("不能同時上傳新的封面圖與移除封面圖，請擇一。");
            }

            string? uploadedKey = null;
            ProgramCoverKeyUpdate coverUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("programs", "cover");
                var uploaded = await UploadCoverAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                coverUpdate = ProgramCoverKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemoveCover)
            {
                coverUpdate = ProgramCoverKeyUpdate.Set(null);
            }
            else
            {
                coverUpdate = ProgramCoverKeyUpdate.Keep;
            }

            try
            {
                var updated = await repository.UpdateAsync(scope, id, request, coverUpdate, operatorId, cancellationToken);
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
        .WithName("AdminUpdateProgram")
        .Produces<AdminProgramDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();
    }

    private static async Task<UploadedImageInfo> UploadCoverAsync(
        AdminClubScope scope, Guid programId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
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

        var objectKeyPrefix = $"{scope.ClubCode}/programs/{programId}/cover";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
