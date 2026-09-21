using Microsoft.AspNetCore.Http.Json;
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

// ── 快取接縫：本次不接 Redis，注入 no-op 實作（見 IQueryCache 上的完整說明） ──────────────
builder.Services.AddSingleton<IQueryCache, NoOpQueryCache>();

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
