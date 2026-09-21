using Microsoft.AspNetCore.Http.Json;
using StackExchange.Redis;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Clubs;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Features.Staff;
using Tcrfc.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// ── JSON：日期一律 ISO 8601（DateOnly/DateTime 預設行為已是），欄位用 camelCase 給前端 ──────
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ── 資料存取：唯讀查詢一律走 Dapper（本次任務範圍全是唯讀，docs/17-deployment.md §0） ──────
builder.Services.AddSingleton<IClubSqlConnectionFactory, ClubSqlConnectionFactory>();

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

// ── 各功能模組的 repository ──────────────────────────────────────────────
builder.Services.AddScoped<ClubsRepository>();
builder.Services.AddScoped<PlayersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Staff.StaffRepository>();
builder.Services.AddScoped<ArticlesRepository>();
builder.Services.AddScoped<MatchesRepository>();

// ── CORS：只允許設定來源，來源清單從環境變數讀，不寫死（docs/17-deployment.md §10.2） ─────
const string CorsPolicyName = "ClubFrontends";
var corsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    // 本機開發若忘記帶 CORS_ALLOWED_ORIGINS，退回 apps/web 開發用的幾個常見 port，
    // 讓本機起步不必先去翻文件；正式環境沒有這個退回值，未設定就是沒有任何來源被允許。
    corsOrigins = ["http://localhost:3000", "http://localhost:3001", "http://localhost:3002"];
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

app.Run();

// 讓測試專案能用 WebApplicationFactory<Program> 啟動這支服務（ASP.NET Core 標準作法，
// 頂層陳述式的 Program 類別預設是 internal，測試組件看不到）——單純是測試基礎設施要求的樣板，
// 不影響任何執行期行為。見 apps/api/Tcrfc.Api.Tests/README.md。
public partial class Program;
