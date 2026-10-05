namespace Tcrfc.Api.Common;

/// <summary>
/// 刪除端點的樂觀並行參數。<c>expectedUpdatedAt</c> 當必填 query 時，最小 API 在參數綁定階段缺值會丟
/// <see cref="BadHttpRequestException"/>（綁定發生在端點 handler 之前，handler 內的授權與驗證都碰不到）；
/// 端點因此宣告成 <c>DateTime?</c>，由這裡丟出帶日常中文訊息的 400，不依賴全域處理。
/// </summary>
public static class ConcurrencyInput
{
    public static DateTime RequireExpectedUpdatedAt(DateTime? value)
        => value ?? throw new AdminValidationException("缺少資料的最後更新時間，請重新整理頁面後再操作（用來避免覆蓋別人剛做的修改）。");
}
