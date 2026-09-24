using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// FAQ 快捷區塊（G-12）掛載點字典的唯讀端點（S1-7a）。全域端點（不含 <c>{club}</c> 路由段——
/// 掛載點是站台結構性代號，不分俱樂部），權限碼沿用既有的 <c>content.faq.view</c>——
/// 這張字典本身沒有獨立的管理情境，只在編輯 FAQ 時當一個唯讀下拉選單，不值得為 4 筆固定值
/// 另外新增一組權限碼（跟 <c>Features/AdminHomeSections/HomeSectionCatalog.cs</c> 那種「固定
/// 字典不開權限碼」的既有慣例一致）。
/// </summary>
public static class AdminFaqEmbedSlotsEndpoints
{
    private const string PermissionView = "content.faq.view";

    public static void MapAdminFaqEmbedSlotsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/faq-embed-slots", async (
            HttpContext httpContext, IAdminSystemAuthorizer authorizer,
            AdminFaqEmbedSlotsRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, PermissionView, cancellationToken);
            var slots = await repository.ListAsync(cancellationToken);
            return Results.Ok(slots);
        })
        .WithName("AdminListFaqEmbedSlots")
        .WithTags("AdminFaqEmbedSlots")
        .WithDescription("FAQ 快捷區塊（G-12）掛載點字典，唯讀，需要登入與 content.faq.view。")
        .Produces<IReadOnlyList<AdminFaqEmbedSlotDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
