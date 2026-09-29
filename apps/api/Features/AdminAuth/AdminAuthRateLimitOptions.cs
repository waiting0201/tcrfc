namespace Tcrfc.Api.Features.AdminAuth;

/// <summary>
/// 2026-09-29（後台登入端點依 IP 限流，修正版）：`/login`／`/refresh` 兩個政策的「額度」
/// （<c>PermitLimit</c>）改成可設定值，讀取環境變數（沿用本專案既有慣例——`CLUB_SQL_CONNECTION_STRING`／
/// `JWT_SIGNING_KEY_CLUB`／`REDIS_HOST` 都是同一種「扁平鍵名、直接讀 <see cref="IConfiguration"/>
/// 索引子」寫法，不是巢狀 Options 區段），**未設定時退回正式環境應有的嚴格預設值**。
///
/// ── 為什麼要從「寫死的常數」改成「可設定值」──
/// 第一版把 `admin-login`／`admin-refresh` 的額度直接寫死成能蓋過既有測試在共用
/// <c>AdminWriteApiFixture</c>（32 個測試檔共用同一個 <c>WebApplicationFactory</c> 行程、
/// 同一個「unknown」IP 分區）裡實際呼叫次數的數字（40／20）——這是本末倒置：**讓測試環境的
/// 用量決定正式環境的安全額度**，而不是反過來讓正式環境的安全需求決定額度、測試環境另外想辦法
/// 適應。改成可設定值之後：正式環境的預設值可以回到業界常見的嚴格數字（見下方兩個預設值常數的
/// 理由），測試環境改由 <c>WebApplicationFactory</c> fixture 在建立測試主機前，用
/// <c>Environment.SetEnvironmentVariable</c> 覆寫成寬鬆值（見
/// <c>Tcrfc.Api.Tests.Fixtures.TestRateLimitOverrides</c>）——跟本專案既有測試 fixture 覆寫
/// <c>REDIS_HOST</c>／<c>CLUB_SQL_CONNECTION_STRING</c> 等設定值的既有寫法完全一致，不是新發明
/// 的機制。
///
/// ── 為什麼只讓「額度」可設定，視窗大小刻意固定不開放設定 ──
/// 視窗固定為 1 分鐘（<see cref="LoginWindow"/>／<see cref="RefreshWindow"/>），不做成環境變數。
/// 「額度」是會依環境、依觀察到的實際濫用狀況需要調整的維度；視窗大小如果也開放設定，等於多一個
/// 可以在不動額度數字的情況下實質放寬限制的旋鈕（例如額度沒變、視窗從 1 分鐘改成 1 小時，效果是
/// 放寬 60 倍卻不會被「額度變大了」這種明顯訊號提醒到）——固定視窗、只開放額度，設定介面更難被
/// 不小心用錯。
///
/// ── 為什麼不用巢狀 <c>IOptions&lt;T&gt;</c> 模式 ──
/// 本檔案讀取的兩個鍵名跟本專案其餘所有設定值（`CLUB_SQL_CONNECTION_STRING` 等）一樣，是給
/// Docker Compose／`deploy/*/club.env` 這種扁平環境變數檔用的，不是給 `appsettings.json` 巢狀
/// JSON 用的——沿用既有慣例，不要在同一個專案裡混用兩種設定風格。
/// </summary>
public static class AdminAuthRateLimitOptions
{
    /// <summary>
    /// 正式環境預設額度：**每 IP 每分鐘 5 次**。理由：
    /// ① 業界常見的登入端點依 IP 限流，多落在每分鐘個位數量級（各家 WAF／API Gateway 的預設範例
    /// 常見 5～10 次／分鐘）；
    /// ② 這是**額外的一層**防線，不是唯一防線——`AdminAuthService` 既有的帳號層級鎖定（連續 5 次
    /// 失敗鎖 15 分鐘）仍然獨立運作，這裡的 IP 限流補的是「密碼噴灑」與「大量自動化嘗試的整體
    /// 速率」，兩層互補，因此不需要為了單一帳號的正常打字失誤（例如打錯 1～2 次密碼）留出很大的
    /// 餘裕——5 次／分鐘已經足夠涵蓋合理的人為誤觸，超過這個頻率的多半不是正常登入行為。
    /// 沒有規劃書條文可依循，屬執行層判斷（跟同檔案 `AdminAuthService.cs` 既有的鎖定門檻判斷同一種
    /// 性質）。
    /// </summary>
    public const int LoginPermitLimitDefault = 5;

    /// <summary>環境變數鍵名：未設定或設定值不是正整數時，退回 <see cref="LoginPermitLimitDefault"/>。</summary>
    public const string LoginPermitLimitConfigKey = "ADMIN_LOGIN_RATE_LIMIT_PERMIT_LIMIT";

    /// <summary>
    /// 正式環境預設額度：**每 IP 每分鐘 30 次**。風險模型跟登入不同：呼叫這支端點需要先持有一把
    /// 有效的更新權杖（256 bits 亂數，伺服器只存 SHA-256 雜湊，見 <c>AdminTokenService</c>），
    /// 用猜的在計算上不可行，這裡的額度不是為了擋「猜出正確權杖」，而是擋「明文權杖已外流時被
    /// 重放濫用的整體速率」與「對這支端點的一般性灌流量」，可以比登入寬鬆很多。
    /// </summary>
    public const int RefreshPermitLimitDefault = 30;

    public const string RefreshPermitLimitConfigKey = "ADMIN_REFRESH_RATE_LIMIT_PERMIT_LIMIT";

    /// <summary>兩個政策共用固定 1 分鐘視窗——理由見本類別檔頭「為什麼只讓額度可設定」。</summary>
    public static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);

    public static int ResolveLoginPermitLimit(IConfiguration configuration)
        => ResolvePositivePermitLimit(configuration, LoginPermitLimitConfigKey, LoginPermitLimitDefault);

    public static int ResolveRefreshPermitLimit(IConfiguration configuration)
        => ResolvePositivePermitLimit(configuration, RefreshPermitLimitConfigKey, RefreshPermitLimitDefault);

    /// <summary>
    /// 讀不到、不是數字、或不是正整數，一律退回安全的預設值——⛔ 不能讓一個打錯的設定值
    /// （例如手滑打成 <c>0</c> 或負數，或者環境變數值被清成空字串）悄悄變成「這支限流形同虛設」。
    /// 寧可在設定壞掉時退回**更嚴格**的預設值，也不要退回「不限流」。
    /// </summary>
    private static int ResolvePositivePermitLimit(IConfiguration configuration, string configKey, int defaultValue)
    {
        var raw = configuration[configKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }
}
