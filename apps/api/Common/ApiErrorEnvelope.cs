using Microsoft.AspNetCore.Mvc;

namespace Tcrfc.Api.Common;

/// <summary>
/// 統一錯誤結構（行動 App 規劃書 §9.5「錯誤代碼、可呈現給使用者的訊息（雙語）、是否可重試」）。
/// 在既有 RFC 7807 <see cref="ProblemDetails"/> 上**相容擴充**，既有欄位（<c>status</c>／<c>title</c>／<c>detail</c>／<c>instance</c>／既有的 <c>code</c>／<c>lockedUntil</c>）一個不刪不改：
/// <list type="bullet">
/// <item><c>code</c>：機器可讀代碼，<b>所有</b>錯誤都有。有專屬代碼的例外（會員一族，見 <see cref="ICodedApiException"/>）用自己的代碼，
/// 其餘依 HTTP 狀態給通用代碼（見 <see cref="DefaultCode"/>）。</item>
/// <item><c>messageZh</c>／<c>messageEn</c>：可直接呈現給使用者的訊息。<c>messageZh</c> 恆等於 <c>detail</c>；
/// <c>messageEn</c> 在已登記代碼（<see cref="ApiErrorMessages"/>）用專屬英文，其餘用該狀態的通用英文。兩者都不含內部實作細節。</item>
/// <item><c>retryable</c>：依 §9.5「網路錯誤與伺服器 5xx 可自動重試、4xx 不重試」：5xx 為 true、4xx（含 423、429）為 false；
/// 例外：<c>FeatureNotConfiguredException</c> 的 503（尚未設定屬永久狀態，重試無益）為 false。</item>
/// </list>
/// 所有產生錯誤本文的路徑都走這裡：<see cref="ApiExceptionHandler"/>（例外）、<c>UseStatusCodePages</c> 經 ProblemDetails
/// 的 <c>CustomizeProblemDetails</c>（空本文的 404／401／429 等）、以及自己用 <c>Results.Problem</c> 的少數端點。
/// </summary>
public static class ApiErrorEnvelope
{
    /// <summary>某狀態沒有專屬代碼時的通用代碼。與 <c>MemberExceptions</c> 各例外的預設代碼同名（<c>validation_failed</c> 等）。</summary>
    public static string DefaultCode(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "validation_failed",
        StatusCodes.Status401Unauthorized => "unauthenticated",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status413PayloadTooLarge => "payload_too_large",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status423Locked => "account_locked",
        StatusCodes.Status429TooManyRequests => "rate_limited",
        StatusCodes.Status503ServiceUnavailable => "service_unavailable",
        >= 500 => "server_error",
        _ => "request_failed",
    };

    /// <summary>空本文（沒有例外、沒有自訂訊息）時補的繁中通用標題與訊息。</summary>
    public static (string Title, string Detail) DefaultZh(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ("輸入內容有誤", "請求內容不正確，請檢查後再試。"),
        StatusCodes.Status401Unauthorized => ("請先登入", "請先登入。"),
        StatusCodes.Status403Forbidden => ("沒有權限", "你沒有權限執行這個操作。"),
        StatusCodes.Status404NotFound => ("找不到資料", "找不到你要的資料。"),
        StatusCodes.Status405MethodNotAllowed => ("請求方法不允許", "這個網址不支援這種請求方法。"),
        StatusCodes.Status409Conflict => ("資料衝突", "操作與目前的資料狀態衝突，請重新整理後再試。"),
        StatusCodes.Status413PayloadTooLarge => ("內容過大", "上傳的內容太大，請縮小後再試。"),
        StatusCodes.Status415UnsupportedMediaType => ("格式不支援", "不支援這種內容格式。"),
        StatusCodes.Status423Locked => ("帳號暫時鎖定", "帳號暫時鎖定，請稍後再試。"),
        StatusCodes.Status429TooManyRequests => ("操作太頻繁", "操作太頻繁，請稍後再試。"),
        StatusCodes.Status503ServiceUnavailable => ("服務暫時無法使用", "服務暫時無法使用，請稍後再試。"),
        >= 500 => ("伺服器發生未預期的錯誤", "請稍後再試；若持續發生請聯繫系統管理員。"),
        _ => ("請求無法處理", "請求無法處理，請稍後再試。"),
    };

    /// <summary>
    /// 把統一錯誤欄位補到 <paramref name="problem"/>。已經有的 <c>code</c> 不覆蓋（例外處理器先放了專屬代碼）；
    /// <c>messageZh</c> 取 <c>detail</c>（沒有就用 <see cref="DefaultZh"/>）。
    /// </summary>
    public static void Fill(ProblemDetails problem, int status, string? explicitCode = null)
    {
        var code = explicitCode
            ?? (problem.Extensions.TryGetValue("code", out var existing) && existing is string s && s.Length > 0 ? s : DefaultCode(status));
        problem.Extensions["code"] = code;

        var (defaultTitle, defaultDetail) = DefaultZh(status);
        problem.Title ??= defaultTitle;
        problem.Detail ??= defaultDetail;

        problem.Extensions["messageZh"] = problem.Detail;
        problem.Extensions["messageEn"] = ApiErrorMessages.English(code, status);
        problem.Extensions["retryable"] = IsRetryable(status, code);
    }

    /// <summary>給 <c>Results.Problem(extensions: …)</c> 用：自己組 ProblemDetails 的端點補上同一組統一錯誤欄位。</summary>
    public static IDictionary<string, object?> Extensions(int status, string code, string messageZh, IDictionary<string, object?>? more = null)
    {
        var result = new Dictionary<string, object?>(more ?? new Dictionary<string, object?>())
        {
            ["code"] = code,
            ["messageZh"] = messageZh,
            ["messageEn"] = ApiErrorMessages.English(code, status),
            ["retryable"] = IsRetryable(status, code),
        };
        return result;
    }

    /// <summary>§9.5：5xx 可重試、4xx 不重試；尚未設定（永久狀態）的 503 不重試。</summary>
    public static bool IsRetryable(int status, string code)
        => status >= 500 && !ApiErrorMessages.NonRetryableServerCodes.Contains(code);
}
