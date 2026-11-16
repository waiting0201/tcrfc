using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.CharityImpact;

/// <summary>公開讀取：B5 慈善與社會影響（規劃書 §3.11，前台 11.2–11.4 與「球迷捐款」導流）。不需要登入。
/// 本站**不承接**捐款：只提供導流網址與文案（由後台 B5 設定），捐款一律在慈善捐款平台。</summary>
public static class CharityEndpoints
{
    public static void MapCharityEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/charity/programs?lang=&page=&pageSize=
        app.MapGet("/api/v1/{club}/charity/programs", async (
            string club, string? lang, int? page, int? pageSize, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 12, maxPageSize: 50);
            return Results.Ok(await repository.ListProgramsAsync(scope, RequestLocale.ToDbLocale(lang), p, ps, cancellationToken));
        })
        .WithName("ListCharityPrograms").WithTags("Charity")
        .Produces<PagedResult<CharityProgramListItemDto>>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/charity/programs/{slug}?lang=
        app.MapGet("/api/v1/{club}/charity/programs/{slug}", async (
            string club, string slug, string? lang, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var program = await repository.GetProgramAsync(scope, slug, RequestLocale.ToDbLocale(lang), cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(program);
        })
        .WithName("GetCharityProgram").WithTags("Charity")
        .Produces<CharityProgramDetailDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/charity/records?program=<slug>&year=2026&lang=&page=&pageSize=
        app.MapGet("/api/v1/{club}/charity/records", async (
            string club, string? program, int? year, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 12, maxPageSize: 50);
            return Results.Ok(await repository.ListRecordsAsync(
                scope, string.IsNullOrWhiteSpace(program) ? null : program.Trim(), year, RequestLocale.ToDbLocale(lang), p, ps, cancellationToken));
        })
        .WithName("ListImpactRecords").WithTags("Charity")
        .Produces<PagedResult<ImpactRecordDto>>().Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/{club}/charity/records/years", async (
            string club, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListRecordYearsAsync(scope, cancellationToken));
        })
        .WithName("ListImpactRecordYears").WithTags("Charity")
        .Produces<IReadOnlyList<int>>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/charity/impact?lang=
        app.MapGet("/api/v1/{club}/charity/impact", async (
            string club, string? lang, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.GetImpactAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("GetCharityImpact").WithTags("Charity")
        .Produces<ImpactSummaryDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/charity/directory —— 慈善捐款平台後台「從官網更新清單」讀取的公益團體與慈善計畫清單（K-2）。
        // 唯讀、只含已公開者的識別與中英名稱，沒有任何個資或金額；行動 App 不使用（已從 shared/openapi.json 排除）。
        // 慈善平台在同一個行程內也必須走 HTTP 呼叫這支，不得直接查主站資料庫（慈善規劃書 §9.3）。
        app.MapGet("/api/v1/{club}/charity/directory", async (
            string club, HttpContext http, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            // 可被瀏覽器與 Cloudflare 邊緣短暫快取；主站改名或下架後最多 60 秒內同步到新內容。
            http.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(await repository.GetDirectoryAsync(scope, cancellationToken));
        })
        .WithName("GetCharityDirectory").WithTags("Charity")
        .Produces<CharityDirectoryDto>().Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/charity/cta?lang= —— 捐款導流網址與按鈕文案（後台 B5 設定，前台不得寫死）。
        app.MapGet("/api/v1/{club}/charity/cta", async (
            string club, string? lang, IClubResolver clubResolver, CharityRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.GetCtaAsync(scope, RequestLocale.ToDbLocale(lang), cancellationToken));
        })
        .WithName("GetCharityCta").WithTags("Charity")
        .Produces<CharityCtaDto>().Produces(StatusCodes.Status404NotFound);
    }
}
