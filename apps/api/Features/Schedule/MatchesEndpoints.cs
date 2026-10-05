using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Schedule;

public static class MatchesEndpoints
{
    public static void MapMatchesEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/schedule?team=D1&season=2026-27&status=scheduled&competition=CTFA-1&from=2026-09-01&to=2026-12-31&lang=zh&page=1&pageSize=20
        // competition＝賽事系列代碼（GET /{club}/competitions 的 code）；from／to＝比賽日期區間（含，yyyy-MM-dd）。App 規劃書 §9.2「支援球隊、俱樂部、賽事系列與期間篩選」。
        app.MapGet("/api/v1/{club}/schedule", async (
            string club,
            string? team,
            string? season,
            string? status,
            string? competition,
            DateOnly? from,
            DateOnly? to,
            string? lang,
            int? page,
            int? pageSize,
            IClubResolver clubResolver,
            MatchesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            if (from is not null && to is not null && from > to)
            {
                throw new PublicValidationException("查詢區間不正確：開始日期不能晚於結束日期。");
            }

            var result = await repository.ListAsync(scope, team, season, status, dbLocale, normalizedPage, normalizedPageSize, cancellationToken, competition, from, to);
            return Results.Ok(result);
        })
        .WithName("ListSchedule")
        .WithTags("Schedule")
        .Produces<PagedResult<MatchDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/schedule/{id}?lang=zh —— 單場賽事（深連結 tcrfc://match/{id}；App 規劃書 §9.2「賽事 列表／單筆」）。
        // 路由刻意放在 /schedule/{id:guid}：球隊代號等非 Guid 的片段不會被當成賽事 id。
        app.MapGet("/api/v1/{club}/schedule/{id:guid}", async (
            string club,
            Guid id,
            string? lang,
            IClubResolver clubResolver,
            MatchesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var match = await repository.GetAsync(scope, id, RequestLocale.ToDbLocale(lang), cancellationToken);
            return match is null ? Results.NotFound() : Results.Ok(match);
        })
        .WithName("GetMatch")
        .WithTags("Schedule")
        .Produces<MatchDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
