using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Proposals;

/// <summary>公開：E3 提案簡介下載（規劃書 §3.9 9.4 CTA）。不需要登入。送出端點掛 <c>public-submission</c> 限流。</summary>
public static class ProposalsEndpoints
{
    public static void MapProposalsEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/{club}/proposals
        app.MapGet("/api/v1/{club}/proposals", async (
            string club, IClubResolver clubResolver, ProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, cancellationToken));
        })
        .WithName("ListPublicProposals").WithTags("Proposals")
        .Produces<IReadOnlyList<PublicProposalDto>>().Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/{club}/proposals/{id}/download-requests
        app.MapPost("/api/v1/{club}/proposals/{id:guid}/download-requests", async (
            string club, Guid id, ProposalDownloadRequest request,
            IClubResolver clubResolver, ProposalsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var result = await repository.RequestDownloadAsync(scope, id, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("RequestProposalDownload").WithTags("Proposals")
        .RequireRateLimiting(PublicRateLimitPolicies.Submission)
        .Produces<ProposalDownloadResultDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status429TooManyRequests);

        // GET /api/v1/{club}/proposals/downloads/{token} —— 憑限時權杖串流檔案。
        app.MapGet("/api/v1/{club}/proposals/downloads/{token}", async (
            string club, string token, HttpContext httpContext, IClubResolver clubResolver, ProposalsRepository repository,
            IDocumentStorageService documents, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            var fileKey = await repository.ResolveDownloadAsync(scope, token, cancellationToken);
            if (fileKey is null)
            {
                return Results.NotFound();
            }

            var read = await documents.OpenReadAsync(DocumentBucket.Private, fileKey, cancellationToken);
            if (read is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Stream(read.Content, read.ContentType, $"sponsorship-proposal{Path.GetExtension(fileKey)}");
        })
        .WithName("DownloadProposalFile").WithTags("Proposals")
        .RequireRateLimiting(PublicRateLimitPolicies.LightInteraction)
        .Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status429TooManyRequests);
    }
}
