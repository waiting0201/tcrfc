namespace Tcrfc.Api.Common;

/// <summary>
/// 所有對外 HTTP 呼叫（加值中心開立／作廢／折讓、LINE Pay、LINE Login、Turnstile、地理編碼）的<b>逾時集中設定</b>。
/// 🔴 為什麼要集中且不得用預設值：<c>HttpClient.Timeout</c> 預設 100 秒；E-338 之後開立憑證、商店發票、退款連動作廢都是
/// 「持有交易層級 <c>sp_getapplock</c> 期間呼叫外部服務」，外部服務卡住時整把鎖（以及交易、連線池連線）會被占住 100 秒，
/// 其他想取同一把鎖的路徑（退款連動作廢最多等 20 秒）會被拖垮。因此持鎖呼叫的逾時必須明顯小於鎖的等待上限。
/// 新增外部呼叫一律經 <see cref="AddExternalHttpClient"/> 註冊（<c>ArchitectureTests</c> 掃描禁止直接 <c>AddHttpClient</c>），逾時值寫在這裡。
/// 實際的加值中心／LINE Pay 串接尚未開發（STATUS B-7／B-10，目前是假實作），名稱與逾時先定好，串接時直接
/// <c>IHttpClientFactory.CreateClient(ExternalHttp.InvoiceIssuer)</c> 即可。
/// </summary>
public static class ExternalHttp
{
    /// <summary>加值中心（電子發票／捐贈收據）開立、作廢、折讓、查詢。持鎖呼叫；退款連動作廢的等鎖上限是 20 秒，所以必須 ≤ 15 秒。</summary>
    public const string InvoiceIssuer = "invoice-issuer";

    /// <summary>LINE Pay（請款、確認、退款）。持鎖呼叫，≤ 15 秒。</summary>
    public const string LinePay = "linepay";

    public const string LineLogin = "line-login";
    public const string ClubTurnstile = "club-turnstile";
    public const string CharityTurnstile = "charity-turnstile";
    public const string GoogleGeocoding = "google-geocoding";

    /// <summary>未特別列出的名稱使用的逾時；任何值都不得超過 <see cref="MaxTimeout"/>。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>單次對外呼叫逾時的絕對上限（持鎖路徑的鎖等待上限 20 秒要大於它）。</summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(15);

    private static readonly Dictionary<string, TimeSpan> Timeouts = new(StringComparer.Ordinal)
    {
        [InvoiceIssuer] = TimeSpan.FromSeconds(15),
        [LinePay] = TimeSpan.FromSeconds(15),
        [LineLogin] = TimeSpan.FromSeconds(10),
        [ClubTurnstile] = TimeSpan.FromSeconds(5),
        [CharityTurnstile] = TimeSpan.FromSeconds(5),
        [GoogleGeocoding] = TimeSpan.FromSeconds(5),
    };

    public static TimeSpan TimeoutFor(string clientName)
    {
        var timeout = Timeouts.GetValueOrDefault(clientName, DefaultTimeout);
        if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
        {
            throw new InvalidOperationException($"外部 HTTP 用戶端 {clientName} 的逾時 {timeout} 不在 (0, {MaxTimeout}] 範圍內。");
        }

        return timeout;
    }

    /// <summary>註冊具名 HttpClient 並套用集中設定的逾時。專案內不得直接呼叫 <c>services.AddHttpClient</c>。</summary>
    public static IHttpClientBuilder AddExternalHttpClient(this IServiceCollection services, string clientName)
        => services.AddHttpClient(clientName, client => client.Timeout = TimeoutFor(clientName));

    /// <summary>預先註冊尚未串接的外部服務用戶端（加值中心、LINE Pay），讓逾時設定先就位；串接時不會漏設。</summary>
    public static IServiceCollection AddExternalHttpClientDefaults(this IServiceCollection services)
    {
        services.AddExternalHttpClient(InvoiceIssuer);
        services.AddExternalHttpClient(LinePay);
        return services;
    }
}
