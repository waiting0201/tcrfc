namespace Tcrfc.Api.Features.Home;

/// <summary>
/// 五大核心價值（主站規劃書 §1.2；首頁區塊「五大核心價值」§3.1：圖示＋中英名稱，可連結至 2.3 足球理念）。
/// 代碼與文章的核心價值標籤（<c>value_tag_links.value_tag</c>）<b>同一組</b>：<c>players_first</c>／<c>excellence</c>／<c>global_pathways</c>／<c>community</c>／<c>integrity</c>。
/// 規劃書 §3.1 的「資料來源」欄寫「後台設定」，但<b>後台沒有任何可編輯這五項的畫面或欄位</b>（B3 只管區塊開關與排序）；五項本身是規劃書 §1.2 定死的品牌主張，
/// 所以由伺服器端的固定目錄提供，與兩個站台共用（藍鯨的標籤文字是否另訂尚待確認，規劃書 §10 第 35 點）。
/// </summary>
public static class CoreValueCatalog
{
    public static readonly IReadOnlyList<CoreValueDto> All =
    [
        new() { Code = "players_first", NameZh = "以球員為本", NameEn = "Players First", SortOrder = 1 },
        new() { Code = "excellence", NameZh = "追求卓越", NameEn = "Excellence", SortOrder = 2 },
        new() { Code = "global_pathways", NameZh = "國際發展", NameEn = "Global Pathways", SortOrder = 3 },
        new() { Code = "community", NameZh = "社區共好", NameEn = "Community", SortOrder = 4 },
        new() { Code = "integrity", NameZh = "誠信專業", NameEn = "Integrity", SortOrder = 5 },
    ];
}
