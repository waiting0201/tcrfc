namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 慣用腳代碼正規化。資料表 <c>player.preferred_foot</c> 的值域是 <c>right</c>／<c>left</c>／<c>both</c>
/// （docs/12a §5.2 enum；官網與 App 都依代碼轉中英文字）。
/// 2026-11-09 前後台的下拉選單把中文「左腳／右腳／雙腳」直接存進資料庫，英文版因此顯示中文；
/// 寫入時一律轉代碼，讀出時也轉一次，讓舊資料不必另跑遷移就能正確顯示。認不得的值原樣保留。
/// </summary>
public static class PreferredFoot
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.ToLowerInvariant() switch
        {
            "right" or "右腳" or "右" => "right",
            "left" or "左腳" or "左" => "left",
            "both" or "雙腳" or "双脚" or "雙" => "both",
            _ => v,
        };
    }
}
