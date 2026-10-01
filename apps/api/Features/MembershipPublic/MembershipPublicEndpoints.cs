using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MembershipPublic;

/// <summary>會籍方案、權益對照表、特約店家的公開讀取端點。全部不需要登入（主站規劃書 §3.14「未登入即可檢視」、§3.8 8.4）。純 GET，無寫入。</summary>
public static class MembershipPublicEndpoints
{
    public static void MapMembershipPublicEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/membership/plans?lang=zh
        app.MapGet("/api/v1/{club}/membership/plans", async (
            string club, string? lang, IClubResolver clubs, MembershipPublicRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListPlansAsync(scope, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListMembershipPlans")
        .WithTags("MembershipPublic")
        .Produces<IReadOnlyList<MembershipPlanPublicDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/membership/benefits?planCode=&lang=zh
        app.MapGet("/api/v1/{club}/membership/benefits", async (
            string club, string? planCode, string? lang, IClubResolver clubs, MembershipPublicRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var table = await repository.GetBenefitsAsync(scope, planCode, RequestLocale.ToDbLocale(lang), ct);
            return table is null ? Results.NotFound() : Results.Ok(table);
        })
        .WithName("GetMembershipBenefits")
        .WithTags("MembershipPublic")
        .Produces<BenefitTablePublicDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/partner-stores?category=&region=&tier=&lang=zh
        app.MapGet("/api/v1/{club}/partner-stores", async (
            string club, string? category, string? region, string? tier, string? lang,
            IClubResolver clubs, MembershipPublicRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.ListStoresAsync(scope, category, region, tier, RequestLocale.ToDbLocale(lang), ct));
        })
        .WithName("ListPartnerStores")
        .WithTags("MembershipPublic")
        .Produces<IReadOnlyList<PartnerStorePublicDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/partner-stores/filters
        app.MapGet("/api/v1/{club}/partner-stores/filters", async (
            string club, IClubResolver clubs, MembershipPublicRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await repository.GetStoreFiltersAsync(scope, ct));
        })
        .WithName("GetPartnerStoreFilters")
        .WithTags("MembershipPublic")
        .Produces<PartnerStoreFiltersDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/partner-stores/{slug}?lang=zh
        app.MapGet("/api/v1/{club}/partner-stores/{slug}", async (
            string club, string slug, string? lang, IClubResolver clubs, MembershipPublicRepository repository, CancellationToken ct) =>
        {
            var scope = await clubs.ResolveAsync(club, ct);
            var store = await repository.GetStoreAsync(scope, slug, RequestLocale.ToDbLocale(lang), ct);
            return store is null ? Results.NotFound() : Results.Ok(store);
        })
        .WithName("GetPartnerStore")
        .WithTags("MembershipPublic")
        .Produces<PartnerStorePublicDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
