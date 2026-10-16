namespace Tcrfc.Api.Features.Home;

/// <summary>
/// 四大核心價值（主站規劃書 v3.29 §1.2；首頁區塊「核心價值」§3.1：圖示＋中英名稱，可連結至 2.3 足球理念；依客戶 2.3 足球理念與四大核心價值）。
/// 代碼與文章的核心價值標籤（<c>value_tag_links.value_tag</c>）<b>同一組</b>：<c>inspiring_players</c>（啟發球員）／<c>shaping_champions</c>（塑造冠軍）／
/// <c>local_pride</c>（在地驕傲）／<c>global_influence</c>（撼動全球）。舊五項（<c>players_first</c>／<c>excellence</c>／<c>global_pathways</c>／<c>community</c>／<c>integrity</c>）已淘汰，
/// 遷移見 <c>db/migrations/20261016_client-doc-alignment_2-contract.sql</c>（<c>integrity</c> 無對應，連結列刪除）。
/// 規劃書 §3.1 的「資料來源」欄寫「後台設定」，但<b>後台沒有任何可編輯這四項的畫面或欄位</b>（B3 只管區塊開關與排序）；四項本身是規劃書 §1.2 定死的品牌主張，
/// 所以由伺服器端的固定目錄提供，與兩個站台共用（藍鯨的標籤文字是否另訂尚待確認，規劃書 §10 第 35 點）。每項的說明段落（客戶 2.3 原文）由前台版型與 B1 頁面承載，不在此 API。
/// </summary>
public static class CoreValueCatalog
{
    public static readonly IReadOnlyList<CoreValueDto> All =
    [
        new() { Code = "inspiring_players", NameZh = "啟發球員", NameEn = "Inspiring players", SortOrder = 1 },
        new() { Code = "shaping_champions", NameZh = "塑造冠軍", NameEn = "Shaping champions", SortOrder = 2 },
        new() { Code = "local_pride", NameZh = "在地驕傲", NameEn = "Local pride", SortOrder = 3 },
        new() { Code = "global_influence", NameZh = "撼動全球", NameEn = "Global influence", SortOrder = 4 },
    ];

    /// <summary>目錄內的代碼集合（驗證與讀取過濾用）。</summary>
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(All.Select(v => v.Code), StringComparer.Ordinal);
}
