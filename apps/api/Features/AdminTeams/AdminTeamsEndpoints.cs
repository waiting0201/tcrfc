using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminTeams;

/// <summary>
/// 前端 agent 回報缺口②：J4「球隊授權」（<c>admin_user_teams</c>，規劃書 §5.3／docs/12b §7.1）
/// 畫面需要一份球隊下拉選單，而且**必須跨俱樂部**——指派球隊授權的操作者是系統管理員，指派對象
/// 是「某個後台帳號可以額外碰哪些球隊」，球隊本身可能來自任何俱樂部（例如學院管理者被授權
/// 藍鯨的某個梯隊），不能限定在單一俱樂部底下才查得到。
///
/// **怎麼讓它拿得到跨俱樂部的球隊清單**：不沿用 <c>Features/AdminCompetitions</c> 那種
/// <c>/api/v1/admin/{club}/...</c>＋<see cref="IAdminClubAuthorizer"/> 的俱樂部範圍端點形狀——
/// 那個形狀天生只查得到一個俱樂部。改成比照 <c>Features/AdminClubs/AdminClubsEndpoints.cs</c>
/// （J4「俱樂部主檔」同樣需要跨俱樂部列出全部俱樂部）的既有先例：全域端點（無 <c>{club}</c>
/// 路由段），用 <see cref="IAdminSystemAuthorizer"/> 一次查完全部俱樂部的球隊，回應內容本身帶
/// <c>ClubCode</c>／<c>ClubNameZh</c> 讓前端可以分組顯示，不必先查俱樂部清單再逐一打
/// 俱樂部範圍端點湊出跨俱樂部畫面。
///
/// 權限碼**比照同模組既有權限碼**，不新增：<c>system.team_grant.view</c> 已經是「球隊授權」
/// 畫面本身的檢視權限（`db/seed/generate-club-seed-sql.py` §18.2，J4／S1-3 續作新增），
/// 這份下拉選單資料就是那個畫面的一部分，沿用同一個權限碼合理，不需要另開
/// 「檢視球隊主檔」這種語意重疊的新權限碼。
/// </summary>
public static class AdminTeamsEndpoints
{
    private const string PermissionView = "system.team_grant.view";

    // C1 球隊管理（俱樂部範圍，S1-7 新增）。權限碼命名照 docs/12b §7.3：module_code=C、
    // submodule_code=C1、domain=team。
    private const string PermissionTeamView = "team.team.view";
    private const string PermissionTeamCreate = "team.team.create";
    private const string PermissionTeamUpdate = "team.team.update";

    public static void MapAdminTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/admin/teams?clubCode=bw（clubCode 可省略＝跨全部俱樂部）
        app.MapGet("/api/v1/admin/teams", async (
            string? clubCode, HttpContext httpContext,
            IAdminSystemAuthorizer authorizer, AdminTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var teams = await repository.ListAsync(clubCode, cancellationToken);
            return Results.Ok(teams);
        })
        .WithTags("AdminTeams")
        .WithName("AdminListTeams")
        .WithDescription("J4 球隊授權畫面用的跨俱樂部球隊下拉選單，需要登入且為系統管理員。")
        .Produces<IReadOnlyList<AdminTeamListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // ── C1 球隊管理：俱樂部範圍 CRUD（S1-7 新增） ────────────────────────────────
        var group = app.MapGroup("/api/v1/admin/{club}/teams")
            .WithTags("AdminTeams")
            .WithDescription("C1 俱樂部範圍的球隊維護，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionTeamView, cancellationToken);
            var teams = await repository.ListForClubAsync(scope, cancellationToken);
            return Results.Ok(teams);
        })
        .WithName("AdminListClubTeams")
        .Produces<IReadOnlyList<AdminTeamAdminListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionTeamView, cancellationToken);
            var team = await repository.GetForClubAsync(scope, id, cancellationToken);
            return team is null ? Results.NotFound() : Results.Ok(team);
        })
        .WithName("AdminGetClubTeam")
        .Produces<AdminTeamDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/teams —— multipart/form-data（payload ＋ 選填 file 主視覺）。
        group.MapPost("", async (
            string club, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver, AdminTeamsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionTeamCreate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionTeamCreate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminTeamRequestForm.ReadAsync<CreateAdminTeamRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            var teamId = Guid.NewGuid();
            string? heroKey = null;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("teams", "hero");
                var uploaded = await UploadHeroAsync(scope, teamId, file, imageStorage, cancellationToken);
                heroKey = uploaded.Key;
            }

            try
            {
                var created = await repository.CreateAsync(scope, rowScope, teamId, request, heroKey, operatorId, cancellationToken);
                return Results.Created($"/api/v1/admin/{club}/teams/{created.Id}", created);
            }
            catch
            {
                // 補償交易：資料列沒有寫成功（代號重複、一線隊已存在……），刪掉剛剛上傳的物件，
                // 不留孤兒物件。CancellationToken.None——請求已取消也要清掉（docs/18 E-47）。
                if (heroKey is not null)
                {
                    await imageStorage.DeleteAsync(heroKey, CancellationToken.None);
                }

                throw;
            }
        })
        .WithName("AdminCreateClubTeam")
        .Produces<AdminTeamDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();

        group.MapPut("/{id:guid}", async (
            string club, Guid id, HttpRequest httpRequest, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver, AdminTeamsRepository repository,
            IImageStorageService imageStorage, IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionTeamUpdate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionTeamUpdate, cancellationToken);
            var operatorId = scope.Identity.AdminUserId;

            var (request, file) = await AdminTeamRequestForm.ReadAsync<UpdateAdminTeamRequest>(
                httpRequest, jsonOptions.Value.SerializerOptions, cancellationToken);

            if (file is not null && request.RemoveHero)
            {
                throw new AdminTeamValidationException("不能同時上傳新的主視覺圖片與移除主視覺圖片，請擇一。");
            }

            string? uploadedKey = null;
            HeroKeyUpdate heroUpdate;
            if (file is not null)
            {
                UploadSlotPolicy.Validate("teams", "hero");
                var uploaded = await UploadHeroAsync(scope, id, file, imageStorage, cancellationToken);
                uploadedKey = uploaded.Key;
                heroUpdate = HeroKeyUpdate.Set(uploaded.Key);
            }
            else if (request.RemoveHero)
            {
                heroUpdate = HeroKeyUpdate.Set(null);
            }
            else
            {
                heroUpdate = HeroKeyUpdate.Keep;
            }

            try
            {
                var updated = await repository.UpdateAsync(scope, rowScope, id, request, heroUpdate, operatorId, cancellationToken);
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
        .WithName("AdminUpdateClubTeam")
        .Produces<AdminTeamDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .DisableAntiforgery();
    }

    private static async Task<UploadedImageInfo> UploadHeroAsync(
        AdminClubScope scope, Guid teamId, IFormFile file, IImageStorageService imageStorage, CancellationToken cancellationToken)
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

        var objectKeyPrefix = $"{scope.ClubCode}/teams/{teamId}/hero";
        return await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, cancellationToken);
    }
}
