using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.Clubs;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Features.Staff;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// ── S0-8：Kestrel 請求主體上限，讓「檔案太大」一律得到我們自訂的友善訊息 ──────────────────
// Kestrel 預設上限是 30 MB。若不調整，10–30 MB 之間的檔案會被 ImageUploadOptions.MaxUploadBytes
// 的程式碼檢查擋下（回我們的中文訊息），但 >30 MB 的檔案會先被 Kestrel 自己擋下，回傳它自己的
// 通用 413（本機驗證時兩者行為確實不同，見 apps/api/README.md）。改小上限（10 MB ＋ 1 MB 緩衝，
// 緩衝是給 multipart 邊界字串與其他表單欄位用）讓兩種情況都回應同一種使用者看得懂的訊息。
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = ImageUploadOptions.MaxUploadBytes + 1024 * 1024;
});

// ── JSON：日期一律 ISO 8601（DateOnly/DateTime 預設行為已是），欄位用 camelCase 給前端 ──────
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ── 資料存取：唯讀查詢一律走 Dapper（docs/17-deployment.md §0），寫入走 EF Core（本輪新增） ──
builder.Services.AddSingleton<IClubSqlConnectionFactory, ClubSqlConnectionFactory>();

// ── EF Core（docs/20-cicd.md §5 的一次性 handoff，本輪完成）────────────────────────
// ⚠️ ClubDbContext 是對已存在資料庫跑 dotnet ef dbcontext scaffold 產出的，Data/EfEntities／
// Data/ClubDbContext.cs 是產生檔，之後改綱要要先改 db/club-schema.sql 再重新 scaffold，
// 不要手改產生檔（客製化寫在 Data/ClubDbContextCustomizations.cs 的 OnModelCreatingPartial）。
// 🔴 DbContext 本身不論開發模式開關是否開啟都會註冊（跟其餘五個唯讀 repository 的既有慣例一致，
// 只是描述怎麼連線，注入不代表會真的開連線）；真正需要關閉的是下面的「路由是否掛上去」，
// 見本檔最下方「寫入端點開發模式開關」。
builder.Services.AddDbContext<ClubDbContext>(options =>
{
    var connectionString = builder.Configuration["CLUB_SQL_CONNECTION_STRING"]
        ?? throw new InvalidOperationException(
            "找不到 CLUB_SQL_CONNECTION_STRING 設定值。請確認環境變數已提供" +
            "（本機開發見 deploy/dev/club.env；正式環境見 /opt/tcrfc/secrets/club.env）。");
    options.UseSqlServer(connectionString);
});

// ── 快取接縫：REDIS_HOST 有設定才接 Redis（S0-7d），沒設定注入 no-op ─────────────────
// 本機 `dotnet run` 不需要 Redis 也能跑；docker-compose.yml／docker-compose.dev.yml 的 api
// 服務一律有帶 REDIS_HOST，所以容器化跑法（本機或正式）一定會走 Redis 實作。見 IQueryCache 上的完整說明。
var redisHost = builder.Configuration["REDIS_HOST"];
if (!string.IsNullOrWhiteSpace(redisHost))
{
    var redisPort = builder.Configuration.GetValue<int?>("REDIS_PORT") ?? 6379;
    var redisPassword = builder.Configuration["REDIS_PASSWORD"];
    var redisOptions = new ConfigurationOptions
    {
        EndPoints = { { redisHost, redisPort } },
        Password = string.IsNullOrEmpty(redisPassword) ? null : redisPassword,
        // 🔴 起始連不上（VM 重開機時 redis 容器還沒 ready、Redis 短暫掛掉）不得讓行程無法啟動——
        // fail-open 從「建立連線」這一刻就開始，不是只在查詢時才吞例外（docs/17 §4 硬規則 1）。
        AbortOnConnectFail = false,
        ConnectTimeout = 500,
        SyncTimeout = 500,
        ConnectRetry = 1,
    };

    try
    {
        var redisMultiplexer = ConnectionMultiplexer.Connect(redisOptions);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
        builder.Services.AddSingleton<IQueryCache, RedisQueryCache>();
    }
    catch (Exception ex)
    {
        // 已知這裡的例外面很窄（AbortOnConnectFail=false 時 Connect() 通常不因連不上而丟例外，
        // 背景會自己重試），但組態本身畸形（例如空字串 host）等極端情況仍可能丟例外；即使如此
        // 也不得讓服務無法啟動——退回 no-op，讓服務照樣用 SQL 直接回源。
        using var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        startupLoggerFactory.CreateLogger("Startup")
            .LogWarning(ex, "Redis 連線初始化失敗，改用 no-op 快取（服務仍會正常啟動，只是不快取）");
        builder.Services.AddSingleton<IQueryCache, NoOpQueryCache>();
    }
}
else
{
    builder.Services.AddSingleton<IQueryCache, NoOpQueryCache>();
}

// ── club_id 強制機制：唯一能建立已驗證 ClubScope 的地方 ──────────────────────────
builder.Services.AddScoped<IClubResolver, ClubResolver>();

// ── 🔴🔴🔴 本輪新增（S1：J1–J3 登入與授權）：後台身分驗證與授權 ──────────────────────
// 存取權杖是 JWT（AdminTokenService 簽發／驗證），更新權杖是不透明字串存 admin_refresh_tokens。
// 設計理由與四種擋下情境的驗收見 apps/api/README.md「後台登入權杖設計」「驗收紀錄」兩節。
builder.Services.AddSingleton<AdminTokenService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<IAdminClubAuthorizer, AdminClubAuthorizer>();
builder.Services.AddScoped<IPermissionChecker, PermissionChecker>();
builder.Services.AddScoped<TwoFactorSecretProtector>();

// Data Protection：加密 admin_users.two_factor_secret_encrypted（Security/TwoFactorSecretProtector.cs）。
// 🔴 正式環境務必設定 DATA_PROTECTION_KEYS_PATH 指向持久化 volume，否則容器重建後全部 2FA
// 密鑰永久無法解密——見 TwoFactorSecretProtector.cs 檔頭的完整說明，這不是本次程式碼能防呆的事。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Tcrfc.Admin");
var dataProtectionKeysPath = builder.Configuration["DATA_PROTECTION_KEYS_PATH"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// JWT Bearer：只驗證存取權杖（簽章、issuer、audience、效期），不做任何資料庫查詢——
// 「這個人是誰」與「這個人能不能做這件事」分屬 AdminIdentity／IAdminClubAuthorizer，
// 理由見 AdminTokenService.cs 檔頭「權杖設計」整段說明。
// ⚠️ AdminTokenService 需要 JWT_SIGNING_KEY_CLUB 才能建構驗證參數，這裡直接讀
// builder.Configuration（DI 容器此時還沒建好，不能注入），與 AdminTokenService 執行期
// 讀同一把設定鍵是同一個值，行為一致。
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new AdminTokenService(builder.Configuration).GetValidationParameters();
        // 不用預設的 401 挑戰行為（會回傳空白 body）——AdminClubAuthorizer／AdminAuthEndpoints
        // 自己判斷 User.Identity.IsAuthenticated 並丟 AdminUnauthenticatedException，
        // 交給 ApiExceptionHandler 統一格式化成含中文訊息的 JSON，兩者行為必須一致。
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse(); // 蓋掉框架預設行為，改成什麼都不做——中介軟體管線
                                           // 往後走，User.Identity.IsAuthenticated 維持 false，
                                           // 由端點自己的檢查負責回應。
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// ── S0-8 圖片上傳共用元件：Azure Blob Storage（本機開發接 Azurite，連線字串格式相容） ──────
// AZURE_BLOB_CONNECTION_STRING 未設定時**不得讓行程無法啟動**——跟 CLUB_SQL_CONNECTION_STRING
// 不一樣：圖片上傳只在 Features/AdminNews 的建立／更新端點內才會用到，本來就不是每個環境都會
// 用到（既有 48 項唯讀端點測試、CI 的其他情境都完全不碰這條路），沒理由讓一個選用功能的缺漏
// 設定拖垮整個服務啟動。
// 真正需要它的呼叫（Features/AdminNews 建立／更新時處理封面圖片上傳、換圖或刪除時清理舊物件）
// 沒設定時會在呼叫當下丟出訊息清楚的例外，不是在啟動階段就讓 healthz／readyz 都連帶壞掉。
// 🔴 S0-8 修正（2026-09-22）：原本還有一個獨立的 Features/Uploads 上傳端點會用到這個服務，
// 已經整支移除（見 Features/Uploads/UploadSlotPolicy.cs 上的說明）——現在唯一的呼叫端是
// Features/AdminNews，未來其他模組接圖片上傳時也應該比照，直接在自己的端點內呼叫，
// 不要重新開一個獨立的上傳端點。
var blobConnectionString = builder.Configuration["AZURE_BLOB_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(blobConnectionString))
{
    var blobContainerName = builder.Configuration["AZURE_BLOB_CONTAINER_IMAGES"] ?? "images";
    builder.Services.AddSingleton(new BlobContainerClient(blobConnectionString, blobContainerName));
    builder.Services.AddSingleton<IImageStorageService, BlobImageStorageService>();
}
else
{
    builder.Services.AddSingleton<IImageStorageService, UnavailableImageStorageService>();
}

// ── 各功能模組的 repository ──────────────────────────────────────────────
builder.Services.AddScoped<ClubsRepository>();
builder.Services.AddScoped<PlayersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Staff.StaffRepository>();
builder.Services.AddScoped<ArticlesRepository>();
builder.Services.AddScoped<MatchesRepository>();

// ── S0-7g：排程發布 hosted service（docs/17-deployment.md「排程」既有定案的落點）────────
// ScheduledPublishRunner 註冊為 Singleton（依賴的 IClubSqlConnectionFactory／IQueryCache 本來就是
// Singleton），讓測試能直接從 DI 容器解析出來呼叫，不必等待 ScheduledPublishBackgroundService
// 的計時器。詳細設計理由見 Features/News/ScheduledPublishRunner.cs 檔頭。
builder.Services.AddSingleton<ScheduledPublishRunner>();
builder.Services.AddHostedService<ScheduledPublishBackgroundService>();

// ── 後台新聞寫入 ──────────────────────────────────────────────────────
// created_by／updated_by 一律來自真實登入者（AdminClubScope.Identity.AdminUserId），
// 見 Features/AdminNews/AdminArticlesEndpoints.cs 檔頭說明。
builder.Services.AddScoped<AdminArticlesRepository>();

// ── CORS：只允許設定來源，來源清單從環境變數讀，不寫死（docs/17-deployment.md §10.2） ─────
const string CorsPolicyName = "ClubFrontends";
var corsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    // 本機開發若忘記帶 CORS_ALLOWED_ORIGINS，退回 apps/web／apps/admin 開發用的幾個常見 port，
    // 讓本機起步不必先去翻文件；正式環境沒有這個退回值，未設定就是沒有任何來源被允許。
    // 5174 是 apps/admin 的 vite dev server（apps/admin/vite.config.ts）——只有這個開發預設清單
    // 是本輪（後台新聞接真實 API）唯一允許改動的 apps/api/ 檔案內容，正式環境走 CORS_ALLOWED_ORIGINS
    // 環境變數，不受這裡影響（deploy/Caddyfile 的後台與 API 本來就是不同網域）。
    corsOrigins = ["http://localhost:3000", "http://localhost:3001", "http://localhost:3002", "http://localhost:5174"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOrigins.Length > 0)
        {
            // AllowCredentials：本輪（S1）起後台更新權杖走 __Host- 前綴 Cookie（跨網域，
            // apps/admin 與本 API 是不同來源），瀏覽器 fetch 要帶 Cookie 必須
            // credentials: 'include' ＋ 伺服器端 Access-Control-Allow-Credentials: true，
            // 兩者缺一都會讓 Cookie 被瀏覽器悄悄丟棄（不是 CORS 錯誤，是請求「送出但沒帶
            // Cookie」，比較難察覺）。⛔ AllowCredentials 不能與 AllowAnyOrigin 併用
            // （規格明文禁止），這裡一律搭配明確的 WithOrigins 清單，符合限制。
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        // corsOrigins 為空（正式環境忘記設定）時刻意不呼叫 AllowAnyOrigin()——沒設定來源清單
        // 就是沒有任何瀏覽器來源被允許，比「忘記設定就開放全部」安全。
    });
});

// ── OpenAPI：只在開發環境開，正式環境關掉或鎖住（CLAUDE.md 任務指示） ─────────────────
builder.Services.AddOpenApi();

// ── 統一例外處理：⛔ 不把資料庫例外訊息吐給呼叫端 ─────────────────────────────
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicyName);

// 🔴 順序要求：UseAuthentication 必須在 UseAuthorization 之前，兩者都必須在會用到
// HttpContext.User／[Authorize] 的端點對映之前——本輪 AdminClubAuthorizer 直接讀
// httpContext.User，不靠 [Authorize] 觸發挑戰，但仍需要 UseAuthentication 先把 JWT
// 解析進 HttpContext.User。UseAuthorization 目前沒有任何端點掛 [Authorize] metadata
// （授權邏輯全部手動在 AdminClubAuthorizer／AdminAuthEndpoints 內），保留呼叫是為了
// 讓管線形狀符合 ASP.NET Core 慣例、未來若改用宣告式 [Authorize] 不需要重新調整順序。
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapClubsEndpoints();
app.MapPlayersEndpoints();
app.MapStaffEndpoints();
app.MapArticlesEndpoints();
app.MapMatchesEndpoints();
app.MapAdminAuthEndpoints();

// 路由一律註冊，每個請求各自由 IAdminClubAuthorizer 驗證登入與授權（401／403）。
// 2026-09-23（使用者裁決）：曾經把關這組端點的 DevWriteGate／ENABLE_UNSAFE_DEV_WRITES／
// IDevOperatorResolver 機制已整支移除（確認真實授權已能證明四種擋下情境都有效，見
// apps/api/README.md「S1」整節），本檔不再有任何殘留引用。
app.MapAdminNewsEndpoints();

app.Run();

// 讓測試專案能用 WebApplicationFactory<Program> 啟動這支服務（ASP.NET Core 標準作法，
// 頂層陳述式的 Program 類別預設是 internal，測試組件看不到）——單純是測試基礎設施要求的樣板，
// 不影響任何執行期行為。見 apps/api/Tcrfc.Api.Tests/README.md。
public partial class Program;
