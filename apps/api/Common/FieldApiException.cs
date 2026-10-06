using System.Text.RegularExpressions;

namespace Tcrfc.Api.Common;

/// <summary>
/// 後台驗證例外可以指出「是哪個欄位有問題」的介面（比照 <see cref="ICodedApiException"/> 的慣例）。
/// <see cref="ApiExceptionHandler"/> 會把 <see cref="FieldErrors"/> 放進 ProblemDetails 的 <c>errors</c>
/// 擴充欄位（欄位鍵 → 訊息陣列，與 ValidationProblemDetails 形狀相容），前端依鍵把訊息標到對應欄位。
/// 欄位鍵是給前端對應用的機器可讀字串，<b>不得出現在訊息文字裡</b>；規則見 <see cref="FieldKey"/>。
/// </summary>
public interface IFieldApiException
{
    /// <summary>欄位鍵 → 該欄位的訊息。沒有指定欄位時為空（此時回應不含 <c>errors</c>）。</summary>
    IReadOnlyDictionary<string, string> FieldErrors { get; }
}

/// <summary>欄位鍵的格式與組合工具。鍵是「邏輯欄位」的 camelCase 名稱，不是資料庫欄位名：
/// 單語欄位 <c>slug</c>；雙語欄位 <c>titleZh</c>／<c>titleEn</c>（<see cref="Bi"/>）；
/// 陣列元素 <c>blocks[2].bodyEn</c>（<see cref="Item"/>）。</summary>
public static partial class FieldKey
{
    /// <summary>合法欄位鍵：camelCase 片段以 <c>.</c> 相接，每段可帶 <c>[數字]</c> 索引。</summary>
    [GeneratedRegex(@"^[a-z][A-Za-z0-9]*(\[\d+\])?(\.[a-z][A-Za-z0-9]*(\[\d+\])?)*$")]
    public static partial Regex Pattern();

    public static bool IsValid(string? key) => !string.IsNullOrEmpty(key) && Pattern().IsMatch(key);

    /// <summary>雙語欄位鍵：<c>Bi("name","zh") → "nameZh"</c>。</summary>
    public static string Bi(string name, string locale)
        => name + char.ToUpperInvariant(locale[0]) + locale[1..].ToLowerInvariant();

    /// <summary>陣列元素鍵：<c>Item("blocks", 2, "bodyEn") → "blocks[2].bodyEn"</c>；省略 <paramref name="sub"/> 則為 <c>blocks[2]</c>。</summary>
    public static string Item(string array, int index, string? sub = null)
        => sub is null ? $"{array}[{index}]" : $"{array}[{index}].{sub}";

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    /// <summary>例外建構子共用：<paramref name="field"/> 為 <c>null</c> 時回傳空字典（不是錯誤，代表沒有欄位歸屬）。</summary>
    public static IReadOnlyDictionary<string, string> Single(string? field, string message)
        => field is null ? Empty : new Dictionary<string, string> { [field] = message };
}
