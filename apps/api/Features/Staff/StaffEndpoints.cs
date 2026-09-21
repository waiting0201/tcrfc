using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Staff;

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/staff?team=D1&lang=zh&page=1&pageSize=50
        app.MapGet("/api/v1/{club}/staff", async (
            string club,
            string? team,
            string? lang,
            int? page,
            int? pageSize,
            IClubResolver clubResolver,
            StaffRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 200);

            var result = await repository.ListAsync(scope, team, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListStaff")
        .WithTags("Staff")
        .Produces<PagedResult<StaffDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
