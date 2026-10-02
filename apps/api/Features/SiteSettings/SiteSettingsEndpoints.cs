using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.SiteSettings;

/// <summary>I 網站設定的前台讀取端點（選單、站台全域設定、政策頁、介面字串、場地）。全部是 GET、不需要登入、不寫入任何資料。</summary>
public static class SiteSettingsEndpoints
{
    public static void MapSiteSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/menus?lang=zh
        app.MapGet("/api/v1/{club}/menus", async (
            string club, string? lang, IClubResolver clubResolver, SiteSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.GetMenusAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("GetMenus").WithTags("SiteSettings")
        .WithDescription("主選單、Mega Menu、頁尾選單（多層級，已依語系挑選標籤）。")
        .Produces<PublicMenusDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/site-settings?lang=zh
        app.MapGet("/api/v1/{club}/site-settings", async (
            string club, string? lang, IClubResolver clubResolver, SiteSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.GetSiteSettingsAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("GetSiteSettings").WithTags("SiteSettings")
        .WithDescription("品牌（Logo、Favicon、品牌色）、維護模式、啟用語系、備援規則、日期數字格式、政策頁索引。")
        .Produces<PublicSiteSettingsDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/policies/{code}?lang=zh —— code：cookie／privacy／member-terms
        app.MapGet("/api/v1/{club}/policies/{code}", async (
            string club, string code, string? lang, IClubResolver clubResolver, SiteSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var policy = await repository.GetPolicyAsync(scope, code.Trim().ToLowerInvariant(), RequestLocale.ToDbLocale(lang), cancellationToken);
            return policy is null ? Results.NotFound() : Results.Ok(policy);
        })
        .WithName("GetPolicy").WithTags("SiteSettings")
        .WithDescription("Cookie 政策、隱私權政策、會員條款（純文字）。沒有內容回 404。")
        .Produces<PublicPolicyDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/ui-strings?lang=zh&group=
        app.MapGet("/api/v1/ui-strings", async (
            string? lang, string? group, SiteSettingsRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetUiStringsAsync(RequestLocale.ToDbLocale(lang), string.IsNullOrWhiteSpace(group) ? null : group.Trim(), cancellationToken)))
        .WithName("GetUiStrings").WithTags("SiteSettings")
        .WithDescription("介面字串翻譯表（全站共用，不分俱樂部）：{ key: 文字 }，要求語系沒有值回退繁中。")
        .Produces<PublicUiStringsDto>();

        // GET /api/v1/{club}/venues?lang=zh
        app.MapGet("/api/v1/{club}/venues", async (
            string club, string? lang, IClubResolver clubResolver, SiteSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListVenuesAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("ListVenues").WithTags("SiteSettings")
        .WithDescription("這個俱樂部用得到的場地（主場＋賽事／梯次／試訓引用的場地）：地址、經緯度、交通說明、照片。")
        .Produces<IReadOnlyList<PublicVenueDto>>().Produces(StatusCodes.Status404NotFound);
    }
}
