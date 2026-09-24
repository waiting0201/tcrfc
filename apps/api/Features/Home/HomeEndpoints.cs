using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Home;

/// <summary>公開讀取端點：B3 首頁編排（規劃書 3.1 首頁九大區塊裡屬於本模組管的兩件事——Hero 輪播、
/// 區塊開關與排序）。其餘七個區塊（核心價值、體系導覽卡、最新賽事、近期賽事、最新消息、夥伴
/// Logo 牆、商店入口）的實際內容分別來自各自的既有模組（設定、賽程、新聞、夥伴、商店），
/// 不在這裡重複提供，見 apps/api/README.md 的範圍說明。</summary>
public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/banners?lang=zh
        app.MapGet("/api/v1/{club}/banners", async (
            string club, string? lang, IClubResolver clubResolver, HomeRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var banners = await repository.ListBannersAsync(scope, dbLocale, cancellationToken);
            return Results.Ok(banners);
        })
        .WithName("ListBanners")
        .WithTags("Home")
        .Produces<IReadOnlyList<BannerDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/home-sections
        app.MapGet("/api/v1/{club}/home-sections", async (
            string club, IClubResolver clubResolver, HomeRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var sections = await repository.ListHomeSectionsAsync(scope, cancellationToken);
            return Results.Ok(sections);
        })
        .WithName("ListHomeSections")
        .WithTags("Home")
        .Produces<IReadOnlyList<HomeSectionDto>>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
