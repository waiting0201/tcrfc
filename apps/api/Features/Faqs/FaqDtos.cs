namespace Tcrfc.Api.Features.Faqs;

/// <summary>公開讀取：主題分類（規劃書 3.12「主題分類導覽卡」）。全站共用（<c>faq_categories</c>
/// 沒有 <c>club_id</c>），不分俱樂部。</summary>
public sealed record FaqCategoryDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public string? Name { get; init; }
}

/// <summary>公開讀取：單題常見問題。依語系回退，只回 <c>published</c> 且屬於本俱樂部或共用的題目。</summary>
public sealed record FaqListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }
    public required int SortOrder { get; init; }
    public string? Question { get; init; }
    public string? Answer { get; init; }
    public required IReadOnlyList<string> CategorySlugs { get; init; }
}

/// <summary>單題回饋（規劃書 3.12「這則說明有幫助嗎？👍 / 👎」）。<c>Helpful</c> 二選一，
/// 不接受第三態——沒有回饋就是不呼叫這支端點，不是傳一個中立值。</summary>
public sealed record FaqFeedbackRequest
{
    public required bool Helpful { get; init; }
}

/// <summary>零結果搜尋回報（規劃書沒有明文要求，是 <c>faq_search_misses</c> 這張既有表的既定用途，
/// 見 <c>FaqsRepository.RecordSearchMissAsync</c> 上的判斷說明）。</summary>
public sealed record FaqSearchMissRequest
{
    public required string Keyword { get; init; }
}
