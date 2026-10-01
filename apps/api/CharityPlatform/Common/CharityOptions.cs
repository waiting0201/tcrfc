using Tcrfc.Api.CharityPlatform.Security;

namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>慈善平台的設定讀取（環境變數／設定檔鍵名與預設值的唯一來源）。</summary>
public static class CharityOptions
{
    /// <summary>慈善前台的公開網址（付款返回網址、QR 目標網址的基底）。取值順序：<c>CHARITY_PUBLIC_BASE_URL</c> →
    /// 由 compose 本來就提供給 api 的 <c>CHARITY_DOMAIN</c> 組出（<c>https://{網域}</c>；<c>*.localhost</c> 用 <c>http://</c>）→ 本機預設
    /// <c>http://charity.localhost</c>。非本機環境三個都沒有時回傳 <c>null</c>，由呼叫端決定怎麼辦
    /// （付款端點據此回 503，不拿一個錯的網址去導向金流）。</summary>
    public const string PublicBaseUrlConfigKey = "CHARITY_PUBLIC_BASE_URL";

    public const string DomainConfigKey = "CHARITY_DOMAIN";

    public static string? ResolvePublicBaseUrl(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[PublicBaseUrlConfigKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var domain = configuration[DomainConfigKey]?.Trim();
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var scheme = domain.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
            return $"{scheme}://{domain}";
        }

        return environment.IsDevelopment() ? "http://charity.localhost" : null;
    }

    /// <summary>建單後多久沒完成付款就視為逾時（分鐘；規劃書 §4.3 建議 30 分鐘）。</summary>
    public const string PaymentTimeoutMinutesConfigKey = "CHARITY_PAYMENT_TIMEOUT_MINUTES";

    public static int ResolvePaymentTimeoutMinutes(IConfiguration configuration)
        => int.TryParse(configuration[PaymentTimeoutMinutesConfigKey], out var v) && v > 0 ? v : 30;

    /// <summary>推導單號用的伺服器端秘密：用慈善後台簽章金鑰（<see cref="CharityTokenService.ConfigKey"/>），
    /// 訊息前綴（<c>order|</c>）做用途區隔，不會與 JWT 簽章混用。</summary>
    public static string ResolveOrderNoSecret(IConfiguration configuration)
    {
        var secret = configuration[CharityTokenService.ConfigKey];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < CharityTokenService.MinSigningKeyLength)
        {
            throw new InvalidOperationException($"{CharityTokenService.ConfigKey} 未設定或長度不足，無法產生捐款單號。");
        }

        return secret;
    }

    /// <summary>背景工作的總開關。🔴 預設：<b>非 Development 環境開、Development 關</b>（需明確設 <c>true</c> 才開）。
    /// 理由：背景工作會把逾時的捐款單轉成 <c>expired</c>、重試憑證，在本機開發庫上會悄悄改動種子資料與其他人正在看的畫面，
    /// 也會讓每個測試主機都在背景動資料庫；正式環境則必須開（不開＝捐款單永遠不逾時、憑證失敗永遠不會被重試）。
    /// 明確設 <c>false</c> 在任何環境都關。</summary>
    public const string WorkersEnabledConfigKey = "CHARITY_WORKERS_ENABLED";

    public static bool WorkersEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[WorkersEnabledConfigKey];
        if (string.Equals(configured, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !environment.IsDevelopment() || string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>背景工作的掃描間隔（秒）。預設 60。</summary>
    public const string WorkerIntervalSecondsConfigKey = "CHARITY_WORKER_INTERVAL_SECONDS";

    public static TimeSpan ResolveWorkerInterval(IConfiguration configuration)
        => TimeSpan.FromSeconds(int.TryParse(configuration[WorkerIntervalSecondsConfigKey], out var v) && v > 0 ? v : 60);
}
