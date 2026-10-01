using System.Text.Json;

namespace Tcrfc.Api.Common;

/// <summary>
/// 寫入資料庫 <c>json</c> 欄位前的共同檢查（docs/14 不變量、docs/18 E-111）。
/// 正式環境（Azure SQL）的 json 欄位是<b>原生 json 型別</b>，只接受 JSON <b>物件或陣列</b>；
/// 字串、數字、<c>true</c>／<c>false</c>、<c>null</c> 字面值都會被資料庫拒絕（Msg 13609）。
/// 本機與測試用的 <c>nvarchar(max)</c> 什麼都收，所以只靠「是合法 JSON」的檢查會在正式環境才爆成 500。
/// </summary>
public static class JsonColumn
{
    /// <summary>內容是否為 JSON 物件或陣列（合法 JSON 但根是純量、或語法錯誤都回 false）。</summary>
    public static bool IsObjectOrArray(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 「只存不查」的外部原始回應（例如金流回應）寫入 json 欄位前的保險：本來就是物件或陣列就原樣寫入，
    /// 否則（純文字、純量、非 JSON）包成 <c>{"raw":"…"}</c>，確保不會因為外部回應的形狀讓資料庫拒絕而中斷主流程。
    /// </summary>
    public static string? CoerceToObject(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : IsObjectOrArray(raw) ? raw : JsonSerializer.Serialize(new Dictionary<string, string> { ["raw"] = raw });

    /// <summary>自由文字存進 json 欄位時的包裝：<c>{"text":"…"}</c>（物件，原生 json 可接受）。空白視為沒有值。</summary>
    public static string? WrapText(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Serialize(new Dictionary<string, string> { ["text"] = text }, WrapOptions);

    // 不把中文與 &、<、+ 等字元轉成 \uXXXX，資料庫裡的內容維持可讀、也不影響以子字串掃描連結的功能（孤兒頁檢查）。
    private static readonly JsonSerializerOptions WrapOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// 「可能是純文字、也可能是區塊編輯器物件／陣列」的欄位（新聞內文 <c>articles_i18n.body</c>）寫入前的正規化：
    /// 空白→<c>null</c>；已是物件或陣列→原樣保存（為日後區塊編輯器保留）；其餘（純文字、純量）→<see cref="WrapText"/>。
    /// 讀取一律用 <see cref="UnwrapText"/> 還原。
    /// </summary>
    public static string? NormalizeTextOrStructured(string? input)
        => string.IsNullOrWhiteSpace(input) ? null : IsObjectOrArray(input) ? input : WrapText(input);

    /// <summary>
    /// 還原 <see cref="WrapText"/> 的自由文字。相容舊格式：早期（2022／nvarchar 欄位）存過 JSON 字串純量，
    /// 以及直接存非 JSON 的純文字，都原樣還原成文字，不丟例外。
    /// </summary>
    public static string? UnwrapText(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(stored);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                return text.GetString();
            }

            return stored;
        }
        catch (JsonException)
        {
            return stored;
        }
    }
}
