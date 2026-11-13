namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 球員位置代碼正規化。資料表 <c>players.position</c> 的值域是 <c>GK</c>／<c>DF</c>／<c>MF</c>／<c>FW</c>（大寫；docs/12a §5.2）。
/// 官網（中英文轉換）、iOS 與 Android 名單分組都只認這四個代碼。
/// 後台「位置」欄原本是自由文字（提示寫「例如 門將、中場」），營運人員照填中文，英文頁就顯示中文、
/// App 把球員歸到「其他」——與 2026-11-09 慣用腳（<see cref="PreferredFoot"/>、docs/18 E-357）同形。
/// 寫入時一律轉代碼，讀出時也轉一次，舊資料不必另跑遷移。認不得的值原樣保留（與 <see cref="PreferredFoot"/> 一致，不拒收）。
/// </summary>
public static class PlayerPosition
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.ToLowerInvariant() switch
        {
            "gk" or "goalkeeper" or "goal keeper" or "keeper" or "門將" or "守門員" or "门将" or "守门员" => "GK",
            "df" or "defender" or "後衛" or "后卫" or "防守" or "後衞" => "DF",
            "mf" or "midfielder" or "中場" or "中场" or "中場球員" => "MF",
            "fw" or "forward" or "striker" or "前鋒" or "前锋" or "攻擊" => "FW",
            _ => v,
        };
    }
}
