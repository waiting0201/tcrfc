using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Partners;

/// <summary>公開讀取：E1 合作夥伴（規劃書 §3.9 9.1）。不需要登入。</summary>
public static class PartnersEndpoints
{
    public static void MapPartnersEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/partners?type=策略夥伴&home=true&footer=true&lang=zh
        app.MapGet("/api/v1/{club}/partners", async (
            string club, string? type, bool? home, bool? footer, string? lang,
            IClubResolver clubResolver, PartnersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.ListAsync(scope, string.IsNullOrWhiteSpace(type) ? null : type.Trim(),
                home == true, footer == true, RequestLocale.ToDbLocale(lang), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListPartners")
        .WithTags("Partners")
        .Produces<IReadOnlyList<PartnerDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
