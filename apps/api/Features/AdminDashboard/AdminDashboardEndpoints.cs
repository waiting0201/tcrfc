using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminDashboard;

/// <summary>
/// A 儀表板後台端點（主站規劃書 §4.1）。<b>沒有專屬權限碼</b>：呼叫者在該俱樂部持有任一個儀表板會用到的檢視／建立權限即可進入
/// （<see cref="AdminDashboardRepository.AllCandidateCodes"/>），每個區塊再依對應模組的檢視權限決定內容，見 <see cref="AdminDashboardRepository"/> 檔頭。
/// 全部走俱樂部範圍（<c>{club}</c>），兩站各自的數字。
/// </summary>
public static class AdminDashboardEndpoints
{
    public static void MapAdminDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/dashboard")
            .WithTags("AdminDashboard")
            .WithDescription("A 儀表板（待辦提醒、內容概況、FAQ 概況、近期行程、會員概況、快速入口；轉換與流量另有端點），需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDashboardRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminDashboardRepository.AllCandidateCodes, cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        })
        .WithName("AdminGetDashboard").Produces<AdminDashboardDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        // GET /dashboard/conversion?period=week|month —— 各表單送出數、報名數、提案下載數、新註冊會員、付費會籍（週／月趨勢）。
        group.MapGet("/conversion", async (
            string club, string? period, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDashboardRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminDashboardRepository.AllCandidateCodes, cancellationToken);
            return Results.Ok(await repository.GetConversionAsync(scope, period, cancellationToken));
        })
        .WithName("AdminGetDashboardConversion").Produces<AdminDashboardConversionDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        // GET /dashboard/traffic —— GA4 流量概況（接縫，目前回「尚未串接」）。
        group.MapGet("/traffic", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminDashboardRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminDashboardRepository.AllCandidateCodes, cancellationToken);
            return Results.Ok(await repository.GetTrafficAsync(scope, cancellationToken));
        })
        .WithName("AdminGetDashboardTraffic").Produces<AdminDashboardTrafficDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
