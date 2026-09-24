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
    }
}
