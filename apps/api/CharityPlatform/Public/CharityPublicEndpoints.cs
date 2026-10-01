using System.Text.RegularExpressions;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Public;

/// <summary>
/// 慈善捐款平台前台公開端點（<b>全部不需要登入</b>——捐款人不登入、不註冊，規劃書 §1）。路徑前綴
/// <c>/api/v1/donation-platform/</c>。語系用 <c>?lang=zh|en</c>（同主站公開端點的契約），英文缺漏回退繁中並以
/// <c>isFallback</c> 標示。<b>公開寫入端點（建單、發起付款、確認、取消）一律掛依訪客 IP 的限流</b>
/// （<see cref="CharityRateLimitPolicies.Write"/>），結果頁輪詢掛 <see cref="CharityRateLimitPolicies.Read"/>。
/// </summary>
public static partial class CharityPublicEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    // 只做「長得像單號」的粗檢（擋掉明顯的垃圾輸入與過長字串，不打資料庫）；真實單號是 CH＋16 碼（見 CharityDonationRules.DeriveOrderNo），
    // 本機種子的單號是 DEVTEST-DN-0001 這種形狀，兩者都要能查。單號的不可猜測性來自 80 bits 亂數，不來自這個格式檢查。
    [GeneratedRegex(@"^[A-Za-z0-9\-]{6,64}$")]
    private static partial Regex OrderNoFormat();

    public static void MapCharityPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/donation-platform").WithTags("CharityPublic");

        // GET /settings?lang=zh —— 站台文案與全站預設金額範圍。
        group.MapGet("/settings", async (string? lang, CharityPublicCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.GetSettingsAsync(RequestLocale.ToDbLocale(lang), cancellationToken)))
            .WithName("CharityGetSettings").Produces<PublicSettingsDto>();

        // GET /stores/{slug}?lang=zh —— 掃碼落地頁的店家識別。對不到有效店家回 200 ＋ store:null（視同無店家歸屬，不報錯）。
        group.MapGet("/stores/{slug}", async (string slug, string? lang, CharityPublicCatalog catalog, CancellationToken cancellationToken) =>
        {
            var store = await catalog.ResolveActiveStoreAsync(slug, RequestLocale.ToDbLocale(lang), cancellationToken);
            return Results.Ok(new PublicStoreLandingDto(store));
        }).WithName("CharityGetStoreLanding").Produces<PublicStoreLandingDto>();

        // GET /projects?lang=zh —— 已上架項目卡片牆（掃碼落地頁與一般入口共用同一份）。
        group.MapGet("/projects", async (string? lang, CharityPublicCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListPublishedProjectsAsync(RequestLocale.ToDbLocale(lang), cancellationToken)))
            .WithName("CharityListProjects").Produces<IReadOnlyList<PublicProjectCardDto>>();

        // GET /projects/{slug}?lang=zh —— 項目詳情（含捐款表單需要的金額選項、金額範圍、憑證模式）。
        group.MapGet("/projects/{slug}", async (string slug, string? lang, CharityPublicCatalog catalog, CancellationToken cancellationToken) =>
        {
            var project = await catalog.GetPublishedProjectAsync(slug, RequestLocale.ToDbLocale(lang), cancellationToken);
            return project is null ? Results.NotFound() : Results.Ok(project);
        }).WithName("CharityGetProject").Produces<PublicProjectDetailDto>().Produces(StatusCodes.Status404NotFound);

        // POST /donations —— 建立捐款單。標頭 Idempotency-Key 必填；同一個鍵重複送出沿用原單（200），首次建立回 201。
        group.MapPost("/donations", async (
            CreateDonationRequest request, HttpContext httpContext, ITurnstileVerifier turnstile,
            CharityDonationService service, CancellationToken cancellationToken) =>
        {
            if (!await turnstile.VerifyAsync(request.TurnstileToken, ClientIpResolver.Resolve(httpContext), cancellationToken))
            {
                throw new CharityUnprocessableException("人機驗證未通過，請重新整理頁面後再試一次。");
            }

            var key = httpContext.Request.Headers[IdempotencyKeyHeader].ToString();
            var created = await service.CreateAsync(request, string.IsNullOrWhiteSpace(key) ? null : key.Trim(), cancellationToken);
            return created.Created
                ? Results.Created($"/api/v1/donation-platform/donations/{created.OrderNo}", created)
                : Results.Ok(created);
        })
        .WithName("CharityCreateDonation")
        .RequireRateLimiting(CharityRateLimitPolicies.Write)
        .Produces<CreateDonationResponse>(StatusCodes.Status201Created)
        .Produces<CreateDonationResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status429TooManyRequests);

        // POST /donations/{orderNo}/pay —— 發起付款，回傳付款網址。created／failed／expired 可（重）新發起，沿用原單。
        group.MapPost("/donations/{orderNo}/pay", async (
            string orderNo, StartPaymentRequest? request, CharityDonationService service, CancellationToken cancellationToken) =>
        {
            RequireOrderNoFormat(orderNo);
            return Results.Ok(await service.StartPaymentAsync(orderNo, request?.Lang, cancellationToken));
        })
        .WithName("CharityStartPayment")
        .RequireRateLimiting(CharityRateLimitPolicies.Write)
        .Produces<StartPaymentResponse>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status429TooManyRequests);

        // POST /donations/{orderNo}/confirm —— 使用者從 LINE Pay 返回後呼叫（帶回交易識別碼）。冪等。
        group.MapPost("/donations/{orderNo}/confirm", async (
            string orderNo, ConfirmPaymentRequest request, string? lang, CharityDonationService service, CancellationToken cancellationToken) =>
        {
            RequireOrderNoFormat(orderNo);
            return Results.Ok(await service.ConfirmAsync(orderNo, request.TransactionId, lang, cancellationToken));
        })
        .WithName("CharityConfirmPayment")
        .RequireRateLimiting(CharityRateLimitPolicies.Write)
        .Produces<PublicDonationResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status429TooManyRequests);

        // POST /donations/{orderNo}/cancel —— 使用者在 LINE Pay 取消、返回本站。冪等。
        group.MapPost("/donations/{orderNo}/cancel", async (
            string orderNo, string? lang, CharityDonationService service, CancellationToken cancellationToken) =>
        {
            RequireOrderNoFormat(orderNo);
            return Results.Ok(await service.CancelAsync(orderNo, lang, cancellationToken));
        })
        .WithName("CharityCancelPayment")
        .RequireRateLimiting(CharityRateLimitPolicies.Write)
        .Produces<PublicDonationResultDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);

        // GET /donations/{orderNo}?lang=zh —— 結果頁資料（前台約每 3 秒輪詢）。不含完整個資。
        group.MapGet("/donations/{orderNo}", async (
            string orderNo, string? lang, CharityDonationService service, CancellationToken cancellationToken) =>
        {
            RequireOrderNoFormat(orderNo);
            return Results.Ok(await service.GetResultAsync(orderNo, lang, cancellationToken));
        })
        .WithName("CharityGetDonationResult")
        .RequireRateLimiting(CharityRateLimitPolicies.Read)
        .Produces<PublicDonationResultDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status429TooManyRequests);
    }

    /// <summary>單號格式不對一律當作找不到，不打資料庫（也不提示「格式錯誤」，避免讓人探測單號規則）。</summary>
    private static void RequireOrderNoFormat(string orderNo)
    {
        if (!OrderNoFormat().IsMatch(orderNo))
        {
            throw new CharityNotFoundException("找不到這筆捐款單。");
        }
    }
}
