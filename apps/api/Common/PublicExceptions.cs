namespace Tcrfc.Api.Common;

/// <summary>
/// 2026-10-02 起新增的公開端點（全站搜尋、電子報訂閱、試訓報名）共用的例外，集中由 <see cref="ApiExceptionHandler"/> 轉成狀態碼。
/// 與 <see cref="AdminValidationException"/> 形狀相同，但名稱不帶 Admin，避免公開端點的程式碼看起來像是後台輸入驗證。
/// 訊息一律是日常中文，會原樣顯示給訪客，<b>不得內插資料庫欄位名、資料表名或例外細節</b>。
/// </summary>
public sealed class PublicValidationException(string message) : Exception(message);

/// <summary>公開端點要求的資源不存在（或對目前俱樂部不可見）。對應 404；<see cref="Title"/> 是給畫面的短標題。</summary>
public sealed class PublicNotFoundException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}

/// <summary>公開端點的狀態衝突（例如試訓已額滿、已截止）。對應 409；<see cref="Title"/> 是給畫面的短標題。</summary>
public sealed class PublicConflictException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}
