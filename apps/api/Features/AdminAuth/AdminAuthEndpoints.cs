using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAuth;

/// <summary>
/// J1 帳號管理：登入、更新權杖、登出、變更密碼、2FA 設定。
/// ⚠️ 這一組端點**不是**俱樂部範圍端點（沒有 <c>{club}</c> 路由段），不經過
/// <see cref="IAdminClubAuthorizer"/>——2FA 設定與變更密碼正是用來滿足
/// 選用的帳號安全功能（2026-09-30 起不再是存取其他端點的前提）。這裡用只確認「有沒有登入」的最小檢查。
/// </summary>
public static class AdminAuthEndpoints
{
    // __Host- 前綴（docs/14-invariants.md 明文規定）：瀏覽器層級強制不得有 Domain 屬性、
    // Path 必須是 /、必須 Secure——設錯這三者任一，瀏覽器直接整顆拒收，不會悄悄放寬。
    public const string RefreshCookieName = "__Host-tcrfc-admin-rt";

    /// <summary>
    /// 依訪客 IP 分區的登入濫用防護政策名稱（2026-09-29 補上，回應 docs/14-invariants.md
    /// 「公開寫入端點一律限流」對 <c>/login</c>／<c>/refresh</c> 原本刻意留下的缺口）。
    ///
    /// ── 為什麼不共用 <c>Common/PublicRateLimitPolicies</c> 既有的兩個政策 ──
    /// 那兩個政策的風險模型是「公開內容端點被灌爆資料庫」（瀏覽數／回饋／表單送出），跟這裡要擋的
    /// 「密碼暴力破解／跨帳號密碼噴灑（password spraying）／刻意觸發帳號鎖定的阻斷服務攻擊」是
    /// 完全不同的威脅模型，額度也需要能各自獨立調整，勉強共用會讓兩邊的數字互相牽制。
    ///
    /// ── 額度是可設定值，不是寫死的常數 ──
    /// 實際額度（正式環境預設值、環境變數鍵名、如何解析）見 <see cref="AdminAuthRateLimitOptions"/>——
    /// 這裡只留政策**名稱**：`Program.cs` 註冊時用
    /// <c>AdminAuthRateLimitOptions.ResolveLoginPermitLimit(builder.Configuration)</c> 取得實際
    /// 額度，測試環境由 <c>Tcrfc.Api.Tests.Fixtures.TestRateLimitOverrides</c> 透過環境變數覆寫成
    /// 寬鬆值（不讓測試用量反過來決定正式環境的安全額度——這是 2026-09-29 當天的修正，第一版曾經
    /// 把額度直接寫死成「蓋過測試呼叫次數」的數字，被回饋認為本末倒置而改掉，教訓見
    /// apps/api/README.md「S1-18d」段）。
    /// ⚠️ **多層防禦**：即使 IP 額度留有餘裕，<c>AdminAuthService</c> 既有的「同一帳號連續 5 次
    /// 失敗鎖定 15 分鐘」機制完全獨立運作、不受這裡數字影響——單一帳號被暴力破解的防線主要仍然
    /// 靠帳號鎖定，這裡的 IP 限流補的是「同一來源對多個不同帳號輪流嘗試（密碼噴灑）」與
    /// 「大量自動化嘗試的整體速率」，兩層防線互補，不是其中一層取代另一層。
    /// </summary>
    public const string LoginRateLimitPolicyName = "admin-login";

    /// <summary>更新權杖端點的依 IP 限流政策名稱——額度同樣是可設定值，見
    /// <see cref="AdminAuthRateLimitOptions"/>。風險模型跟 <see cref="LoginRateLimitPolicyName"/>
    /// 不同：呼叫這支端點需要先持有一把有效的更新權杖（256 bits 亂數，伺服器只存 SHA-256 雜湊，
    /// 見 <c>AdminTokenService</c>），用猜的在計算上不可行，這裡防的是「明文權杖已外流時被重放
    /// 濫用的整體速率」與「對這支端點的一般性灌流量」，額度可以比登入寬鬆。</summary>
    public const string RefreshRateLimitPolicyName = "admin-refresh";

    /// <summary>已登入後仍驗證密碼／TOTP 的端點（變更密碼、2FA 確認／停用）的依 IP 限流政策名稱，
    /// 理由與額度見 <see cref="AdminAuthRateLimitOptions.CredentialCheckPermitLimitDefault"/>。</summary>
    public const string CredentialCheckRateLimitPolicyName = "admin-credential-check";

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
        .RequireRateLimiting(LoginRateLimitPolicyName)
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status423Locked)
        .Produces(StatusCodes.Status429TooManyRequests);

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
        .RequireRateLimiting(RefreshRateLimitPolicyName)
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests);

        // ⚠️ 刻意不掛 RequireRateLimiting：這支端點只清 Cookie ＋（若帶著有效權杖）撤銷單一一筆
        // admin_refresh_tokens，沒有能被濫用來鎖住別人帳號或灌爆資料表的業務副作用，且無論呼不呼叫
        // 這支端點，Cookie 一律會在回應裡被清空——重複呼叫的代價只是多幾次「查一筆、可能撤銷一筆」
        // 的輕量操作。2026-09-29 盤點時評估後決定不需要另開政策，見 apps/api/README.md 對應段落。
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
        .RequireRateLimiting(CredentialCheckRateLimitPolicyName)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests);

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
        .RequireRateLimiting(CredentialCheckRateLimitPolicyName)
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
        .RequireRateLimiting(CredentialCheckRateLimitPolicyName)
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
