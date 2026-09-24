using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCompetitions;

/// <summary>俱樂部範圍端點（<c>competitions.club_id</c> 必填），比照 <c>Features/AdminNews</c>
/// 用 <see cref="IAdminClubAuthorizer"/>。權限碼歸在 module_code=C（球隊管理）、submodule=C4
/// （賽程與賽果——賽事系列是排程分類用的支援型別，執行層判斷歸在這裡而非 J，見
/// apps/api/README.md 的說明），與 STATUS.md 把「Club／Competition 當成可維護型別」這件工作
/// 一併列在 J4 底下並不衝突：J4 是「這件事本輪由誰去做」的工作分類，module_code 是權限碼的
/// 資料分類，兩者不必是同一個字母。</summary>
public static class AdminCompetitionsEndpoints
{
    private const string PermissionView = "team.competition.view";
    private const string PermissionCreate = "team.competition.create";
    private const string PermissionUpdate = "team.competition.update";

    public static void MapAdminCompetitionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/competitions")
            .WithTags("AdminCompetitions")
            .WithDescription("俱樂部範圍的賽事系列（Competition）維護，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/seasons —— 前端 agent 回報缺口②：賽事系列表單的球季下拉選單。
        // 🔴 路由段掛在 {club} 底下但不是 /competitions 的子路徑（球季是獨立型別，不是賽事系列的
        // 子資源）；權限碼比照同模組既有的 team.competition.view，不另外新增權限碼——球季本身
        // 目前只有這一個唯讀查詢用途，還沒有獨立的維護畫面，等真的要維護球季本身時再評估要不要
        // 拆一組專屬權限碼。
        app.MapGet("/api/v1/admin/{club}/seasons", async (
            string club, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var seasons = await repository.ListSeasonsAsync(scope, cancellationToken);
            return Results.Ok(seasons);
        })
        .WithTags("AdminCompetitions")
        .WithName("AdminListSeasons")
        .Produces<IReadOnlyList<AdminSeasonListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("", async (
            string club, Guid? seasonId, string? status, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, seasonId, status, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListCompetitions")
        .Produces<IReadOnlyList<AdminCompetitionListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var competition = await repository.GetByIdAsync(scope, id, cancellationToken);
            return competition is null ? Results.NotFound() : Results.Ok(competition);
        })
        .WithName("AdminGetCompetition")
        .Produces<AdminCompetitionDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminCompetitionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/competitions/{created.Id}", created);
        })
        .WithName("AdminCreateCompetition")
        .Produces<AdminCompetitionDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminCompetitionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateCompetition")
        .Produces<AdminCompetitionDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
