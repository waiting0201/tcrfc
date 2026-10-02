using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Newsletter;

/// <summary>G-09 電子報訂閱公開端點。全部不需要登入，一律套 <see cref="PublicRateLimitPolicies.Newsletter"/> 限流。</summary>
public static class NewsletterEndpoints
{
    public static void MapNewsletterEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/v1/{club}/newsletter/subscribe  body: { email, consent: true, source?: footer|home|news|app, website?: "" }
        app.MapPost("/api/v1/{club}/newsletter/subscribe", async (
            string club, SubscribeNewsletterRequest request,
            IClubResolver clubResolver, NewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            await repository.SubscribeAsync(scope, request, cancellationToken);
            return Results.Ok(new NewsletterSubscribeResultDto());
        })
        .WithName("SubscribeNewsletter")
        .WithTags("Newsletter")
        .WithDescription("頁尾電子報訂閱。回應不透露名單狀態；曾經退訂的信箱不會被改回訂閱。")
        .RequireRateLimiting(PublicRateLimitPolicies.Newsletter)
        .Produces<NewsletterSubscribeResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);

        // POST /api/v1/{club}/newsletter/unsubscribe  body: { token }
        app.MapPost("/api/v1/{club}/newsletter/unsubscribe", async (
            string club, UnsubscribeNewsletterRequest request,
            IClubResolver clubResolver, NewsletterRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await clubResolver.ResolveAsync(club, cancellationToken);
            return Results.Ok(await repository.UnsubscribeAsync(scope, request.Token, cancellationToken));
        })
        .WithName("UnsubscribeNewsletter")
        .WithTags("Newsletter")
        .WithDescription("以退訂連結上的憑證退訂電子報（冪等）。")
        .RequireRateLimiting(PublicRateLimitPolicies.Newsletter)
        .Produces<NewsletterUnsubscribeResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);
    }
}
