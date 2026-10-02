namespace Tcrfc.Api.Features.Search;

/// <summary>搜尋範圍代碼（主站規劃書 §3.0 G-02「跨新聞、球員、教練、課程、FAQ、慈善事蹟」）。<c>type</c> 篩選與結果的 <c>type</c> 欄位都用這組字串。</summary>
public static class SearchTypes
{
    public const string News = "news";
    public const string Faq = "faq";
    public const string Program = "program";
    public const string Player = "player";
    public const string Coach = "coach";
    public const string Charity = "charity";

    /// <summary>結果排序時，分數相同的項目依這個順序排列；也是分類篩選的顯示順序。</summary>
    public static readonly IReadOnlyList<string> All = [News, Faq, Program, Player, Coach, Charity];
}

public sealed record SearchResultItemDto
{
    /// <summary><see cref="SearchTypes"/> 之一。</summary>
    public required string Type { get; init; }

    /// <summary>細分類：<c>charity</c> 為 <c>program</c>（慈善計畫）或 <c>record</c>（事蹟紀錄）；其餘為 <c>null</c>。</summary>
    public string? SubType { get; init; }

    public required Guid Id { get; init; }

    /// <summary>有網址名稱的內容（新聞、FAQ、課程、慈善計畫）才有；前台用它組詳情頁連結。</summary>
    public string? Slug { get; init; }

    public required string Title { get; init; }

    /// <summary>內文摘錄（約 120 字，圍繞第一個命中處；已去除標記）。前台用 <see cref="SearchResponseDto.Tokens"/> 在標題與摘錄上做關鍵字高亮。</summary>
    public string? Snippet { get; init; }

    /// <summary>新聞的發布時間、慈善事蹟的發生日期（UTC，不帶 Z）；其餘為 <c>null</c>。</summary>
    public DateTime? Date { get; init; }

    /// <summary>新聞的分類代碼（前台詳情頁路徑需要）。</summary>
    public string? CategoryCode { get; init; }

    /// <summary>球員所屬球隊代碼。</summary>
    public string? TeamCode { get; init; }

    /// <summary>縮圖網址。球員與教練照片遵守肖像同意 fail-closed：未取得同意一律 <c>null</c>。</summary>
    public string? ImageUrl { get; init; }

    /// <summary>true＝要求英文但這筆沒有英文內容，標題與摘錄是繁中回退（前台可標示「尚無英文版本」）。</summary>
    public required bool IsFallbackLocale { get; init; }
}

public sealed record SearchFacetDto
{
    public required string Type { get; init; }

    /// <summary>依 <c>lang</c> 的分類顯示名稱。</summary>
    public required string Label { get; init; }

    /// <summary>此分類的命中總數（精確）。</summary>
    public required int Count { get; init; }
}

public sealed record SearchResponseDto
{
    /// <summary>正規化後的查詢字串（全形轉半形、去前後空白、壓縮空白）。</summary>
    public required string Query { get; init; }

    /// <summary>拆出的關鍵字（空白分隔，最多 5 個）；全部都要命中才算符合。前台高亮用這個，不要自己再拆一次。</summary>
    public required IReadOnlyList<string> Tokens { get; init; }

    public required IReadOnlyList<SearchResultItemDto> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>可翻頁取得的項目總數：每個分類最多取前 <see cref="Limits.PerTypeLimit"/> 筆（超過請縮小關鍵字或加分類篩選）。</summary>
    public required int TotalCount { get; init; }

    /// <summary>各分類的精確命中數（不受 <c>type</c> 篩選影響，供分類頁籤顯示數字）。</summary>
    public required IReadOnlyList<SearchFacetDto> Facets { get; init; }

    /// <summary>true＝至少一個分類的命中數超過每類上限，<see cref="TotalCount"/> 小於各分類命中數加總。</summary>
    public required bool Truncated { get; init; }

    /// <summary>所有分類的命中數加總為 0。前台此時應呼叫既有的 <c>POST /faqs/search-misses</c> 記錄零結果關鍵字（本端點是 GET，不寫入任何資料）。</summary>
    public required bool IsEmpty { get; init; }

    public static class Limits
    {
        public const int PerTypeLimit = 100;
    }
}
