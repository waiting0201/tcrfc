using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminBenefits;

/// <summary>K4 權益對照表條目後台端點（主站規劃書 §4.11 K4）。權限碼 <c>member.benefit.*</c>（module=K、submodule=K4、domain=member）。</summary>
public static class AdminBenefitsEndpoints
{
    private const string PermissionView = "member.benefit.view";
    private const string PermissionCreate = "member.benefit.create";
    private const string PermissionUpdate = "member.benefit.update";
    private const string PermissionDelete = "member.benefit.delete";

    public static void MapAdminBenefitsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/membership-benefits")
            .WithTags("AdminMembershipBenefits")
            .WithDescription("K4 權益對照表條目維護，需要登入與俱樂部授權。");

        // GET /membership-benefits?planId=&group=&status=
        group.MapGet("", async (
            string club, Guid? planId, string? group, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, planId, group, status, cancellationToken));
        })
        .WithName("AdminListMembershipBenefits").Produces<IReadOnlyList<AdminBenefitListItemDto>>();

        group.MapGet("/groups", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(AdminBenefitsRepository.Groups);
        })
        .WithName("AdminListMembershipBenefitGroups").Produces<IReadOnlyList<AdminBenefitGroupDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var benefit = await repository.GetByIdAsync(scope, id, cancellationToken);
            return benefit is null ? Results.NotFound() : Results.Ok(benefit);
        })
        .WithName("AdminGetMembershipBenefit").Produces<AdminBenefitDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminBenefitRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/membership-benefits/{created.Id}", created);
        })
        .WithName("AdminCreateMembershipBenefit").Produces<AdminBenefitDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminBenefitRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateMembershipBenefit").Produces<AdminBenefitDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteMembershipBenefit").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/order", async (
            string club, ReorderBenefitsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminBenefitsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderMembershipBenefits").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }
}
