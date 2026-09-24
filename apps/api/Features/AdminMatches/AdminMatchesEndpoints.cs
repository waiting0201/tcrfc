using System.Text;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMatches;

/// <summary>C4「賽程與賽果」後台維護端點。權限碼命名照 docs/12b §7.3：module_code=C、
/// submodule_code=C4、domain=team（跟既有 <c>team.competition.*</c>／<c>team.team.*</c> 同一個
/// domain，方便權限查詢時整組 <c>domain = 'team'</c> 一次撈）。</summary>
public static class AdminMatchesEndpoints
{
    private const string PermissionView = "team.match.view";
    private const string PermissionCreate = "team.match.create";
    private const string PermissionUpdate = "team.match.update";
    private const string PermissionDelete = "team.match.delete";

    public static void MapAdminMatchesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/matches")
            .WithTags("AdminMatches")
            .WithDescription("C4 俱樂部範圍的賽程與賽果維護，需要登入、俱樂部授權與球隊列級授權。");

        // GET /api/v1/admin/{club}/matches?seasonId=&teamId=&status=
        group.MapGet("", async (
            string club, Guid? seasonId, Guid? teamId, string? status, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var result = await repository.ListAsync(scope, seasonId, teamId, status, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListMatches")
        .Produces<IReadOnlyList<AdminMatchListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var match = await repository.GetByIdAsync(scope, id, cancellationToken);
            return match is null ? Results.NotFound() : Results.Ok(match);
        })
        .WithName("AdminGetMatch")
        .Produces<AdminMatchDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminMatchRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver,
            AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, rowScope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/matches/{created.Id}", created);
        })
        .WithName("AdminCreateMatch")
        .Produces<AdminMatchDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminMatchRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver,
            AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, rowScope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateMatch")
        .Produces<AdminMatchDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // 🔴 硬刪除（見 AdminMatchesRepository.DeleteAsync 上的說明：賽事是純資料紀錄，不是「人」，
        // 資料輸入錯誤直接刪掉重建，沒有狀態轉換可用）。
        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, IAdminTeamRowScopeResolver rowScopeResolver,
            AdminMatchesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, rowScope, id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteMatch")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/matches/import  body：CSV 檔案原始位元組（逐字比照既有
        // Features/AdminFaqs 的匯入端點形狀：不是 multipart/form-data，直接讀 HTTP 請求主體）。
        // 🔴 權限碼用 PermissionCreate 不是 PermissionUpdate——本匯入是整批新建
        // （見 AdminMatchesRepository.ImportCsvAsync 檔頭「不是 upsert」的說明），語意上更貼近
        // 「建立」而不是「更新既有資料」，跟 AdminFaqsEndpoints 用 PermissionUpdate（FAQ 匯入是
        // upsert，含更新既有題目）刻意不同。
        group.MapPost("/import", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            IAdminTeamRowScopeResolver rowScopeResolver, AdminMatchesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var rowScope = await rowScopeResolver.ResolveAsync(scope, PermissionCreate, cancellationToken);

            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);

            var result = await repository.ImportCsvAsync(scope, rowScope, csvContent, scope.Identity.AdminUserId, cancellationToken);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        })
        .WithName("AdminImportMatchesCsv")
        .Produces<MatchCsvImportResultDto>()
        .Produces<MatchCsvImportResultDto>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
