using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Auth;

/// <summary>
/// 慈善後台登入、更新權杖、登出、變更密碼、兩階段驗證、個人檔案。
/// 路徑 <c>/api/v1/donation-platform/admin/auth/*</c>；與主站 <c>/api/v1/admin/auth/*</c> 是兩套完全獨立的帳號體系。
/// </summary>
public static class CharityAdminAuthEndpoints
{
    /// <summary>
    /// 🔴 Cookie 名稱必須與主站不同：兩個後台的 API 呼叫都打<b>同一個 API 網域</b>，Cookie 是依網域（不是依路徑或
    /// 前端來源）保存的，同名會讓協會後台的登入蓋掉俱樂部後台的更新權杖，反之亦然（兩邊同時登入的人會互相踢出）。
    /// <c>__Host-</c> 前綴的限制（Secure、Path=/、不得有 Domain）同主站，docs/14。
    /// </summary>
    public const string RefreshCookieName = "__Host-tcrfc-charity-admin-rt";

    public const string LoginRateLimitPolicyName = "charity-admin-login";
    public const string RefreshRateLimitPolicyName = "charity-admin-refresh";

    public static void MapCharityAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/donation-platform/admin/auth").WithTags("CharityAdminAuth");

        group.MapPost("/login", async (
            LoginRequest request, HttpContext httpContext, CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request.Username, request.Password, request.TotpCode, cancellationToken);

            return result.Outcome switch
            {
                LoginOutcome.Success => WriteSuccessResponse(httpContext, result),
                LoginOutcome.TotpRequired => Results.Ok(new { status = "totp_required", message = result.Message }),
                LoginOutcome.Locked => Results.Json(new { status = "locked", message = result.Message }, statusCode: StatusCodes.Status423Locked),
                LoginOutcome.Disabled => Results.Json(new { status = "disabled", message = result.Message }, statusCode: StatusCodes.Status401Unauthorized),
                _ => Results.Json(new { status = "invalid_credentials", message = result.Message }, statusCode: StatusCodes.Status401Unauthorized),
            };
        })
        .WithName("CharityAdminLogin")
        .RequireRateLimiting(LoginRateLimitPolicyName)
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status423Locked)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (
            HttpContext httpContext, CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) || string.IsNullOrEmpty(rawToken))
            {
                throw new AdminUnauthenticatedException();
            }

            var result = await authService.RefreshAsync(rawToken, cancellationToken);
            if (result is null)
            {
                ClearRefreshCookie(httpContext);
                throw new AdminUnauthenticatedException();
            }

            return WriteSuccessResponse(httpContext, result);
        })
        .WithName("CharityAdminRefreshToken")
        .RequireRateLimiting(RefreshRateLimitPolicyName)
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests);

        // 刻意不掛限流：只清 Cookie ＋（若帶著有效權杖）撤銷單一一筆更新權杖，沒有可被濫用的副作用（同主站）。
        group.MapPost("/logout", async (HttpContext httpContext, CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            if (httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) && !string.IsNullOrEmpty(rawToken))
            {
                await authService.LogoutAsync(rawToken, cancellationToken);
            }
            ClearRefreshCookie(httpContext);
            return Results.NoContent();
        })
        .WithName("CharityAdminLogout")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/change-password", async (
            ChangePasswordRequest request, HttpContext httpContext, ICharityAdminAuthorizer authorizer,
            CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = await authorizer.RequireSignedInAsync(httpContext, cancellationToken);
            var ok = await authService.ChangePasswordAsync(identity.AdminUserId, request.CurrentPassword, request.NewPassword, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "目前密碼不正確。" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("CharityAdminChangePassword")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/2fa/setup", async (
            HttpContext httpContext, ICharityAdminAuthorizer authorizer, CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = await authorizer.RequireSignedInAsync(httpContext, cancellationToken);
            return Results.Ok(await authService.BeginTwoFactorSetupAsync(identity.AdminUserId, cancellationToken));
        })
        .WithName("CharityAdminTwoFactorSetup")
        .Produces<TwoFactorSetupResponse>();

        group.MapPost("/2fa/confirm", async (
            TwoFactorConfirmRequest request, HttpContext httpContext, ICharityAdminAuthorizer authorizer,
            CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = await authorizer.RequireSignedInAsync(httpContext, cancellationToken);
            var ok = await authService.ConfirmTwoFactorAsync(identity.AdminUserId, request.Code, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "驗證碼不正確。" }, statusCode: StatusCodes.Status400BadRequest);
        })
        .WithName("CharityAdminTwoFactorConfirm")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/2fa/disable", async (
            TwoFactorDisableRequest request, HttpContext httpContext, ICharityAdminAuthorizer authorizer,
            CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = await authorizer.RequireSignedInAsync(httpContext, cancellationToken);
            var ok = await authService.DisableTwoFactorAsync(identity.AdminUserId, request.Password, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "密碼不正確。" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("CharityAdminTwoFactorDisable")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (
            HttpContext httpContext, ICharityAdminAuthorizer authorizer, CharityAdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = await authorizer.RequireSignedInAsync(httpContext, cancellationToken);
            return Results.Ok(await authService.GetMeAsync(identity, cancellationToken));
        })
        .WithName("CharityAdminGetMe")
        .Produces<CharityMeResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static IResult WriteSuccessResponse(HttpContext httpContext, LoginResult result)
    {
        SetRefreshCookie(httpContext, result.RefreshTokenRaw!, result.RefreshTokenExpiresAtUtc!.Value);
        return Results.Ok(new LoginResponse(
            result.AccessToken!, result.AccessTokenExpiresAtUtc!.Value, result.MustChangePassword,
            result.TwoFactorEnabled, result.Username!, result.IsSuperAdmin));
    }

    private static void SetRefreshCookie(HttpContext httpContext, string rawToken, DateTime expiresAtUtc)
    {
        httpContext.Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // __Host- 前綴強制要求
            // SameSite=None：協會後台前端與本 API 是不同來源（不同子網域），跨站 fetch 帶 Cookie 一定要 None。
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = expiresAtUtc,
            // ⛔ 不設定 Domain（docs/14：__Host- 前綴不允許，上線前到正式期絕對不得設 Domain）。
        });
    }

    private static void ClearRefreshCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Append(RefreshCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
        });
    }
}
