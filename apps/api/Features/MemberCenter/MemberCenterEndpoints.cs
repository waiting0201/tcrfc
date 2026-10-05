using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberCenter;

/// <summary>
/// 會員中心端點（主站規劃書 §3.14）：我的會籍、電子會員卡、球衣登記、我的報名，與電子會員卡<b>公開驗證</b>。
/// 除了 <c>GET /api/v1/m/{token}</c> 外一律需要會員權杖，且只能讀寫「權杖本人」的資料。寫入依 IP 限流。
/// </summary>
public static class MemberCenterEndpoints
{
    public static void MapMemberCenterEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/v1/member/memberships?lang=zh —— 逐俱樂部列出會籍（含每份會籍的會員卡）＋可加入的俱樂部
        app.MapGet("/api/v1/member/memberships", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await center.GetMembershipsAsync(me.MemberId, Lang(lang), ct));
        })
        .WithName("GetMyMemberships")
        .WithTags("MemberCenter")
        .Produces<MyMembershipsDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/{club}/member/memberships/join —— 加入這個俱樂部（免費一般會員）
        app.MapPost("/api/v1/{club}/member/memberships/join", async (
            string club, string? lang, HttpContext http, IClubResolver clubs, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await center.JoinAsync(me.MemberId, scope, Lang(lang), ct));
        })
        .WithName("JoinClubMembership")
        .WithTags("MemberCenter")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MyMembershipDto>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict);

        // GET /api/v1/member/cards?lang=zh —— 電子會員卡（每份會籍一張，家庭方案多張），已展平
        app.MapGet("/api/v1/member/cards", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await center.GetCardsAsync(me.MemberId, Lang(lang), ct));
        })
        .WithName("GetMyCards")
        .WithTags("MemberCenter")
        .Produces<IReadOnlyList<MemberCardDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/member/cards/{cardId}/regenerate —— 重新產生 QR（舊 token 立即失效）
        app.MapPost("/api/v1/member/cards/{cardId:guid}/regenerate", async (
            Guid cardId, string? lang, HttpContext http, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await center.RegenerateCardAsync(me.MemberId, cardId, Lang(lang), ct));
        })
        .WithName("RegenerateMyCard")
        .WithTags("MemberCenter")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MemberCardDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // GET /api/v1/m/{token}?lang=zh —— 電子會員卡公開驗證（掃 QR 開的頁面），只回五個欄位；不快取
        app.MapGet("/api/v1/m/{token}", async (
            string token, string? lang, HttpContext http, MemberCenterService center, CancellationToken ct) =>
        {
            var result = await center.VerifyCardAsync(token, Lang(lang), ct);
            http.Response.Headers.CacheControl = "no-store"; // 會員卡驗證不得被任何一層快取（docs/14）
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("VerifyMemberCard")
        .WithTags("MemberCenter")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<CardVerificationDto>()
        .Produces(StatusCodes.Status404NotFound);

        // ── 球衣登記 ─────────────────────────────────────────────────────────
        app.MapGet("/api/v1/member/jerseys", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await center.GetJerseysAsync(me.MemberId, Lang(lang), ct));
        })
        .WithName("GetMyJerseys")
        .WithTags("MemberCenter")
        .Produces<IReadOnlyList<MemberJerseyGroupDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/v1/{club}/member/jerseys", async (
            string club, MemberJerseyRequest request, string? lang, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            var scope = await clubs.ResolveAsync(club, ct);
            var created = await center.CreateJerseyAsync(me.MemberId, scope, request, Lang(lang), ct);
            return Results.Created($"/api/v1/member/jerseys", created);
        })
        .WithName("CreateMyJersey")
        .WithTags("MemberCenter")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MemberJerseyDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        app.MapPut("/api/v1/{club}/member/jerseys/{jerseyId:guid}", async (
            string club, Guid jerseyId, MemberJerseyUpdateRequest request, string? lang, HttpContext http,
            IClubResolver clubs, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct, requireVerifiedEmail: true);
            var scope = await clubs.ResolveAsync(club, ct);
            return Results.Ok(await center.UpdateJerseyAsync(me.MemberId, scope, jerseyId, request, Lang(lang), ct));
        })
        .WithName("UpdateMyJersey")
        .WithTags("MemberCenter")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MemberJerseyDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // ── 我的報名（行動 App 用；網頁前台不做報名歸戶）─────────────────────────
        app.MapGet("/api/v1/member/registrations", async (
            string? lang, HttpContext http, MemberAuthenticator authenticator, MemberCenterService center, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await center.GetRegistrationsAsync(me.MemberId, Lang(lang), ct));
        })
        .WithName("GetMyRegistrations")
        .WithTags("MemberCenter")
        .Produces<IReadOnlyList<MemberRegistrationDto>>()
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static string Lang(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
}
