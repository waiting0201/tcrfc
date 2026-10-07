using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Tcrfc.Api.Caching;
using Tcrfc.Api.CharityPlatform;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminAccounts;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Features.AdminBanners;
using Tcrfc.Api.Features.AdminComics;
using Tcrfc.Api.Features.AdminFanEvents;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.AdminDraws;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AdminApp;
using Tcrfc.Api.Features.AdminSecurity;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Features.Comics;
using Tcrfc.Api.Features.FanEvents;
using Tcrfc.Api.Features.Shop;
using Tcrfc.Api.Features.Standings;
using Tcrfc.Api.Features.MemberAuth;
using Tcrfc.Api.Features.MemberCenter;
using Tcrfc.Api.Features.MembershipPayments;
using Tcrfc.Api.Features.MembershipPublic;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Features.AdminClubs;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminFaqs;
using Tcrfc.Api.Features.AdminForms;
using Tcrfc.Api.Features.AdminHomeSections;
using Tcrfc.Api.Features.AdminHonors;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Features.AdminPress;
using Tcrfc.Api.Features.AdminBenefits;
using Tcrfc.Api.Features.AdminJerseys;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.AdminPartnerStores;
using Tcrfc.Api.Features.AdminMemberships;
using Tcrfc.Api.Features.AdminProposals;
using Tcrfc.Api.Features.AdminTrials;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminRoles;
using Tcrfc.Api.Features.AdminSeasons;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.AdminSessions;
using Tcrfc.Api.Features.AdminSiteFacts;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Features.AdminStaff;
using Tcrfc.Api.Features.AdminStandings;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Features.AdminVenues;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AdminApp;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Features.AdminSecurity;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Features.Calendar;
using Tcrfc.Api.Features.Clubs;
using Tcrfc.Api.Features.Faqs;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Features.Home;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Pages;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Features.SiteFacts;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Features.Honors;
using Tcrfc.Api.Features.Partners;
using Tcrfc.Api.Features.Press;
using Tcrfc.Api.Features.Proposals;
using Tcrfc.Api.Features.Sponsors;
using Tcrfc.Api.Features.Staff;
using Tcrfc.Api.Features.Teams;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Videos;

// ── 一次性維運指令（不啟動 Web 主機、不讀設定、不連資料庫）：正式庫首次建立第一個管理員時，
// 由 deploy/prod-db-init.sh 以 `docker run -i <api 映像檔> --hash-password` 呼叫，密碼只走標準輸入。
// 見 Security/PasswordHashCli.cs 與 docs/20 §5「正式庫首次初始化」。
if (args is [PasswordHashCli.Flag])
{
    Environment.ExitCode = PasswordHashCli.Run(Console.In, Console.Out, Console.Error);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ── S0-8：Kestrel 請求主體上限，讓「檔案太大」一律得到我們自訂的友善訊息 ──────────────────
// Kestrel 預設上限是 30 MB。若不調整，會被程式碼檢查擋下（回我們的中文訊息）的檔案大小之上，
// 檔案會先被 Kestrel 自己擋下，回傳它自己的通用 413（本機驗證時兩者行為確實不同，見
// apps/api/README.md）。改小上限讓兩種情況都回應同一種使用者看得懂的訊息。
// 🔴 v3.14：Hero 輪播「影片」模式在同一次 multipart 請求裡同時送海報圖（≤10 MB）與影片
// （≤50 MB，VideoUploadOptions.MaxUploadBytes），上限要能同時容納兩者＋緩衝（multipart 邊界
// 字串與其他表單欄位），不能只算圖片那組數字。
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        ImageUploadOptions.MaxUploadBytes + VideoUploadOptions.MaxUploadBytes + 1024 * 1024;
});

// ── JSON：日期一律 ISO 8601（DateOnly/DateTime 預設行為已是），欄位用 camelCase 給前端 ──────
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    // D 批（2026-09-30）：DateTime 一律輸出 UTC 並帶 Z、輸入沒有時區記號視為 UTC，見 Common/UtcDateTimeJsonConverter.cs。
    options.SerializerOptions.Converters.Add(new Tcrfc.Api.Common.UtcDateTimeJsonConverter());
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

// ── 🔴🔴🔴 本輪新增（S1-3 續作：J1 帳號管理／J2 角色與權限／J4 俱樂部與授權）────────────
// AdminSystemAuthorizer 是 J1／J2／J4 全域端點（不含 {club} 路由段）的唯一授權入口，
// 跟既有的 IAdminClubAuthorizer 是同一設計哲學的另一半，見 Security/AdminSystemScope.cs。
builder.Services.AddScoped<IAdminSystemAuthorizer, AdminSystemAuthorizer>();

// ── 🔴🔴🔴 S1-8 新增：列級授權強制（role_permissions.scope_type，own_teams／academy_only）──────
// 見 Security/TeamRowScope.cs／IAdminTeamRowScopeResolver.cs 檔頭的完整說明。跟 IAdminClubAuthorizer
// 是先後兩道關卡：先確認「對這個俱樂部有沒有授權、有沒有這個權限碼」，再問「這個權限碼對這個人
// 是不是被縮限到特定球隊」。C1–C4 的寫入端點共用同一個解析器。
builder.Services.AddScoped<IAdminTeamRowScopeResolver, AdminTeamRowScopeResolver>();
builder.Services.AddScoped<AdminAccountsRepository>();
builder.Services.AddScoped<AdminRolesRepository>();
builder.Services.AddScoped<AdminClubsRepository>();
builder.Services.AddScoped<AdminCompetitionsRepository>();
builder.Services.AddScoped<AdminTeamsRepository>();

// ── S1-7：C1–C3 球隊／球員／教練俱樂部範圍 CRUD ─────────────────────────────
builder.Services.AddScoped<AdminPlayersRepository>();
builder.Services.AddScoped<AdminStaffRepository>();
builder.Services.AddScoped<AdminPartnersRepository>();
builder.Services.AddScoped<AdminSponsorsRepository>();
builder.Services.AddScoped<AdminSponsorPackagesRepository>();
builder.Services.AddScoped<AdminSponsorActivationsRepository>();
builder.Services.AddScoped<AdminProposalsRepository>();
builder.Services.AddScoped<AdminLeadsRepository>();
builder.Services.AddScoped<AdminCharityOrgsRepository>();
builder.Services.AddScoped<AdminCharityProgramsRepository>();
builder.Services.AddScoped<AdminImpactRecordsRepository>();
builder.Services.AddScoped<AdminImpactMetricsRepository>();
builder.Services.AddScoped<AdminCharitySettingsRepository>();
builder.Services.AddScoped<AdminPressRepository>();
builder.Services.AddScoped<AdminHonorsRepository>();
builder.Services.AddScoped<PartnersRepository>();
builder.Services.AddScoped<SponsorsRepository>();
builder.Services.AddScoped<CharityRepository>();
builder.Services.AddScoped<PressRepository>();
builder.Services.AddScoped<HonorsRepository>();
builder.Services.AddScoped<ProposalsRepository>();

// ── S1-8：C4 賽程與賽果／積分榜俱樂部範圍 CRUD ＋ CSV 批次匯入 ─────────────────
builder.Services.AddScoped<Tcrfc.Api.Features.AdminMatches.AdminMatchesRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminStandings.AdminStandingsRepository>();

// ── S1-9：P1–P3 課程項目／梯次／報名 ─────────────────────────────────────
builder.Services.AddScoped<Tcrfc.Api.Features.AdminPrograms.AdminProgramsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSessions.AdminSessionsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminRegistrations.AdminRegistrationsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Programs.ProgramsRepository>();

// ── S1-10：G1 表單設計器／G2 詢問收件匣 ＋ 10 表單中心公開讀取與送出 ──────────────
builder.Services.AddScoped<AdminFormsRepository>();
builder.Services.AddScoped<AdminSeasonsRepository>();
builder.Services.AddScoped<AdminEnquiriesRepository>();
// 公開表單的 Cloudflare Turnstile 驗證：設了 TURNSTILE_SECRET_KEY 才啟用，否則放行（只剩 IP 限流＋honeypot）。
{
    var clubTurnstileSecret = builder.Configuration[Tcrfc.Api.Security.CloudflareClubTurnstileVerifier.SecretConfigKey];
    if (string.IsNullOrWhiteSpace(clubTurnstileSecret))
    {
        builder.Services.AddSingleton<Tcrfc.Api.Security.IClubTurnstileVerifier, Tcrfc.Api.Security.NotConfiguredClubTurnstileVerifier>();
    }
    else
    {
        builder.Services.AddHttpClient(Tcrfc.Api.Security.CloudflareClubTurnstileVerifier.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        builder.Services.AddSingleton<Tcrfc.Api.Security.IClubTurnstileVerifier>(sp => new Tcrfc.Api.Security.CloudflareClubTurnstileVerifier(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(Tcrfc.Api.Security.CloudflareClubTurnstileVerifier.HttpClientName),
            clubTurnstileSecret, sp.GetRequiredService<ILogger<Tcrfc.Api.Security.CloudflareClubTurnstileVerifier>>()));
    }
}
builder.Services.AddScoped<FormsRepository>();

// ── S1-11：L1 行事曆總覽／L2 自建事件 ＋ 13 賽事行事曆公開讀取（含單場 .ics） ──────
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarOverviewRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarCustomEventsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarTracksRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarSettingsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarSubscriptionsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminCalendar.AdminCalendarExportRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Calendar.CalendarFeedRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Calendar.CalendarSettingsPublicRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Calendar.CalendarRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Calendar.CalendarIcsRepository>();

// Data Protection：加密 admin_users.two_factor_secret_encrypted（Security/TwoFactorSecretProtector.cs）。
// 🔴 正式環境必須設定 DATA_PROTECTION_KEYS_PATH 指向持久化 volume，否則容器重建後全部 2FA
// 密鑰永久無法解密——見 TwoFactorSecretProtector.cs 檔頭；Production 缺值會啟動失敗（下方）。
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Tcrfc.Admin");
// Production 缺值、目錄不存在或不可寫會在啟動時丟例外（E-109，Common/DataProtectionKeyRing.cs）。
var dataProtectionKeysDirectory = Tcrfc.Api.Common.DataProtectionKeyRing.Resolve(builder.Configuration, builder.Environment);
if (dataProtectionKeysDirectory is not null)
{
    dataProtection.PersistKeysToFileSystem(dataProtectionKeysDirectory);
}

// JWT Bearer：只驗證存取權杖（簽章、issuer、audience、效期），不做任何資料庫查詢——
// 「這個人是誰」與「這個人能不能做這件事」分屬 AdminIdentity／IAdminClubAuthorizer，
// 理由見 AdminTokenService.cs 檔頭「權杖設計」整段說明。
// ⚠️ AdminTokenService 需要 JWT_SIGNING_KEY_CLUB 才能建構驗證參數，這裡直接讀
// builder.Configuration（DI 容器此時還沒建好，不能注入），與 AdminTokenService 執行期
// 讀同一把設定鍵是同一個值，行為一致。
// E-79：必填設定在 Build 前驗證，缺值時啟動失敗（而不是每個請求 500）。測試主機的 fixture 皆以
// Environment.SetEnvironmentVariable 在建立 Server 前設定此鍵，環境變數在 CreateBuilder 時就已讀入。
AdminTokenService.ValidateSigningKeyConfigured(builder.Configuration);
MemberTokenService.ValidateConfigured(builder.Configuration); // E 批：會員權杖的金鑰（未設 JWT_SIGNING_KEY_MEMBER 時由後台金鑰衍生）
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

// E 批（2026-10-01，S2-11）：會員（前台帳號）的存取權杖——獨立的驗證機制、獨立的 issuer／audience、獨立的簽章金鑰，
// 與上面的後台機制互不認帳，見 Security/MemberTokenService.cs。不是預設機制：只有會員端點透過 MemberAuthenticator 明確要求它。
builder.Services
    .AddAuthentication()
    .AddJwtBearer(MemberTokenService.Scheme, options =>
    {
        options.TokenValidationParameters = new MemberTokenService(builder.Configuration).GetValidationParameters();
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse(); // 同後台：不用框架預設的空白 401，由 MemberAuthenticator 丟例外、統一格式化
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// ── CH-2／CH-3（2026-10-01）：慈善捐款平台（獨立後台、獨立資料庫、協會收款）。有 CHARITY_SQL_CONNECTION_STRING 才啟用，
// 沒設定時不註冊任何服務也不對映端點；實作都在 apps/api/CharityPlatform/，與主站 Features 完全分開。
builder.AddCharityPlatform();

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
    // 公開網址基底（選填）：讓圖片／影片／documents 的公開網址指到 Cloudflare CDN 子網域；上傳與刪除仍走連線字串。
    // 未設定時回退 BlobContainerClient.Uri；格式錯誤（非絕對 https，Development 放行 http）啟動即失敗。
    // 只套用在三個公開解析器；proposals 私有容器只經 API 串流，不經此設定。
    builder.Services.AddSingleton(PublicBlobBaseUrl.FromConfiguration(
        builder.Configuration, "AZURE_BLOB_PUBLIC_BASE_URL", builder.Environment.IsDevelopment()));
    builder.Services.AddSingleton<IImageStorageService, BlobImageStorageService>();
    // S1-12（驗收退回後補做）：物件鍵 → 公開網址，供 OG 圖片等需要輸出完整網址的情境使用。
    builder.Services.AddSingleton<IImagePublicUrlResolver, BlobImagePublicUrlResolver>();

    // 🔴 v3.14 Hero 輪播影片：同一個帳號、獨立容器（跟圖片分開，方便未來各自套用不同的
    // 保留政策／CDN 快取規則）。用具名服務（keyed DI，.NET 8+）注入，避免跟上面圖片用的
    // 「未具名」BlobContainerClient 單例互相覆蓋——見 Videos/BlobVideoStorageService.cs。
    var blobVideoContainerName = builder.Configuration["AZURE_BLOB_CONTAINER_VIDEOS"] ?? "videos";
    builder.Services.AddKeyedSingleton("videos", new BlobContainerClient(blobConnectionString, blobVideoContainerName));
    builder.Services.AddSingleton<IVideoStorageService, BlobVideoStorageService>();
    // E-64 修正：影片物件鍵 → 公開網址，跟圖片那顆 IImagePublicUrlResolver 同一個機制、
    // 分開宣告（容器不同，見 IVideoPublicUrlResolver 檔頭）。
    builder.Services.AddSingleton<IVideoPublicUrlResolver, BlobVideoPublicUrlResolver>();

    // E1a：非圖片、非影片的檔案（新聞稿 PDF、品牌識別包 ZIP、贊助提案 PDF）。兩個具名容器：公開下載與私有
    // （提案 PDF 只能經 API 串流）。容器名稱可用環境變數覆寫，預設值見 Documents/BlobDocumentStorageService。
    builder.Services.AddKeyedSingleton("documents-public",
        new BlobContainerClient(blobConnectionString, builder.Configuration["AZURE_BLOB_CONTAINER_DOCUMENTS"] ?? "documents"));
    builder.Services.AddKeyedSingleton("documents-private",
        new BlobContainerClient(blobConnectionString, builder.Configuration["AZURE_BLOB_CONTAINER_PROPOSALS"] ?? "proposals"));
    builder.Services.AddSingleton<IDocumentStorageService, BlobDocumentStorageService>();
    builder.Services.AddSingleton<IDocumentPublicUrlResolver, BlobDocumentPublicUrlResolver>();
}
else
{
    builder.Services.AddSingleton<IImageStorageService, UnavailableImageStorageService>();
    builder.Services.AddSingleton<IVideoStorageService, UnavailableVideoStorageService>();
    builder.Services.AddSingleton<IImagePublicUrlResolver, UnavailableImagePublicUrlResolver>();
    builder.Services.AddSingleton<IVideoPublicUrlResolver, UnavailableVideoPublicUrlResolver>();
    builder.Services.AddSingleton<IDocumentStorageService, UnavailableDocumentStorageService>();
    builder.Services.AddSingleton<IDocumentPublicUrlResolver, UnavailableDocumentPublicUrlResolver>();
}

// ── 各功能模組的 repository ──────────────────────────────────────────────
builder.Services.AddScoped<ClubsRepository>();
builder.Services.AddScoped<PlayersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Staff.StaffRepository>();
builder.Services.AddScoped<TeamsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Competitions.CompetitionsRepository>();
builder.Services.AddScoped<ArticlesRepository>();
builder.Services.AddScoped<MatchesRepository>();
builder.Services.AddScoped<PagesRepository>();

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

// ── S1-4：後台頁面管理（B1）寫入 ──────────────────────────────────────────
// 見 Features/AdminPages/AdminPagesRepository.cs 檔頭說明——寫入走 EF Core、公開讀取走 Dapper
// 的 PagesRepository（上面已註冊），跟新聞模組同一種切分方式。
builder.Services.AddSingleton<IPageTemplateCatalog>(PageTemplateCatalog.Default); // B1 固定頁版型目錄（測試主機可取代，見 PageTemplates.cs）
builder.Services.AddScoped<AdminPagesRepository>();

// ── S1-6：B3 首頁編排／B4 常見問題 ────────────────────────────────────────
// 寫入走 EF Core（AdminBanners／AdminHomeSections／AdminFaqs，AdminFaqCategories 全域不分俱樂部），
// 公開讀取走 Dapper（Home／Faqs），跟既有模組同一種切分方式。
builder.Services.AddScoped<AdminBannersRepository>();
builder.Services.AddScoped<AdminHomeSectionsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminFaqs.AdminFaqsRepository>();
builder.Services.AddScoped<AdminFaqCategoriesRepository>();
builder.Services.AddScoped<AdminFaqEmbedSlotsRepository>();
builder.Services.AddScoped<HomeRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Faqs.FaqsRepository>();

// ── S1-12：H 搜尋與 AI 能見度 ─────────────────────────────────────────────
// 全站 SEO 預設／追蹤碼／redirects 走 EF Core（比照既有 AdminXxxRepository 慣例），
// 公開端點（sitemap-entries／robots-directives／redirects／tracking）與孤立頁面偵測
// 走 Dapper（IClubSqlConnectionFactory，比照既有唯讀查詢慣例）。
builder.Services.AddScoped<AdminSeoSettingsRepository>();
builder.Services.AddScoped<AdminRedirectsRepository>();
builder.Services.AddScoped<AdminSeoReportRepository>();
builder.Services.AddScoped<AdminSeoSchemaCompletenessRepository>();
builder.Services.AddScoped<AdminGeoLlmsRepository>();
builder.Services.AddScoped<AdminGeoCrawlerRepository>();
builder.Services.AddScoped<SeoRepository>();

// ── S1-12d：I 網站設定（GEO-03／GEO-04 站台事實） ────────────────────────────
builder.Services.AddScoped<AdminSiteFactsRepository>();
builder.Services.AddScoped<SiteFactsRepository>();

// ── S1-12d 後續缺口補完：全站共用場地主檔唯讀清單（見 Features/AdminVenues 檔頭） ──────────
builder.Services.AddScoped<AdminVenuesRepository>();

// B1（S2-4／S2-5／S2-6）：P4 試訓、K1–K4 會員系統、L3／L4 行事曆進階。
builder.Services.AddScoped<ClubSettingsStore>();
builder.Services.AddScoped<ClubTextSettings>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminComics.AdminComicsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminFanEvents.AdminFanEventsRepository>();
// ── C1（S3-3／S3-4）：站內商店 S1–S6。⛔ 庫存、訂單、商品可購買狀態屬「不得讀快取」五類——這些類別刻意不注入 IQueryCache（ArchitectureTests 鎖定）。
builder.Services.AddScoped<Tcrfc.Api.Features.AdminDraws.AdminDrawsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.InventoryService>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.ShopSettingsReader>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopCollectionsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopProductsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.ShopOrderLifecycle>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopInventoryRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopOrdersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopShipmentsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopRefundsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopSettingsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopCredentialsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopReportsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.AdminShopDonationCodesRepository>();
// 金流與電子發票接縫：B-10（LINE Pay 商店號未到位）本期不串接，預設註冊「尚未串接」實作；日後換掉這兩行即可，後台邏輯不需改動。
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.ILinePayGateway, Tcrfc.Api.Features.AdminShop.NotConfiguredLinePayGateway>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminShop.IEInvoiceService, Tcrfc.Api.Features.AdminShop.NotConfiguredEInvoiceService>();
builder.Services.AddScoped<SensitiveActionLogger>();

// ── D 批（2026-09-30）：G3 電子報、E4–E6 廣告、M1–M5 App 後台、App 公開端點、J3 帳號活動 ──
builder.Services.AddScoped<Tcrfc.Api.Features.AdminNewsletter.AdminNewsletterRepository>();
builder.Services.AddSingleton<Tcrfc.Api.Features.AdminNewsletter.INewsletterEdmSync, Tcrfc.Api.Features.AdminNewsletter.NotConfiguredEdmSync>(); // 供應商未定，見 docs/17 §3「D 批的接縫」
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdminAdSlotsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdminAdvertisersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdminAdCampaignsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdminAdCreativesRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdminAdReportsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdCreativeMapper>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminAds.AdMaintenanceService>();
builder.Services.AddScoped<PushTokenProtector>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AppConfigComposer>();
builder.Services.AddSingleton<Tcrfc.Api.Features.AppPublic.IAppConfigPublisher, Tcrfc.Api.Features.AppPublic.NotConfiguredAppConfigPublisher>(); // Cloudflare 靜態設定尚未建立，見 docs/19 §7
builder.Services.AddSingleton<Tcrfc.Api.Features.AdminApp.IPushTransport, Tcrfc.Api.Features.AdminApp.NotConfiguredPushTransport>(); // APNs／FCM 金鑰尚未建立，見 docs/19 §5
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.PushContentGuard>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.PushDispatcher>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.PushRulesStore>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.AdminAppReleasesRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.AdminAppLayoutRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.AdminAppPushRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.AdminAppDevicesRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminApp.AdminAppConfigRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AppDevicesService>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AppLayoutReader>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AdServingService>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AdEventIngestService>();
builder.Services.AddScoped<Tcrfc.Api.Features.AppPublic.AppDiagnosticsIntake>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSecurity.AdminSecurityOverviewRepository>();
builder.Services.AddHostedService<Tcrfc.Api.Features.AdminApp.AppMaintenanceBackgroundService>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminMembers.MemberNumberGenerator>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminMembers.AdminMembersRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminMemberships.AdminMembershipPlansRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminMemberships.AdminMembershipsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminJerseys.AdminJerseysRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminPartnerStores.AdminPartnerStoresRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminBenefits.AdminBenefitsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminTrials.AdminTrialsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminTrials.AdminTrialRegistrationsRepository>();
// 2026-10-02：G-09 電子報訂閱、P4 試訓公開端點（退訂憑證用 Data Protection，purpose 獨立）。
builder.Services.AddScoped<Tcrfc.Api.Features.Newsletter.NewsletterRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Newsletter.NewsletterUnsubscribeTokens>();
builder.Services.AddScoped<Tcrfc.Api.Features.Trials.TrialsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Search.SearchRepository>(); // G-02 全站搜尋（LIKE，不用全文檢索，取捨見該類別檔頭）
// 2026-10-02：I 網站設定其餘子模組（I2 選單／I3 全域設定／I4 多語系與字串翻譯表／I6 EDM 設定）與公開讀取。
builder.Services.AddScoped<Tcrfc.Api.Common.ClubSettingsEditor>();
builder.Services.AddScoped<Tcrfc.Api.Features.SiteSettings.SiteSettingsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSiteSettings.AdminGlobalSettingsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSiteSettings.TranslationStatusReader>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSiteSettings.AdminI18nRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSiteSettings.AdminUiStringsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.AdminSiteSettings.AdminEdmSettingsRepository>();
// 2026-10-02：A 儀表板；GA4 流量資料來源接縫（憑證未取得，預設「尚未串接」，見 docs/17 §3）。
builder.Services.AddScoped<Tcrfc.Api.Features.AdminDashboard.AdminDashboardRepository>();
builder.Services.AddSingleton<Tcrfc.Api.Features.AdminDashboard.IAnalyticsSource, Tcrfc.Api.Features.AdminDashboard.NotConfiguredAnalyticsSource>();

// ── CORS：只允許設定來源，來源清單從環境變數讀，不寫死（docs/17-deployment.md §10.2） ─────
// ── E 批（2026-10-01，S2-11／S3-2）：主站前台會員中心、會籍付款訂單、文化公開端點 ──────────────────────────
builder.Services.AddHttpClient(Tcrfc.Api.Features.MemberAuth.LineLoginClient.HttpClientName);
builder.Services.AddSingleton<Tcrfc.Api.Features.MemberAuth.ILineLoginClient, Tcrfc.Api.Features.MemberAuth.LineLoginClient>(); // 憑證缺值時端點回 503，見 LineLoginClient
builder.Services.AddSingleton<MemberTokenService>();
builder.Services.AddSingleton<Tcrfc.Api.Features.MemberAuth.MemberSecureTokens>();
builder.Services.AddScoped<MemberAuthenticator>();
builder.Services.AddScoped<Tcrfc.Api.Features.MemberAuth.AppDeviceSessionService>(); // AP-3：App 更新權杖鏈（掛 app_devices）
builder.Services.AddScoped<Tcrfc.Api.Features.MemberAuth.MemberSessionService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MemberAuth.MemberMembershipService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MemberAuth.MemberAuthService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MemberCenter.MemberCenterService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MemberDraws.MemberDrawsService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MembershipPayments.MembershipActivationService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MembershipPayments.MembershipOrderService>();
builder.Services.AddScoped<Tcrfc.Api.Features.MembershipPublic.MembershipPublicRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Comics.ComicsRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.FanEvents.FanEventsRepository>();

// F 批（2026-10-01，S3-5 站內商店前台）：目錄、購物車、結帳與訂單、發票、定時維護。🔴 這一組類別刻意不注入 IQueryCache（庫存、購物車、訂單狀態不得讀快取）。
builder.Services.AddScoped<Tcrfc.Api.Features.Standings.StandingsRepository>(); // F 批：公開積分榜與球員數據彙總
builder.Services.AddScoped<Tcrfc.Api.Features.Shop.ShopCatalogRepository>();
builder.Services.AddScoped<Tcrfc.Api.Features.Shop.ShopCartService>();
builder.Services.AddScoped<Tcrfc.Api.Features.Shop.ShopInvoiceService>();
builder.Services.AddScoped<Tcrfc.Api.Features.Shop.ShopOrderService>();
builder.Services.AddScoped<Tcrfc.Api.Features.Shop.ShopMaintenanceService>();
builder.Services.AddHostedService<Tcrfc.Api.Features.Shop.ShopMaintenanceBackgroundService>();

// 寄信接縫：開發環境（或非 Production 且 EMAIL_SENDER=localfile）寫成本機檔案；其餘環境「尚未串接」（供應商留待部署時決定，docs/17 §3「E 批的接縫」）。
// 🔴 Production 絕不註冊本機寫檔實作（信件含一次性權杖）。
var localEmail = !builder.Environment.IsProduction()
                 && (builder.Environment.IsDevelopment() || string.Equals(builder.Configuration["EMAIL_SENDER"], "localfile", StringComparison.OrdinalIgnoreCase));
if (localEmail)
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Email.IEmailSender, Tcrfc.Api.Features.Email.LocalFileEmailSender>();
}
else
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Email.IEmailSender, Tcrfc.Api.Features.Email.NotConfiguredEmailSender>();
}

// 會籍付款金流接縫：LINE Pay 商店號未取得（B-10），預設「尚未串接」；開發環境預設用本機假金流（絕不碰真實金流）。
// PAYMENT_GATEWAY=fake 在 Production 啟動就失敗——寧可起不來，也不要讓假金流在正式環境開通會籍。
var paymentGatewayMode = builder.Configuration["PAYMENT_GATEWAY"];
var fakePayment = string.Equals(paymentGatewayMode, "fake", StringComparison.OrdinalIgnoreCase)
                  || (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(paymentGatewayMode));
if (fakePayment && builder.Environment.IsProduction())
{
    throw new InvalidOperationException("PAYMENT_GATEWAY=fake 不得用於 Production 環境。");
}

if (fakePayment)
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.MembershipPayments.IPaymentGateway, Tcrfc.Api.Features.MembershipPayments.LocalFakePaymentGateway>();
}
else
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.MembershipPayments.IPaymentGateway, Tcrfc.Api.Features.MembershipPayments.NotConfiguredPaymentGateway>(); // 取得商店號後只換這一行，見 docs/17 §3
}

// 商店電子發票開立接縫：發票服務未選定（B-10），預設「尚未串接」；開發環境預設用本機假發票（絕不碰任何發票服務）。
// INVOICE_ISSUER=fake 在 Production 啟動就失敗——寧可起不來，也不要讓假發票號碼出現在正式訂單上。
var invoiceIssuerMode = builder.Configuration["INVOICE_ISSUER"];
var fakeInvoice = string.Equals(invoiceIssuerMode, "fake", StringComparison.OrdinalIgnoreCase)
                  || (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(invoiceIssuerMode));
if (fakeInvoice && builder.Environment.IsProduction())
{
    throw new InvalidOperationException("INVOICE_ISSUER=fake 不得用於 Production 環境。");
}

if (fakeInvoice)
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Shop.IInvoiceIssuer, Tcrfc.Api.Features.Shop.LocalFakeInvoiceIssuer>();
}
else
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Shop.IInvoiceIssuer, Tcrfc.Api.Features.Shop.NotConfiguredInvoiceIssuer>(); // 取得發票服務後只換這一行，見 docs/17 §3
}

// 「由地址定位」接縫（S2-5，K4 特約店家）：正式供應商 Google Maps Geocoding API（2026-10-02 拍板）。
// GEOCODER=google 啟用；金鑰 GOOGLE_MAPS_GEOCODING_API_KEY 缺值時比照 LINE 登入的慣例優雅降級（端點回 503、存檔不阻擋），不讓啟動失敗。
// GEOCODER=fake 在 Production 啟動就失敗——寧可起不來，也不要讓假座標出現在正式地圖上。開發環境預設用本機假定位（絕不碰外部服務）。
var geocoderMode = builder.Configuration["GEOCODER"];
var fakeGeocoder = string.Equals(geocoderMode, "fake", StringComparison.OrdinalIgnoreCase)
                   || (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(geocoderMode));
if (fakeGeocoder && builder.Environment.IsProduction())
{
    throw new InvalidOperationException("GEOCODER=fake 不得用於 Production 環境。");
}

if (fakeGeocoder)
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Geocoding.IGeocoder, Tcrfc.Api.Features.Geocoding.LocalFakeGeocoder>();
}
else if (string.Equals(geocoderMode, "google", StringComparison.OrdinalIgnoreCase))
{
    Tcrfc.Api.Features.Geocoding.GoogleGeocoderRegistration.AddGoogleGeocoder(builder.Services); // 🔴 內含關閉 HttpClient URL 日誌（金鑰在查詢字串），見 GoogleGeocoder
}
else
{
    builder.Services.AddSingleton<Tcrfc.Api.Features.Geocoding.IGeocoder, Tcrfc.Api.Features.Geocoding.NotConfiguredGeocoder>();
}

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
            // WithExposedHeaders：跨來源時瀏覽器預設只讓前端讀得到「安全清單」標頭，Content-Disposition（下載檔名）
            // 與 Retry-After（限流 429 的等待秒數）都不在其中，不 expose 就永遠讀到 null。
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                .WithExposedHeaders("Content-Disposition", "Retry-After", "ETag");
        }
        // corsOrigins 為空（正式環境忘記設定）時刻意不呼叫 AllowAnyOrigin()——沒設定來源清單
        // 就是沒有任何瀏覽器來源被允許，比「忘記設定就開放全部」安全。
    });
});

// ── S1-10（審查回饋修正，2026-09-25）／S1-17 修正（2026-09-29，多重受信任來源）：限流依「真實
// 訪客 IP」分區，不是連線本身看到的 IP ──────────────────────────────────────────
// 有兩條路徑會讓 api 收到公開表單送出請求，兩條都要正確解出訪客真實 IP：
//   ① Cloudflare → Caddy → api（後台 SPA／行動 App／任何直打 API_DOMAIN 的呼叫端）：
//      api 容器的 TCP 連線來源是 Caddy 容器的 Docker 內部 IP。
//   ② Cloudflare → Caddy → nuxt-tcrfc／nuxt-bw（Nuxt 伺服器端代理 server/api/backend/[...path].ts）
//      → api（S1-17 起，10 表單中心公開送出走這條）：api 容器的 TCP 連線來源變成
//      nuxt-tcrfc／nuxt-bw 容器自己的 Docker 內部 IP，不是 Caddy、也不是訪客。
// 兩條路徑下，Caddy 或 nuxt-tcrfc／nuxt-bw 都不會自動讓下游的 api 認得訪客真實 IP
// （Caddy 的 trusted_proxies／client_ip_headers 只解決 Caddy 自己怎麼看 Cloudflare；
// nuxt 的代理路由把 Caddy 解析好的 X-Forwarded-For 原封轉發，不代表 api 會自動信任它）。
// ASP.NET Core 的 ForwardedHeadersMiddleware 負責把連線來源換成 X-Forwarded-For 帶的訪客真實
// IP，**但只在來源是受信任的代理時才生效**——見 Security/TrustedProxyConfiguration.cs 的完整
// 說明（含「為什麼信任這一組固定 IP、不信任整個 Docker 網段」，以及🔴「未設定時中介軟體本身
// 完全不掛，不是掛了但清單留空」——空的 KnownProxies／KnownIPNetworks 對
// ForwardedHeadersMiddleware 而言是「信任所有來源」，不是「不信任任何人」，這是 S1-10 開發時
// 親自踩到、務必記住的框架陷阱）。務必放在 UseRateLimiter（甚至任何其他中介軟體）之前，這樣
// 後續所有讀取 HttpContext.Connection.RemoteIpAddress 的地方都已經是修正後的值。
var trustedProxyIps = builder.Configuration[TrustedProxyConfiguration.ConfigKey];
builder.Services.Configure<ForwardedHeadersOptions>(options => TrustedProxyConfiguration.Configure(options, trustedProxyIps));

// ── S1-10：10 表單中心公開送出端點的濫用防護（規劃書「防機器人」，見 FormsRepository 檔頭） ──
// 全系統沒有串接任何 CAPTCHA 服務（Turnstile／reCAPTCHA），這裡改用依 IP 分區的固定視窗限流當
// 第一層防線：同一個 IP 5 分鐘內最多 20 次送出，超過直接 429（QueueLimit=0，不排隊等待，
// 公開表單沒有排隊的必要）。防機器人現況盤點見 apps/api/README.md「S1-17 修正」段。
// 🔴 這是「規劃書或 docs 沒寫、執行層自行決定」的具體選擇（任務指示原文），數字沒有規格依據，
// 屬最小可行防護，比照 Common/CsvUtils.cs 檔頭「沒定義就採最小可行」的既有慣例。
// ⚠️ 20 這個數字同時要照顧到 WebApplicationFactory 整合測試：測試主機的
// httpContext.Connection.RemoteIpAddress 一律是同一個值（TestServer 沒有真實連線，且測試環境
// 未設定 TRUSTED_PROXY_IPS，ForwardedHeadersMiddleware 不會信任任何來源，行為不受本輪修正影響），
// Tcrfc.Api.Tests.AdminFormsEnquiriesTests 全部公開送出呼叫共用同一個分區，單一測試檔約
// 11 次呼叫，20 留有餘裕；對正式環境而言，同一個真實訪客 IP 5 分鐘內 20 次送出仍遠低於正常訪客的
// 使用量，作為第一層防線足夠，見 apps/api/README.md「S1-10」段。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 429 帶 Retry-After（秒）：固定視窗限流器會在 lease metadata 提供 RetryAfter；沒有時（理論上不會）不補，
    // 不自己猜數字。BFF 與後台依此顯示「請於 N 秒後再試」。
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    // 🔴 依「呼叫端 IP」分區，不是 AddFixedWindowLimiter 那種全站共用同一個計數的寫法——
    // 後者會讓所有訪客共用同一組額度，一個人洗流量就會擋到所有人，不是本來想要的「擋住單一
    // 來源洗版」，見 RateLimitPartition.GetFixedWindowLimiter 用法。分區鍵用
    // ClientIpResolver.Resolve（讀 HttpContext.Connection.RemoteIpAddress）而不是自己重新解析
    // X-Forwarded-For，是為了不繞過 ForwardedHeadersMiddleware 的信任判斷，見該類別上的說明。
    options.AddPolicy(FormsEndpoints.RateLimitPolicyName, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        });
    });

    // ── S1-18c：補齊其餘公開寫入端點的限流缺口（見 Common/PublicRateLimitPolicies.cs 檔頭完整
    // 說明——兩個政策為什麼分開、數值怎麼來的）。同一套「依訪客 IP 分區」寫法，不重新解析
    // X-Forwarded-For，理由同上一個政策。──────────────────────────────────────────
    options.AddPolicy(PublicRateLimitPolicies.LightInteraction, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.LightInteractionPermitLimit,
            Window = PublicRateLimitPolicies.LightInteractionWindow,
            QueueLimit = 0,
        });
    });

    options.AddPolicy(PublicRateLimitPolicies.Submission, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.SubmissionPermitLimit,
            Window = PublicRateLimitPolicies.SubmissionWindow,
            QueueLimit = 0,
        });
    });

    // ── D 批（2026-09-30）：App 公開寫入端點（裝置註冊、訂閱、廣告事件、診斷、通知開啟）。額度讀
    // httpContext.RequestServices 的 IConfiguration（理由同下方 admin-login 政策），測試可覆寫，見
    // PublicRateLimitPolicies.App 的說明。
    options.AddPolicy(PublicRateLimitPolicies.App, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveAppPermitLimit(configuration),
            Window = PublicRateLimitPolicies.AppWindow,
            QueueLimit = 0,
        });
    });

    // ── E 批（2026-10-01，S2-11）：會員登入類與會員寫入類兩個政策，額度與理由見 PublicRateLimitPolicies.MemberAuth／MemberWrite。
    options.AddPolicy(PublicRateLimitPolicies.MemberAuth, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveMemberAuthPermitLimit(configuration),
            Window = PublicRateLimitPolicies.MemberAuthWindow,
            QueueLimit = 0,
        });
    });
    options.AddPolicy(PublicRateLimitPolicies.MemberWrite, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveMemberWritePermitLimit(configuration),
            Window = PublicRateLimitPolicies.MemberWriteWindow,
            QueueLimit = 0,
        });
    });

    // ── 2026-10-02：全站搜尋（G-02）、電子報訂閱（G-09）、試訓報名（P4）三個公開端點的限流，
    // 額度與理由見 PublicRateLimitPolicies.Search／Newsletter／TrialRegistration，額度可由設定覆寫（測試主機用寬鬆值）。
    options.AddPolicy(PublicRateLimitPolicies.Search, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveSearchPermitLimit(configuration),
            Window = PublicRateLimitPolicies.SearchWindow,
            QueueLimit = 0,
        });
    });
    options.AddPolicy(PublicRateLimitPolicies.Newsletter, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveNewsletterPermitLimit(configuration),
            Window = PublicRateLimitPolicies.NewsletterWindow,
            QueueLimit = 0,
        });
    });
    options.AddPolicy(PublicRateLimitPolicies.TrialRegistration, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PublicRateLimitPolicies.ResolveTrialRegistrationPermitLimit(configuration),
            Window = PublicRateLimitPolicies.TrialRegistrationWindow,
            QueueLimit = 0,
        });
    });
    // ── 後台登入／更新權杖端點的依 IP 限流（2026-09-29，補齊 docs/14-invariants.md 對
    // AdminAuth 原本刻意留下的缺口；同日兩度修正——① 額度改成可設定值，見
    // AdminAuthRateLimitOptions.cs 檔頭「為什麼要從寫死的常數改成可設定值」；② 額度改讀
    // httpContext.RequestServices 解析出來的 IConfiguration，不是這裡的 builder.Configuration
    // 閉包，理由見下一段）。兩個政策的風險模型（密碼噴灑／帳號鎖定型阻斷服務 vs. 已有效權杖被
    // 濫用）跟公開內容端點不同，刻意不共用 PublicRateLimitPolicies 的既有政策。
    //
    // 🔴 為什麼不直接用 builder.Configuration（跟 TrustedProxyConfiguration／
    // CLUB_SQL_CONNECTION_STRING 等既有讀法一樣）：那些既有讀法全部發生在 Program.cs 頂層、
    // builder.Build() **之前**，這裡不一樣——PermitLimit 是包在
    // RateLimitPartition.GetFixedWindowLimiter 的第二個引數（factory）裡，只有在**真的有請求
    // 打進來、且該分區鍵第一次出現時**才會被呼叫一次，時間點在整個 IHost 建置完成、開始服務
    // 請求之後，遠比 builder.Build() 晚。改用 httpContext.RequestServices 解析
    // IConfiguration（DI 容器裡 Build() 完成後的那一份，會包含
    // WebApplicationFactory.ConfigureWebHost／ConfigureAppConfiguration 加入的所有設定來源）
    // 是這個時間點該用的正確讀法，也讓測試端可以用 IWebHostBuilder.ConfigureAppConfiguration
    // 加入行程內、只屬於該測試主機的設定來源覆寫額度（見
    // Tcrfc.Api.Tests.Fixtures.TestRateLimitOverrides），不必再用
    // Environment.SetEnvironmentVariable 寫行程全域狀態——後者在不同 collection 的
    // WebApplicationFactory 需要「彼此不同」的覆寫值時（本例：一般測試要寬鬆值、驗證 429 的
    // 測試要一個很小的值）會互相覆蓋，造成間歇性失敗。────────────────────────────
    options.AddPolicy(AdminAuthEndpoints.LoginRateLimitPolicyName, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = AdminAuthRateLimitOptions.ResolveLoginPermitLimit(configuration),
            Window = AdminAuthRateLimitOptions.LoginWindow,
            QueueLimit = 0,
        });
    });

    options.AddPolicy(AdminAuthEndpoints.RefreshRateLimitPolicyName, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = AdminAuthRateLimitOptions.ResolveRefreshPermitLimit(configuration),
            Window = AdminAuthRateLimitOptions.RefreshWindow,
            QueueLimit = 0,
        });
    });

    // S1-18 收尾（2026-10-02）：已登入後仍驗證密碼／TOTP 的端點（變更密碼、2FA 確認／停用）。
    options.AddPolicy(AdminAuthEndpoints.CredentialCheckRateLimitPolicyName, httpContext =>
    {
        var partitionKey = ClientIpResolver.Resolve(httpContext);
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = AdminAuthRateLimitOptions.ResolveCredentialCheckPermitLimit(configuration),
            Window = AdminAuthRateLimitOptions.CredentialCheckWindow,
            QueueLimit = 0,
        });
    });
});

// ── OpenAPI：只在開發環境開，正式環境關掉或鎖住（CLAUDE.md 任務指示） ─────────────────
builder.Services.AddOpenApi();

// ── 統一例外處理：⛔ 不把資料庫例外訊息吐給呼叫端 ─────────────────────────────
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// 統一錯誤結構（App 規劃書 §9.5）：UseStatusCodePages 為「空本文」的 401／403／404／429 等補 ProblemDetails 時，
// 一併帶上 code／messageZh／messageEn／retryable（例外路徑見 ApiExceptionHandler，Results.Problem 見 ApiErrorEnvelope.Extensions）。
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Status = status;
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        Tcrfc.Api.Common.ApiErrorEnvelope.Fill(context.ProblemDetails, status);
    };
});

var app = builder.Build();

// 🔴 只有真的設定了 TRUSTED_PROXY_IPS 才掛這個中介軟體——這才是真正的防線，不是「掛了但清單留空」
// （ForwardedHeadersMiddleware 把空的信任清單當成「信任所有來源」，見
// Security/TrustedProxyConfiguration.cs 檔頭「未設定時中介軟體本身完全不掛」的完整說明）。
// 一定要放在管線最前面：後面任何一段（例外處理的記錄、CORS、限流、一般端點邏輯）只要讀了
// HttpContext.Connection.RemoteIpAddress，都要讀到已經套用信任判斷之後的值。
if (TrustedProxyConfiguration.IsEnabled(trustedProxyIps))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
// 沒有本文的錯誤狀態（Results.NotFound()、中介軟體直接回 429 等）補成統一的 ProblemDetails；已有本文的回應不動。
app.UseStatusCodePages();

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
app.UseRateLimiter();
// 公開列表端點的 ETag／If-None-Match → 304（App 規劃書 §2.4；只掛白名單路徑，不碰「不得讀快取」的五類，見該類別檔頭）。
app.UseMiddleware<Tcrfc.Api.Common.ConditionalGetMiddleware>();

app.MapHealthEndpoints();
app.MapClubsEndpoints();
app.MapPlayersEndpoints();
app.MapStaffEndpoints();
app.MapTeamsEndpoints();
app.MapArticlesEndpoints();
app.MapMatchesEndpoints();
Tcrfc.Api.Features.Competitions.CompetitionsEndpoints.MapCompetitionsEndpoints(app);
app.MapPagesEndpoints();

// ── S1-6：B3／B4 公開讀取 ────────────────────────────────────────────────
app.MapHomeEndpoints();
app.MapFaqsEndpoints();

// ── S1-9：05 課程與活動公開讀取＋報名送出 ─────────────────────────────────
app.MapProgramsEndpoints();
Tcrfc.Api.Features.Newsletter.NewsletterEndpoints.MapNewsletterEndpoints(app);
Tcrfc.Api.Features.Trials.TrialsEndpoints.MapTrialsEndpoints(app);
Tcrfc.Api.Features.Search.SearchEndpoints.MapSearchEndpoints(app);
Tcrfc.Api.Features.SiteSettings.SiteSettingsEndpoints.MapSiteSettingsEndpoints(app);
Tcrfc.Api.Features.AdminSiteSettings.AdminGlobalSettingsEndpoints.MapAdminGlobalSettingsEndpoints(app);
Tcrfc.Api.Features.AdminSiteSettings.AdminI18nEndpoints.MapAdminI18nEndpoints(app);
Tcrfc.Api.Features.AdminSiteSettings.AdminUiStringsEndpoints.MapAdminUiStringsEndpoints(app);
Tcrfc.Api.Features.AdminSiteSettings.AdminEdmSettingsEndpoints.MapAdminEdmSettingsEndpoints(app);
Tcrfc.Api.Features.AdminDashboard.AdminDashboardEndpoints.MapAdminDashboardEndpoints(app);

// ── S1-10：10 表單中心公開讀取＋送出 ─────────────────────────────────────
app.MapFormsEndpoints();

// ── S1-11：13 賽事行事曆公開讀取（合併賽事＋公開自建事件、單場 .ics） ────────────
app.MapCalendarEndpoints();
app.MapCalendarFeedEndpoints();

app.MapAdminAuthEndpoints();

// 路由一律註冊，每個請求各自由 IAdminClubAuthorizer 驗證登入與授權（401／403）。
// 2026-09-23（使用者裁決）：曾經把關這組端點的 DevWriteGate／ENABLE_UNSAFE_DEV_WRITES／
// IDevOperatorResolver 機制已整支移除（確認真實授權已能證明四種擋下情境都有效，見
// apps/api/README.md「S1」整節），本檔不再有任何殘留引用。
app.MapAdminNewsEndpoints();

// ── 🔴🔴🔴 本輪新增（S1-3 續作）：J1／J2／J4 端點 ──────────────────────────────
app.MapAdminAccountsEndpoints();
app.MapAdminRolesEndpoints();
app.MapAdminClubsEndpoints();
app.MapAdminCompetitionsEndpoints();
app.MapAdminTeamsEndpoints();

// ── S1-7：C1–C3 球隊／球員／教練俱樂部範圍 CRUD ─────────────────────────────
app.MapAdminPlayersEndpoints();
app.MapAdminStaffEndpoints();
app.MapAdminPartnersEndpoints();
app.MapAdminSponsorsEndpoints();
app.MapAdminProposalsEndpoints();
app.MapAdminCharityEndpoints();
app.MapAdminPressEndpoints();
app.MapAdminHonorsEndpoints();
app.MapPartnersEndpoints();
app.MapSponsorsEndpoints();
app.MapCharityEndpoints();
app.MapPressEndpoints();
app.MapHonorsEndpoints();
app.MapProposalsEndpoints();
app.MapAdminMatchesEndpoints();
app.MapAdminStandingsEndpoints();

// ── S1-4：B1 頁面管理 ────────────────────────────────────────────────────
app.MapAdminPagesEndpoints();

// ── S1-6：B3 首頁編排／B4 常見問題 ────────────────────────────────────────
app.MapAdminBannersEndpoints();
app.MapAdminHomeSectionsEndpoints();
app.MapAdminFaqsEndpoints();
app.MapAdminFaqCategoriesEndpoints();
app.MapAdminFaqEmbedSlotsEndpoints();

// ── S1-9：P1–P3 課程項目／梯次／報名 ─────────────────────────────────────
app.MapAdminProgramsEndpoints();
app.MapAdminSessionsEndpoints();
app.MapAdminRegistrationsEndpoints();

// ── S1-10：G1 表單設計器／G2 詢問收件匣 ──────────────────────────────────
app.MapAdminFormsEndpoints();
app.MapAdminSeasonsEndpoints();
app.MapAdminEnquiriesEndpoints();

// ── S1-11：L1 行事曆總覽／L2 自建事件 ────────────────────────────────────
app.MapAdminCalendarEndpoints();
app.MapAdminCalendarAdvancedEndpoints();

// ── S1-12：H 搜尋與 AI 能見度 ─────────────────────────────────────────────
app.MapAdminSeoSettingsEndpoints();
app.MapAdminRedirectsEndpoints();
app.MapAdminSeoReportEndpoints();

// ── S1-12c：GEO-05 結構化資料完整性檢查 ───────────────────────────────────
app.MapAdminSeoSchemaCompletenessEndpoints();

// ── S1-12a／S1-12b：GEO-01 llms.txt 維護／GEO-02 AI 爬蟲授權 ──────────────
app.MapAdminGeoLlmsEndpoints();
app.MapAdminGeoCrawlerEndpoints();

app.MapSeoEndpoints();

// ── S1-12d：I 網站設定（GEO-03／GEO-04 站台事實） ────────────────────────────
app.MapAdminSiteFactsEndpoints();
app.MapSiteFactsEndpoints();

// ── S1-12d 後續缺口補完：全站共用場地主檔唯讀清單 ────────────────────────────
app.MapAdminVenuesEndpoints();
app.MapAdminMembersEndpoints();
app.MapAdminMembershipsEndpoints();
app.MapAdminJerseysEndpoints();
app.MapAdminPartnerStoresEndpoints();
app.MapAdminBenefitsEndpoints();
app.MapAdminTrialsEndpoints();
app.MapAdminComicsEndpoints();
app.MapAdminFanEventsEndpoints();
app.MapAdminShopCatalogEndpoints();
app.MapAdminShopOrdersEndpoints();
app.MapAdminShopBackofficeEndpoints();
app.MapAdminDrawsEndpoints();

// ── D 批（2026-09-30）：G3 電子報、E4–E6 廣告、M1–M5 App 後台、J3 帳號活動、App 公開端點 ──
app.MapAdminNewsletterEndpoints();
app.MapAdminAdsEndpoints();
app.MapAdminAppEndpoints();
app.MapAdminSecurityEndpoints();
app.MapAppPublicEndpoints();

// ── E 批（2026-10-01）：會員中心與文化公開端點 ─────────────────────────────────────
app.MapMemberAuthEndpoints();
app.MapMemberCenterEndpoints();
Tcrfc.Api.Features.MemberDraws.MemberDrawsEndpoints.MapMemberDrawsEndpoints(app);
app.MapMembershipOrderEndpoints();
app.MapMembershipPublicEndpoints();
app.MapComicsEndpoints();
app.MapFanEventsEndpoints();
app.MapShopEndpoints();
app.MapStandingsEndpoints();

// ── CH-2／CH-3：慈善捐款平台前台公開端點、慈善後台登入與 N1–N3 API（未啟用時什麼都不做）──
app.MapCharityPlatform();

app.Run();

// 讓測試專案能用 WebApplicationFactory<Program> 啟動這支服務（ASP.NET Core 標準作法，
// 頂層陳述式的 Program 類別預設是 internal，測試組件看不到）——單純是測試基礎設施要求的樣板，
// 不影響任何執行期行為。見 apps/api/Tcrfc.Api.Tests/README.md。
public partial class Program;
