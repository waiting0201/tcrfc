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

    /// <summary>
    /// 🔴（S1-8 前端回報缺口，見 apps/admin/README.md「已知的 API 缺口彙整」第 14 點）
    /// C1–C4「所屬球隊／參賽球隊」下拉選單目前只能列出整個俱樂部的球隊，不分一線隊／學院——
    /// <c>academy_only</c>／<c>own_teams</c> 列級授權（S1-8）只擋得住寫入端點，前端選了範圍外的
    /// 球隊要等按下儲存才會被 403 擋下。這裡補一支「我能寫哪些球隊」的唯讀端點，讓前端把選單
    /// 收斂成呼叫端真的能寫的球隊。
    ///
    /// **模組 → 權限碼對照表**：每個模組有兩個權限碼——**檢視碼**（這支端點本身要求的權限，
    /// 比照 C1–C4 各自既有列表端點的既定慣例，只要看得到該模組就能查「我能寫哪些」，不需要先有
    /// 寫入權限才能問這個問題）與**寫入碼**（拿去問 <see cref="IAdminTeamRowScopeResolver"/>
    /// 算出 <see cref="TeamRowScope"/>，決定哪些球隊算「能寫」）。**用 <c>.update</c> 而不是
    /// <c>.create</c> 當寫入碼**：這支端點回答的是「這支**既有**球隊，我能不能碰」（對應
    /// <see cref="TeamRowScope.Allows"/>），跟 C1「建立全新球隊」用的
    /// <see cref="TeamRowScope.AllowsCreatingTeamOfType"/> 是不同問題（後者連 <c>teamId</c> 都
    /// 還不存在，不適用於「列出既有球隊」這個情境）——目前種子資料裡同一個角色的
    /// <c>.create</c>／<c>.update</c> 一律共用同一個 <c>scope_type</c>（見
    /// <c>db/seed/generate-club-seed-sql.py</c> <c>ROLE_PERMISSIONS</c> 的既有寫法：同一個
    /// tuple 裡的權限碼共用同一個 <c>scope_type</c>），但這是現況慣例不是保證，日後如果角色權限
    /// 拆到「能新增但不能改」這種更細的組合，這裡要重新檢視用哪個碼。
    ///
    /// **為什麼是獨立端點（<c>/teams/writable</c>），不是在既有 <c>GET /teams</c> 加
    /// <c>canWrite</c> 旗標**：前端要的是「參賽球隊／所屬球隊選單只列我能寫的」（收斂選項），
    /// 不是「列出全部球隊、每筆自己附註能不能寫」——選單元件直接綁這支端點的回應就是完整選項清單，
    /// 不需要在畫面上再過濾一次。獨立端點也完全不動既有
    /// <c>GET /api/v1/admin/{club}/teams</c> 的回應形狀，球隊管理列表頁等既有畫面與測試零風險；
    /// 唯一的取捨是多一個 GET 端點要維護，但這支端點的邏輯是純讀取＋既有 <c>TeamRowScope</c>
    /// 過濾，維護成本很低。
    /// </summary>
    private static readonly Dictionary<string, (string ViewPermission, string WritePermission)> WritableModulePermissions = new(StringComparer.Ordinal)
    {
        ["team"] = ("team.team.view", "team.team.update"),
        ["player"] = ("team.player.view", "team.player.update"),
        ["staff"] = ("team.staff.view", "team.staff.update"),
        ["match"] = ("team.match.view", "team.match.update"),
    };

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

        // GET /api/v1/admin/{club}/teams/writable?module=team|player|staff|match
        // 見上方 WritableModulePermissions 宣告處的完整說明。路由常數字面在 `/{id:guid}` 之前
        // 宣告不影響比對結果（guid 路由約束本來就不會吃到 "writable" 這個字面路徑）。
        group.MapGet("/writable", async (
            string club, string? module, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver,
            AdminTeamsRepository repository, CancellationToken cancellationToken) =>
        {
            // 訊息刻意不回顯呼叫端傳入的原始 module 字面值——這支端點的呼叫端是前端固定寫死的
            // 選單參數，不是使用者輸入，回顯內部參數名稱與致本身跟介面不顯示技術詞是同一條規則
            // （docs/06-conventions.md §1），不能因為「這裡呼叫端是前端不是使用者」就放寬。
            if (module is null || !WritableModulePermissions.TryGetValue(module, out var permissions))
            {
                throw new AdminTeamValidationException("查詢的球隊用途不正確，必須是「球隊」「球員」「教練」「賽事」其中之一。");
            }

            var scope = await authorizer.AuthorizeAsync(httpContext, club, permissions.ViewPermission, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, permissions.WritePermission, cancellationToken);
            var teams = await repository.ListWritableForClubAsync(scope, rowScope, cancellationToken);
            return Results.Ok(teams);
        })
        .WithName("AdminListWritableClubTeams")
        .Produces<IReadOnlyList<AdminWritableTeamDto>>()
        .Produces(StatusCodes.Status400BadRequest)
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
