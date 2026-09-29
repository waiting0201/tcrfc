using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 唯讀場地清單端點（S1-12d 後續缺口補完）。掛在 <c>{club}</c> 路由段下只是為了沿用既有
/// <see cref="IAdminClubAuthorizer"/> 的授權管線（帳號狀態、俱樂部存在、俱樂部授權、權限碼四步
/// 一次到齊）——回傳內容本身**與俱樂部無關**（<c>Venue</c> 不帶 <c>club_id</c>，見
/// <see cref="AdminVenuesRepository"/> 檔頭），任何俱樂部呼叫都會拿到同一份全站清單。
///
/// 🔴 **權限採 <see cref="IAdminClubAuthorizer.AuthorizeAnyAsync"/>，允許兩組既有權限碼任一通過**
/// ——這是任務指示「能看網站設定或賽事的人都能讀」的具體判斷：
/// <c>site.fact.view</c>（<c>I</c> 網站設定，`Features/AdminSiteFacts` 既有權限碼，挑選主場要用）
/// 與 <c>team.match.view</c>（<c>C4</c> 賽程與賽果，`Features/AdminMatches` 既有權限碼，挑選比賽
/// 地點要用）——這兩個是目前僅有的兩處「需要挑選既有場地」的既有畫面／缺口（見
/// <c>apps/admin/README.md</c>「I：網站設定」規格疑點第 1 點、
/// <c>apps/admin/src/views/teams/MatchEditView.vue</c> 檔頭）。**不新增權限碼**——這只是一份
/// 共用主檔的唯讀清單，不是需要獨立授權把關的新業務功能，比照既有「唯讀清單掛在既有相關模組權限碼
/// 底下」的原則（例如 <c>Features/AdminCompetitions.ListSeasonsAsync</c> 掛在賽事模組底下而不是
/// 另開球季模組的權限碼）。
/// </summary>
public static class AdminVenuesEndpoints
{
    private static readonly string[] ViewCandidateCodes = ["site.fact.view", "team.match.view"];

    public static void MapAdminVenuesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/{club}/venues", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminVenuesRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, ViewCandidateCodes, cancellationToken);
            var result = await repository.ListAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListVenues")
        .WithTags("AdminVenues")
        .WithDescription("全站共用場地主檔的唯讀清單，供後台下拉選單挑選既有場地（例如網站設定挑主場、賽程挑比賽地點）。")
        .Produces<IReadOnlyList<AdminVenueListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
