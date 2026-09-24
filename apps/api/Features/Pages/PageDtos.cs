using System.Text.Json;

namespace Tcrfc.Api.Features.Pages;

/// <summary>公開讀取的單一區塊。<see cref="Content"/> 已經由
/// <see cref="PageContentLocalizer"/> 依請求語系把區塊內容裡的雙語 <c>{zh,en}</c> 物件化簡成單一
/// 字串——前台拿到的是「這個語系該顯示的最終值」，不需要自己再挑語系（除了圖片替代文字的
/// <c>altZh</c>／<c>altEn</c> 兩個扁平欄位，那兩個刻意不參與化簡，見 <see cref="PageContentLocalizer"/>
/// 檔頭說明，前台自行依語系挑選）。</summary>
public sealed record PageBlockPublicDto
{
    public required string BlockType { get; init; }
    public required JsonElement Content { get; init; }
    public required int SortOrder { get; init; }
}

/// <summary>公開頁面詳情。⚠️ 沒有標題欄位——B1 頁面沒有獨立的標題資料表欄位，
/// 內文與標題全部走區塊化編輯器（docs/12c-i18n-tables.md §3.1）。</summary>
public sealed record PageDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required IReadOnlyList<PageBlockPublicDto> Blocks { get; init; }
}

/// <summary>未發布可分享的預覽內容。刻意跟 <see cref="PageDetailDto"/> 分開——預覽回傳的是
/// 「某個版本快照當下的樣子」，不是「這個頁面目前的狀態」，欄位组成故意不同（多了
/// <see cref="VersionNo"/>／<see cref="Status"/>，沒有 <see cref="PageDetailDto.PublishedAt"/>
/// 的語意保證，因為預覽的版本不一定是已發布版本）。</summary>
public sealed record PagePreviewDto
{
    public required Guid PageId { get; init; }
    public required int VersionNo { get; init; }

    /// <summary>頁面**目前**的狀態（不是這個版本當時的狀態——版本快照沒有記錄狀態，只記錄內容），
    /// 純粹提供給看預覽連結的人一個「這個頁面現在是草稿還是已發布」的參考資訊。</summary>
    public required string Status { get; init; }

    public required string Slug { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public required IReadOnlyList<PageBlockPublicDto> Blocks { get; init; }
}
