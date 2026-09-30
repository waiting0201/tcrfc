using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSecurity;

/// <summary>J3 稽核與備份（主站規劃書 §4.10 J3）的可查閱部分：帳號活動概況與登入異常提醒。權限碼 <c>system.audit.view</c>（僅系統管理員）。
/// 操作稽核記錄與登入歷程<b>沒有資料表</b>（見 <see cref="AdminSecurityOverviewDto"/>）；資料備份是託管資料庫的基礎設施設定（docs/17 §6），沒有 API。</summary>
public static class AdminSecurityEndpoints
{
    public static void MapAdminSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/security").WithTags("AdminSecurity")
            .WithDescription("J3 帳號活動概況與登入異常提醒，僅系統管理員。");

        group.MapGet("/overview", async (HttpContext http, IAdminSystemAuthorizer auth, AdminSecurityOverviewRepository repo, CancellationToken ct) =>
        {
            await auth.AuthorizeAsync(http, "system.audit.view", ct);
            return Results.Ok(await repo.GetAsync(ct));
        }).WithName("AdminSecurityOverview").Produces<AdminSecurityOverviewDto>().Produces(StatusCodes.Status403Forbidden);
    }
}
