namespace Tcrfc.Api.Common;

/// <summary>
/// S1-18c（2026-09-29，補齊公開寫入端點的限流缺口）：公開、不需要登入的寫入端點依訪客 IP 分區的
/// 濫用防護限流，兩個新政策的名稱與額度常數集中於此——<c>Program.cs</c> 的 <c>AddRateLimiter</c>
/// 註冊政策時、對應端點掛 <c>.RequireRateLimiting(...)</c> 時，以及
/// <c>Tcrfc.Api.Tests.PublicRateLimitPoliciesTests</c>／<c>PublicWriteEndpointRateLimitingTests</c>
/// 驗證限流行為時，三邊都讀同一組常數，不重複寫魔術數字（跟數值本身分家會讓「改一處、忘了改
/// 另一處」變成可能，見 docs/18-work-errors.md 對「同一份事實只寫一次」的一貫要求）。
///
/// 依端點「業務嚴重度」分成兩類，跟既有 <c>FormsEndpoints.RateLimitPolicyName</c>（"form-submission"，
/// 20 次／5 分鐘，<b>本輪刻意不動這組既有數值與政策名稱</b>，任務指示原文「不要改表單既有政策的
/// 數值」）分開設計：
///
/// ① <see cref="Submission"/>："建立一筆真正業務紀錄"這個風險等級跟表單送出相同（目前只有
///    05 課程與活動的公開報名送出用得到，見 <c>Features/Programs/ProgramsEndpoints.cs</c>）——
///    數值刻意抄表單那組（20／5 分鐘），但用獨立政策名稱、獨立額度計數，不共用表單的計數，
///    避免兩個功能互搶額度，也讓兩者未來各自調整數值互不牽連。
/// ② <see cref="LightInteraction"/>：瀏覽數＋1（FAQ／News）、👍／👎 回饋、零結果搜尋關鍵字記錄——
///    都是「使用者正常瀏覽時就可能連續觸發好幾次」的輕量互動（連續點開多篇常見問題、快速翻頁
///    閱讀多篇新聞），額度刻意比表單寬鬆：60 次／1 分鐘。跟表單政策一樣「規劃書或 docs 沒寫、
///    執行層自行決定」，數字沒有規格依據，屬最小可行防護（比照 <c>Program.cs</c> 既有
///    <c>FormsEndpoints.RateLimitPolicyName</c> 註冊時的既有慣例）。這裡另外用「本機測試實際
///    呼叫次數」校過留有餘裕：<c>PublicFaqsTests</c>／<c>NewsPublicFilterAndViewCountTests</c>
///    共用同一個 <c>AdminWriteCollection</c> 測試主機、共用同一個「unknown」IP 分區（TestServer
///    底下 <c>RemoteIpAddress</c> 恆為 null，見 <c>ClientIpResolver</c> 檔頭），兩份檔案加總
///    約 13 次呼叫，60 留有數倍餘裕——跟既有表單政策「20 對照 AdminFormsEnquiriesTests 約 11 次
///    呼叫」是同一種校正方式。
/// </summary>
public static class PublicRateLimitPolicies
{
    /// <summary>瀏覽數／回饋／零結果搜尋等輕量互動端點共用的政策名稱。</summary>
    public const string LightInteraction = "public-light-interaction";
    public const int LightInteractionPermitLimit = 60;
    public static readonly TimeSpan LightInteractionWindow = TimeSpan.FromMinutes(1);

    /// <summary>會建立業務紀錄、風險等級比照表單送出的公開送出端點共用的政策名稱
    /// （目前只有 05 課程與活動的公開報名送出）。</summary>
    public const string Submission = "public-submission";
    public const int SubmissionPermitLimit = 20;
    public static readonly TimeSpan SubmissionWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// D 批（2026-09-30，App 公開端點）：裝置註冊、訂閱更新、廣告事件批次、診斷回報、通知開啟回報共用的政策名稱。
    /// 行動網路大量使用者共用同一個來源 IP（電信業者的 CGNAT），所以額度比網頁表單寬鬆：每 IP 每分鐘 <see cref="AppPermitLimit"/> 次，
    /// 且額度可用設定 <see cref="AppPermitLimitConfigKey"/> 覆寫（測試主機用寬鬆值，同 <c>AdminAuthRateLimitOptions</c> 的做法）。
    /// 廣告事件是「一批最多 200 筆」的批次上報，正常 App 每 30 秒才送一次，這個額度遠高於正常使用。
    /// </summary>
    public const string App = "public-app";
    public const int AppPermitLimit = 120;
    public const string AppPermitLimitConfigKey = "APP_PUBLIC_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan AppWindow = TimeSpan.FromMinutes(1);

    public static int ResolveAppPermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[AppPermitLimitConfigKey], out var v) && v > 0 ? v : AppPermitLimit;

    /// <summary>
    /// E 批（2026-10-01，S2-11 會員前台）：會員登入／註冊／驗證信／忘記密碼／重設密碼／LINE 登入這類「對帳號下手」的公開端點。
    /// 風險模型同後台登入（密碼暴力破解、密碼噴灑、用「忘記密碼」洗版寄信），所以額度嚴：每 IP 每 5 分鐘 <see cref="MemberAuthPermitLimit"/> 次。
    /// 帳號本身另有「連續 5 次失敗鎖 15 分鐘」，兩層互補（同 <c>AdminAuthEndpoints.LoginRateLimitPolicyName</c> 的說明）。
    /// 額度可用設定 <see cref="MemberAuthPermitLimitConfigKey"/> 覆寫（測試主機用寬鬆值）。
    /// </summary>
    public const string MemberAuth = "public-member-auth";
    public const int MemberAuthPermitLimit = 30;
    public const string MemberAuthPermitLimitConfigKey = "MEMBER_AUTH_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan MemberAuthWindow = TimeSpan.FromMinutes(5);

    /// <summary>E 批：已登入會員的寫入（改資料、重產 QR、球衣登記、建立訂單、報名活動…）與公開的報名送出。每 IP 每分鐘 <see cref="MemberWritePermitLimit"/> 次，
    /// 可用 <see cref="MemberWritePermitLimitConfigKey"/> 覆寫。</summary>
    public const string MemberWrite = "public-member-write";
    public const int MemberWritePermitLimit = 60;
    public const string MemberWritePermitLimitConfigKey = "MEMBER_WRITE_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan MemberWriteWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 2026-10-02（G-02 全站搜尋）：公開搜尋端點。是 GET，但一次查多張表的 LIKE，比一般讀取貴，所以仍要限流。
    /// 每 IP 每分鐘 <see cref="SearchPermitLimit"/> 次（正常使用者邊打字邊搜也遠低於此；前台應 debounce 後再呼叫）。
    /// 額度可用設定 <see cref="SearchPermitLimitConfigKey"/> 覆寫（測試主機用寬鬆值）。
    /// </summary>
    public const string Search = "public-search";
    public const int SearchPermitLimit = 30;
    public const string SearchPermitLimitConfigKey = "PUBLIC_SEARCH_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan SearchWindow = TimeSpan.FromMinutes(1);

    /// <summary>2026-10-02（G-09 電子報訂閱）：頁尾訂閱表單。寫入名單的公開端點，同一個來源短時間內不該訂閱很多次：
    /// 每 IP 每 10 分鐘 <see cref="NewsletterPermitLimit"/> 次。訂閱與退訂共用。額度可用 <see cref="NewsletterPermitLimitConfigKey"/> 覆寫。</summary>
    public const string Newsletter = "public-newsletter";
    public const int NewsletterPermitLimit = 10;
    public const string NewsletterPermitLimitConfigKey = "PUBLIC_NEWSLETTER_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan NewsletterWindow = TimeSpan.FromMinutes(10);

    /// <summary>2026-10-02（P4 試訓報名）：風險等級同課程報名（<see cref="Submission"/>，建立一筆含個資的業務紀錄），
    /// 但獨立政策名稱與獨立計數，兩個功能不互搶額度；每 IP 每 5 分鐘 <see cref="TrialRegistrationPermitLimit"/> 次，
    /// 可用 <see cref="TrialRegistrationPermitLimitConfigKey"/> 覆寫。</summary>
    public const string TrialRegistration = "public-trial-registration";
    public const int TrialRegistrationPermitLimit = 20;
    public const string TrialRegistrationPermitLimitConfigKey = "PUBLIC_TRIAL_REGISTRATION_RATE_LIMIT_PERMITS";
    public static readonly TimeSpan TrialRegistrationWindow = TimeSpan.FromMinutes(5);

    public static int ResolveSearchPermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[SearchPermitLimitConfigKey], out var v) && v > 0 ? v : SearchPermitLimit;

    public static int ResolveNewsletterPermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[NewsletterPermitLimitConfigKey], out var v) && v > 0 ? v : NewsletterPermitLimit;

    public static int ResolveTrialRegistrationPermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[TrialRegistrationPermitLimitConfigKey], out var v) && v > 0 ? v : TrialRegistrationPermitLimit;

    public static int ResolveMemberAuthPermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[MemberAuthPermitLimitConfigKey], out var v) && v > 0 ? v : MemberAuthPermitLimit;

    public static int ResolveMemberWritePermitLimit(IConfiguration configuration)
        => int.TryParse(configuration[MemberWritePermitLimitConfigKey], out var v) && v > 0 ? v : MemberWritePermitLimit;
}
