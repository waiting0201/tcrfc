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
