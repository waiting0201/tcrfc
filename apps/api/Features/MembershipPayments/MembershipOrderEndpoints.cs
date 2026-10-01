using System.Security.Cryptography;
using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MembershipPayments;

/// <summary>
/// 會員的會籍付款訂單端點，以及內部開通端點 <c>POST /api/membership/activate</c>（App 規劃書 §9.2、§9.7）。
/// 會員端點需要會員權杖與已驗證的 Email；全部寫入依 IP 限流。內部端點用「憑證」保護（<c>MEMBERSHIP_ACTIVATE_CREDENTIAL</c>，不對外開放）。
/// </summary>
public static class MembershipOrderEndpoints
{
    public const string InternalCredentialHeader = "X-Internal-Credential";
    public const string InternalCredentialConfigKey = "MEMBERSHIP_ACTIVATE_CREDENTIAL";
    private const int MinCredentialLength = 32;

    public static void MapMembershipOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/v1/{club}/member/membership-orders      標頭 Idempotency-Key（必填）；本文 { planCode }（沒有金額欄位——金額由伺服器重算）
        app.MapPost("/api/v1/{club}/member/membership-orders", async (
            string club, CreateMembershipOrderRequest request, string? lang, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            var scope = await clubs.ResolveAsync(club, ct);
            var (order, created) = await orders.CreateAsync(me, scope, http.Request.Headers["Idempotency-Key"].ToString(), request, Lang(lang), ct);
            return created ? Results.Created($"/api/v1/member/membership-orders/{order.OrderNo}", order) : Results.Ok(order);
        })
        .WithName("CreateMembershipOrder")
        .WithTags("MembershipOrders")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MembershipOrderDto>(StatusCodes.Status201Created)
        .Produces<MembershipOrderDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);

        app.MapGet("/api/v1/member/membership-orders", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await orders.ListAsync(me.MemberId, Lang(lang), ct));
        })
        .WithName("ListMembershipOrders")
        .WithTags("MembershipOrders")
        .Produces<IReadOnlyList<MembershipOrderDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/v1/member/membership-orders/{orderNo}", async (
            string orderNo, string? lang, HttpContext http, MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await orders.GetAsync(me.MemberId, orderNo, Lang(lang), ct));
        })
        .WithName("GetMembershipOrder")
        .WithTags("MembershipOrders")
        .Produces<MembershipOrderDto>()
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/member/membership-orders/{orderNo}/pay", async (
            string orderNo, string? lang, HttpContext http, MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            return Results.Ok(await orders.PayAsync(me.MemberId, orderNo, Lang(lang), ct));
        })
        .WithName("PayMembershipOrder")
        .WithTags("MembershipOrders")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MembershipOrderDto>()
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/v1/member/membership-orders/{orderNo}/confirm", async (
            string orderNo, ConfirmMembershipOrderRequest request, string? lang, HttpContext http,
            MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            return Results.Ok(await orders.ConfirmAsync(me.MemberId, orderNo, request.TransactionId, Lang(lang), ct));
        })
        .WithName("ConfirmMembershipOrder")
        .WithTags("MembershipOrders")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MembershipOrderDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        app.MapPost("/api/v1/member/membership-orders/{orderNo}/cancel", async (
            string orderNo, string? lang, HttpContext http, MemberAuthenticator authenticator, MembershipOrderService orders, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await orders.CancelAsync(me.MemberId, orderNo, Lang(lang), ct));
        })
        .WithName("CancelMembershipOrder")
        .WithTags("MembershipOrders")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MembershipOrderDto>()
        .Produces(StatusCodes.Status409Conflict);

        // POST /api/membership/activate —— 伺服器內部呼叫，憑證保護，不對外開放（App 規劃書 §9.7）。冪等：以訂單編號為冪等鍵。
        app.MapPost("/api/membership/activate", async (
            InternalActivateRequest request, HttpContext http, IConfiguration configuration,
            MembershipActivationService activation, CancellationToken ct) =>
        {
            var expected = configuration[InternalCredentialConfigKey];
            if (string.IsNullOrWhiteSpace(expected) || expected.Length < MinCredentialLength)
            {
                throw new FeatureNotConfiguredException("內部開通端點尚未啟用。", "internal_activation_disabled");
            }

            var provided = http.Request.Headers[InternalCredentialHeader].ToString();
            // 比對雜湊的固定時間結果，避免時序側錄；失敗一律 401，不分「沒帶」與「帶錯」。
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(expected))))
            {
                throw new MemberUnauthenticatedException("憑證不正確。", "invalid_credential");
            }

            return Results.Ok(await activation.ActivatePaidOrderAsync(request.OrderNo, MembershipActivationService.SourceInternal, ct));
        })
        .WithName("InternalActivateMembership")
        .WithTags("MembershipOrders")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MembershipActivationResultDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }

    private static string Lang(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
}
