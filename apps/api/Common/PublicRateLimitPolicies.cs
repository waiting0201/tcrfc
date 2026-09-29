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
}
