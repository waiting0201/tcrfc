using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
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

// ── S0-8 圖片上傳共用元件：Azure Blob Storage（本機開發接 Azurite，連線字串格式相容） ──────
// AZURE_BLOB_CONNECTION_STRING 未設定時**不得讓行程無法啟動**——跟 CLUB_SQL_CONNECTION_STRING
// 不一樣：圖片上傳端點掛在 DevWriteGate 後面，本來就不是每個環境都會用到（既有 48 項唯讀端點
// 測試、CI 的其他情境都完全不碰這條路），沒理由讓一個選用功能的缺漏設定拖垮整個服務啟動。
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

// ── 後台新聞寫入（本輪新增）：repository／假操作者解析一律註冊，跟其餘 repository 同一慣例 ──
// 🔴 註冊 ≠ 對外可用。真正決定「這組功能存不存在」的是下面 app.MapAdminNewsEndpoints() 前的
// DevWriteGate 判斷式，DI 註冊本身只是描述「怎麼組出這個物件」，不會主動開任何連線或路由。
builder.Services.AddScoped<AdminArticlesRepository>();
builder.Services.AddScoped<IDevOperatorResolver, DevOperatorResolver>();

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
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
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

app.MapHealthEndpoints();
app.MapClubsEndpoints();
app.MapPlayersEndpoints();
app.MapStaffEndpoints();
app.MapArticlesEndpoints();
app.MapMatchesEndpoints();

// ── 🔴🔴🔴 寫入端點開發模式開關（見 Security/DevWriteGate.cs 的完整說明）─────────────────
// 這組端點在接上登入與權限之前不得在任何對外環境啟用。關閉時（預設）這裡完全不會呼叫
// MapAdminNewsEndpoints()，路由不存在，打了回 404——不是 403，不透露「這裡本來有東西」。
// 開啟需要同時滿足：ASPNETCORE_ENVIRONMENT=Development ＋ ENABLE_UNSAFE_DEV_WRITES=true。
if (DevWriteGate.IsEnabled(builder.Configuration, app.Environment))
{
    app.Logger.LogWarning(
        "🔴 寫入端點開發模式開關已開啟（ENABLE_UNSAFE_DEV_WRITES=true）。" +
        "這組端點沒有登入與權限保護，僅供本機開發測試，切勿在任何對外環境開啟此設定。");
    app.MapAdminNewsEndpoints();
}

app.Run();

// 讓測試專案能用 WebApplicationFactory<Program> 啟動這支服務（ASP.NET Core 標準作法，
// 頂層陳述式的 Program 類別預設是 internal，測試組件看不到）——單純是測試基礎設施要求的樣板，
// 不影響任何執行期行為。見 apps/api/Tcrfc.Api.Tests/README.md。
public partial class Program;
