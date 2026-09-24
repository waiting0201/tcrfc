namespace Tcrfc.Api.Common;

/// <summary>
/// 搜尋關鍵字正規化（S1-8 補強，docs/18-work-errors.md <c>E-51</c> 的後續）：
/// <c>faq_search_misses</c> 是「彙總列」（<c>(club_id, keyword)</c> 唯一鍵、<c>hit_count</c>
/// 只累加不分列，見 <c>Features/Faqs/FaqsRepository.RecordSearchMissAsync</c> 上的說明），
/// 同一個使用者意圖打出來的關鍵字如果沒有先正規化就直接拿去當 upsert 鍵，
/// 「Fee」「fee」「ｆｅｅ」「 fee 」會被拆成三四筆不同的列，後台排行因此失真——
/// 而且**寫入當下沒做，讀取端事後補救不了**：資料庫裡已經拆散的列，
/// 沒有對 <c>hit_count</c> 做 <c>SUM…GROUP BY</c> 的設計（也不打算補，
/// 見 <c>Features/AdminFaqs/AdminFaqsRepository.ListSearchMissesAsync</c> 的說明），
/// 拆散了就是拆散了。正規化因此**必須在寫入前**做一次，這裡是唯一入口。
///
/// 三件事，依序處理：
/// 1. 全形字母／數字／符號（<c>U+FF01</c>–<c>U+FF5E</c>）與全形空白（<c>U+3000</c>）轉半形。
/// 2. 前後空白去除。
/// 3. 大小寫統一（<see cref="string.ToLowerInvariant"/>——只影響有大小寫概念的拉丁字母，
///    中文字、數字、符號不受影響，不會有 Turkish-I 那類地區特殊大小寫規則的疑慮）。
/// </summary>
public static class SearchKeywordNormalizer
{
    /// <summary>正規化後的關鍵字；輸入是 <c>null</c>／空白字串時回傳 <see cref="string.Empty"/>，
    /// 呼叫端據此判斷「不值得記錄」（比照既有的 <c>keyword.Trim().Length == 0</c> 判斷）。</summary>
    public static string Normalize(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return string.Empty;
        }

        var chars = new char[keyword.Length];
        for (var i = 0; i < keyword.Length; i++)
        {
            var c = keyword[i];
            chars[i] = c switch
            {
                '　' => ' ', // 全形空白 → 半形空白
                >= '！' and <= '～' => (char)(c - 0xFEE0), // 全形字母／數字／符號 → 半形
                _ => c,
            };
        }

        return new string(chars).Trim().ToLowerInvariant();
    }
}
