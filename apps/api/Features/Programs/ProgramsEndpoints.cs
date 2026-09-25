using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Programs;

/// <summary>05 課程與活動公開讀取＋報名送出端點（主站規劃書 §3.5）。全部不需要登入。</summary>
public static class ProgramsEndpoints
{
    public static void MapProgramsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/programs?type=&lang=zh&page=&pageSize=
        app.MapGet("/api/v1/{club}/programs", async (
            string club, string? type, string? lang, int? page, int? pageSize,
            IClubResolver clubResolver, ProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 50, maxPageSize: 100);

            var result = await repository.ListAsync(scope, type, dbLocale, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListPrograms")
        .WithTags("Programs")
        .Produces<PagedResult<ProgramListItemDto>>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/{club}/programs/{slug}?lang=zh
        app.MapGet("/api/v1/{club}/programs/{slug}", async (
            string club, string slug, string? lang,
            IClubResolver clubResolver, ProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var dbLocale = RequestLocale.ToDbLocale(lang);

            var program = await repository.GetBySlugAsync(scope, slug, dbLocale, cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(program);
        })
        .WithName("GetProgram")
        .WithTags("Programs")
        .Produces<ProgramDetailDto>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/programs/sessions/{sessionId}/registrations
        app.MapPost("/api/v1/{club}/programs/sessions/{sessionId:guid}/registrations", async (
            string club, Guid sessionId, SubmitProgramRegistrationRequest request,
            IClubResolver clubResolver, ProgramsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.SubmitRegistrationAsync(scope, sessionId, request, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("SubmitProgramRegistration")
        .WithTags("Programs")
        .Produces<ProgramRegistrationSubmittedDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}
