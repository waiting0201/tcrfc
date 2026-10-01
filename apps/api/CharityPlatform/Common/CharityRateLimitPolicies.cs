namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// 慈善平台公開端點的依訪客 IP 限流政策（規劃書 §3.3「防濫用：Turnstile 或同等機制；同一 IP 短時間內大量建單需節流」、
/// §11.2「捐款建單須有防濫用機制（Turnstile、IP 節流）」）。名稱與額度集中在這裡，<c>Program.cs</c> 註冊政策、
/// 端點掛 <c>.RequireRateLimiting(...)</c>、測試驗證 429 三邊讀同一組常數。
///
/// 兩個政策分開，因為風險模型不同：
/// ① <see cref="Write"/>：建立捐款單、發起付款、確認、取消——會寫資料庫、會呼叫金流，最需要擋「同一來源大量建單」。
///    額度 <b>30 次／10 分鐘／IP</b>。⚠️ 掃碼場景常見「店家 Wi-Fi 同一個出口 IP」，一家店的客人會共用同一個額度，
///    所以不能像登入那樣壓到個位數；30 次足以涵蓋正常的一個捐款流程（建單＋發起付款＋確認＋重試各 1～3 次）有餘，
///    又擋得住腳本洗單。沒有規劃書條文給數字，屬執行層判斷，上線後依實際流量調整（環境變數可覆寫）。
/// ② <see cref="Read"/>：結果頁輪詢與單筆查詢。單號 80 bits 不可猜，但仍擋「大量枚舉嘗試」；
///    額度 <b>120 次／分鐘／IP</b>（結果頁約每 3 秒輪詢一次，單一捐款人 1 分鐘約 20 次）。
///
/// 額度可由設定覆寫（測試主機要寬鬆值；比照 <c>AdminAuthRateLimitOptions</c> 的做法），讀不到或不是正整數一律退回
/// 預設值——寧可設定壞掉時退回更嚴格的值，也不要退回「不限流」。
/// </summary>
public static class CharityRateLimitPolicies
{
    public const string Write = "charity-public-write";
    public const int WritePermitLimitDefault = 30;
    public const string WritePermitLimitConfigKey = "CHARITY_PUBLIC_WRITE_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan WriteWindow = TimeSpan.FromMinutes(10);

    public const string Read = "charity-public-read";
    public const int ReadPermitLimitDefault = 120;
    public const string ReadPermitLimitConfigKey = "CHARITY_PUBLIC_READ_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan ReadWindow = TimeSpan.FromMinutes(1);

    public static int ResolveWritePermitLimit(IConfiguration configuration)
        => ResolvePositive(configuration, WritePermitLimitConfigKey, WritePermitLimitDefault);

    public static int ResolveReadPermitLimit(IConfiguration configuration)
        => ResolvePositive(configuration, ReadPermitLimitConfigKey, ReadPermitLimitDefault);

    private static int ResolvePositive(IConfiguration configuration, string key, int defaultValue)
        => int.TryParse(configuration[key], out var parsed) && parsed > 0 ? parsed : defaultValue;
}
