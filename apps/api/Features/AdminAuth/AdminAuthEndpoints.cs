using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAuth;

/// <summary>
/// J1 帳號管理：登入、更新權杖、登出、變更密碼、2FA 設定。
/// ⚠️ 這一組端點**不是**俱樂部範圍端點（沒有 <c>{club}</c> 路由段），不經過
/// <see cref="IAdminClubAuthorizer"/>——2FA 設定與變更密碼正是用來滿足
/// <see cref="AdminClubAuthorizer"/> 強制要求的兩個前提，本身當然不能被同一個檢查卡住，
/// 否則會是雞生蛋蛋生雞的死結。這裡改用只確認「有沒有登入」的最小檢查。
/// </summary>
public static class AdminAuthEndpoints
{
    // __Host- 前綴（docs/14-invariants.md 明文規定）：瀏覽器層級強制不得有 Domain 屬性、
    // Path 必須是 /、必須 Secure——設錯這三者任一，瀏覽器直接整顆拒收，不會悄悄放寬。
    public const string RefreshCookieName = "__Host-tcrfc-admin-rt";

    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/auth").WithTags("AdminAuth");

        group.MapPost("/login", async (
            LoginRequest request, HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
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
        .WithName("AdminLogin")
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status423Locked);

        group.MapPost("/refresh", async (
            HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
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
        .WithName("AdminRefreshToken")
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            if (httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) && !string.IsNullOrEmpty(rawToken))
            {
                await authService.LogoutAsync(rawToken, cancellationToken);
            }
            ClearRefreshCookie(httpContext);
            return Results.NoContent();
        })
        .WithName("AdminLogout")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/change-password", async (
            ChangePasswordRequest request, HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = RequireAuthenticated(httpContext);
            var ok = await authService.ChangePasswordAsync(identity.AdminUserId, request.CurrentPassword, request.NewPassword, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "目前密碼不正確。" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("AdminChangePassword")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/2fa/setup", async (HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = RequireAuthenticated(httpContext);
            var setup = await authService.BeginTwoFactorSetupAsync(identity.AdminUserId, cancellationToken);
            return Results.Ok(setup);
        })
        .WithName("AdminTwoFactorSetup")
        .Produces<TwoFactorSetupResponse>();

        group.MapPost("/2fa/confirm", async (
            TwoFactorConfirmRequest request, HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = RequireAuthenticated(httpContext);
            var ok = await authService.ConfirmTwoFactorAsync(identity.AdminUserId, request.Code, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "驗證碼不正確。" }, statusCode: StatusCodes.Status400BadRequest);
        })
        .WithName("AdminTwoFactorConfirm")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/v1/admin/auth/me —— 前端 agent 回報缺口①：站台切換器需要的個人檔案與俱樂部授權清單。
        // ⛔ 不回傳密碼雜湊、2FA 密文等任何機密欄位——見 AdminAuthDtos.cs 的 MeResponse 說明。
        group.MapGet("/me", async (HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var me = await authService.GetMeAsync(httpContext, cancellationToken);
            return Results.Ok(me);
        })
        .WithName("AdminGetMe")
        .Produces<MeResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/2fa/disable", async (
            TwoFactorDisableRequest request, HttpContext httpContext, AdminAuthService authService, CancellationToken cancellationToken) =>
        {
            var identity = RequireAuthenticated(httpContext);
            var ok = await authService.DisableTwoFactorAsync(identity.AdminUserId, request.Password, cancellationToken);
            return ok ? Results.NoContent() : Results.Json(new { message = "密碼不正確。" }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("AdminTwoFactorDisable")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static AdminIdentity RequireAuthenticated(HttpContext httpContext)
        => AdminIdentity.FromClaimsPrincipal(httpContext.User) ?? throw new AdminUnauthenticatedException();

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
            Secure = true, // __Host- 前綴強制要求，本機 http 測試見 apps/api/README.md 的說明
            // SameSite=None：後台前端（apps/admin）與本 API 是不同來源（不同子網域，
            // docs/17-deployment.md 的部署拓撲），跨站 fetch 帶 Cookie 一定要 None，
            // Lax／Strict 都會讓瀏覽器不送出這顆 Cookie，整個更新權杖機制形同虛設。
            // 這是 __Host- 前綴＋Secure 之外，多網域拓撲下必然的取捨，詳見 README。
            SameSite = SameSiteMode.None,
            Path = "/", // __Host- 前綴要求 Path 必須是根路徑
            Expires = expiresAtUtc,
            // ⛔ 不設定 Domain——docs/14-invariants.md 明文「上線前到正式期絕對不得設 Domain 屬性」。
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
