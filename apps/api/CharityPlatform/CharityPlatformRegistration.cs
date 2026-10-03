using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Auth;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.CharityPlatform.Reconciliation;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.CharityPlatform.Workers;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminAuth;

namespace Tcrfc.Api.CharityPlatform;

/// <summary>
/// 慈善捐款平台（獨立後台、獨立資料庫、協會為收款主體）在<b>同一個 API 行程</b>裡的註冊與端點對映
/// （docs/17 §5：與俱樂部共用同一台 VM 與同一個 API 行程，但資料庫、帳號體系、金流憑證全部分開）。
///
/// 🔴 <b>以設定開關：有 <c>CHARITY_SQL_CONNECTION_STRING</c> 才啟用</b>（<see cref="IsEnabled"/>）。沒有設定時整個慈善平台
/// 不註冊任何服務、不對映任何端點（路由直接 404）——與 <c>/readyz</c> 對慈善庫「有設定才檢查」是同一個約定；
/// 只跑俱樂部 API 的環境與測試完全不受影響。啟用時 <c>JWT_SIGNING_KEY_CHARITY</c> 是必填（缺值啟動失敗，比照 E-79）。
/// </summary>
public static class CharityPlatformRegistration
{
    public const string ConnectionStringKey = "CHARITY_SQL_CONNECTION_STRING";

    public static bool IsEnabled(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration[ConnectionStringKey]);

    public static void AddCharityPlatform(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        if (!IsEnabled(configuration))
        {
            return;
        }

        CharityTokenService.ValidateSigningKeyConfigured(configuration);
        var services = builder.Services;

        // ── 獨立資料庫（與 ClubDbContext 不共用連線、不共用 migration 歷史）────────────────
        services.AddDbContext<CharityDbContext>(options => options.UseSqlServer(configuration[ConnectionStringKey]));

        // ── 慈善後台自己的帳號體系：第二個具名 JwtBearer 方案，不取代預設（主站）方案 ───────────
        services.AddSingleton<CharityTokenService>();
        services.AddAuthentication().AddJwtBearer(CharityTokenService.SchemeName, options =>
        {
            options.TokenValidationParameters = new CharityTokenService(configuration).GetValidationParameters();
            // 授權器自己用 AuthenticateAsync 驗證並丟 AdminUnauthenticatedException，不靠框架的挑戰回應。
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    return Task.CompletedTask;
                },
            };
        });
        services.AddSingleton<CharityDataProtector>();
        services.AddScoped<CharityPermissionChecker>();
        services.AddScoped<ICharityAdminAuthorizer, CharityAdminAuthorizer>();
        services.AddScoped<CharityAdminAuthService>();
        services.AddScoped<CharityAuditLogger>();

        // ── 外部服務接縫：目前只有本機假實作（協會的 LINE Pay 商店號／電子發票管道／寄信服務尚未到位，STATUS B-7）──
        // 🔴 假實作只在 Development（或明確 CHARITY_ALLOW_FAKE_PROVIDERS=true）運作，正式環境一律「尚未設定」，見 CharityFakeGuard。
        // 正式實作到位時，把這三行換成真實作即可，捐款流程與後台不動。
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<IInvoiceIssuer, FakeInvoiceIssuer>();
        services.AddSingleton<IEmailSender, FakeEmailSender>();
        // 每日對帳的金流交易明細來源（規劃書 §4.5）。本機假實作＝以本站自己的金流紀錄當明細（永遠一致，只驗證流程）；
        // 正式實作（B-7，協會 LINE Pay 的交易查詢／結算檔）到位時換掉這一行。取不到明細一律記成失敗批次，不會判定全部差異。
        services.AddScoped<IPaymentReconciliationSource, FakePaymentReconciliationSource>();

        // ── Turnstile：有設定密鑰才啟用，否則放行（第一道防線是 IP 限流）──────────────────
        var turnstileSecret = configuration[CloudflareTurnstileVerifier.SecretConfigKey];
        if (string.IsNullOrWhiteSpace(turnstileSecret))
        {
            services.AddSingleton<ITurnstileVerifier, NotConfiguredTurnstileVerifier>();
        }
        else
        {
            services.AddHttpClient("charity-turnstile", client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddSingleton<ITurnstileVerifier>(sp => new CloudflareTurnstileVerifier(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("charity-turnstile"), turnstileSecret,
                sp.GetRequiredService<ILogger<CloudflareTurnstileVerifier>>()));
        }

        // ── 圖片儲存：慈善自己的容器（連線字串 AZURE_BLOB_CONNECTION_STRING_CHARITY），沒設定用替身 ──
        var blobConnection = configuration["AZURE_BLOB_CONNECTION_STRING_CHARITY"];
        if (string.IsNullOrWhiteSpace(blobConnection))
        {
            services.AddSingleton<ICharityImageStorage, UnavailableCharityImageStorage>();
        }
        else
        {
            var container = new BlobContainerClient(blobConnection, configuration["AZURE_BLOB_CONTAINER_CHARITY"] ?? "charity-images");
            // 公開網址基底（指到 Cloudflare CDN 子網域）：慈善自己的設定，來源是 charity.env，不與俱樂部共用。
            var publicBase = PublicBlobBaseUrl.FromConfiguration(configuration, "AZURE_BLOB_PUBLIC_BASE_URL_CHARITY", builder.Environment.IsDevelopment());
            services.AddSingleton<ICharityImageStorage>(sp => new BlobCharityImageStorage(container, publicBase, sp.GetRequiredService<ILogger<BlobCharityImageStorage>>()));
        }

        // ── 前台（公開）與後台的 service ───────────────────────────────────────────
        services.AddScoped<CharityPublicCatalog>();
        services.AddScoped<CharityEmailService>();
        services.AddScoped<CharityInvoiceService>();
        services.AddScoped<CharityDonationService>();
        services.AddScoped<CharityStoresAdminService>();
        services.AddScoped<CharityProjectsAdminService>();
        services.AddScoped<CharityDonationsAdminService>();
        services.AddScoped<CharityRecognitionCatalog>();
        services.AddScoped<CharityReconciliationRunner>();
        services.AddScoped<CharitySettlementsAdminService>();
        services.AddScoped<CharityReconciliationAdminService>();
        services.AddScoped<CharityInvoicesAdminService>();
        services.AddScoped<CharityReportsAdminService>();
        services.AddScoped<CharitySettingsAdminService>();
        services.AddScoped<CharityAuditQueryService>();
        services.AddScoped<CharityAdminAccountsService>();
        services.AddScoped<CharityAdminRolesService>();

        // ── 背景維護（逾時轉換、憑證重試）：Development 預設關閉，見 CharityOptions.WorkersEnabled ──
        services.AddScoped<CharityMaintenanceRunner>();
        services.AddHostedService<CharityBackgroundService>();

        // ── 依訪客 IP 的限流（公開寫入端點、結果頁輪詢、後台登入）。額度可由設定覆寫（測試用寬鬆值）。──
        // 分區鍵用 ClientIpResolver（讀 ForwardedHeadersMiddleware 處理過的 RemoteIpAddress，不自己解析 X-Forwarded-For）。
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(CharityRateLimitPolicies.Write, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIpResolver.Resolve(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = CharityRateLimitPolicies.ResolveWritePermitLimit(http.RequestServices.GetRequiredService<IConfiguration>()),
                    Window = CharityRateLimitPolicies.WriteWindow,
                    QueueLimit = 0,
                }));

            options.AddPolicy(CharityRateLimitPolicies.Read, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIpResolver.Resolve(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = CharityRateLimitPolicies.ResolveReadPermitLimit(http.RequestServices.GetRequiredService<IConfiguration>()),
                    Window = CharityRateLimitPolicies.ReadWindow,
                    QueueLimit = 0,
                }));

            // 後台登入／更新權杖：額度與主站共用同一組設定鍵（AdminAuthRateLimitOptions），政策名稱與計數分開。
            options.AddPolicy(CharityAdminAuthEndpoints.LoginRateLimitPolicyName, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIpResolver.Resolve(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AdminAuthRateLimitOptions.ResolveLoginPermitLimit(http.RequestServices.GetRequiredService<IConfiguration>()),
                    Window = AdminAuthRateLimitOptions.LoginWindow,
                    QueueLimit = 0,
                }));

            options.AddPolicy(CharityAdminAuthEndpoints.RefreshRateLimitPolicyName, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIpResolver.Resolve(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AdminAuthRateLimitOptions.ResolveRefreshPermitLimit(http.RequestServices.GetRequiredService<IConfiguration>()),
                    Window = AdminAuthRateLimitOptions.RefreshWindow,
                    QueueLimit = 0,
                }));
        });
    }

    public static void MapCharityPlatform(this WebApplication app)
    {
        if (!IsEnabled(app.Configuration))
        {
            return;
        }

        app.MapCharityAdminAuthEndpoints();
        app.MapCharityAdminEndpoints();
        app.MapCharityPublicEndpoints();
    }
}
