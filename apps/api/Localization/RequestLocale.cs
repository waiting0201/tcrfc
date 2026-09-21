namespace Tcrfc.Api.Localization;

/// <summary>
/// 語系代碼轉換與回退規則。
///
/// 🔴 對外契約（查詢參數 <c>?lang=</c>）與資料庫儲存值是兩套代碼，不能直接拿其中一套當另一套用：
/// - **對外**：一律 <c>zh</c> / <c>en</c>（docs/06-conventions.md §「語系代碼」，與 App 的
///   <c>AppDevice.lang</c> 用同一套，也是 Redis key 必須是有限集合的前提）。
/// - **資料庫**：<c>locales.code</c> 實際存的是 <c>zh-Hant</c> / <c>en</c>（db/club-schema.sql）。
///
/// 英文缺漏時的回退規則（寫進 README，不是只在這裡）：
/// **請求語系的欄位值非空白 → 用它；否則 → 用預設語系（<c>zh-Hant</c>）的同一欄位值；
/// 兩者都沒有 → 回傳 null（不是空字串）。** 這確保前台拿到的要嘛是有意義的內容、要嘛是明確的
/// null（可以判斷要不要顯示佔位文字），不會是「看起來像正常回應、其實是空字串」的靜默空白。
/// </summary>
public static class RequestLocale
{
    public const string DefaultDbLocale = "zh-Hant";

    /// <summary>外部語系代碼（大小寫不拘，非 en 一律視為 zh）轉成資料庫 locales.code 值。</summary>
    public static string ToDbLocale(string? externalLang)
        => string.Equals(externalLang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : DefaultDbLocale;

    /// <summary>
    /// 回退挑值：請求語系的值非空白就用它，否則用預設語系（<c>zh-Hant</c>）的值，兩者皆空白回傳 null。
    /// </summary>
    public static string? Pick(string? requestedLocaleValue, string? defaultLocaleValue)
        => string.IsNullOrWhiteSpace(requestedLocaleValue) ? NullIfBlank(defaultLocaleValue) : requestedLocaleValue;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
