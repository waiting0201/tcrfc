using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Pages;

public static class PagesEndpoints
{
    public static void MapPagesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/pages/{slug}?lang=zh
        app.MapGet("/api/v1/{club}/pages/{*slug}", async (
            string club,
            string slug,
            string? lang,
            IClubResolver clubResolver,
            PagesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var page = await repository.GetBySlugAsync(scope, slug, dbLocale, cancellationToken);
            return page is null ? Results.NotFound() : Results.Ok(page);
        })
        .WithName("GetPage")
        .WithTags("Pages")
        .Produces<PageDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/pages/preview/{token}?lang=zh
        // 🔴 沒有 {club} 路由段：權杖本身已經是唯一識別（見 PagesRepository.GetPreviewAsync 上的說明），
        // 不需要先知道俱樂部才能查。⚠️ 防禦性補上 X-Robots-Tag（規劃書「預覽頁面不得被索引」）——
        // 這是 API 回應層的補強，真正擋搜尋引擎的是前台渲染頁面本身的 noindex（見 apps/api/README.md
        // 「預覽連結」一節：這件事的完整落實需要前台配合，不是本次 API 任務範圍能單獨完成）。
        app.MapGet("/api/v1/pages/preview/{token}", async (
            string token,
            string? lang,
            HttpContext httpContext,
            PagesRepository repository,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");

            var dbLocale = RequestLocale.ToDbLocale(lang);
            var preview = await repository.GetPreviewAsync(token, dbLocale, cancellationToken);
            return preview is null ? Results.NotFound() : Results.Ok(preview);
        })
        .WithName("GetPagePreview")
        .WithTags("Pages")
        .Produces<PagePreviewDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
