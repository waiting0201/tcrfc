namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// 慈善平台業務例外的基底：帶著要回給呼叫端的狀態碼與日常中文訊息（公開前台與慈善後台共用）。
/// <c>ApiExceptionHandler</c> 以這個基底統一轉成 <c>ProblemDetails</c>，慈善每新增一種例外不必再去改那支檔案。
/// ⛔ 訊息一律是給使用者看的日常中文，不得含資料表名、欄位名、SQL、堆疊或內部識別碼
/// （規劃書 §4.0、安全清單「API 回應不洩露內部實作細節」）。
/// 輸入驗證失敗沿用既有的 <c>AdminValidationException</c>（400），不另外發明。
/// </summary>
public class CharityApiException(int statusCode, string title, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}

/// <summary>找不到資料（含「對這個呼叫端而言等同找不到」）。404。</summary>
public sealed class CharityNotFoundException(string message)
    : CharityApiException(StatusCodes.Status404NotFound, "找不到資料", message);

/// <summary>與目前狀態衝突：重複的代碼、狀態不允許這個操作、冪等鍵被拿去送不同內容等。409。</summary>
public sealed class CharityConflictException(string title, string message)
    : CharityApiException(StatusCodes.Status409Conflict, title, message);

/// <summary>語意上合法但目前不能做（例如項目已下架、金額超出範圍）。422。</summary>
public sealed class CharityUnprocessableException(string message)
    : CharityApiException(StatusCodes.Status422UnprocessableEntity, "目前無法處理這筆請求", message);

/// <summary>外部服務（金流、發票加值中心）暫時無法使用。503；前台據此顯示明確錯誤與聯絡方式，不留半完成狀態。</summary>
public sealed class CharityServiceUnavailableException(string message)
    : CharityApiException(StatusCodes.Status503ServiceUnavailable, "服務暫時無法使用", message);
