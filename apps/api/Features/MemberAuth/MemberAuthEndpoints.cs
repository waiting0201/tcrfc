using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberAuth;

/// <summary>
/// 會員（前台帳號）註冊、登入與帳號管理端點（主站規劃書 §3.14「前台功能」）。路徑 <c>/api/v1/member/…</c>（帳號層，不分俱樂部）。
/// 全部<b>公開寫入</b>端點一律依訪客 IP 限流（<see cref="PublicRateLimitPolicies.MemberAuth"/>／<see cref="PublicRateLimitPolicies.MemberWrite"/>，ArchitectureTests 會掃）。
/// 更新權杖：<c>tokenDelivery=cookie</c>（預設）→ <c>__Host-tcrfc-member-rt</c> HttpOnly Cookie；<c>body</c>（App、伺服器端代理）→ 放在回應本文。
/// </summary>
public static class MemberAuthEndpoints
{
    public const string RefreshCookieName = "__Host-tcrfc-member-rt";

    public static void MapMemberAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/member").WithTags("MemberAuth");

        group.MapPost("/auth/register", async (
            MemberRegisterRequest request, IClubResolver clubs, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await clubs.ResolveAsync(request.Club ?? string.Empty, ct);
            return Results.Created("/api/v1/member/me", await auth.RegisterAsync(request, club, ct));
        })
        .WithName("MemberRegister")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberRegisteredDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/verify-email", async (MemberVerifyEmailRequest request, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await auth.VerifyEmailAsync(request.Token, ct);
            return Results.Ok(new { verified = true, club });
        })
        .WithName("MemberVerifyEmail")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/resend-verification", async (
            MemberResendVerificationRequest request, IClubResolver clubs, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await clubs.ResolveAsync(request.Club ?? string.Empty, ct);
            await auth.ResendVerificationAsync(request, club, ct);
            return Results.Accepted(); // 一律 202：不洩漏 Email 是否註冊過
        })
        .WithName("MemberResendVerification")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/login", async (MemberLoginRequest request, HttpContext http, MemberAuthService auth, CancellationToken ct) =>
        {
            var (tokens, summary) = await auth.LoginAsync(request, ct);
            return WriteSession(http, tokens, summary, IsBodyMode(request.TokenDelivery, request.DeviceInstallId));
        })
        .WithName("MemberLogin")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberSessionDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status423Locked)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/refresh", async (MemberRefreshRequest? request, HttpContext http, MemberAuthService auth, CancellationToken ct) =>
        {
            var bodyMode = !string.IsNullOrEmpty(request?.RefreshToken) || IsBodyMode(request?.TokenDelivery);
            var raw = request?.RefreshToken;
            if (string.IsNullOrEmpty(raw))
            {
                http.Request.Cookies.TryGetValue(RefreshCookieName, out raw);
            }

            var result = string.IsNullOrEmpty(raw) ? null : await auth.RefreshAsync(raw, ct);
            if (result is null)
            {
                ClearRefreshCookie(http);
                throw new MemberUnauthenticatedException("登入已逾時，請重新登入。", "session_expired");
            }

            return WriteSession(http, result.Value.Tokens, result.Value.Summary, bodyMode);
        })
        .WithName("MemberRefresh")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberSessionDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/logout", async (MemberRefreshRequest? request, HttpContext http, MemberSessionService sessions, CancellationToken ct) =>
        {
            var raw = request?.RefreshToken;
            if (string.IsNullOrEmpty(raw))
            {
                http.Request.Cookies.TryGetValue(RefreshCookieName, out raw);
            }

            if (!string.IsNullOrEmpty(raw))
            {
                await sessions.RevokeAsync(raw, ct);
            }

            ClearRefreshCookie(http);
            return Results.NoContent();
        })
        .WithName("MemberLogout")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/auth/logout-all", async (
            HttpContext http, MemberAuthenticator authenticator, MemberSessionService sessions, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            await sessions.RevokeAllAsync(me.MemberId, ct);
            ClearRefreshCookie(http);
            return Results.NoContent();
        })
        .WithName("MemberLogoutAll")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/auth/forgot-password", async (
            MemberForgotPasswordRequest request, IClubResolver clubs, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await clubs.ResolveAsync(request.Club ?? string.Empty, ct);
            await auth.ForgotPasswordAsync(request, club, ct);
            return Results.Accepted(); // 一律 202：不洩漏 Email 是否註冊過
        })
        .WithName("MemberForgotPassword")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/reset-password", async (MemberResetPasswordRequest request, MemberAuthService auth, CancellationToken ct) =>
        {
            await auth.ResetPasswordAsync(request, ct);
            return Results.NoContent();
        })
        .WithName("MemberResetPassword")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/change-password", async (
            MemberChangePasswordRequest request, HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            var (tokens, summary) = await auth.ChangePasswordAsync(me.MemberId, request, ct);
            return WriteSession(http, tokens, summary, IsBodyMode(request.TokenDelivery, request.DeviceInstallId));
        })
        .WithName("MemberChangePassword")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberSessionDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests);

        // ── LINE 一鍵登入與綁定 ──────────────────────────────────────────────
        group.MapPost("/auth/line/authorize", async (
            MemberLineAuthorizeRequest request, HttpContext http, IClubResolver clubs, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await clubs.ResolveAsync(request.Club ?? string.Empty, ct);
            var me = request.Mode == "bind" ? await authenticator.RequireAsync(http, ct) : null;
            return Results.Ok(auth.LineAuthorize(request, club, me?.MemberId));
        })
        .WithName("MemberLineAuthorize")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberLineAuthorizeDto>()
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/line/callback", async (
            MemberLineCallbackRequest request, HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.TryAsync(http, ct);
            var (dto, tokens) = await auth.LineCallbackAsync(request, me?.MemberId, ct);
            if (tokens is null || dto.Session is null)
            {
                return Results.Ok(dto);
            }

            var bodyMode = IsBodyMode(request.TokenDelivery, request.DeviceInstallId);
            ApplyRefresh(http, tokens, bodyMode);
            return Results.Ok(dto with
            {
                Session = dto.Session with
                {
                    RefreshToken = bodyMode ? tokens.RefreshToken : null,
                    RefreshTokenExpiresAt = bodyMode ? tokens.RefreshTokenExpiresAtUtc : null,
                },
            });
        })
        .WithName("MemberLineCallback")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberLineCallbackDto>()
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/auth/line/complete", async (
            MemberLineCompleteRequest request, HttpContext http, IClubResolver clubs, MemberAuthService auth, CancellationToken ct) =>
        {
            var club = await clubs.ResolveAsync(request.Club ?? string.Empty, ct);
            var (session, tokens) = await auth.LineCompleteAsync(request, club, ct);
            var bodyMode = IsBodyMode(request.TokenDelivery, request.DeviceInstallId);
            ApplyRefresh(http, tokens, bodyMode);
            return Results.Ok(session! with
            {
                RefreshToken = bodyMode ? tokens.RefreshToken : null,
                RefreshTokenExpiresAt = bodyMode ? tokens.RefreshTokenExpiresAtUtc : null,
            });
        })
        .WithName("MemberLineComplete")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces<MemberSessionDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/me/line", async (HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            await auth.UnbindLineAsync(me.MemberId, ct);
            return Results.NoContent();
        })
        .WithName("MemberUnbindLine")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict);

        // ── App 裝置（AP-3）：會員自己的裝置清單與單一裝置撤銷 ──────────────────────
        // 規劃書 §4.3 只硬性要求「登出全部裝置」；單一裝置撤銷是「更新權杖須可由伺服器端撤銷」的自然延伸，無畫面規格（由 App 設定頁自行決定是否使用）。
        group.MapGet("/devices", async (
            HttpContext http, MemberAuthenticator authenticator, AppDeviceSessionService appSessions, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            var devices = await appSessions.ListForMemberAsync(me.MemberId, ct);
            return Results.Ok(devices.Select(d => new MemberDeviceDto
            {
                DeviceId = d.DeviceId, Platform = d.Platform, OsVersion = d.OsVersion, AppVersion = d.AppVersion,
                LastActiveAt = d.LastActiveAt, HasActiveSession = d.HasActiveSession,
            }).ToList());
        })
        .WithName("MemberListDevices")
        .Produces<IReadOnlyList<MemberDeviceDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/devices/{deviceId:guid}/revoke", async (
            Guid deviceId, HttpContext http, MemberAuthenticator authenticator, AppDeviceSessionService appSessions, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return await appSessions.RevokeDeviceAsync(me.MemberId, deviceId, ct) ? Results.NoContent() : throw new MemberNotFoundException();
        })
        .WithName("MemberRevokeDevice")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // ── 個人資料 ─────────────────────────────────────────────────────────
        group.MapGet("/me", async (HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await auth.GetProfileAsync(me.MemberId, ct));
        })
        .WithName("MemberGetProfile")
        .Produces<MemberProfileDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/me", async (
            MemberUpdateProfileRequest request, HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            return Results.Ok(await auth.UpdateProfileAsync(me.MemberId, request, ct));
        })
        .WithName("MemberUpdateProfile")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberWrite)
        .Produces<MemberProfileDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/me", async (
            [Microsoft.AspNetCore.Mvc.FromBody] MemberDeleteAccountRequest request, HttpContext http, MemberAuthenticator authenticator, MemberAuthService auth, CancellationToken ct) =>
        {
            var me = await authenticator.RequireAsync(http, ct);
            await auth.DeleteAccountAsync(me.MemberId, request, ct);
            ClearRefreshCookie(http);
            return Results.NoContent();
        })
        .WithName("MemberDeleteAccount")
        .RequireRateLimiting(PublicRateLimitPolicies.MemberAuth)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    /// <summary>App（帶 <c>deviceInstallId</c>）一律 body 交付：沒有 Cookie 這回事，更新權杖要進安全儲存區。</summary>
    private static bool IsBodyMode(string? delivery, string? deviceInstallId = null)
        => !string.IsNullOrEmpty(deviceInstallId) || string.Equals(delivery, "body", StringComparison.OrdinalIgnoreCase);

    private static IResult WriteSession(HttpContext http, MemberSessionTokens tokens, MemberSummaryDto summary, bool bodyMode)
    {
        ApplyRefresh(http, tokens, bodyMode);
        return Results.Ok(new MemberSessionDto
        {
            AccessToken = tokens.AccessToken, AccessTokenExpiresAt = tokens.AccessTokenExpiresAtUtc,
            RefreshToken = bodyMode ? tokens.RefreshToken : null,
            RefreshTokenExpiresAt = bodyMode ? tokens.RefreshTokenExpiresAtUtc : null,
            Member = summary,
        });
    }

    private static void ApplyRefresh(HttpContext http, MemberSessionTokens tokens, bool bodyMode)
    {
        if (bodyMode)
        {
            return;
        }

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // __Host- 前綴強制要求
            SameSite = SameSiteMode.None, // 前台（Nuxt）與 API 不同來源（docs/17 拓撲），跨站 fetch 帶 Cookie 必須 None，同後台
            Path = "/",
            // ⛔ 不設 Domain（__Host- 前綴與 docs/14 都禁止）
        };
        if (tokens.IsPersistent)
        {
            options.Expires = tokens.RefreshTokenExpiresAtUtc; // 「記住我」：持久 Cookie；否則為工作階段 Cookie（伺服器端仍有 24 小時上限）
        }

        http.Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken, options);
    }

    private static void ClearRefreshCookie(HttpContext http)
        => http.Response.Cookies.Append(RefreshCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Path = "/", Expires = DateTimeOffset.UnixEpoch,
        });
}
