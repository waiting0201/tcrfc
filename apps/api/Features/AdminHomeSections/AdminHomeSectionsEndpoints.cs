using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminHomeSections;

/// <summary>B3 首頁區塊編排，俱樂部範圍（<c>home_sections.club_id</c> 必填），
/// 一律經 <see cref="IAdminClubAuthorizer"/>。</summary>
public static class AdminHomeSectionsEndpoints
{
    private const string PermissionView = "content.home_section.view";
    private const string PermissionUpdate = "content.home_section.update";

    public static void MapAdminHomeSectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/home-sections")
            .WithTags("AdminHomeSections")
            .WithDescription("後台首頁區塊開關與排序，需要登入與俱樂部授權。九個代碼固定，見 HomeSectionCatalog。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminHomeSectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var sections = await repository.ListAsync(scope, cancellationToken);
            return Results.Ok(sections);
        })
        .WithName("AdminListHomeSections")
        .Produces<IReadOnlyList<AdminHomeSectionDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // PUT /api/v1/admin/{club}/home-sections/{sectionCode}
        group.MapPut("/{sectionCode}", async (
            string club, string sectionCode, UpdateHomeSectionRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminHomeSectionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, sectionCode, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateHomeSection")
        .Produces<AdminHomeSectionDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
