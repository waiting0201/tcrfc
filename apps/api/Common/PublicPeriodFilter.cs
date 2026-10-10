namespace Tcrfc.Api.Common;

/// <summary>
/// 公開端「期間涵蓋今日者才顯示」的單一判斷（主站規劃書 v3.25 §3.9 9.1、§3.11 11.2）：起訖皆可為空，空＝進行中（該側不設限）。
/// 夥伴列表（<c>PartnersRepository</c>）與慈善計畫詳情的合作夥伴／贊助商共用這一份，改濾法只改這裡，避免兩處各寫一份後漸行漸遠。
/// 查詢必須帶一個名為 <c>@Today</c> 的日期參數，值用 <see cref="Today"/> 轉成 <see cref="DateTime"/>。
/// </summary>
public static class PublicPeriodFilter
{
    /// <summary>與公開夥伴／贊助商列表既有的「今日」一致（UTC 日期）；快取鍵含此日期，跨日自然換新。</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>SQL 片段：<c>(start IS NULL OR start &lt;= @Today) AND (end IS NULL OR end &gt;= @Today)</c>。欄位名稱只能是程式碼常數，不得串入使用者輸入。</summary>
    public static string CoversToday(string startColumn, string endColumn)
        => $"({startColumn} IS NULL OR {startColumn} <= @Today) AND ({endColumn} IS NULL OR {endColumn} >= @Today)";
}
